using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 狼人社群 Boss（N10）。继承 enemy，复用受伤/掉落框架，自带完整状态机。
///
/// 人形态（戴红色满月面具）：
///   · 普通追击玩家（类小怪）
///   · 能量狼爪震地范围攻击（5s CD）
///   · 半血自动触发「抓取处决变身」
///
/// 半血抓取处决变身：
///   瞬移到玩家身旁 → 定格第一帧（单手掐脖举起）+ 限制玩家移动
///   → 撕咬一爪：全屏利爪特效 + 造成玩家「现有生命值 10%」的处决伤害
///   → 继续播放变身动画 → 变身为狼（缩放×2、回血 10%、变身期间无敌）
///
/// 狼形态（黑毛红光）—— 技能状态机「CD 就绪则随机释放」，busy 保证互斥不冲突：
///   · 能量狼爪震地攻击
///   · 四脚扑击处决——后退蓄力后猛扑；扑空无事，扑中则抓取处决（撕咬一爪 + 全屏利爪）
///   · 四脚冲刺——锁定方向直线冲锋（可被玩家躲开、不顶推玩家），固定 +30% 移速、冲刺全程高闪避
///   · 常驻 1% 全能吸血
///
/// 死亡：播放死亡动画后销毁。
/// 动画通过 Animator.Play(stateName) 切换：HumanWalk/Transform/WolfWalk/WolfRun/Pounce/Death。
/// </summary>
public class WolfBoss : enemy
{
    [Header("体型")]
    public float humanScale = 4f;
    public float wolfScale  = 6f;   // 狼形态缩放

    [Header("速度")]
    public float humanSpeed    = 4f;
    public float wolfWalkSpeed  = 5f;
    public float wolfRunSpeed   = 22f;

    [Header("技能 CD / 参数")]
    public float quakeInterval  = 5f;   // 人形震地 CD（也用于狼形震地）
    public float quakeRadius    = 6f;   // 震地范围
    public float pounceCd       = 10f;  // 狼形扑击处决 CD
    public float dashCd         = 10f;  // 狼形冲刺漂移 CD
    public float dashDuration   = 4f;   // 冲刺漂移持续时长
    public float pounceRange    = 5f;   // 扑击命中判定半径
    public float pounceTriggerRange = 7f;   // 扑击触发距离（仅对近距离玩家释放）
    public float transformInvincibleTime = 3f; // 变身无敌时长
    [Range(0f, 1f)] public float wolfLifestealPct = 0.01f; // 狼形 1% 全能吸血

    [Header("特效")]
    public GameObject clawFxPrefab;      // 世界空间能量狼爪特效
    public Sprite[]   clawScreenFrames;  // 全屏利爪特效帧（ClawFx1~4）

    [Header("Boss UI")]
    [HideInInspector] public battleUI battleUI; // 由 battleUI.SpawnBoss() 赋值；死亡时调用 OnBossDefeated 触发胜利

    private enum Phase { Human, Transforming, Wolf, Dead }
    private Phase phase = Phase.Human;

    private Animator  anim;
    private SpriteRenderer _sr;
    private Collider  _bodyCol;             // 实体碰撞盒（冲刺时临时切 Trigger，避免顶推玩家）
    private float groundY;          // 地面 Y（从玩家所在平面采样，而非出生点高空）
    private bool  groundYSet = false;
    private float _vy = 0f;                 // 竖直速度（脚本模拟重力用）
    private const float GRAVITY = 45f;      // 模拟重力加速度
    private const float DASH_LIFT = 2.2f;   // 冲刺起跳抬高的高度（基于地面向上）
    private bool  busy       = false; // 技能演出中，暂停常规移动/选技能
    private bool  invincible = false;
    private int   lockedHealth;
    private string curAnim = "";

    // 技能 CD 计时（各自独立倒计时）
    private float cdQuake  = 0f;
    private float cdPounce = 0f;
    private float cdDash   = 0f;
    private float skillGap = 0f;       // 技能间歇，释放后正常追击一会再选下一个
    private readonly List<int> _ready = new List<int>();

    private float dmgCooldown  = 0f;
    private float dashHitCd    = 0f;
    private float busyWatchdog = 0f; // busy 卡死看门狗（协程异常/timeScale=0复活弹窗等极端情况兜底）

    // ── 生命周期 ──
    protected new void OnEnable()
    {
        playerlayer = GameObject.Find("playerlayer")?.transform;
        anim = GetComponent<Animator>();
        _sr  = GetComponent<SpriteRenderer>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 501f;
        // 缓存"实体（非 Trigger 且启用）"碰撞盒，冲刺时临时切 Trigger 让 Boss 穿过玩家而不顶推
        _bodyCol = null;
        foreach (var c in GetComponents<Collider>())
            if (c.enabled && !c.isTrigger) { _bodyCol = c; break; }

        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier);
            health    = healthmax;
            atk       = Mathf.RoundToInt(atk * cfg.atkMultiplier);
        }

        phase = Phase.Human;
        Sca = humanScale;
        transform.localScale = new Vector3(Sca, Sca, Sca);
        rolestate = state.move;
        curAnim = "";
        cdQuake = quakeInterval;
        if (anim != null) anim.speed = 1f;
        PlayAnim("HumanWalk");
    }

    private void PlayAnim(string n)
    {
        if (curAnim == n) return;
        curAnim = n;

        // 缩放只看人/狼阶段，变身后不再改 Sca
        if (n == "Transform" && phase == Phase.Transforming)
            Sca = humanScale;
        else if (phase == Phase.Human)
            Sca = humanScale;
        else
            Sca = wolfScale;

        if (anim != null) { anim.speed = 1f; anim.Play(n, 0, 0f); }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (invincible) health = lockedHealth;

        // 地面 Y 基准：从玩家所在平面采样（玩家永远站在正确地面上）。
        // 不用出生点的 Y——出生点在高空(y≈2.43)，用它会让 Boss 悬空。
        if (role != null)
        {
            groundY = role.transform.position.y;
            groundYSet = true;
        }

        // 脚本模拟重力：始终把 Boss 竖直方向拉向地面（玩家所在平面），落到地面即停。
        // 好处：① 不依赖场景地板碰撞体；② Kinematic 不会被物理冲量弹飞/隧穿；
        //       ③ 永不悬空、永不遁地；④ 冲刺起跳抬高后能自然落回，绝不返回出生点（只动 Y，不碰 XZ）。
        if (groundYSet)
        {
            Vector3 pp = transform.position;
            _vy -= GRAVITY * Time.deltaTime;
            float ny = pp.y + _vy * Time.deltaTime;
            if (ny <= groundY) { ny = groundY; _vy = 0f; } // 落地
            pp.y = ny;
            transform.position = pp;
        }
    }

    private void FaceTarget()
    {
        if (role == null) return;
        float dx = role.transform.position.x - transform.position.x;
        FaceByDirX(dx);
    }

    private void FaceByDirX(float dx)
    {
        transform.localScale = dx >= 0 ? new Vector3(Sca, Sca, Sca) : new Vector3(-Sca, Sca, Sca);
    }

    // ── 主循环 ──
    protected override void FixedUpdate()
    {
        if (phase == Phase.Dead) return;
        if (GetComponent<MindControlled>() != null) return;

        if (busy)
        {
            // 看门狗：技能协程正常最长耗时 < 7s（TransformRoutine 最长约 6.75s）。
            // 若因玩家死亡弹出复活UI（Time.timeScale=0 冻住 WaitForSeconds）或引用失效异常
            // 导致协程卡死超过阈值，强制复位，避免 Boss 永久卡在原地不动（X/Y/Z 全部冻结）。
            busyWatchdog += Time.fixedDeltaTime;
            if (busyWatchdog > 9f)
            {
                StopAllCoroutines();
                busy = false;
                invincible = false;
                busyWatchdog = 0f;
                if (anim != null) anim.speed = 1f;
                SetPlayersMovementLocked(false);
                curAnim = ""; // 强制 PlayAnim 重新播放，避免卡在冻结帧
                PlayAnim(phase == Phase.Human ? "HumanWalk" : "WolfWalk");
            }
            return;
        }
        busyWatchdog = 0f;

        if (role == null) { getrole(); return; }
        FaceTarget();
        float dt = Time.fixedDeltaTime;

        if (phase == Phase.Human)
        {
            PlayAnim("HumanWalk");
            MoveToward(role.transform.position, humanSpeed, dt);

            // 半血抓取处决变身（最高优先级）
            if (health <= healthmax * 0.5f)
            {
                StartCoroutine(TransformRoutine());
                return;
            }

            // 人形唯一技能：震地（CD 就绪即放）
            cdQuake -= dt;
            if (cdQuake <= 0f)
            {
                cdQuake = quakeInterval;
                StartCoroutine(QuakeRoutine(false));
            }
        }
        else if (phase == Phase.Wolf)
        {
            PlayAnim("WolfWalk");
            MoveToward(role.transform.position, wolfWalkSpeed, dt);

            // 技能状态机：各技能独立 CD，就绪则随机释放一个（busy 保证互斥不冲突）
            cdQuake  -= dt;
            cdPounce -= dt;
            cdDash   -= dt;
            if (skillGap > 0f) { skillGap -= dt; return; }

            // 距离检查：扑击只对近处玩家释放（避免远距离扑空浪费）
            float distToPlayer = Vector3.Distance(transform.position, role.transform.position);

            _ready.Clear();
            if (cdQuake  <= 0f) _ready.Add(0);
            if (cdPounce <= 0f && distToPlayer <= pounceTriggerRange) _ready.Add(1); // 近距离才放扑击
            if (cdDash   <= 0f) _ready.Add(2);
            if (_ready.Count == 0) return;

            int pick = _ready[Random.Range(0, _ready.Count)];
            skillGap = 1.5f; // 技能间隔
            if (pick == 0)      { cdQuake  = quakeInterval; StartCoroutine(QuakeRoutine(true)); }
            else if (pick == 1) { cdPounce = pounceCd;      StartCoroutine(PounceRoutine()); }
            else                { cdDash   = dashCd;        StartCoroutine(DashDriftRoutine()); }
        }
    }

    private void MoveToward(Vector3 target, float spd, float dt)
    {
        Vector3 d = target - transform.position; d.y = 0;
        if (d.sqrMagnitude < 0.01f) return;
        transform.position += d.normalized * spd * dt;
    }

    // ── 能量狼爪震地（人形/狼形通用） ──
    private IEnumerator QuakeRoutine(bool wolf)
    {
        busy = true;
        SpawnClawFx(transform.position);
        yield return new WaitForSeconds(0.3f);
        int dmg = Mathf.RoundToInt(atk * (wolf ? 1.2f : 1f));
        DamagePlayers(transform.position, quakeRadius, dmg, wolf ? 2f : 0f);
        yield return new WaitForSeconds(0.3f);
        busy = false;
    }

    // ── 半血：抓取处决 → 变身 ──
    // 【健壮性】try/finally 保证：即便中途异常/中断，也一定收敛到合法的 Wolf 状态
    //（phase=Wolf、解除无敌、解锁玩家、恢复 anim.speed、busy 复位），
    // 绝不会把 phase 永久卡在 Transforming 导致 Boss 冻结不动。
    private IEnumerator TransformRoutine()
    {
        if (phase != Phase.Human) yield break;
        phase = Phase.Transforming;
        busy = true;
        invincible = true;
        lockedHealth = health;

        try
        {
            // 1) 先锁定玩家移动 → 再瞬移到玩家身旁，杜绝瞬移期间玩家走位造成误差
            SetPlayersMovementLocked(true);
            getrole();
            if (role != null)
            {
                Vector3 pp = role.transform.position;
                float side = (transform.position.x <= pp.x) ? -0.5f : 0.5f;
                transform.position = new Vector3(pp.x + side, pp.y, pp.z);
                FaceTarget();
            }

            // 2) 播放抓取第一帧 → 短暂可见后定格
            Sca = humanScale;
            PlayAnim("Transform");
            yield return new WaitForSeconds(0.25f);
            if (anim != null) anim.speed = 0f;      // 冻结在抓取帧
            yield return new WaitForSeconds(2f);    // 停顿 2 秒让玩家充分意识到被抓取

            // 3) 撕咬一爪：全屏利爪特效 + 造成玩家最大生命值 10% 的处决伤害
            WolfClawScreenFx.Show(clawScreenFrames);
            SpawnClawFx(role != null ? role.transform.position : transform.position);
            ExecutionBiteByMaxHp(0.10f);
            yield return new WaitForSeconds(0.6f);

            // 4) 解除定身，继续播放变身动画
            SetPlayersMovementLocked(false);
            if (anim != null) anim.speed = 1f; // 从抓取首帧继续播完变身
            yield return new WaitForSeconds(0.9f);

            float remain = Mathf.Max(0f, transformInvincibleTime - 0.9f);
            yield return new WaitForSeconds(remain);
        }
        finally
        {
            // 完成变身并复位所有状态（正常/异常都执行，保证收敛到 Wolf）
            Sca = wolfScale;
            transform.localScale = new Vector3(Sca, Sca, Sca);
            health = Mathf.Min(healthmax, health + Mathf.RoundToInt(healthmax * 0.1f));
            lockedHealth = health;
            phase = Phase.Wolf;
            if (anim != null) anim.speed = 1f;
            SetPlayersMovementLocked(false);
            PlayAnim("WolfWalk");
            invincible = false;
            busy = false;
            // 变身后错开各技能初始 CD，避免同时触发
            cdQuake = 1.5f; cdPounce = 3f; cdDash = 4.5f; skillGap = 1f;
        }
    }

    // ── 狼形扑击处决（两段式）──
    //   第一段：冲刺撞击（快速冲向玩家，途中实时判定命中）
    //   第二段：命中则 Boss 与玩家同时定身停顿 → 撕咬（全屏利爪 + 伤害）→ 同时解除束缚
    //           未命中则直接结束
    //
    // 【健壮性】整个协程包在 try/finally，无论中途任何一步抛异常（如玩家在等待期间被销毁、
    // 复活弹窗把 timeScale 归零等），finally 都保证 busy 复位、玩家解锁、回到 WolfWalk，
    // 绝不会把 Boss 永久卡死在原地（此前"Z 固定不动/浮空"的真正根因就是这里卡死后 FixedUpdate
    // 里的 MoveToward 再也执行不到，导致 X/Z 全部冻结）。
    private IEnumerator PounceRoutine()
    {
        busy = true;
        Transform lockedPlayer = null; // 记录被定身的玩家，finally 里保底解锁
        try
        {
            Vector3 toT = ((role != null ? role.transform.position : transform.position) - transform.position);
            toT.y = 0; toT = toT.sqrMagnitude > 0.01f ? toT.normalized : Vector3.right;
            FaceTarget();

            // 前摇：后退蓄力
            PlayAnim("WolfRun");
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.fixedDeltaTime;
                Vector3 bp = transform.position - toT * wolfRunSpeed * 0.3f * Time.fixedDeltaTime;
                transform.position = bp; // 只改 XZ（toT.y=0），Y 交给重力
                yield return new WaitForFixedUpdate();
            }

            // 悬停紧张
            yield return new WaitForSeconds(0.18f);

            // ═══ 第一段：冲刺撞击 ═══
            Vector3 pounceTarget = role != null ? role.transform.position : transform.position + toT * 8f;
            Vector3 dir = (pounceTarget - transform.position); dir.y = 0;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : toT;
            PlayAnim("Pounce");
            t = 0f;
            Player hitPlayer = null;
            Transform hitTf = null;
            while (t < 0.45f && hitPlayer == null)
            {
                t += Time.fixedDeltaTime;
                Vector3 fp = transform.position + dir * wolfRunSpeed * 1.15f * Time.fixedDeltaTime;
                transform.position = fp; // 只改 XZ（dir.y=0），Y 交给重力
                // 冲刺途中实时检查是否撞到玩家
                if (playerlayer != null)
                {
                    foreach (Transform p in playerlayer)
                    {
                        var pl = p.GetComponent<Player>();
                        if (pl == null || pl.health <= 0) continue;
                        if (Vector3.Distance(p.position, transform.position) <= pounceRange)
                        { hitPlayer = pl; hitTf = p; break; }
                    }
                }
                yield return new WaitForFixedUpdate();
            }

            // 扑空：直接结束（finally 收尾）
            if (hitPlayer == null)
            {
                yield return new WaitForSeconds(0.2f);
                yield break;
            }

            // ═══ 第二段：抓取处决（Boss 与玩家同时定身）═══
            hitPlayer.movementLocked = true;
            lockedPlayer = hitTf;
            // Boss 贴身零距离"咬住"视觉
            Vector3 pp = hitTf.position;
            float side = (transform.position.x <= pp.x) ? -0.3f : 0.3f;
            transform.position = new Vector3(pp.x + side, pp.y, pp.z);
            FaceTarget();

            // 定身停顿：让玩家意识到被抓住
            yield return new WaitForSeconds(0.8f);

            // 撕咬一爪：全屏利爪 + 结算伤害（全程判空，玩家可能已在等待中被销毁）
            WolfClawScreenFx.Show(clawScreenFrames);
            if (hitTf != null) SpawnClawFx(hitTf.position);
            if (hitPlayer != null && hitTf != null && hitPlayer.health > 0)
            {
                // 处决伤害：玩家「最大生命值」10% 的固定伤害（与半血变身处决一致，已被抓住无视防御）
                int d = Mathf.Max(1, Mathf.RoundToInt(hitPlayer.healthmax * 0.10f));
                hitPlayer.health -= d;
                ShowDamageNumber(hitTf.position, d);
                WolfLifesteal(d);
                hitPlayer.startturnred();
                Vector3 kb = (hitTf.position - transform.position); kb.y = 0;
                if (kb.sqrMagnitude > 0.01f) hitTf.position += kb.normalized * 4f;
                if (hitPlayer.health <= 0) hitPlayer.death();
            }

            // 处决后继续定身一小段（让玩家看到撕咬后果）
            yield return new WaitForSeconds(0.6f);
        }
        finally
        {
            // 无论正常结束还是异常/中断：解锁玩家、复位状态、回到行走
            if (lockedPlayer != null)
            {
                var pl = lockedPlayer.GetComponent<Player>();
                if (pl != null) pl.movementLocked = false;
            }
            if (anim != null) anim.speed = 1f;
            PlayAnim("WolfWalk");
            busy = false;
        }
    }

    // ── 狼形冲刺（3 段式：每段重新锁定玩家方向后直线冲，段内不追踪）──
    // 【健壮性】try/finally 保证无论如何都恢复 EVA、碰撞盒、复位 busy、回到 WolfWalk，绝不卡死。
    private IEnumerator DashDriftRoutine()
    {
        const int   SEGMENTS     = 3;
        const float SEG_DURATION = 1.2f;   // 每段时长
        const float SEG_DIST_MAX = 14f;    // 每段最大冲刺距离

        busy = true;
        float baseEVA = EVA;
        try
        {
            PlayAnim("WolfRun");

            // 起跳：把 Y 抬高（基于玩家地面向上），随后由 LateUpdate 的模拟重力自然落回地面。
            if (groundYSet)
            {
                Vector3 up = transform.position;
                up.y = groundY + DASH_LIFT;
                transform.position = up;
                _vy = 0f;
            }

            // 冲刺期间实体碰撞盒切 Trigger：Boss 从玩家身上穿过而不物理顶推玩家。
            if (_bodyCol != null) _bodyCol.isTrigger = true;

            float spd = wolfWalkSpeed * 1.3f;  // 固定 +30% 移速
            dashHitCd = 0f;
            EVA = Mathf.RoundToInt(baseEVA + 40f); // 冲刺全程高闪避

            for (int seg = 0; seg < SEGMENTS && phase == Phase.Wolf; seg++)
            {
                // 每段开始时，朝玩家当前最新位置重新锁定方向（段内直线冲、不追踪）
                Vector3 dir = Vector3.right;
                if (role != null)
                {
                    Vector3 dd = role.transform.position - transform.position; dd.y = 0;
                    if (dd.sqrMagnitude > 0.01f) dir = dd.normalized;
                }

                float traveled = 0f;
                float elapsed  = 0f;
                while (elapsed < SEG_DURATION && traveled < SEG_DIST_MAX && phase == Phase.Wolf)
                {
                    float dt = Time.fixedDeltaTime;
                    elapsed  += dt;

                    float step = spd * dt;
                    transform.position += dir * step;
                    traveled += step;
                    FaceByDirX(dir.x);

                    if (dashHitCd > 0f) dashHitCd -= dt;
                    else DashTouchDamage();

                    yield return new WaitForFixedUpdate();
                }
            }
        }
        finally
        {
            if (_bodyCol != null) _bodyCol.isTrigger = false; // 恢复实体碰撞
            EVA = Mathf.RoundToInt(baseEVA);
            PlayAnim("WolfWalk");
            busy = false;
        }
    }

    private void DashTouchDamage()
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            if (Vector3.Distance(p.position, transform.position) > pounceRange * 0.9f) continue;
            if (pl.EVA > Random.value * 100f) { MissNumber.Show(atknumber, p.position); dashHitCd = 0.4f; continue; }
            int d = Mathf.Max(1, Mathf.RoundToInt(atk * 1.3f) - (int)pl.def);
            pl.health -= d;
            ShowDamageNumber(p.position, d);
            WolfLifesteal(d);
            pl.startturnred();
            Vector3 kb = (p.position - transform.position); kb.y = 0;
            if (kb.sqrMagnitude > 0.01f) p.position += kb.normalized * 3f;
            if (pl.health <= 0) pl.death();
            dashHitCd = 0.5f;
        }
    }

    // 抓取处决（撕咬一爪）：全屏利爪 + 高额伤害 + 短暂定身 + 击退 + 吸血
    private void ExecuteGrab(Player pl, Transform pt)
    {
        if (pl.EVA > Random.value * 100f) { MissNumber.Show(atknumber, pt.position); return; }

        WolfClawScreenFx.Show(clawScreenFrames);        // 全屏利爪特效
        SpawnClawFx(pt.position);
        StartCoroutine(BriefGrabLock(pl));               // 咬住瞬间限制移动

        int d = Mathf.Max(1, Mathf.RoundToInt(atk * 3f) - (int)pl.def);
        pl.health -= d;
        ShowDamageNumber(pt.position, d);
        WolfLifesteal(d);
        pl.startturnred();
        Vector3 kb = (pt.position - transform.position); kb.y = 0;
        if (kb.sqrMagnitude > 0.01f) pt.position += kb.normalized * 4f;
        if (pl.health <= 0) pl.death();
    }

    private IEnumerator BriefGrabLock(Player pl)
    {
        if (pl == null) yield break;
        pl.movementLocked = true;
        // 抓取撕扯：定身 1.2s 让玩家感受到被咬住，再解除
        yield return new WaitForSeconds(1.2f);
        if (pl != null) pl.movementLocked = false;
    }

    // 处决一击：固定造成玩家「最大生命值」的百分比伤害（无视防御与闪避——已被抓住）
    private void ExecutionBiteByMaxHp(float pct)
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            int d = Mathf.Max(1, Mathf.RoundToInt(pl.healthmax * pct));
            pl.health -= d;
            ShowDamageNumber(p.position, d);
            pl.startturnred();
            if (pl.health <= 0) pl.death();
        }
    }

    // 范围伤害玩家（含闪避 Miss + 吸血）
    private void DamagePlayers(Vector3 center, float radius, int dmg, float knockback)
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            if (Vector3.Distance(p.position, center) > radius) continue;
            if (pl.EVA > Random.value * 100f) { MissNumber.Show(atknumber, p.position); continue; }
            int d = Mathf.Max(1, dmg - (int)pl.def);
            pl.health -= d;
            ShowDamageNumber(p.position, d);
            WolfLifesteal(d);
            pl.startturnred();
            if (knockback > 0f)
            {
                Vector3 kb = (p.position - center); kb.y = 0;
                if (kb.sqrMagnitude > 0.01f) p.position += kb.normalized * knockback;
            }
            if (pl.health <= 0) pl.death();
        }
    }

    // 狼形 1% 全能吸血
    private void WolfLifesteal(int dmg)
    {
        if (phase != Phase.Wolf) return;
        int heal = Mathf.Max(1, Mathf.RoundToInt(dmg * wolfLifestealPct));
        health = Mathf.Min(healthmax, health + heal);
        if (invincible) lockedHealth = health;
    }

    private void ShowDamageNumber(Vector3 pos, int d)
    {
        if (DamageNumberSettings.Visible && atknumber != null)
        {
            GameObject n = Instantiate(atknumber, pos, default);
            n.transform.localScale *= DamageNumberSettings.SizeScale;
            n.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = d.ToString();
        }
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

    private void SpawnClawFx(Vector3 pos)
    {
        if (clawFxPrefab == null) return;
        GameObject fx = Instantiate(clawFxPrefab, pos + Vector3.up * 0.5f, Quaternion.Euler(45, 0, 0));
        float s = (phase == Phase.Wolf || phase == Phase.Transforming) ? wolfScale : humanScale;
        fx.transform.localScale = new Vector3(s, s, s) * 0.5f;
        Destroy(fx, 0.5f);
    }

    // 接触伤害：变身/死亡时不造成；与其他敌人互不碰撞（不被挤走）；加冷却防多碰撞体重复
    protected override void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("enemy")) return; // 不与敌人互推
        if (phase == Phase.Transforming || phase == Phase.Dead) return;
        if (Time.time - dmgCooldown < 0.3f) return;
        dmgCooldown = Time.time;
        base.OnCollisionEnter(collision);
    }

    // 死亡
    public override void Destroy1()
    {
        if (invincible) { health = lockedHealth; return; } // 无敌期免死
        if (phase == Phase.Dead) return;

        phase = Phase.Dead;
        rolestate = state.dead;
        busy = true;
        StopAllCoroutines();
        SetPlayersMovementLocked(false); // 防止死亡时玩家仍被定身
        battleUI?.OnBossDefeated();      // 作为关底Boss：死亡即触发胜利结算
        PlayAnim("Death");
        foreach (var col in GetComponents<Collider>()) col.enabled = false;
        if (expstone != null) Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
        StartCoroutine(DieAfter());
    }

    private IEnumerator DieAfter()
    {
        yield return new WaitForSeconds(1.6f);
        Destroy(gameObject);
    }
}
