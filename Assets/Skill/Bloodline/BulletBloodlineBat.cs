using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 血族蝙蝠使魔：
/// 学会技能后常驻环绕玩家；技能触发时扑向目标造成伤害，再回到环绕轨道。
/// </summary>
[DefaultExecutionOrder(-500)]
public class BulletBloodlineBat : Bulletbase
{
    private enum Phase { Orbit, ToEnemy, ToPlayer }

    [Header("飞行阶段")]
    [SerializeField] private float orbitRadius = 2.8f;
    [SerializeField] private float orbitAngularSpeedDeg = 150f;
    [SerializeField] private float orbitMoveSpeedMultiplier = 1.25f;
    [SerializeField] private float attackMoveSpeedMultiplier = 6f;
    [SerializeField] private float returnMoveSpeedMultiplier = 12f;
    [SerializeField] private float returnSnapDistance = 0.22f;
    [SerializeField] private float hitResolveRadius = 0.45f;
    [SerializeField] private bool hardLockOrbitPosition = true;

    [Header("自动扑击（常驻使魔模式）")]
    [SerializeField] private bool autoAttackInOrbit = false;
    [SerializeField] private float autoAttackInterval = 0.35f;
    
    [Header("阵型校正（防止长时间漂移打乱卫星间距）")]
    [SerializeField] private bool periodicReposition = true;
    [SerializeField] private float repositionInterval = 0.5f;

    [Header("外观")]
    [SerializeField] private bool forceRedTint = true;
    [SerializeField] private Color forcedRedColor = new Color(1f, 0.12f, 0.12f, 1f);
    // 性能优化：以下两个红色叠加层在大量血族蝙蝠（每个使魔最多到 6+ 只 × 多个玩家）时
    // 会让 DrawCall 翻倍/三倍，并且每帧在 Update/LateUpdate/OnWillRenderObject 三处反复
    // 设置 sr.color + sr.material（material getter 会实例化材质，破坏 SRP batching）。
    // 改为默认关闭叠加层、只用一次性 tint，自蝙蝠登场后游戏卡顿明显，先把这两层去掉。
    [SerializeField] private bool useRedOverlay = false;
    [SerializeField] private float redOverlayAlpha = 0.85f;
    [SerializeField] private bool useGlobalRedMirrorOverlay = false;
    [SerializeField] private int redMirrorSortingBoost = 50;
    [SerializeField] private bool forceTiltX45 = true;

    private Phase _phase = Phase.Orbit;
    private Transform _playerAnchor;
    private Transform _enemyTarget;
    private bool _hitDone;
    private bool _hasAttackedThisCooldown;
    private bool _lifestealOn;
    private float _lifestealRatio;

    private float _orbitAngle;
    private float _baseScaleXAbs;
    private int _orbitSlot;
    private int _orbitTotal = 1;
    private float _autoAttackTimer;
    private float _orbitPhaseDeg;
    private float _repositionTimer;
    private SpriteRenderer[] _allSpriteRenderers;
    private SpriteRenderer _baseRenderer;
    private SpriteRenderer _overlayRenderer;
    private Material _overlayMat;
    private readonly List<SpriteMirrorPair> _redMirrors = new List<SpriteMirrorPair>();
    // 缓存敌人层根 Transform，避免每次扑击搜敌都做 GameObject.Find（场景里物体一多很贵）。
    private static Transform _cachedEnemyLayer;

    private static Transform GetEnemyLayer()
    {
        if (_cachedEnemyLayer != null) return _cachedEnemyLayer;
        GameObject go = GameObject.Find("enemylayer");
        if (go != null) _cachedEnemyLayer = go.transform;
        return _cachedEnemyLayer;
    }

    private class SpriteMirrorPair
    {
        public SpriteRenderer source;
        public SpriteRenderer mirror;
    }

    private void Awake()
    {
        autoAttackInOrbit = false;
        attackMoveSpeedMultiplier = 6f;
        returnMoveSpeedMultiplier = 12f;
        DetachBatEnemyBrainIfPresent();
        _baseScaleXAbs = Mathf.Abs(transform.localScale.x) > 0.0001f ? Mathf.Abs(transform.localScale.x) : 1f;
        _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _baseRenderer = ResolvePrimaryRenderer();
        if (useRedOverlay) EnsureRedOverlay();
        if (useGlobalRedMirrorOverlay) BuildRedMirrors();
        transform.localScale = new Vector3(_baseScaleXAbs, Mathf.Abs(transform.localScale.y), Mathf.Abs(transform.localScale.z));
        // 颜色只在初始化时刷一次（以及替换 sprite/动画切帧时通过 LateUpdate 兜底）。
        // 不再每帧 ×多回调地反复设置 color/material，那会让大量蝙蝠场景下 GPU/CPU 双爆。
        if (forceRedTint) ApplyForcedRedTint();
        if (forceTiltX45) ForceTiltRotation();
    }

    private void LateUpdate()
    {
        // 之前在 Update / LateUpdate / OnWillRenderObject 三处都做同样的颜色刷新，
        // 在大量血族蝙蝠时是主要的卡顿源。统一收敛到 LateUpdate 一次。
        // 颜色这里会跟随 SpriteRenderer 当前 sprite 帧重新写一遍，足以覆盖动画播放期间被
        // 美术资源里的内嵌色覆盖的情况。
        if (forceRedTint) ApplyForcedRedTint();
        if (useRedOverlay) SyncRedOverlay();
        if (useGlobalRedMirrorOverlay) SyncRedMirrors();
        if (forceTiltX45) ForceTiltRotation();
    }

    /// <summary>
    /// 若子弹物体上误挂了 Bat（enemy AI），拆掉以免与弹道逻辑冲突。
    /// </summary>
    public void DetachBatEnemyBrainIfPresent()
    {
        var batAi = GetComponent<Bat>();
        if (batAi != null) Destroy(batAi);
    }

    public override void GetFather()
    {
        base.GetFather();
        if (fatherskill != null && fatherskill.player != null)
        {
            var attr = fatherskill.player.GetComponent<Attribute>();
            if (attr != null) player = attr;
        }
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    public void SetupLifesteal(bool enable, float ratio)
    {
        _lifestealOn = enable;
        _lifestealRatio = ratio;
    }

    public void SetTargets(Transform playerTr, Transform enemyTr)
    {
        _playerAnchor = playerTr;
        _enemyTarget = enemyTr;
        _phase = Phase.ToEnemy;
    }

    public void ConfigureOrbit(Transform playerTr, int slot, int total)
    {
        if (playerTr != null) _playerAnchor = playerTr;
        int newTotal = Mathf.Max(1, total);
        int newSlot = Mathf.Clamp(slot, 0, newTotal - 1);
        _orbitTotal = newTotal;
        _orbitSlot = newSlot;
        // 注意：相位 _orbitPhaseDeg 不再使用每只蝙蝠的独立随机值（那会导致所有
        // 蝙蝠堆在一起、槽位间距形同虚设）。改为在 UpdateOrbit 中用全局时间统一
        // 计算，所有蝙蝠拿同一个相位 + 各自 slotAngle 即可严格等距分布。
    }

    public void BeginCooldownWindow()
    {
        _hasAttackedThisCooldown = false;
    }

    public bool CanAttackThisCooldown()
    {
        return !_hasAttackedThisCooldown && _phase == Phase.Orbit;
    }

    public void CommandAttack(Transform target)
    {
        if (target == null) return;
        if (!CanAttackThisCooldown()) return;

        _hasAttackedThisCooldown = true;
        _enemyTarget = target;
        _hitDone = false;
        _phase = Phase.ToEnemy;
    }

    void FixedUpdate()
    {
        if (!cango) return;
        if (_playerAnchor == null)
        {
            if (fatherskill != null && fatherskill.player != null)
                _playerAnchor = fatherskill.player.transform;
            if (_playerAnchor == null)
            {
                Transform p = GameObject.Find("playerlayer")?.transform;
                if (p != null && p.childCount > 0) _playerAnchor = p.GetChild(0);
            }
        }
        if (_playerAnchor == null)
        {
            // 没有玩家锚点时先待机，不直接自毁（避免初始化时序抖动）
            return;
        }

        switch (_phase)
        {
            case Phase.Orbit:
                UpdateOrbit();
                break;
            case Phase.ToEnemy:
                UpdateToEnemy();
                break;
            case Phase.ToPlayer:
                UpdateToPlayer();
                break;
        }
    }

    private void UpdateOrbit()
    {
        // 所有蝙蝠用同一全局相位（Time.time 驱动）+ 各自 slotAngle，
        // 才能保证 N 只蝙蝠等间距环绕玩家、不会聚成一坨。
        _orbitPhaseDeg = (Time.time * orbitAngularSpeedDeg) % 360f;
        float gap = 360f / Mathf.Max(1, _orbitTotal);
        float slotAngle = _orbitSlot * gap;
        Vector3 offset = OrbitOffset(_orbitPhaseDeg + slotAngle, orbitRadius);
        Vector3 targetPos = _playerAnchor.position + offset;
        targetPos.y = _playerAnchor.position.y;
        if (hardLockOrbitPosition)
        {
            transform.position = targetPos;
        }
        else
        {
            if (Vector3.Distance(transform.position, _playerAnchor.position) > orbitRadius * 2.2f)
                transform.position = targetPos; // 掉队时直接拉回轨道
            MoveTowards(targetPos, speed * orbitMoveSpeedMultiplier);
        }
        FaceByDirection(targetPos - transform.position);

        if (periodicReposition)
        {
            _repositionTimer += Time.fixedDeltaTime;
            if (_repositionTimer >= repositionInterval)
            {
                _repositionTimer = 0f;
                RepositionToSlot();
            }
        }

        if (autoAttackInOrbit)
        {
            _autoAttackTimer -= Time.fixedDeltaTime;
            if (_autoAttackTimer <= 0f)
            {
                _autoAttackTimer = autoAttackInterval;
                enemy target = ResolveEnemyForImpact(_playerAnchor.position);
                if (target != null)
                    CommandAttack(target.transform);
            }
        }
    }

    private void UpdateToEnemy()
    {
        if (_enemyTarget == null || _enemyTarget.GetComponent<enemy>()?.rolestate == global::enemy.state.dead)
            _enemyTarget = ResolveEnemyForImpact(transform.position)?.transform;

        if (_enemyTarget == null)
        {
            _phase = Phase.ToPlayer;
            return;
        }

        // 超出技能作用范围则放弃本次扑击，回轨道
        float atkRange = GetSkillAttackRadius();
        if (atkRange > 0f)
        {
            Vector3 center = _playerAnchor != null ? _playerAnchor.position : transform.position;
            float dToCenter = Vector3.Distance(center, _enemyTarget.position);
            if (dToCenter > atkRange + 0.1f)
            {
                _phase = Phase.ToPlayer;
                return;
            }
        }

        Vector3 targetPos = _enemyTarget.position;
        targetPos.y = transform.position.y;
        MoveTowards(targetPos, speed * attackMoveSpeedMultiplier);
        FaceByDirection(targetPos - transform.position);

        float dist = Vector3.Distance(transform.position, targetPos);
        if (!_hitDone && dist <= hitResolveRadius)
        {
            enemy targetEnemy = _enemyTarget != null ? _enemyTarget.GetComponent<enemy>() : null;
            if (targetEnemy != null && targetEnemy.health > 0 && targetEnemy.rolestate != global::enemy.state.dead)
            {
                ApplyDamage(targetEnemy);
                _hitDone = true;
            }
            else
            {
                TryHitEnemyNear(transform.position);
            }
        }

        if (_hitDone || dist <= returnSnapDistance)
        {
            if (!_hitDone) TryHitEnemyNear(transform.position);
            _phase = Phase.ToPlayer;
        }
    }

    private void UpdateToPlayer()
    {
        // 用与 UpdateOrbit 一致的全局相位计算回归点，避免回程时落在过期相位上。
        float phase = (Time.time * orbitAngularSpeedDeg) % 360f;
        float gap = 360f / Mathf.Max(1, _orbitTotal);
        float slotAngle = _orbitSlot * gap;
        Vector3 home = _playerAnchor.position + OrbitOffset(phase + slotAngle, orbitRadius);
        home.y = _playerAnchor.position.y;
        MoveTowards(home, speed * returnMoveSpeedMultiplier);
        FaceByDirection(home - transform.position);

        if (Vector3.Distance(transform.position, home) <= returnSnapDistance)
            _phase = Phase.Orbit;
    }

    private Vector3 OrbitOffset(float angleDeg, float r)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
    }

    private void MoveTowards(Vector3 targetPos, float speedMul)
    {
        float step = Mathf.Max(0.01f, speedMul) * Time.fixedDeltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
    }

    private void FaceByDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude <= 1e-6f) return;

        Vector3 sc = transform.localScale;
        sc.x = dir.x >= 0f ? _baseScaleXAbs : -_baseScaleXAbs;
        transform.localScale = sc;
        if (forceTiltX45) ForceTiltRotation();
    }

    private void ForceTiltRotation()
    {
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private void RepositionToSlot()
    {
        if (_playerAnchor == null) return;
        float phase = (Time.time * orbitAngularSpeedDeg) % 360f;
        float gap = 360f / Mathf.Max(1, _orbitTotal);
        float slotAngle = _orbitSlot * gap;
        Vector3 offset = OrbitOffset(phase + slotAngle, orbitRadius);
        Vector3 p = _playerAnchor.position + offset;
        p.y = _playerAnchor.position.y;
        transform.position = p;
    }

    private void ApplyForcedRedTint()
    {
        if (_allSpriteRenderers == null || _allSpriteRenderers.Length == 0)
            _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        // 性能优化：
        // 1) 不再访问 sr.material —— 那个 getter 会复制材质实例，立刻打破 SRP batching，
        //    使每只蝙蝠至少多一个 DrawCall（多人多蝙蝠场景下肉眼可见地掉帧）。
        //    SpriteRenderer 渲染默认就用 sr.color 做色调乘法，单独设 color 已经够。
        // 2) 仅在颜色和当前不一致时写回，避免在 sprite 不变的帧里反复触发脏标记。
        Color want = forcedRedColor;
        for (int i = 0; i < _allSpriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _allSpriteRenderers[i];
            if (sr == null) continue;
            if (sr.color != want) sr.color = want;
        }
    }

    private SpriteRenderer ResolvePrimaryRenderer()
    {
        if (_allSpriteRenderers == null || _allSpriteRenderers.Length == 0) return null;
        SpriteRenderer best = _allSpriteRenderers[0];
        int bestOrder = best != null ? best.sortingOrder : int.MinValue;
        for (int i = 1; i < _allSpriteRenderers.Length; i++)
        {
            SpriteRenderer sr = _allSpriteRenderers[i];
            if (sr == null) continue;
            if (sr.sortingOrder >= bestOrder)
            {
                bestOrder = sr.sortingOrder;
                best = sr;
            }
        }
        return best;
    }

    private void EnsureRedOverlay()
    {
        if (_baseRenderer == null) return;
        if (_overlayRenderer != null) return;

        GameObject overlayObj = new GameObject("RedOverlay");
        overlayObj.transform.SetParent(_baseRenderer.transform, false);
        _overlayRenderer = overlayObj.AddComponent<SpriteRenderer>();

        if (_overlayMat == null)
        {
            Shader sp = Shader.Find("Sprites/Default");
            if (sp != null) _overlayMat = new Material(sp);
        }
        if (_overlayMat != null) _overlayRenderer.material = _overlayMat;

        _overlayRenderer.sortingLayerID = _baseRenderer.sortingLayerID;
        _overlayRenderer.sortingOrder = _baseRenderer.sortingOrder + 1;
        SyncRedOverlay();
    }

    private void SyncRedOverlay()
    {
        if (_baseRenderer == null) _baseRenderer = ResolvePrimaryRenderer();
        if (_baseRenderer == null) return;
        if (_overlayRenderer == null) EnsureRedOverlay();
        if (_overlayRenderer == null) return;

        _overlayRenderer.sprite = _baseRenderer.sprite;
        _overlayRenderer.flipX = _baseRenderer.flipX;
        _overlayRenderer.flipY = _baseRenderer.flipY;
        _overlayRenderer.enabled = _baseRenderer.enabled;
        _overlayRenderer.transform.localScale = Vector3.one;

        Color c = forcedRedColor;
        c.a = Mathf.Clamp01(redOverlayAlpha);
        _overlayRenderer.color = c;
    }

    private void BuildRedMirrors()
    {
        _redMirrors.Clear();
        if (_allSpriteRenderers == null || _allSpriteRenderers.Length == 0)
            _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < _allSpriteRenderers.Length; i++)
        {
            SpriteRenderer src = _allSpriteRenderers[i];
            if (src == null) continue;
            if (src == _overlayRenderer) continue;
            if (src.gameObject.name.Contains("RedMirror")) continue;

            GameObject go = new GameObject(src.gameObject.name + "_RedMirror");
            go.transform.SetParent(src.transform.parent, false);
            go.transform.localPosition = src.transform.localPosition;
            go.transform.localRotation = src.transform.localRotation;
            go.transform.localScale = src.transform.localScale;

            SpriteRenderer mirror = go.AddComponent<SpriteRenderer>();
            mirror.sortingLayerID = src.sortingLayerID;
            mirror.sortingOrder = src.sortingOrder + redMirrorSortingBoost;
            mirror.maskInteraction = src.maskInteraction;
            mirror.drawMode = src.drawMode;
            mirror.size = src.size;

            _redMirrors.Add(new SpriteMirrorPair { source = src, mirror = mirror });
        }
    }

    private void SyncRedMirrors()
    {
        if (_redMirrors.Count == 0)
        {
            BuildRedMirrors();
            if (_redMirrors.Count == 0) return;
        }

        Color c = forcedRedColor;
        c.a = Mathf.Clamp01(redOverlayAlpha);

        for (int i = _redMirrors.Count - 1; i >= 0; i--)
        {
            SpriteMirrorPair p = _redMirrors[i];
            if (p == null || p.source == null || p.mirror == null)
            {
                _redMirrors.RemoveAt(i);
                continue;
            }

            Transform st = p.source.transform;
            Transform mt = p.mirror.transform;
            mt.SetParent(st.parent, false);
            mt.localPosition = st.localPosition;
            mt.localRotation = st.localRotation;
            mt.localScale = st.localScale;

            p.mirror.enabled = p.source.enabled;
            p.mirror.sprite = p.source.sprite;
            p.mirror.flipX = p.source.flipX;
            p.mirror.flipY = p.source.flipY;
            p.mirror.drawMode = p.source.drawMode;
            p.mirror.size = p.source.size;
            p.mirror.sortingLayerID = p.source.sortingLayerID;
            p.mirror.sortingOrder = p.source.sortingOrder + redMirrorSortingBoost;
            p.mirror.color = c;
        }
    }


    protected override void OnTriggerEnter(Collider other)
    {
        if (_phase != Phase.ToEnemy || _hitDone) return;
        enemy hit = other.GetComponent<enemy>();
        if (hit == null) hit = other.GetComponentInParent<enemy>();
        if (hit == null || hit.health <= 0 || hit.rolestate == global::enemy.state.dead) return;
        // 亡者领域：血脉之蝠不打被控制为友军的目标
        if (hit.GetComponent<MindControlled>() != null) return;

        ApplyDamage(hit);
        _hitDone = true;
        _phase = Phase.ToPlayer;
    }

    private void TryHitEnemyNear(Vector3 pos)
    {
        if (_hitDone) return;

        Transform layer = GetEnemyLayer();
        if (layer == null) return;

        float bestSq = hitResolveRadius * hitResolveRadius;
        enemy best = null;

        foreach (Transform t in layer)
        {
            var en = t.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == global::enemy.state.dead) continue;
            // 亡者领域：血脉蝙蝠扑击不打友军
            if (en._mindControlledFlag) continue;
            Vector3 d = t.position - pos;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = en;
            }
        }

        if (best != null)
            ApplyDamage(best);
        _hitDone = true;
    }

    private enemy ResolveEnemyForImpact(Vector3 pos)
    {
        Transform layer = GetEnemyLayer();
        if (layer == null) return null;

        enemy best = null;
        float bestSq = float.MaxValue;
        float atkRange = GetSkillAttackRadius();
        float maxRangeSq = atkRange > 0f ? atkRange * atkRange : float.MaxValue;
        Vector3 center = _playerAnchor != null ? _playerAnchor.position : pos;
        foreach (Transform t in layer)
        {
            enemy en = t.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == global::enemy.state.dead) continue;
            // 亡者领域：扑击落点搜敌跳过友军
            if (en._mindControlledFlag) continue;
            Vector3 dCenter = t.position - center;
            dCenter.y = 0f;
            float centerSq = dCenter.sqrMagnitude;
            if (centerSq > maxRangeSq) continue;

            Vector3 d = t.position - pos;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = en;
            }
        }
        return best;
    }

    private float GetSkillAttackRadius()
    {
        if (fatherskill is SkillBloodline sb) return sb.attackRadius;
        return 0f;
    }

    private void ApplyDamage(enemy e)
    {
        if (e == null || player == null) return;

        if (e.EVA > Random.value * 100f)
        {
            // 敌人闪避成功：在敌人位置弹青蓝色 Miss
            MissNumber.Show(e.atknumber, e.transform.position);
            return;
        }

        // 伤害公式：技能基础伤害 × (1 + 攻击力 × 0.1)，再走暴击与防御
        float finaldamage = damage * (1f + player.atk * 0.1f);
        bool isCrit = false;
        if (player.CR > Random.value * 100f)
        {
            finaldamage *= player.CD / 100f;
            isCrit = true;
        }
        finaldamage -= e.def;

        int dealt = Mathf.Max(0, (int)finaldamage);
        e.health -= dealt;

        if (e.atknumber != null && DamageNumberSettings.Visible)
        {
            GameObject num = Instantiate(e.atknumber, e.transform.position, default);
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = dealt.ToString();
                if (isCrit) txt.color = new Color32(255, 215, 0, 255);
            }
        }
        e.startturnred();
        TryLifesteal(dealt, e.atknumber);
        // SSR_10 饮血剑：全局吸血（与血族吸血叠加生效）
        EquipmentInitializer.TryAllSourceLifesteal(dealt, e.atknumber, e.transform.position);

        if (e.health <= 0)
            e.Destroy1();
    }

    private void TryLifesteal(int dealt, GameObject floatingTextPrefab)
    {
        if (!_lifestealOn || dealt <= 0 || _lifestealRatio <= 0f || player == null) return;

        var pl = player.GetComponent<Player>();
        if (pl == null) pl = player.GetComponentInParent<Player>();
        if (pl == null) return;

        int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * _lifestealRatio));
        pl.health = Mathf.Min(pl.health + heal, pl.healthmax);

        // 吸血反馈：在玩家身上弹绿色回血数字
        if (floatingTextPrefab != null)
        {
            GameObject num = Instantiate(floatingTextPrefab, pl.transform.position, default);
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = heal.ToString();
                txt.color = new Color32(80, 255, 120, 255);
                txt.fontSize *= 0.65f;
            }
        }
    }
}
