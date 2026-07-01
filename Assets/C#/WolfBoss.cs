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
///   · 四脚高速冲刺漂移——来回加速冲刺、难以转向，按移速动态提升闪避（最高 +50）
///   · 常驻 1% 全能吸血
///
/// 死亡：播放死亡动画后销毁。
/// 动画通过 Animator.Play(stateName) 切换：HumanWalk/Transform/WolfWalk/WolfRun/Pounce/Death。
/// </summary>
public class WolfBoss : enemy
{
    [Header("体型")]
    public float humanScale = 4f;
    public float wolfScale  = 5f;   // 变身后缩放（新素材较大，降为 5）

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

    private enum Phase { Human, Transforming, Wolf, Dead }
    private Phase phase = Phase.Human;

    private Animator  anim;
    private SpriteRenderer _sr;
    private float     groundY;    // 出生时的 y，防止掉地板
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

    // ── 生命周期 ──
    protected new void OnEnable()
    {
        playerlayer = GameObject.Find("playerlayer")?.transform;
        anim = GetComponent<Animator>();
        _sr  = GetComponent<SpriteRenderer>();
        groundY = transform.position.y;

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
        // 强制锁 y——任何地方改动了 transform.position.y 都会被覆盖回地面
        Vector3 pp = transform.position;
        if (Mathf.Abs(pp.y - groundY) > 0.01f) { pp.y = groundY; transform.position = pp; }
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
        if (busy) return;

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
    private IEnumerator TransformRoutine()
    {
        if (phase != Phase.Human) yield break;
        phase = Phase.Transforming;
        busy = true;
        invincible = true;
        lockedHealth = health;

        // 1) 瞬移到玩家身旁（y 锁在地面）
        getrole();
        if (role != null)
        {
            Vector3 pp = role.transform.position;
            float side = (transform.position.x <= pp.x) ? -1.6f : 1.6f;
            transform.position = new Vector3(pp.x + side, groundY, pp.z);
            FaceTarget();
        }

        // 2) 播放抓取第一帧 → 短暂可见后定格 → 限制玩家移动（让玩家意识到被抓住）
        Sca = humanScale;
        PlayAnim("Transform");
        yield return new WaitForSeconds(0.25f); // 先让玩家看到 Boss 瞬移过来 + 抓取姿态
        if (anim != null) anim.speed = 0f;      // 冻结在抓取帧
        SetPlayersMovementLocked(true);
        yield return new WaitForSeconds(2f);    // 停顿 2 秒让玩家充分意识到被抓取

        // 3) 撕咬一爪：全屏利爪特效 + 造成玩家现有生命值 10% 的处决伤害
        WolfClawScreenFx.Show(clawScreenFrames);
        SpawnClawFx(role != null ? role.transform.position : transform.position);
        ExecutionBiteByCurrentHp(0.10f);
        yield return new WaitForSeconds(0.6f);

        // 4) 解除定身，继续播放变身动画
        SetPlayersMovementLocked(false);
        if (anim != null) anim.speed = 1f; // 从抓取首帧继续播完变身
        yield return new WaitForSeconds(0.9f);

        // 5) 完成变身：缩放×2、回血 10%、锁 y 到地面
        Sca = wolfScale;
        Vector3 vp = transform.position; vp.y = groundY;
        transform.position = vp;
        transform.localScale = new Vector3(Sca, Sca, Sca);
        health = Mathf.Min(healthmax, health + Mathf.RoundToInt(healthmax * 0.1f));
        lockedHealth = health;
        phase = Phase.Wolf;
        PlayAnim("WolfWalk");

        float remain = Mathf.Max(0f, transformInvincibleTime - 0.9f);
        yield return new WaitForSeconds(remain);
        invincible = false;
        busy = false;
        // 变身后错开各技能初始 CD，避免同时触发
        cdQuake = 1.5f; cdPounce = 3f; cdDash = 4.5f; skillGap = 1f;
    }

    // ── 狼形扑击处决（两段式）──
    //   第一段：冲刺撞击（快速冲向玩家）
    //   第二段：命中则 Boss 与玩家同时定身停顿 → 播放抓取动画 → 出伤害 → 解除束缚
    //           未命中则直接结束
    private IEnumerator PounceRoutine()
    {
        busy = true;

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
            bp.y = groundY;
            transform.position = bp;
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
            fp.y = groundY;
            transform.position = fp;
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

        if (hitPlayer == null)
        {
            // 扑空：无事发生
            yield return new WaitForSeconds(0.2f);
            PlayAnim("WolfWalk");
            busy = false;
            yield break;
        }

        // ═══ 第二段：抓取处决（Boss 与玩家同时定身，停顿感受被抓住）═══
        hitPlayer.movementLocked = true;
        // 让 Boss 站到玩家旁（稍前一点）以便"咬住"视觉
        Vector3 pp = hitTf.position;
        float side = (transform.position.x <= pp.x) ? -1.2f : 1.2f;
        transform.position = new Vector3(pp.x + side, groundY, pp.z);
        FaceTarget();

        // 定身停顿：让玩家意识到自己被抓住了
        yield return new WaitForSeconds(0.8f);

        // 撕咬一爪：全屏利爪 + 结算伤害
        WolfClawScreenFx.Show(clawScreenFrames);
        SpawnClawFx(hitTf.position);
        if (hitPlayer != null && hitPlayer.health > 0)
        {
            int d = Mathf.Max(1, Mathf.RoundToInt(atk * 3f) - (int)hitPlayer.def);
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

        // ═══ 同时解除双方束缚 ═══
        if (hitPlayer != null) hitPlayer.movementLocked = false;
        PlayAnim("WolfWalk");
        busy = false;
    }

    // ── 狼形四脚冲刺漂移（朝玩家猛冲 + 惯性漂移感、从慢到快、按移速提升闪避、不飞出地图、不闪现） ──
    private IEnumerator DashDriftRoutine()
    {
        busy = true;
        PlayAnim("WolfRun");

        // 初始方向直冲玩家
        Vector3 dir = Vector3.right;
        if (role != null)
        {
            Vector3 dd = role.transform.position - transform.position; dd.y = 0;
            if (dd.sqrMagnitude > 0.01f) dir = dd.normalized;
        }
        float baseEVA = EVA;
        float spd      = wolfRunSpeed * 0.2f;   // 起步更慢
        float maxSpeed = wolfRunSpeed * 0.55f;  // 最高速度减半（避免闪现）
        float accel    = wolfRunSpeed * 0.08f;
        float t        = 0f;
        dashHitCd = 0f;
        float mapX = 28f, mapZ = 28f;

        while (t < dashDuration && phase == Phase.Wolf)
        {
            float dt = Time.fixedDeltaTime;
            t += dt;

            // 逐步加速
            spd = Mathf.Min(maxSpeed, spd + accel * dt);

            // 距离玩家近时自动减速（不会冲过头）
            if (role != null)
            {
                Vector3 toP = (role.transform.position - transform.position); toP.y = 0;
                float d = toP.magnitude;
                if (d < 4f) spd = Mathf.Min(spd, wolfRunSpeed * 0.15f);
            }

            // 方向缓慢追向玩家（避免方向突变导致闪现感）
            if (role != null)
            {
                Vector3 want = (role.transform.position - transform.position); want.y = 0;
                if (want.sqrMagnitude > 0.01f)
                {
                    dir = Vector3.Slerp(dir, want.normalized, 1.0f * dt).normalized;
                }
            }
            Vector3 newPos = transform.position + dir * spd * dt;
            newPos.x = Mathf.Clamp(newPos.x, -mapX, mapX);
            newPos.z = Mathf.Clamp(newPos.z, -mapZ, mapZ);
            newPos.y = groundY;
            transform.position = newPos;
            FaceByDirX(dir.x);

            float ratio = Mathf.InverseLerp(wolfRunSpeed * 0.2f, maxSpeed, spd);
            EVA = Mathf.RoundToInt(baseEVA + 50f * ratio);

            if (dashHitCd > 0f) dashHitCd -= dt;
            else DashTouchDamage();

            yield return new WaitForFixedUpdate();
        }

        EVA = Mathf.RoundToInt(baseEVA);
        PlayAnim("WolfWalk");
        busy = false;
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

    // 处决一击：固定造成玩家「现有生命值」的百分比伤害（无视防御与闪避——已被抓住）
    private void ExecutionBiteByCurrentHp(float pct)
    {
        if (playerlayer == null) return;
        foreach (Transform p in playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            int d = Mathf.Max(1, Mathf.RoundToInt(pl.health * pct));
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
