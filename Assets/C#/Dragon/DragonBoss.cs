using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 最终龙王 Boss（N13 关底，无时间限制）。继承 enemy，复用受伤/掉落框架，自带完整状态机。
///
/// —— 策划案实现 ——
/// 开场：停止一切 BGM → 10s 内翅膀扇动声由疏到密、身影从高空由远及近 → 龙吼 → 约2s 从天而降砸地。
/// 倒计时区：进场显示鲜红「呵呵」，随形态切换为对应文字与颜色。
///
/// 5 个元素形态（各自独立满血血条；某形态血量清空→起飞→下一形态从天而降回满血）：
///   1. 红色火龙   Fire  【以何救赎】：龙吼砸地→3s 旋转横扫火焰吐息(10sCD) + 蓝焰喷飞标·命中灼烧(3sCD)
///   2. 黑色蝙蝠龙 Bat   【猩红剧院】：起飞抓取玩家带至半空+全屏撕咬动画(10sCD)+施加吸血反噬debuff 5s + 血刃弹幕
///   3. 白色钢化利爪龙 Steel【逐无变者】：弹刺利爪震地固定玩家(5sCD，固定成功立即追加龙卷风) + 龙卷风追击(10sCD)
///   4. 史莱姆龙   Slime 【无形塑者】：史莱姆凝弹(3.5sCD) + 定期召唤史莱姆分裂弹群(7sCD)
///   5. 黄金龙     Gold  【永不熄灭】：狂暴——黄金瞳全屏强控1s(6sCD)+金龙鳞环射追踪(2.5sCD)，移速/伤害提升
///
/// 通用：所有形态自带 0.05%/s 自然回血；不会被亡者领域复活；每击败一个世界Boss全形态攻击-5%(上限-20%,独立乘区)。
/// 黄金龙被击败→死亡动画化为原地金鳞雕塑。
///
/// 动画：每形态 3 帧扑翼行走序列（base/翅上扬/翅下压，由原图垂直形变派生，零抖动）+ 悬浮 bob，纯代码构建。
/// </summary>
public class DragonBoss : enemy
{
    public enum DragonPhase { Entrance, Fire, Bat, Steel, Slime, Gold, Dead }
    private DragonPhase _phase = DragonPhase.Entrance;
    private const int LAST_COMBAT_PHASE = 4;   // 0火 1蝠 2钢 3史 4金
    private int _phaseIndex = 0;

    // ── 由 Builder 注入 ──
    [System.NonSerialized] public Sprite[][] phaseFrames;   // [5][3] 每形态 3 帧行走序列
    [System.NonSerialized] public Sprite fireballSprite, blueDartSprite, tornadoSprite, slimeBlobSprite, goldScaleSprite;
    // 火系补充素材：红火焰喷飞标 / 火焰吐息束 / 玩家灼烧红焰
    [System.NonSerialized] public Sprite redDartSprite, fireBreathSprite, burnFlameSprite;
    // 蝙蝠血刃「毒镖」/ 黄金龙死亡全屏特效两帧
    [System.NonSerialized] public Sprite bloodBladeSprite, goldDeath1Sprite, goldDeath2Sprite;
    // 史莱姆龙复用「史莱姆世界Boss」的手持武器资产（吞噬 5 次得剑、10 次得弓）
    [System.NonSerialized] public Sprite     slimeSwordSprite, slimeBowSprite;
    [System.NonSerialized] public GameObject slimeSwordQiPrefab, slimeArrowPrefab;
    [HideInInspector] public battleUI battleUI;

    public GameObject AtkNumberPrefab => atknumber;

    // ── 基础参数 ──
    public float bossScale       = 1.0f;
    public float goldScale       = 1.18f;
    public int   perPhaseHealth  = 500;
    public float baseAtk         = 50f;
    public float baseDef         = 50f;
    public float moveSpeed       = 4.5f;
    public float contactDamageCd = 0.6f;
    public float naturalHealPct  = 0.0005f; // 0.05%/s

    // ── 运行时 ──
    private SpriteRenderer _sr;
    private Rigidbody      _rb;
    private bool  _busy, _invincible, _transitioning;
    private int   _lockedHealth;
    private float _cdA, _cdB;          // 当前形态两个技能独立 CD
    private float _contactTimer, _busyWatchdog;
    private float _hoverBaseY; private bool _hoverYSet;
    private float _animT, _frameTimer; private int _frameSeqIdx;
    private float _facing = 1f;
    private float _dmgMul = 1f;         // 黄金龙狂暴伤害倍率
    private float _healAccum;
    private static readonly int[] FLAP_SEQ = { 0, 1, 0, 2 }; // 扑翼循环

    // ── 碰撞忽略：龙与其它单位（含被控制的友军）互不物理碰撞 ──
    private Collider _selfCol; private float _ignoreColCd;
    private void RefreshIgnoreCollisions()
    {
        if (_selfCol == null) _selfCol = GetComponent<Collider>();
        if (_selfCol == null) return;
        int myLayer = gameObject.layer;   // 敌人层：被控制的友军仍在此层
        var hits = Physics.OverlapSphere(transform.position, 20f);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h == null || h == _selfCol) continue;
            // 仅忽略同为敌人层的单位（友军/其它怪），不影响玩家层/子弹层对龙的命中判定
            if (h.gameObject.layer != myLayer) continue;
            Physics.IgnoreCollision(_selfCol, h, true);
        }
    }

    // ── 史莱姆龙：吞噬增益 + 环绕武器（无限吞噬，剑/弓交替生成，绕 Boss 均匀公转防重叠）──
    private int   _absorbedCount;
    private float _cdDevour;
    private readonly System.Collections.Generic.List<DragonSlimeWeapon> _slimeWeapons = new System.Collections.Generic.List<DragonSlimeWeapon>();
    private float _orbitPhase;
    private const float WEAPON_ORBIT_RADIUS = 5.5f;

    private static readonly Color FIRE_COL  = new Color(1f, 0.45f, 0.1f, 1f);
    private static readonly Color BAT_COL   = new Color(0.75f, 0.1f, 0.2f, 1f);
    private static readonly Color STEEL_COL = new Color(0.8f, 0.85f, 0.95f, 1f);
    private static readonly Color SLIME_COL = new Color(0.2f, 0.85f, 0.95f, 1f);
    private static readonly Color GOLD_COL  = new Color(1f, 0.85f, 0.15f, 1f);

    // 分阶段倒计时文字与颜色
    private static readonly string[] PHASE_TEXT = { "【以何救赎】", "【猩红剧院】", "【逐无变者】", "【无形塑者】", "【永不熄灭】" };
    private static readonly Color[]  PHASE_TEXT_COL = {
        new Color(1f, 0.25f, 0.12f), new Color(0.85f, 0.06f, 0.18f), new Color(0.75f, 0.82f, 0.95f),
        new Color(0.15f, 0.85f, 0.85f), new Color(1f, 0.82f, 0.15f)
    };

    // ───────────────────────────── 生命周期 ─────────────────────────────
    protected new void OnEnable()
    {
        playerlayer = GameObject.Find("playerlayer")?.transform;
        _sr = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody>();
        if (_rb != null) { _rb.isKinematic = true; _rb.useGravity = false; }

        rolename = "最终龙王";

        float hpMul = 1f, atkMul = 1f;
        if (DifficultyManager.Instance != null)
        {
            hpMul  = DifficultyManager.Instance.Current.hpMultiplier;
            atkMul = DifficultyManager.Instance.Current.atkMultiplier;
        }
        healthmax = Mathf.Max(1, Mathf.RoundToInt(perPhaseHealth * hpMul));
        health    = healthmax;
        def       = baseDef;

        int wbDefeated = WorldBossManager.Instance != null ? WorldBossManager.Instance.DefeatedCountThisRun : 0;
        float atkReduction = Mathf.Min(0.20f, wbDefeated * 0.05f);
        atk = baseAtk * atkMul * (1f - atkReduction);

        _phase = DragonPhase.Entrance;
        _phaseIndex = 0; _busy = false; _invincible = true; _transitioning = false;
        _dmgMul = 1f;
        rolestate = state.move;
        Sca = bossScale;
        SetFrame(0, 0);

        StartCoroutine(EntranceRoutine());
    }

    private void SetFrame(int phaseIdx, int frame)
    {
        if (_sr == null || phaseFrames == null) return;
        if (phaseIdx < 0 || phaseIdx >= phaseFrames.Length) return;
        var frames = phaseFrames[phaseIdx];
        if (frames == null || frames.Length == 0) return;
        int f = Mathf.Clamp(frame, 0, frames.Length - 1);
        if (frames[f] != null) _sr.sprite = frames[f];
    }

    // ───────────────────────────── 开场：10s 逼近 + 龙吼 + 天降 ─────────────────────────────
    private IEnumerator EntranceRoutine()
    {
        _invincible = true; _lockedHealth = health;

        battleUI?.EnterDragonBossMode();
        battleUI?.SetBossCountdownText("呵呵", new Color(1f, 0.05f, 0.05f));
        AudioManager.StopBgm();

        getrole();
        Vector3 ground = transform.position;
        if (role != null) ground = role.transform.position + new Vector3(6f * _facing, 0f, 0f);

        // 10s：翅膀声由疏到密（越来越频繁），身影从高空由远及近、逐渐放大变清晰
        float total = 10f, t = 0f, flapTimer = 0f;
        Vector3 farPos = ground + new Vector3(0f, 22f, 16f);   // 远高空
        Vector3 nearPos = ground + new Vector3(0f, 10f, 6f);   // 逼近点（仍在空中）
        transform.position = farPos;
        while (t < total)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / total);
            transform.position = Vector3.Lerp(farPos, nearPos, k);
            float s = Mathf.Lerp(bossScale * 0.35f, bossScale * 0.8f, k);
            transform.localScale = new Vector3(_facing * s, s, s);
            if (_sr != null) _sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.25f, 0.85f, k));
            // 扇翅序列 + 声音：间隔从 1.1s 缩短到 0.18s（越来越频繁）
            float interval = Mathf.Lerp(1.1f, 0.18f, k);
            flapTimer -= Time.deltaTime;
            if (flapTimer <= 0f)
            {
                flapTimer = interval;
                AudioManager.PlaySfx(AudioManager.SfxKey.DragonFlap);
                SetFrame(0, (_frameSeqIdx++ % 2 == 0) ? 1 : 2); // 上扬/下压交替
            }
            _lockedHealth = health;
            yield return null;
        }

        // 龙吼
        AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
        yield return new WaitForSeconds(0.5f);

        // 约2s 从天而降
        Vector3 sky = ground + new Vector3(0f, 16f, 6f);
        transform.position = sky;
        t = 0f;
        while (t < 1.4f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.4f));
            transform.position = Vector3.Lerp(sky, ground, k);
            float s = Mathf.Lerp(bossScale * 0.8f, bossScale, k);
            transform.localScale = new Vector3(_facing * s, s, s);
            if (_sr != null) _sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.85f, 1f, k));
            _lockedHealth = health;
            yield return null;
        }
        transform.localScale = new Vector3(_facing * bossScale, bossScale, bossScale);
        if (_sr != null) _sr.color = Color.white;

        // 落地砸地冲击
        SpawnRingFx(transform.position, FIRE_COL, 7f);
        DamagePlayersInRadius(transform.position, 7f, Mathf.RoundToInt(atk * 1.3f), 3.5f, 0f, false);

        // 翅膀声 + 天降落地后，奏响激昂的龙王战斗 BGM（循环）
        AudioManager.PlayBgm(AudioManager.BgmKey.DragonBattle);

        EnterPhase(0);
        _invincible = false; _busy = false;
    }

    // ───────────────────────────── 主循环 ─────────────────────────────
    protected override void FixedUpdate()
    {
        if (_phase == DragonPhase.Dead || _phase == DragonPhase.Entrance) return;
        if (GetComponent<MindControlled>() != null) return;

        float dt = Time.fixedDeltaTime;
        TickNaturalHeal(dt);

        // 与友军/其它单位彻底不发生物理碰撞（防止被控制的友军把龙挤到天上）
        _ignoreColCd -= dt;
        if (_ignoreColCd <= 0f) { _ignoreColCd = 0.5f; RefreshIgnoreCollisions(); }

        if (_busy)
        {
            _busyWatchdog += dt;
            if (_busyWatchdog > 10f)
            {
                StopAllCoroutines();
                _busy = false; _transitioning = false; _invincible = false;
                RestorePinnedSprite();
                SetPlayersMovementLocked(false);
                _cdA = 1f; _cdB = 1f; _busyWatchdog = 0f;
            }
            TickContactDamage(dt);
            return;
        }
        _busyWatchdog = 0f;

        if (role == null) { getrole(); return; }
        FaceTarget();

        // 朝玩家移动但保持一定距离，不贴到玩家脸上
        Vector3 to = role.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > 6f)
            transform.position += to.normalized * moveSpeed * dt;

        TickContactDamage(dt);

        // 史莱姆形态：吞噬增益 + 手持武器（独立于 A/B 技能）
        if (_phase == DragonPhase.Slime) TickSlimeAbilities(dt);

        _cdA -= dt; _cdB -= dt;
        if (_cdA <= 0f) { TriggerSkillA(); }
        else if (_cdB <= 0f) { TriggerSkillB(); }
    }

    // ═══════════════════════ 史莱姆龙：吞噬增益 + 环绕武器 ═══════════════════════
    private void TickSlimeAbilities(float dt)
    {
        // 吞噬：定时吸收史莱姆精华 → 永久 +5% 最大生命上限并同步回血，累计吞噬次数
        _cdDevour -= dt;
        if (_cdDevour <= 0f) { _cdDevour = 4.5f; DoDevour(); }

        // 无限生成：每满 5 次吞噬生成一件武器（剑/弓交替：5剑 10弓 15剑 20弓 …），排上环绕轨道
        int shouldHave = _absorbedCount / 5;
        while (_slimeWeapons.Count < shouldHave)
        {
            bool isBow = (_slimeWeapons.Count % 2) == 1; // 0剑 1弓 2剑 3弓…
            SpawnSlimeWeapon(isBow);
        }

        // 逐个武器到点开火
        for (int i = 0; i < _slimeWeapons.Count; i++)
        {
            var w = _slimeWeapons[i];
            if (w == null || w.busy) continue;
            w.fireCd -= dt;
            if (w.fireCd <= 0f)
            {
                w.fireCd = 6f;
                if (w.isBow) StartCoroutine(SlimeBowDraw(w));
                else StartCoroutine(SlimeSwordSwing(w));
            }
        }
    }

    private void DoDevour()
    {
        int hpGain = Mathf.Max(1, Mathf.RoundToInt(healthmax * 0.02f));
        healthmax += hpGain;
        health = Mathf.Min(healthmax, health + hpGain);
        if (_invincible) _lockedHealth = health;
        atk = Mathf.Max(atk + 1f, atk * 1.02f);
        _absorbedCount++;
        SpawnRingFx(transform.position, SLIME_COL, 5f);
    }

    // 生成一件环绕武器（世界空间，不作 Boss 子物体，避免受 Boss 翻转/缩放影响；由 LateUpdate 排布轨道）
    private void SpawnSlimeWeapon(bool isBow)
    {
        Sprite spr = isBow ? slimeBowSprite : slimeSwordSprite;
        var go = new GameObject(isBow ? "SlimeOrbitBow" : "SlimeOrbitSword");
        go.transform.position = transform.position;
        go.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        float baseSize = spr != null ? Mathf.Max(0.01f, spr.bounds.size.x) : 1f;
        float targetW = isBow ? 1.4f : 2.4f;   // 弓比剑小，避免过大喧宾夺主
        float s = targetW / baseSize;
        go.transform.localScale = new Vector3(s, s, s);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = spr;
        sr.material = new Material(Shader.Find("Sprites/Default"));
        sr.sortingOrder = (_sr != null ? _sr.sortingOrder : 5) + 2;
        var w = go.AddComponent<DragonSlimeWeapon>();
        w.isBow = isBow; w.sr = sr;
        w.fireCd = 2.5f + _slimeWeapons.Count * 0.4f; // 错开各武器首次开火
        _slimeWeapons.Add(w);
    }

    // 每帧把武器均匀排在 Boss 周围的公转轨道上（防重叠）
    private void UpdateSlimeWeaponOrbit()
    {
        int n = _slimeWeapons.Count;
        if (n == 0) return;
        _orbitPhase += Time.deltaTime * 40f; // 度/秒 公转
        for (int i = 0; i < n; i++)
        {
            var w = _slimeWeapons[i];
            if (w == null) continue;
            if (w.busy) continue; // 开火演出期间不强制归位
            float ang = (_orbitPhase + i * (360f / n)) * Mathf.Deg2Rad;
            w.transform.position = transform.position +
                new Vector3(Mathf.Cos(ang) * WEAPON_ORBIT_RADIUS, 0.8f, Mathf.Sin(ang) * WEAPON_ORBIT_RADIUS);
        }
    }

    private IEnumerator SlimeSwordSwing(DragonSlimeWeapon w)
    {
        if (w == null) yield break;
        w.busy = true;
        Transform tr = w.transform;
        yield return LerpWeaponZ(tr, 0f, -85f, 0.14f);   // 蓄力后仰
        yield return LerpWeaponZ(tr, -85f, 90f, 0.12f);  // 猛挥
        if (slimeSwordQiPrefab != null)
        {
            Vector3 dir = DirFromTo(tr.position, PlayerBodyPoint());
            Vector3 spawnPos = tr.position + dir * 0.9f + Vector3.up * 0.3f;
            var obj = Instantiate(slimeSwordQiPrefab, spawnPos, Quaternion.Euler(45, 0, 0));
            var proj = obj.GetComponent<SlimeBossProjectile>();
            if (proj != null) { proj.flipFacing = true; proj.Launch(dir, Dmg(1.2f), 12f, 1.6f); }
        }
        yield return LerpWeaponZ(tr, 90f, 0f, 0.18f);    // 收回
        w.busy = false;
    }

    private IEnumerator SlimeBowDraw(DragonSlimeWeapon w)
    {
        if (w == null) yield break;
        w.busy = true;
        Transform tr = w.transform;
        Vector3 baseScale = tr.localScale;
        float t = 0f;
        while (t < 0.28f && tr != null)
        {
            t += Time.deltaTime; float k = Mathf.Clamp01(t / 0.28f);
            tr.localScale = new Vector3(baseScale.x * (1f - 0.15f * k), baseScale.y * (1f + 0.1f * k), baseScale.z);
            yield return null;
        }
        if (slimeArrowPrefab != null && role != null)
        {
            Vector3 baseDir = DirFromTo(tr.position, PlayerBodyPoint());
            float baseAng = Mathf.Atan2(baseDir.z, baseDir.x) * Mathf.Rad2Deg;
            int cnt = 5; float spread = 35f;
            float start = baseAng - spread * 0.5f, stepA = spread / (cnt - 1);
            Vector3 spawnPos = tr.position + baseDir * 0.9f + Vector3.up * 0.3f;
            int dmg = Dmg(0.6f);
            for (int i = 0; i < cnt; i++)
            {
                float ang = (start + stepA * i) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                var obj = Instantiate(slimeArrowPrefab, spawnPos, Quaternion.Euler(45, 0, 0));
                var proj = obj.GetComponent<SlimeBossProjectile>();
                if (proj != null) proj.Launch(d, dmg, 18f, 3f);
            }
        }
        if (tr != null) tr.localScale = baseScale;
        w.busy = false;
    }

    private Vector3 PlayerBodyPoint()
        => role != null ? role.transform.position + Vector3.up * 1.0f : transform.position + new Vector3(_facing, 0, 0);
    private Vector3 DirFromTo(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from; d.y = 0;
        return d.sqrMagnitude > 0.01f ? d.normalized : new Vector3(_facing, 0, 0);
    }

    // 保留 X=45° 斜视角倾斜，绕 Z 旋转做挥砍
    private IEnumerator LerpWeaponZ(Transform tr, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur && tr != null)
        {
            t += Time.deltaTime;
            tr.rotation = Quaternion.Euler(45f, 0f, Mathf.Lerp(from, to, Mathf.Clamp01(t / dur)));
            yield return null;
        }
        if (tr != null) tr.rotation = Quaternion.Euler(45f, 0f, to);
    }

    private void DestroySlimeWeapons()
    {
        for (int i = 0; i < _slimeWeapons.Count; i++)
            if (_slimeWeapons[i] != null) Destroy(_slimeWeapons[i].gameObject);
        _slimeWeapons.Clear();
    }

    private void TriggerSkillA()
    {
        switch (_phase)
        {
            case DragonPhase.Fire:  _cdA = 10f; StartCoroutine(SkFireSweep());   break;
            case DragonPhase.Bat:   _cdA = 10f; StartCoroutine(SkBatGrab());     break;
            case DragonPhase.Steel: _cdA = 10f; StartCoroutine(SkTornado());     break;
            case DragonPhase.Slime: _cdA = 7f;  StartCoroutine(SkSlimeSummon()); break;
            case DragonPhase.Gold:  _cdA = IsInnocencePlayer() ? 10f : 6f;  StartCoroutine(SkGoldControl()); break;
        }
    }
    private void TriggerSkillB()
    {
        switch (_phase)
        {
            case DragonPhase.Fire:  _cdB = 3f;   StartCoroutine(SkFireDarts());    break;
            case DragonPhase.Bat:   _cdB = 4f;   StartCoroutine(SkBatVolley());    break;
            case DragonPhase.Steel: _cdB = 5f;   StartCoroutine(SkClawPin());      break;
            case DragonPhase.Slime: _cdB = 3.5f; StartCoroutine(SkSlimeSpit());    break;
            case DragonPhase.Gold:  _cdB = 2.5f; StartCoroutine(SkGoldScaleRing());break;
        }
    }

    private void TickNaturalHeal(float dt)
    {
        if (_invincible || health <= 0 || health >= healthmax) return;
        _healAccum += healthmax * naturalHealPct * dt;
        if (_healAccum >= 1f)
        {
            int g = (int)_healAccum; _healAccum -= g;
            health = Mathf.Min(healthmax, health + g);
        }
    }

    // 程序化动画：帧序列扑翼 + 悬浮 bob + 朝向翻转
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_phase == DragonPhase.Dead) return;
        if (_invincible) health = _lockedHealth;

        if (role != null) { _hoverBaseY = role.transform.position.y; _hoverYSet = true; }
        _animT += Time.deltaTime;

        if (_phase == DragonPhase.Entrance || _transitioning) return; // 进场/切换由协程控位

        // 悬浮
        float bob = Mathf.Sin(_animT * 2f) * 0.35f;
        if (_hoverYSet)
        {
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, _hoverBaseY + 1.6f + bob, 0.2f);
            transform.position = p;
        }

        // 扑翼帧序列（移动/悬浮时循环）
        _frameTimer -= Time.deltaTime;
        if (_frameTimer <= 0f)
        {
            _frameTimer = 0.13f;
            _frameSeqIdx = (_frameSeqIdx + 1) % FLAP_SEQ.Length;
            SetFrame(_phaseIndex, FLAP_SEQ[_frameSeqIdx]);
        }

        float cur = (_phase == DragonPhase.Gold) ? goldScale : bossScale;
        transform.localScale = new Vector3(_facing * cur, cur, cur);

        // 史莱姆环绕武器公转排布（防重叠）
        if (_phase == DragonPhase.Slime && _slimeWeapons.Count > 0) UpdateSlimeWeaponOrbit();
    }

    private void FaceTarget()
    {
        if (role == null) return;
        float dx = role.transform.position.x - transform.position.x;
        if (dx > 0.05f) _facing = 1f;
        else if (dx < -0.05f) _facing = -1f;
    }

    // ───────────────────────────── 形态切换（起飞→天降）─────────────────────────────
    private void EnterPhase(int idx)
    {
        _phaseIndex = Mathf.Clamp(idx, 0, 4);
        _phase = (DragonPhase)((int)DragonPhase.Fire + _phaseIndex);
        SetFrame(_phaseIndex, 0);

        health = healthmax; _lockedHealth = health;
        _cdA = 2f; _cdB = 1.2f; // 进形态短暂缓冲

        def    = (_phase == DragonPhase.Steel) ? baseDef * 2.2f : baseDef;
        _dmgMul = (_phase == DragonPhase.Gold) ? 1.3f : 1f;
        Sca    = (_phase == DragonPhase.Gold) ? goldScale : bossScale;
        if (_phase == DragonPhase.Gold) moveSpeed = 6.5f;

        // 进入史莱姆形态：重置吞噬计数（首次吞噬稍作缓冲）；其它形态清理环绕武器
        if (_phase == DragonPhase.Slime)
        {
            _absorbedCount = 0;
            _cdDevour = 3f;
            DestroySlimeWeapons();
        }
        else DestroySlimeWeapons();

        battleUI?.SetBossCountdownText(PHASE_TEXT[_phaseIndex], PHASE_TEXT_COL[_phaseIndex]);
    }

    public override void Destroy1()
    {
        if (_phase == DragonPhase.Dead) return;
        if (_invincible || _transitioning) { health = _lockedHealth; return; }

        StopAllCoroutines();
        _busy = false;
        RestorePinnedSprite();          // 兜底：被击杀打断钢化抓取时恢复玩家精灵角度
        SetPlayersMovementLocked(false);

        if (_phaseIndex < LAST_COMBAT_PHASE) StartCoroutine(PhaseTransitionRoutine(_phaseIndex + 1));
        else StartCoroutine(DeathRoutine());
    }

    private IEnumerator PhaseTransitionRoutine(int nextIdx)
    {
        _transitioning = true; _invincible = true; _busy = true;
        health = 1; _lockedHealth = 1;
        SetPlayersMovementLocked(false);

        Color c = PhaseColorByIndex(_phaseIndex);
        SpawnRingFx(transform.position, c, 8f);
        DamagePlayersInRadius(transform.position, 8f, Mathf.RoundToInt(atk), 4f, 0f, false);
        AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);

        // 起飞离场
        Vector3 start = transform.position;
        Vector3 up = start + new Vector3(0f, 18f, 6f);
        float t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            transform.position = Vector3.Lerp(start, up, k);
            float s = Mathf.Lerp(Sca, Sca * 0.5f, k);
            transform.localScale = new Vector3(_facing * s, s, s);
            if (_sr != null) _sr.color = new Color(1f, 1f, 1f, 1f - k * 0.5f);
            _lockedHealth = health;
            yield return null;
        }

        // 切换形态
        bool berserk = (nextIdx == 4);
        EnterPhase(nextIdx);
        if (berserk) { DragonScreenFx.Flash(GOLD_COL, 1.0f); }

        // 从天而降
        getrole();
        Vector3 land = role != null ? role.transform.position + new Vector3(5f * _facing, 0f, 0f) : start;
        Vector3 sky = land + new Vector3(0f, 18f, 6f);
        transform.position = sky;
        if (_sr != null) _sr.color = Color.white;
        t = 0f;
        float target = (_phase == DragonPhase.Gold) ? goldScale : bossScale;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.7f);
            transform.position = Vector3.Lerp(sky, land, k);
            float s = Mathf.Lerp(target * 0.5f, target, k);
            transform.localScale = new Vector3(_facing * s, s, s);
            _lockedHealth = health;
            yield return null;
        }
        SpawnRingFx(transform.position, PhaseColorByIndex(_phaseIndex), 7f);
        DamagePlayersInRadius(transform.position, 7f, Mathf.RoundToInt(atk), 3.5f, 0f, false);

        yield return new WaitForSeconds(0.3f);
        _transitioning = false; _invincible = false; _busy = false;
    }

    private IEnumerator DeathRoutine()
    {
        _phase = DragonPhase.Dead; rolestate = state.dead;
        _busy = true; _invincible = false;
        RestorePinnedSprite();
        DestroySlimeWeapons();
        SetPlayersMovementLocked(false);
        battleUI?.OnBossDefeated();
        foreach (var col in GetComponents<Collider>()) col.enabled = false;

        // 不在此处停止 BGM——BGM 持续到返回主菜单才停
        AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
        AudioManager.PlaySfx(AudioManager.SfxKey.TombRevive);

        // 全屏 AI 特效：金色大爆炸扩张 + 旋转金色光芒（不再只是改色）
        DragonScreenFx.Flash(new Color(1f, 0.95f, 0.7f), 0.5f);
        DragonFullScreenFx.Show(goldDeath1Sprite, 1.5f, 0.25f, 1.4f, 0f, 1f);    // 爆炸由小到大
        DragonFullScreenFx.Show(goldDeath2Sprite, 2.8f, 0.6f, 1.6f, 22f, 0.9f);  // 光芒缓慢旋转
        SpawnRingFx(transform.position, GOLD_COL, 13f);

        if (expstone != null) Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        // 龙身：镀金爆亮 → 抖动 → 溶解淡出
        float t = 0f;
        Vector3 basePos = transform.position;
        while (t < 2.8f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 2.8f);
            if (_sr != null)
                _sr.color = Color.Lerp(new Color(1f, 0.95f, 0.55f, 1f), new Color(1f, 0.82f, 0.2f, 0f), k * k);
            float shake = (1f - k) * 0.13f;
            transform.position = basePos + new Vector3(Mathf.Sin(t * 42f) * shake, 0f, 0f);
            yield return null;
        }
        Destroy(gameObject);
    }

    // ═══════════════════════ 火龙技能 ═══════════════════════
    // A：龙吼→砸地→3s 旋转横扫火焰吐息
    private IEnumerator SkFireSweep()
    {
        _busy = true;
        try
        {
            FaceTarget();
            AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
            yield return new WaitForSeconds(0.4f);
            SpawnRingFx(transform.position, FIRE_COL, 6f);
            DamagePlayersInRadius(transform.position, 6f, Dmg(1.2f), 3f, 0f, true);
            yield return new WaitForSeconds(0.3f);

            Vector3 baseDir = AimDir();
            float baseAng = Mathf.Atan2(baseDir.z, baseDir.x) * Mathf.Rad2Deg;
            float sweep = 3f, t = 0f, tick = 0f;
            while (t < sweep)
            {
                t += Time.fixedDeltaTime; tick -= Time.fixedDeltaTime;
                float ang = (baseAng + Mathf.Lerp(-55f, 55f, t / sweep)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                if (tick <= 0f)
                {
                    tick = 0.22f;
                    SpawnBreathFx(dir, FIRE_COL, 18f, 0.4f);   // 火焰束尺寸翻倍
                    DamagePlayersInCone(transform.position, dir, 14f, 28f, Dmg(0.55f), 0f, true);
                }
                yield return new WaitForFixedUpdate();
            }
        }
        finally { _busy = false; }
    }
    // B：红焰喷飞标（直线瞄准玩家射出，箭头始终朝向飞行方向，命中灼烧 DoT）
    private IEnumerator SkFireDarts()
    {
        _busy = true;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                FaceTarget();                       // 每发都重新瞄准当前玩家位置
                Sprite dartSpr = redDartSprite != null ? redDartSprite : fireballSprite;
                // homing=true → 从 Boss 身体周围射出后短暂俯冲修正（对准玩家身体中心），之后直线飞行，命中灼烧
                SpawnProjectile(dartSpr, new Color(1f, 0.55f, 0.2f), Dmg(0.6f), 12f, 0f, false, true, true, 2.2f);
                yield return new WaitForSeconds(0.2f);
            }
        }
        finally { _busy = false; }
    }

    // ═══════════════════════ 蝙蝠龙技能 ═══════════════════════
    // A：起飞抓取玩家带到半空 + 全屏撕咬动画 + 吸血反噬 debuff
    private IEnumerator SkBatGrab()
    {
        _busy = true;
        // 抓取为短演出：期间 Boss 无敌，避免被击杀导致协程中止、玩家永久悬空/被锁
        _invincible = true; _lockedHealth = health;
        Transform grabbed = null; float restoreY = 0f;
        try
        {
            Player pl; Transform pt;
            if (!GetNearestPlayer(out pl, out pt)) yield break;

            // 距离过远（>10 单位）抓空：只自己起跳落下，不抓玩家、无伤害/回血
            Vector3 flatB = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 flatP = new Vector3(pt.position.x, 0f, pt.position.z);
            if (Vector3.Distance(flatB, flatP) > 10f)
            {
                Vector3 jBase = transform.position; float jt = 0f;
                while (jt < 0.7f)
                {
                    jt += Time.deltaTime; float k = jt / 0.7f;
                    transform.position = jBase + new Vector3(0f, Mathf.Sin(k * Mathf.PI) * 6f, 0f); // 起跳-落下抛物线
                    yield return null;
                }
                transform.position = jBase;
                yield break;
            }

            grabbed = pt; restoreY = pt.position.y;
            pl.movementLocked = true;

            // 贴近并带起玩家到半空
            float t = 0f;
            Vector3 airOff = new Vector3(0.4f * _facing, 6f, 0f);
            Vector3 pStart = pt.position, bStart = transform.position;
            while (t < 0.5f && pl != null && pl.health > 0)
            {
                t += Time.deltaTime; float k = t / 0.5f;
                if (pt != null) pt.position = pStart + airOff * k;
                transform.position = bStart + airOff * k + new Vector3(1.2f * _facing, 0f, 0f);
                yield return null;
            }

            DragonScreenFx.Flash(new Color(0.7f, 0.05f, 0.12f), 1.0f); // 猩红剧院·全屏撕咬
            AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
            yield return new WaitForSeconds(0.5f);

            if (pl != null && pt != null && pl.health > 0)
            {
                int d = Mathf.Max(1, Dmg(1.5f) - (int)pl.def);
                pl.health -= d; ShowDamageNumber(pt.position, d); pl.startturnred();
                // 空中绞杀：额外回复 Boss 5% 最大生命（叠加伤害吸血）
                HealBoss(d + Mathf.Max(1, Mathf.RoundToInt(healthmax * 0.05f)));
                DragonDrainDebuff.Apply(pl, this, Mathf.Max(1, Mathf.RoundToInt(atk * 0.12f)), 5f); // 吸血反噬 5s
                if (pl.health <= 0) pl.death();
            }
            yield return new WaitForSeconds(0.4f);
        }
        finally
        {
            if (grabbed != null)
            {
                var pl = grabbed.GetComponent<Player>();
                if (pl != null) pl.movementLocked = false;
                Vector3 pp = grabbed.position; pp.y = restoreY; grabbed.position = pp; // 放回地面高度
            }
            _invincible = false;
            _busy = false;
        }
    }
    // B：血刃弹幕（血刃「毒镖」，从身体周围射出→俯冲修正→直线，命中吸血）
    private IEnumerator SkBatVolley()
    {
        _busy = true;
        try
        {
            for (int i = 0; i < 5; i++)
            {
                FaceTarget();
                Sprite bladeSpr = bloodBladeSprite != null ? bloodBladeSprite : fireballSprite;
                SpawnProjectile(bladeSpr, new Color(1f, 0.85f, 0.85f), Dmg(0.7f), 11f, 0f, true, true, false, 2.2f);
                yield return new WaitForSeconds(0.16f);
            }
        }
        finally { _busy = false; }
    }

    // ═══════════════════════ 钢化利爪龙技能 ═══════════════════════
    // A：连续释放 5 个龙卷风，一个比一个大（末个最大），路径带 3 次追踪修正
    private IEnumerator SkTornado()
    {
        _busy = true;
        try
        {
            FaceTarget();
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 5; i++)
            {
                float size = Mathf.Lerp(6f, 13f, i / 4f);  // 6 → 13，逐个增大，末个最大
                SpawnTornado(size);
                yield return new WaitForSeconds(0.4f);
            }
        }
        finally { _busy = false; }
    }
    // B：一小段冲刺 → 把玩家按在地上（玩家精灵按冲刺方向旋转90°，事后恢复；不位移玩家避免卡出地形）
    private Transform _pinnedPlayerSprite; private Quaternion _pinnedSpriteOrigRot; private bool _pinnedSpriteSaved;
    private IEnumerator SkClawPin()
    {
        _busy = true;
        Transform pinnedTf = null, pinnedSprite = null;
        try
        {
            FaceTarget();
            Vector3 dir = AimDir();

            // 后撤蓄力（一小段）
            float t = 0f;
            while (t < 0.28f) { t += Time.fixedDeltaTime; transform.position -= dir * 3.5f * Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }
            yield return new WaitForSeconds(0.1f);

            // 一小段冲刺：更大冲刺距离与抓取触发范围，仍限制总位移避免卡出地形
            t = 0f; bool pinned = false;
            float dashDist = 0f;
            const float DASH_SPEED = 26f, DASH_MAX_DIST = 12f, GRAB_RANGE = 5.5f;
            while (t < 0.46f && dashDist < DASH_MAX_DIST)
            {
                float dd = DASH_SPEED * Time.fixedDeltaTime;
                transform.position += dir * dd; dashDist += dd; t += Time.fixedDeltaTime;
                if (playerlayer != null)
                    foreach (Transform p in playerlayer)
                    {
                        var pl = p.GetComponent<Player>();
                        if (pl == null || pl.health <= 0) continue;
                        if (Vector3.Distance(p.position, transform.position) > GRAB_RANGE) continue;
                        if (pl.IsDashInvincibleActive) continue;
                        // 按倒：锁定移动 + 玩家精灵旋转90°（视觉躺倒，不动物理位置→不会卡出地形）
                        pl.movementLocked = true; pinned = true; pinnedTf = p;
                        if (p.childCount > 0)
                        {
                            pinnedSprite = p.GetChild(0);
                            _pinnedPlayerSprite = pinnedSprite;
                            _pinnedSpriteOrigRot = pinnedSprite.localRotation;
                            _pinnedSpriteSaved = true;
                            // 倒下方向由 Boss 与玩家的 X 轴左右关系决定（玩家在 Boss 右侧→向右倒）
                            float zRot = p.position.x >= transform.position.x ? -90f : 90f;
                            pinnedSprite.localRotation = _pinnedSpriteOrigRot * Quaternion.Euler(0f, 0f, zRot);
                        }
                        // 抓取处决：固定造成玩家「当前生命值」5% 的伤害（无视防御——已被按住）
                        int d = Mathf.Max(1, Mathf.RoundToInt(pl.health * 0.05f));
                        pl.health -= d; ShowDamageNumber(p.position, d); pl.startturnred();
                        if (pl.health <= 0) pl.death();
                        break;
                    }
                if (pinned) break;
                yield return new WaitForFixedUpdate();
            }

            SpawnRingFx(transform.position, STEEL_COL, 5f);
            if (pinned)
            {
                SpawnTornado(); // 固定成功立即追加龙卷风
                yield return new WaitForSeconds(1.2f);
            }
            else yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            // 恢复玩家精灵角度 + 解除固定
            RestorePinnedSprite();
            if (pinnedTf != null) { var pl = pinnedTf.GetComponent<Player>(); if (pl != null) pl.movementLocked = false; }
            SetPlayersMovementLocked(false);
            _busy = false;
        }
    }

    private void RestorePinnedSprite()
    {
        if (_pinnedSpriteSaved && _pinnedPlayerSprite != null)
            _pinnedPlayerSprite.localRotation = _pinnedSpriteOrigRot;
        _pinnedSpriteSaved = false; _pinnedPlayerSprite = null;
    }

    // 终极兜底：Boss 被 Destroy 时必然触发恢复，
    // 修复"Boss 死亡/形态切换后玩家永久倒地不起"的 bug。
    private void OnDestroy()
    {
        RestorePinnedSprite();
        SetPlayersMovementLocked(false);
    }

    // ═══════════════════════ 史莱姆龙技能 ═══════════════════════
    // A：召唤史莱姆分裂弹群（近似"召唤小怪"，追踪玩家）
    private IEnumerator SkSlimeSummon()
    {
        _busy = true;
        try
        {
            FaceTarget();
            SpawnRingFx(transform.position, SLIME_COL, 4f);
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 6; i++)
            {
                Vector3 off = Quaternion.Euler(0, i * 60f, 0) * new Vector3(2.2f, 0, 0);
                SpawnProjectileAt(transform.position + off, slimeBlobSprite, Color.white, Dmg(0.6f), 6.5f, 1.5f, false, true, false, 2.6f);
                yield return new WaitForSeconds(0.08f);
            }
        }
        finally { _busy = false; }
    }
    // B：史莱姆凝弹（命中减速）
    private IEnumerator SkSlimeSpit()
    {
        _busy = true;
        try
        {
            FaceTarget();
            yield return new WaitForSeconds(0.2f);
            for (int i = 0; i < 5; i++)
            {
                FaceTarget();
                SpawnProjectile(slimeBlobSprite, Color.white, Dmg(0.8f), 8f, 2.5f, false, true, false, 2.4f);
                yield return new WaitForSeconds(0.16f);
            }
        }
        finally { _busy = false; }
    }

    // ═══════════════════════ 黄金龙技能（狂暴）═══════════════════════
    // A：黄金瞳全屏强控 1s
    private IEnumerator SkGoldControl()
    {
        _busy = true;
        try
        {
            FaceTarget();
            if (IsInnocencePlayer())
            {
                // 无罪反噬：不播金边控制动画、无罪不被控制；改播无罪复活瞪眼全屏动画，龙反被控制、限制移动3s
                DragonScreenFx.Flash(GOLD_COL, 1.0f); // 纯装饰：金黄闪光，不影响控制逻辑
                ReviveBossEffect.Spawn(transform, false);
                AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
                StunEffect.Attach(transform, 3f);   // 龙自己被控制：头顶眩晕转圈 3s
                if (!_innocenceControlLineShown)
                {
                    _innocenceControlLineShown = true;
                    GameConsole.ShowOnScreen("我不会再被这肮脏的囚笼控制哪怕一秒", 0.8f);
                    yield return new WaitForSeconds(3.0f);   // 第一句 1.6s 消失 → 1.4s 空档 → 第二句出现
                    GameConsole.ShowOnScreen("从今往后，只有我控制别人！", 2.0f);
                    // 无需再等，合计 3s 龙被控制结束
                }
                else
                {
                    yield return new WaitForSeconds(3f);   // 龙被控制、限制移动3s
                }
            }
            else
            {
                DragonScreenFx.Flash(GOLD_COL, 1.0f);
                AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
                SetPlayersMovementLocked(true);
                // 伤害 + 强控 1s；被控制的玩家头顶出现眩晕转圈
                DamagePlayersInRadius(transform.position, 20f, Dmg(0.8f), 0f, 0f, false);
                if (playerlayer != null)
                    foreach (Transform p in playerlayer)
                    {
                        var pl = p.GetComponent<Player>();
                        if (pl != null && pl.health > 0) StunEffect.Attach(p, 1f);
                    }
                yield return new WaitForSeconds(1.0f);
                SetPlayersMovementLocked(false);
            }
        }
        finally { SetPlayersMovementLocked(false); _busy = false; }
    }

    // 当前玩家是否为「无罪」(SKIN_TOMB==3)
    private bool _innocenceControlLineShown = false;
    private bool IsInnocencePlayer() => PlayerPrefs.GetInt("SelectedSkin", 0) == 3;
    // B：金龙鳞环射（成圈后追踪玩家）
    private IEnumerator SkGoldScaleRing()
    {
        _busy = true;
        try
        {
            FaceTarget();
            yield return new WaitForSeconds(0.15f);
            int n = 10;
            for (int i = 0; i < n; i++)
            {
                Vector3 off = Quaternion.Euler(0, i * (360f / n), 0) * new Vector3(2f, 0, 0);
                SpawnProjectileAt(transform.position + off, goldScaleSprite, GOLD_COL, Dmg(0.9f), 11f, 0f, false, true, false, 2.2f);
            }
            yield return new WaitForSeconds(0.25f);
        }
        finally { _busy = false; }
    }

    // ───────────────────────────── 伤害 / 接触 ─────────────────────────────
    private int Dmg(float mul) => Mathf.RoundToInt(atk * mul * _dmgMul);

    private void TickContactDamage(float dt)
    {
        if (_invincible) return;
        _contactTimer -= dt;
        if (_contactTimer > 0f) return;
        if (playerlayer == null) return;
        bool hit = false;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            if (Vector3.Distance(p.position, transform.position) > 3.2f * Mathf.Max(0.5f, Sca)) continue;
            ApplyDamageToPlayer(pl, p, Dmg(1f), 0f, false); hit = true;
        }
        if (hit) _contactTimer = contactDamageCd;
    }

    private void DamagePlayersInRadius(Vector3 center, float radius, int dmg, float knockback, float slowSec, bool burn)
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            if (Vector3.Distance(p.position, center) > radius) continue;
            ApplyDamageToPlayer(pl, p, dmg, slowSec, burn);
            if (knockback > 0f) { Vector3 kb = p.position - center; kb.y = 0; if (kb.sqrMagnitude > 0.01f) p.position += kb.normalized * knockback; }
        }
    }

    private void DamagePlayersInCone(Vector3 origin, Vector3 dir, float range, float halfAngleDeg, int dmg, float slowSec, bool burn)
    {
        if (playerlayer == null) return;
        dir.y = 0; if (dir.sqrMagnitude < 0.001f) dir = Vector3.right; dir.Normalize();
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            Vector3 to = p.position - origin; to.y = 0;
            if (to.magnitude > range) continue;
            if (Vector3.Angle(dir, to) > halfAngleDeg) continue;
            ApplyDamageToPlayer(pl, p, dmg, slowSec, burn);
        }
    }

    public void ApplyDamageToPlayer(Player pl, Transform pt, int rawDmg, float slowSec, bool burn)
    {
        if (pl == null || pl.health <= 0) return;
        if (pl.IsDashInvincibleActive) return;
        if (pl.EVA > Random.value * 100f) { MissNumber.Show(atknumber, pt.position); return; }
        int d = Mathf.Max(1, rawDmg - (int)pl.def);
        pl.health -= d;
        ShowDamageNumber(pt.position, d);
        AudioManager.PlaySfx(AudioManager.SfxKey.Hit);
        pl.startturnred();
        if (slowSec > 0f) DragonSlowDebuff.Apply(pl, 0.5f, slowSec);
        if (burn) DragonBurnDebuff.Apply(pl, this, Mathf.Max(1, Mathf.RoundToInt(atk * 0.12f)), 3f);
        if (pl.health <= 0) pl.death();
    }

    public void LifestealHeal(int dmgDealt) => HealBoss(Mathf.Max(1, Mathf.RoundToInt(dmgDealt * 0.5f)));
    public void HealBoss(int amount)
    {
        if (_phase == DragonPhase.Dead || amount <= 0) return;
        health = Mathf.Min(healthmax, health + amount);
        if (_invincible) _lockedHealth = health;
    }

    private void ShowDamageNumber(Vector3 pos, int d)
    {
        if (!DamageNumberSettings.Visible || atknumber == null) return;
        GameObject n = Instantiate(atknumber, pos, default);
        n.transform.localScale *= DamageNumberSettings.SizeScale;
        var tmp = n.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.text = d.ToString();
    }

    private Vector3 AimDir()
    {
        if (role == null) return new Vector3(_facing, 0, 0);
        Vector3 d = role.transform.position - transform.position; d.y = 0;
        return d.sqrMagnitude > 0.01f ? d.normalized : new Vector3(_facing, 0, 0);
    }

    private bool GetNearestPlayer(out Player pl, out Transform pt)
    {
        pl = null; pt = null; float best = float.MaxValue;
        if (playerlayer == null) return false;
        foreach (Transform p in playerlayer)
        {
            var c = p.GetComponent<Player>();
            if (c == null || c.health <= 0) continue;
            float dd = Vector3.Distance(p.position, transform.position);
            if (dd < best) { best = dd; pl = c; pt = p; }
        }
        return pl != null;
    }

    private Color PhaseColorByIndex(int idx)
    {
        switch (idx) { case 0: return FIRE_COL; case 1: return BAT_COL; case 2: return STEEL_COL; case 3: return SLIME_COL; default: return GOLD_COL; }
    }

    private void SetPlayersMovementLocked(bool locked)
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl != null) pl.movementLocked = locked;
        }
    }

    // ───────────────────────────── 投射物 / 特效 ─────────────────────────────
    private void SpawnProjectile(Sprite spr, Color tint, int dmg, float speed, float slowSec, bool lifesteal, bool homing, bool burn, float size)
    {
        Vector3 spawn = transform.position + new Vector3(_facing * 1.5f, 0.5f, 0f);
        SpawnProjectileAt(spawn, spr, tint, dmg, speed, slowSec, lifesteal, homing, burn, size);
    }

    private void SpawnProjectileAt(Vector3 spawn, Sprite spr, Color tint, int dmg, float speed, float slowSec, bool lifesteal, bool homing, bool burn, float size)
    {
        if (role == null) getrole();
        var go = new GameObject("DragonProjectile");
        go.transform.position = spawn;
        var proj = go.AddComponent<DragonProjectile>();
        proj.Init(this, role != null ? role.transform : null, spr, tint, dmg, speed, slowSec, lifesteal, homing, burn, size);
        proj.SetOrientToVelocity(true);   // 弹体箭头/形状始终朝向飞行方向
    }

    private void SpawnTornado(float size = 9f)
    {
        Vector3 spawn = transform.position + new Vector3(_facing * 1.5f, 0f, 0f);
        var go = new GameObject("DragonTornado");
        go.transform.position = spawn;
        var proj = go.AddComponent<DragonProjectile>();
        // 龙卷风：钢蓝色笼罩、可变大小、较慢、命中减速；路径带 3 次追踪修正后直线
        proj.Init(this, role != null ? role.transform : null, tornadoSprite, new Color(0.55f, 0.75f, 1f, 0.92f),
                  Dmg(0.7f), 5.5f, 2.5f, false, true, false, size);
        proj.SetOrientToVelocity(false);  // 龙卷风保持竖直漏斗，不随方向翻转
        proj.SetSpin(220f);               // 左右 churn 摆动营造旋卷感
        proj.SetCorrections(3);           // 飞行中修正 3 次追踪玩家，之后直线
    }

    private void SpawnBreathFx(Vector3 dir, Color col, float length, float life)
    {
        dir.y = 0; if (dir.sqrMagnitude < 0.001f) dir = new Vector3(_facing, 0, 0); dir.Normalize();
        var go = new GameObject("DragonBreathFx");
        // 素材：火焰束尖端在左（龙嘴）、向右扩张；pivot 居中，故中心置于龙嘴前方 length/2 处
        go.transform.position = transform.position + dir * (length * 0.5f) + Vector3.up * 0.6f;
        var sr = go.AddComponent<SpriteRenderer>();
        bool hasArt = fireBreathSprite != null;
        sr.sprite = hasArt ? fireBreathSprite : DragonFx.WhiteSprite();
        sr.color = hasArt ? new Color(1f, 1f, 1f, 0.95f) : col;
        sr.sortingOrder = 20;
        float ang = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(45f, 0f, ang);
        if (hasArt)
        {
            float baseLen = Mathf.Max(0.01f, fireBreathSprite.bounds.size.x);
            float s = length / baseLen;                 // 等比缩放使火焰束长度 ≈ length
            go.transform.localScale = new Vector3(s, s, 1f);
        }
        else go.transform.localScale = new Vector3(length, 3.2f, 1f);
        go.AddComponent<DragonFadeSprite>().Init(life);
    }

    private void SpawnRingFx(Vector3 center, Color col, float radius)
    {
        var go = new GameObject("DragonRingFx");
        go.transform.position = center + Vector3.up * 0.2f;
        go.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = DragonFx.WhiteSprite();
        sr.color = new Color(col.r, col.g, col.b, 0.5f);
        sr.sortingOrder = 19;
        go.transform.localScale = Vector3.one * 0.5f;
        go.AddComponent<DragonRingFx>().Init(radius * 2f, 0.4f);
    }

    protected override void OnCollisionEnter(Collision collision) { /* 手动 proximity 处理 */ }
}
