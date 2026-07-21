using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 命途:寄生 的触手弹道。
///
/// 三阶段状态机（每次施放独立一条触手）：
///   Extending  从玩家伸向目标（视觉上像橡胶手指拉长）
///   Impact     命中造成伤害 + 1% 吸血 + 短暂停留；若装备「寄生的暗种」则再向 3m 内另一位敌人弹射一次
///   Retracting 缩回玩家（视觉上像橡胶手指回收），到达后销毁
///
/// 伤害公式复用 BulletBloodlineBat.ApplyDamage 的完整套路（含攻击力 ×0.1、暴击、防御、闪避、
/// 金色暴击飘字、绿色回血飘字），并联动 EquipmentInitializer.TryAllSourceLifesteal（饮血剑）。
///
/// 视觉：一根从玩家指向敌人的红色触手，用 LineRenderer 绘制两端渐变；不依赖 sprite。
/// 后续可通过 <see cref="tentacleSprite"/> 替换为像素触手贴图。
/// </summary>
public class BulletParasite : Bulletbase
{
    private enum Phase { Extending, Impact, Retracting }

    [Header("触手视觉")]
    [Tooltip("触手每秒伸出/缩回速度（世界单位/秒）。")]
    public float extendSpeed  = 30f;
    [Tooltip("触手粗细。")]
    public float tentacleWidth = 0.22f;
    [Tooltip("触手根部颜色（玩家侧）—— 手指皮肤肉色。")]
    public Color rootColor = new Color(0.98f, 0.80f, 0.68f, 1f);
    [Tooltip("触手尖端颜色（爪根侧）—— 略深肉色，模拟血液充盈。")]
    public Color tipColor  = new Color(0.92f, 0.62f, 0.52f, 1f);
    [Tooltip("Impact 阶段的停顿时长（秒），让玩家看清触手到位。")]
    public float impactHold = 0.08f;
    [Tooltip("尖端银白利爪 sprite 的世界尺寸（正方形，单位为米）。")]
    public float clawSize = 1.8f;

    [Header("弹射（装备「寄生的暗种」触发）")]
    [Tooltip("弹射搜敌半径（以当前命中目标为圆心）。")]
    public float bounceRadius = 3f;

    // ── 运行时状态 ────────────────────────────────────────
    private Phase   _phase = Phase.Extending;
    private Transform _ownerAnchor;
    private Transform _enemyTarget;
    private enemy   _enemyTargetCached;   // 命中判定用，避免每帧 GetComponent
    private float   _lifestealRatio = 0.01f;
    private bool    _bounceOnce;
    private bool    _hasBounced;
    private float   _maxRange = 6f;

    private LineRenderer   _line;
    private SpriteRenderer _clawSr;
    private Transform      _clawTr;
    private Vector3 _tipPos;
    private bool    _hitDone;

    // ── 性能缓存：整份触手 sprite/material 全局只加载一次 ────
    private static Sprite   s_clawSpriteCache;
    private static bool     s_clawSpriteTried;
    private static Material s_lineMaterialCache;
    // enemylayer 场景静态节点，Find 一次即可（切场景后 == null 会自动重找）
    private static Transform s_enemyLayerCache;

    /// <summary>创建/获取触手尖端的银白利爪 sprite（寄生兽 Migi 风格直刀片）。</summary>
    private void EnsureClaw()
    {
        if (_clawSr != null) return;

        GameObject go = new GameObject("ParasiteClawTip");
        _clawTr = go.transform;
        _clawTr.SetParent(transform, false);

        _clawSr = go.AddComponent<SpriteRenderer>();
        // 【性能】爪 sprite 全局静态缓存 —— 每次施放都走 LoadSpriteFallback 会做
        //   RenderTexture.Blit + GetPixels32 + BFS 泛洪 + SetPixels32，几万像素级 CPU 操作，
        //   打包后触手一多就明显卡顿。故只在第一次调用时算，之后所有触手复用同一 Sprite。
        Sprite sp = GetOrLoadClawSprite();
        if (sp != null) _clawSr.sprite = sp;
        // 排序层足够高，避免被地图/敌人 sprite 遮挡（其它 sprite 常见 sortingOrder 0~30）
        _clawSr.sortingOrder = 100;
        _clawSr.color = new Color(1f, 1f, 1f, 1f);

        // Sprite 默认 100 PPU，Sprite.Create 后的世界宽度 ≈ (width/PPU)。
        // 用 clawSize / 当前世界宽度 得到统一缩放，让实际展示是 clawSize×clawSize 米。
        if (sp != null)
        {
            float worldW = sp.bounds.size.x;
            float k = worldW > 0.01f ? clawSize / worldW : 1f;
            _clawTr.localScale = new Vector3(k, k, k);
        }
        else
        {
            _clawTr.localScale = new Vector3(clawSize, clawSize, clawSize);
            Debug.LogWarning("[BulletParasite] Resources/Wolf/claw_tip 加载失败：既不是 Sprite 也不是 Texture2D。触手尖端将不显示利爪。");
        }
    }

    /// <summary>
    /// 【性能】爪 sprite 静态缓存入口：首次调用时执行 LoadSpriteFallback（含 Blit + BFS 抠图），
    /// 之后所有触手复用同一份 Sprite（Sprite 本身可被多个 SpriteRenderer 共享，只算一次 draw call 合批）。
    /// 加载失败也会打上"已尝试"标记，避免每根触手都重新去 Resources 找一遍。
    /// </summary>
    private static Sprite GetOrLoadClawSprite()
    {
        if (s_clawSpriteCache != null) return s_clawSpriteCache;
        if (s_clawSpriteTried) return null;
        s_clawSpriteTried = true;
        s_clawSpriteCache = LoadSpriteFallback("Wolf/claw_tip", conservative: true);
        return s_clawSpriteCache;
    }

    /// <summary>
    /// 【性能】触手 LineRenderer 材质全局共享（Sprites/Default）：Shader.Find + new Material
    /// 都是非常昂贵的调用，每根触手都建一份会导致明显卡顿。共享后所有触手可参与合批。
    /// </summary>
    private static Material GetSharedLineMaterial()
    {
        if (s_lineMaterialCache != null) return s_lineMaterialCache;
        Shader sh = Shader.Find("Sprites/Default");
        if (sh == null) return null;
        s_lineMaterialCache = new Material(sh);
        return s_lineMaterialCache;
    }

    /// <summary>
    /// 兜底 sprite 加载 + 自动去背景：
    ///   1. 从 Resources 读原始 Texture2D（不管 TextureType 是 Default/Sprite 都能拿到）；
    ///   2. 用 Graphics.Blit + RenderTexture 复制成一份 RGBA32 格式的 CPU 可读写副本
    ///      （原 texture 常被 Unity 压缩成 DXT/ASTC，直接 SetPixels32 会抛
    ///      "texture uses an unsupported format" 异常）；
    ///   3. 对副本调 <see cref="EquipmentIcon.MakeTextureTransparent"/> 做边缘泛洪去背景；
    ///   4. 用 Sprite.Create 把副本打包为透明 sprite 返回。
    /// 副本创建后与源 texture 完全独立，不再依赖 png meta 里的 isReadable / 压缩格式。
    /// </summary>
    public static Sprite LoadSpriteFallback(string resPath, bool conservative = false)
    {
        Texture2D src = Resources.Load<Texture2D>(resPath);
        if (src == null)
        {
            // 极端情况：连 Texture2D 都拿不到，退回 Resources.Load<Sprite>
            return Resources.Load<Sprite>(resPath);
        }

        Texture2D writable = CreateWritableCopy(src);
        if (writable == null)
        {
            // 复制失败退化：直接用原 texture 造 sprite（保留背景）
            return Sprite.Create(src,
                new Rect(0, 0, src.width, src.height),
                new Vector2(0.5f, 0.5f), 100f);
        }

        // 抠背景（AI 生成图的深色/纯色矩形背景 → 透明）
        try
        {
            if (conservative)
                MakeTextureTransparentConservative(writable);
            else
                EquipmentIcon.MakeTextureTransparent(writable);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BulletParasite.LoadSpriteFallback] 抠背景失败：{ex.Message}，将返回带背景的 sprite");
        }

        return Sprite.Create(writable,
            new Rect(0, 0, writable.width, writable.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    /// <summary>
    /// 保守版抠背景：
    ///   - 只从四角种子做 BFS 泛洪（不采样整圈边缘做直方图，避免把主体色也当成背景色）
    ///   - TOLERANCE 更小（35，只处理和四角背景色高度相似的像素）
    ///   - 不做洞填充（避免把主体内部的浅色/灰色区域误判为"背景残留"抠掉）
    /// 用途：主体本身颜色单一（如银白刀刃）、内部无明显色块的 sprite。
    /// </summary>
    private static void MakeTextureTransparentConservative(Texture2D tex)
    {
        int w = tex.width, h = tex.height;
        Color32[] pixels = tex.GetPixels32();
        bool[] visited = new bool[pixels.Length];

        // 四角种子
        int[] cornerIdx = { 0, w - 1, (h - 1) * w, h * w - 1 };
        var queue = new System.Collections.Generic.Queue<int>();
        var bgColors = new System.Collections.Generic.List<Color32>();
        foreach (int ci in cornerIdx)
        {
            if (ci < 0 || ci >= pixels.Length) continue;
            bgColors.Add(pixels[ci]);
            queue.Enqueue(ci);
            visited[ci] = true;
        }

        const int TOLERANCE = 35;
        int tolSq = TOLERANCE * TOLERANCE;

        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            Color32 c = pixels[idx];

            bool isBg = false;
            for (int k = 0; k < bgColors.Count; k++)
            {
                Color32 bg = bgColors[k];
                int dr = c.r - bg.r, dg = c.g - bg.g, db = c.b - bg.b;
                if (dr * dr + dg * dg + db * db <= tolSq) { isBg = true; break; }
            }
            if (!isBg) continue;

            pixels[idx] = new Color32(0, 0, 0, 0);

            int x = idx % w, y = idx / w;
            if (x > 0)      { int n = idx - 1; if (!visited[n]) { visited[n] = true; queue.Enqueue(n); } }
            if (x < w - 1)  { int n = idx + 1; if (!visited[n]) { visited[n] = true; queue.Enqueue(n); } }
            if (y > 0)      { int n = idx - w; if (!visited[n]) { visited[n] = true; queue.Enqueue(n); } }
            if (y < h - 1)  { int n = idx + w; if (!visited[n]) { visited[n] = true; queue.Enqueue(n); } }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    /// <summary>
    /// 把任意 Texture2D 复制成一份 RGBA32 CPU 可读写副本（绕开 DXT/ASTC 等 GPU 压缩格式限制）。
    /// </summary>
    private static Texture2D CreateWritableCopy(Texture2D src)
    {
        if (src == null) return null;

        RenderTexture rt = RenderTexture.GetTemporary(
            src.width, src.height, 0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);
        try
        {
            Graphics.Blit(src, rt);

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0, false);
            copy.Apply(false, false);
            copy.name = src.name + "_writable_copy";

            RenderTexture.active = prevActive;
            return copy;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BulletParasite.CreateWritableCopy] Blit 失败：{ex.Message}");
            return null;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    /// <summary>把爪 sprite 摆到 <paramref name="tip"/> 位置，沿 root→tip 方向旋转指向前进方向。</summary>
    private void UpdateClawTransform(Vector3 root, Vector3 tip)
    {
        if (_clawTr == null) return;
        _clawTr.position = tip;

        Vector3 dir = tip - root; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-4f) return;
        // sprite 原始朝向：向右（+X）；用 atan2 让它朝向伸出方向；X 轴 45° 是场景常用倾斜
        float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        _clawTr.rotation = Quaternion.Euler(45f, 0f, angle);
    }

    private void SetClawVisible(bool visible)
    {
        if (_clawSr != null) _clawSr.enabled = visible;
    }

    /// <summary>由 SkillParasite.Useskill 施放时统一注入运行时参数。</summary>
    public void SetupParasite(Transform ownerTr, Transform enemyTr,
                              float lifestealRatio, bool bounceOnce, float maxRange)
    {
        _ownerAnchor    = ownerTr;
        _enemyTarget    = enemyTr;
        // 【性能】缓存 enemy 组件，避免 Update / ResolveAimTarget / TryResolveHit 每次都 GetComponent
        _enemyTargetCached = enemyTr != null ? enemyTr.GetComponent<enemy>() : null;
        _lifestealRatio = lifestealRatio;
        _bounceOnce     = bounceOnce;
        _hasBounced     = false;
        _maxRange       = Mathf.Max(1f, maxRange);
    }

    public override void GetFather()
    {
        // 触手不是"抛射体子弹"—— 不能走父类的 rb.velocity 移动流程，
        // 也不能被父类锁 Y 到 1f 破坏其贴地表现。这里手工只继承数值字段。
        if (fatherskill != null)
        {
            damage   = fatherskill.damage;
            level    = fatherskill.level;
            lifetime = fatherskill.lifetime > 0f ? fatherskill.lifetime : 3f;
            speed    = fatherskill.speed;
            size     = fatherskill.size > 0f ? fatherskill.size : 1f;
            // 【性能】player 优先由 fatherskill 直传（SkillParasite 已缓存 _ownerTransform），
            // 避免每根触手都 GameObject.Find("playerlayer")。fatherskill.player 是 GameObject 引用。
            if (player == null && fatherskill.player != null)
                player = fatherskill.player.GetComponent<Attribute>();
        }
        // Rigidbody / Collider 可选（触手用距离判定 + LineRenderer 表现，不依赖物理）
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity  = false;
            rb.isKinematic = true;
        }

        // 触手线渲染器：从根部（玩家）拉到尖端（触手当前伸出位置）
        _line = GetComponent<LineRenderer>();
        if (_line == null) _line = gameObject.AddComponent<LineRenderer>();
        _line.positionCount   = 2;
        _line.useWorldSpace   = true;
        _line.widthMultiplier = tentacleWidth * size;
        // 【性能】用全局共享材质替代每根触手 new Material —— Shader.Find + new Material 都很贵
        Material shared = GetSharedLineMaterial();
        if (shared != null) _line.sharedMaterial = shared;
        _line.startColor      = rootColor;
        _line.endColor        = tipColor;
        _line.numCapVertices  = 4;

        // 尖端银白利爪 sprite（寄生兽风格直刀片），随触手一起伸缩
        EnsureClaw();
    }

    private void Update()
    {
        if (!cango) return;
        if (_ownerAnchor == null || _line == null) { Destroy(gameObject); return; }

        Vector3 root = _ownerAnchor.position + Vector3.up * 0.8f; // 从玩家胸口伸出
        Vector3 aimTarget = ResolveAimTarget(root);

        // 生命周期兜底：避免异常状态永远残留（如目标瞬移出图）
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        switch (_phase)
        {
            case Phase.Extending:
                UpdateExtending(root, aimTarget);
                break;
            case Phase.Impact:
                UpdateImpact(root, aimTarget);
                break;
            case Phase.Retracting:
                UpdateRetracting(root);
                break;
        }
    }

    /// <summary>
    /// 决定触手当前的"瞄准点"：优先目标位置；目标死亡/丢失则朝原初方向继续伸到最远后缩回。
    /// </summary>
    private Vector3 ResolveAimTarget(Vector3 root)
    {
        if (_enemyTarget != null)
        {
            enemy en = _enemyTargetCached;
            if (en != null && en.health > 0 && en.rolestate != global::enemy.state.dead)
            {
                Vector3 p = _enemyTarget.position;
                p.y = root.y;
                // 限制在最大射程内
                Vector3 dir = p - root; dir.y = 0f;
                if (dir.magnitude > _maxRange) p = root + dir.normalized * _maxRange;
                return p;
            }
        }
        // 目标丢失：以当前触手方向继续拉到最远端
        Vector3 fallbackDir = _tipPos - root; fallbackDir.y = 0f;
        if (fallbackDir.sqrMagnitude < 0.01f) fallbackDir = Vector3.right;
        return root + fallbackDir.normalized * _maxRange;
    }

    private void UpdateExtending(Vector3 root, Vector3 aim)
    {
        if (_tipPos == Vector3.zero) _tipPos = root;
        float step = extendSpeed * Time.deltaTime;
        _tipPos = Vector3.MoveTowards(_tipPos, aim, step);
        _line.SetPosition(0, root);
        _line.SetPosition(1, _tipPos);
        SetClawVisible(true);
        UpdateClawTransform(root, _tipPos);

        if (Vector3.Distance(_tipPos, aim) <= 0.05f)
        {
            TryResolveHit();
            _phase = Phase.Impact;
            StartCoroutine(ImpactHold());
        }
    }

    private void UpdateImpact(Vector3 root, Vector3 aim)
    {
        // Impact 阶段仅保持视觉——由 ImpactHold 协程切到 Retracting
        _line.SetPosition(0, root);
        _line.SetPosition(1, _tipPos);
        UpdateClawTransform(root, _tipPos);
    }

    private IEnumerator ImpactHold()
    {
        yield return new WaitForSeconds(impactHold);
        _phase = Phase.Retracting;
    }

    private void UpdateRetracting(Vector3 root)
    {
        float step = extendSpeed * 1.4f * Time.deltaTime; // 缩回快一点
        _tipPos = Vector3.MoveTowards(_tipPos, root, step);
        _line.SetPosition(0, root);
        _line.SetPosition(1, _tipPos);
        UpdateClawTransform(root, _tipPos);

        // 缩回到根部时爪贴脸不好看，快到根部时提前隐藏
        if (Vector3.Distance(_tipPos, root) <= 0.3f)
            SetClawVisible(false);

        if (Vector3.Distance(_tipPos, root) <= 0.08f)
            Destroy(gameObject);
    }

    // ── 命中结算 ─────────────────────────────────────────
    /// <summary>
    /// 到达目标后统一结算命中：先对主目标造成伤害，若装备「寄生的暗种」则再对 3m 内另一敌人弹射一次。
    /// </summary>
    private void TryResolveHit()
    {
        if (_hitDone) return;
        _hitDone = true;

        // 主目标伤害
        if (_enemyTarget != null)
        {
            enemy en = _enemyTargetCached;
            if (en != null && en.health > 0 && en.rolestate != global::enemy.state.dead
                && !en._mindControlledFlag)
            {
                ApplyDamage(en);
            }
        }

        // 弹射（一次）
        if (_bounceOnce && !_hasBounced && _enemyTarget != null)
        {
            enemy second = FindBounceTarget(_enemyTarget);
            if (second != null)
            {
                _hasBounced = true;
                ApplyDamage(second);
                // 视觉上让触手尖端顺势跳到弹射目标位置 —— 靠 lifetime 内的 Retracting 阶段自然收回
                _tipPos = second.transform.position;
                _tipPos.y = _ownerAnchor != null ? _ownerAnchor.position.y + 0.8f : _tipPos.y;
            }
        }
    }

    private enemy FindBounceTarget(Transform excludeTr)
    {
        // 【性能】静态缓存 enemylayer Transform；场景切换后 s_enemyLayerCache 变 null（Unity fake null）会自动重找
        if (s_enemyLayerCache == null)
        {
            var go = GameObject.Find("enemylayer");
            s_enemyLayerCache = go != null ? go.transform : null;
        }
        Transform layer = s_enemyLayerCache;
        if (layer == null) return null;

        Vector3 center = excludeTr != null ? excludeTr.position : transform.position;
        float bestSq  = bounceRadius * bounceRadius;
        enemy best    = null;

        int cnt = layer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = layer.GetChild(i);
            if (t == null || t == excludeTr) continue;
            enemy en = t.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == global::enemy.state.dead) continue;
            if (en._mindControlledFlag) continue;

            Vector3 d = t.position - center; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq <= bestSq)
            {
                bestSq = sq;
                best   = en;
            }
        }
        return best;
    }

    /// <summary>
    /// 与 BulletBloodlineBat.ApplyDamage 一致的完整伤害结算（含暴击/防御/闪避 + 吸血 + 全局吸血）。
    /// </summary>
    private void ApplyDamage(enemy e)
    {
        if (e == null || player == null) return;

        // 闪避
        if (e.EVA > Random.value * 100f)
        {
            MissNumber.Show(e.atknumber, e.transform.position);
            return;
        }

        float finaldamage = damage * (1f + player.atk * 0.1f);
        bool isCrit = false;
        if (player.CR > Random.value * 100f)
        {
            finaldamage *= player.CD / 100f;
            isCrit = true;
        }
        finaldamage -= e.def;
        if (finaldamage < 1f) finaldamage = 1f;
        int dealt = (int)finaldamage;

        // 会话追踪
        if (GameSessionTracker.Instance != null && fatherskill != null)
            GameSessionTracker.Instance.RecordDamage(fatherskill.Skillname, dealt);

        e.health -= dealt;

        if (e.atknumber != null && DamageNumberSettings.Visible)
        {
            GameObject num = Instantiate(e.atknumber, e.transform.position, default);
            num.transform.localScale *= DamageNumberSettings.SizeScale;
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = dealt.ToString();
                if (isCrit) txt.color = new Color32(255, 215, 0, 255);
            }
        }
        e.startturnred();

        // 命途:寄生自带 1% 吸血
        TryLifesteal(dealt, e.atknumber);

        // SSR_10 饮血剑：全局吸血叠加
        EquipmentInitializer.TryAllSourceLifesteal(dealt, e.atknumber, e.transform.position);

        // 命中音效（走 Skillbase 的通用派发；命途:寄生 Skillname 不含火/冰关键字，走默认无声）
        if (fatherskill != null) fatherskill.PlayHitSfx();
        else AudioManager.PlaySfx(AudioManager.SfxKey.Hit);

        if (e.health <= 0)
            e.Destroy1();
    }

    private void TryLifesteal(int dealt, GameObject floatingTextPrefab)
    {
        if (dealt <= 0 || _lifestealRatio <= 0f || player == null) return;

        Player pl = player.GetComponent<Player>();
        if (pl == null) pl = player.GetComponentInParent<Player>();
        if (pl == null) return;
        if (pl.health >= pl.healthmax) return;

        int heal = Mathf.Max(1, Mathf.RoundToInt(dealt * _lifestealRatio));
        pl.health = Mathf.Min(pl.healthmax, pl.health + heal);

        if (floatingTextPrefab != null && DamageNumberSettings.Visible)
        {
            GameObject num = Instantiate(floatingTextPrefab, pl.transform.position, default);
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = "+" + heal;
                txt.color = new Color32(80, 255, 120, 255);
                txt.fontSize *= 0.65f;
            }
        }
    }

    // 触手不走父类 OnTriggerEnter 的通用子弹碰撞逻辑（我们靠距离结算），
    // 但保留 override 以避免父类流程被意外触发（例如 Instantiate 时 prefab 自带 Collider）
    protected override void OnTriggerEnter(Collider other) { /* no-op */ }
}
