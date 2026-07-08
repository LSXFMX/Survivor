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
        ToastManager.Show("<color=#FF2020>时间已到……最终龙王正在逼近！</color>");

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
        ToastManager.Show("<color=#FF3010>—— 龙 吼 ——</color>");
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

        if (_busy)
        {
            _busyWatchdog += dt;
            if (_busyWatchdog > 10f)
            {
                StopAllCoroutines();
                _busy = false; _transitioning = false; _invincible = false;
                SetPlayersMovementLocked(false);
                _cdA = 1f; _cdB = 1f; _busyWatchdog = 0f;
            }
            TickContactDamage(dt);
            return;
        }
        _busyWatchdog = 0f;

        if (role == null) { getrole(); return; }
        FaceTarget();

        Vector3 to = role.transform.position - transform.position; to.y = 0f;
        if (to.magnitude > 3f)
            transform.position += to.normalized * moveSpeed * dt;

        TickContactDamage(dt);

        _cdA -= dt; _cdB -= dt;
        if (_cdA <= 0f) { TriggerSkillA(); }
        else if (_cdB <= 0f) { TriggerSkillB(); }
    }

    private void TriggerSkillA()
    {
        switch (_phase)
        {
            case DragonPhase.Fire:  _cdA = 10f; StartCoroutine(SkFireSweep());   break;
            case DragonPhase.Bat:   _cdA = 10f; StartCoroutine(SkBatGrab());     break;
            case DragonPhase.Steel: _cdA = 10f; StartCoroutine(SkTornado());     break;
            case DragonPhase.Slime: _cdA = 7f;  StartCoroutine(SkSlimeSummon()); break;
            case DragonPhase.Gold:  _cdA = 6f;  StartCoroutine(SkGoldControl()); break;
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

        battleUI?.SetBossCountdownText(PHASE_TEXT[_phaseIndex], PHASE_TEXT_COL[_phaseIndex]);
        ToastManager.Show($"<color=#FF6060>龙王形态：{PHASE_TEXT[_phaseIndex]}</color>");
    }

    public override void Destroy1()
    {
        if (_phase == DragonPhase.Dead) return;
        if (_invincible || _transitioning) { health = _lockedHealth; return; }

        StopAllCoroutines();
        _busy = false;

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
        if (berserk) { DragonScreenFx.Flash(GOLD_COL, 1.0f); ToastManager.Show("<color=#FFD24A>黄金真龙觉醒——狂暴！</color>"); }

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
        SetPlayersMovementLocked(false);
        battleUI?.OnBossDefeated();
        foreach (var col in GetComponents<Collider>()) col.enabled = false;

        // 停战斗 BGM，让死亡演出更聚焦
        AudioManager.StopBgm();
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
                    SpawnBreathFx(dir, FIRE_COL, 9f, 0.4f);
                    DamagePlayersInCone(transform.position, dir, 10f, 26f, Dmg(0.55f), 0f, true);
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
                pl.health -= d; ShowDamageNumber(pt.position, d); HealBoss(d); pl.startturnred();
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
    // A：龙卷风追击
    private IEnumerator SkTornado()
    {
        _busy = true;
        try
        {
            FaceTarget();
            yield return new WaitForSeconds(0.3f);
            SpawnTornado();
        }
        finally { _busy = false; }
    }
    // B：弹刺利爪震地固定（固定成功立即追加龙卷风）
    private IEnumerator SkClawPin()
    {
        _busy = true;
        try
        {
            FaceTarget();
            Vector3 dir = AimDir();
            float t = 0f; // 后撤蓄力
            while (t < 0.35f) { t += Time.fixedDeltaTime; transform.position -= dir * 4f * Time.fixedDeltaTime; yield return new WaitForFixedUpdate(); }
            yield return new WaitForSeconds(0.12f);
            // 瞬冲
            t = 0f; bool pinned = false; Transform pinnedTf = null;
            while (t < 0.32f)
            {
                t += Time.fixedDeltaTime;
                transform.position += dir * 24f * Time.fixedDeltaTime;
                if (playerlayer != null)
                    foreach (Transform p in playerlayer)
                    {
                        var pl = p.GetComponent<Player>();
                        if (pl == null || pl.health <= 0) continue;
                        if (Vector3.Distance(p.position, transform.position) > 3f) continue;
                        // 震地固定
                        if (!pl.IsDashInvincibleActive)
                        {
                            pl.movementLocked = true; pinned = true; pinnedTf = p;
                            int d = Mathf.Max(1, Dmg(1.2f) - (int)pl.def);
                            pl.health -= d; ShowDamageNumber(p.position, d); pl.startturnred();
                            if (pl.health <= 0) pl.death();
                        }
                    }
                if (pinned) break;
                yield return new WaitForFixedUpdate();
            }
            SpawnRingFx(transform.position, STEEL_COL, 5f);
            if (pinned)
            {
                SpawnTornado(); // 固定成功立即追加龙卷风
                yield return new WaitForSeconds(1.2f);
                if (pinnedTf != null) { var pl = pinnedTf.GetComponent<Player>(); if (pl != null) pl.movementLocked = false; }
            }
            else yield return new WaitForSeconds(0.3f);
        }
        finally
        {
            SetPlayersMovementLocked(false);
            _busy = false;
        }
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
            DragonScreenFx.Flash(GOLD_COL, 1.0f);
            AudioManager.PlaySfx(AudioManager.SfxKey.DragonRoar);
            SetPlayersMovementLocked(true);
            // 伤害 + 强控 1s
            DamagePlayersInRadius(transform.position, 20f, Dmg(0.8f), 0f, 0f, false);
            yield return new WaitForSeconds(1.0f);
            SetPlayersMovementLocked(false);
        }
        finally { SetPlayersMovementLocked(false); _busy = false; }
    }
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

    private void SpawnTornado()
    {
        Vector3 spawn = transform.position + new Vector3(_facing * 1.5f, 0f, 0f);
        var go = new GameObject("DragonTornado");
        go.transform.position = spawn;
        var proj = go.AddComponent<DragonProjectile>();
        // 龙卷风：钢蓝色笼罩、2 倍大小（9 单位）、较慢、命中减速、全程缓慢追踪玩家
        proj.Init(this, role != null ? role.transform : null, tornadoSprite, new Color(0.55f, 0.75f, 1f, 0.92f),
                  Dmg(0.7f), 5.5f, 2.5f, false, true, false, 9f);
        proj.SetOrientToVelocity(false);  // 龙卷风保持竖直漏斗，不随方向翻转
        proj.SetSpin(220f);               // 左右 churn 摆动营造旋卷感
        proj.SetContinuousHoming(true);   // 全程追踪，缓慢逼近玩家
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
