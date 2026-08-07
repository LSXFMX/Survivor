using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 蘑菇人Boss：继承 enemy，增加冲刺技能。
/// 在剩余时间1分钟时由 battleUI 生成。
/// 
/// Inspector 配置：
/// - dashWarningLine：预警线对象（LineRenderer），用于显示冲刺方向
/// - dashInterval：冲刺间隔（秒），默认5秒
/// - dashSpeed：冲刺速度，默认20
/// - dashDistance：冲刺距离，默认10
/// - warningDuration：预警持续时间（秒），默认2
/// </summary>
public class BossMushroomMan : enemy
{
    /// <summary>关底 Boss（世界版 WorldBossMushroomMan 会覆写为 WorldBoss）。</summary>
    public override BossTag bossTag => BossTag.StageBoss;

    [Header("Boss 冲刺设置")]
    public float dashInterval   = 5f;
    public float dashSpeed      = 20f;
    public float dashDistance   = 10f;
    public float warningDuration = 2f;

    [Header("Boss 体型")]
    public float bossScale = 20f; // Boss 固定缩放，覆盖 Sca

    [Header("预警线（LineRenderer）")]
    public LineRenderer dashWarningLine;

    [Header("自然回血")]
    [Tooltip("每秒按 healthmax 的百分比自然回血。被亡者领域操控后失效（MindControlled 一旦挂上，FixedUpdate 短路，回血不再 tick）。")]
    public float naturalHealPctPerSecond = 0.02f; // 默认 2%/s
    private float _healAccum;

    [HideInInspector]
    public battleUI battleUI; // 由 battleUI.SpawnBoss() 赋值

    private enum BossState { idle, move, warning, dash, dead }
    private BossState bossState = BossState.idle;

    private bool isDashing  = false;
    private float dashTimer = 0f;
    private Vector3 dashDir;
    private float damageCooldown = 0f; // 防止多碰撞体重复伤害

    protected override void OnCollisionEnter(Collision collision)
    {
        // 0.1秒内只造成一次伤害，防止多碰撞体重复触发
        if (Time.time - damageCooldown < 0.1f) return;
        damageCooldown = Time.time;
        base.OnCollisionEnter(collision);
    }

    private Rigidbody _rb;

    // 覆盖 OnEnable，强制设置 Boss 体型
    protected new void OnEnable()
    {
        // 父类已将 playerlayer 改为 protected，直接赋值
        playerlayer = GameObject.Find("playerlayer")?.transform;

        cachedAni = GetComponent<Animator>();

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 501f; // Boss质量略高于玩家(500)，仍能推动玩家

        // 难度缩放：统一走基类幂等方法（含难度 / 奇遇 / 无尽三重倍率），避免就地自乘累积。
        ApplyDifficultyScaling();

        Sca = bossScale;
        transform.localScale = new Vector3(Sca, Sca, Sca);

        _rb = GetComponent<Rigidbody>();
        if (_rb != null) _rb.mass = 10f;
    }

    // 覆盖父类 FixedUpdate，加入 Boss 状态机
    protected override void FixedUpdate()
    {
        if (bossState == BossState.dead) return;

        // 亡者领域：被控制为友军后，MindControlled 只负责把 role 喂成"最近的敌人"，
        //   移动/冲刺状态机仍由本脚本驱动（原先在这里直接 return，导致友军蘑菇王
        //   既不追敌也永不冲刺，只能靠 MindControlled 的通用平A，技能完全失效）。
        // 自然回血在友军状态下失效（等价于"被亡者领域操控后失去自然回血词条"）。
        bool mindControlled = GetComponent<MindControlled>() != null;
        if (!mindControlled) TickNaturalHeal();

        if (role != null && bossState != BossState.dash)
        {
            float chazhi = role.transform.position.x - transform.position.x;
            float s = Sca;
            transform.localScale = chazhi > 0
                ? new Vector3(s, s, s)
                : new Vector3(-s, s, s);
        }

        switch (bossState)
        {
            case BossState.idle:
                GetAnimator()?.SetBool("ismove", false);
                if (role == null) getrole();
                else bossState = BossState.move;
                break;

            case BossState.move:
                GetAnimator()?.SetBool("ismove", true);
                if (role == null) { bossState = BossState.idle; break; }

                Vector3 dir = (role.transform.position - transform.position);
                dir = new Vector3(dir.x, 0, dir.z).normalized;
                transform.position += dir * speed * Time.fixedDeltaTime;

                dashTimer += Time.fixedDeltaTime;
                if (dashTimer >= dashInterval && !isDashing)
                {
                    dashTimer = 0f;
                    StartCoroutine(DashRoutine());
                }
                break;

            case BossState.warning:
                GetAnimator()?.SetBool("ismove", false);
                break;

            case BossState.dash:
                transform.position += dashDir * dashSpeed * Time.fixedDeltaTime;
                // 友军状态：碰撞伤害在 enemy 基类被 _mindControlledFlag 拦掉，
                //   这里改用距离判定对敌人造成冲刺撞击伤害，让技能真正生效。
                if (mindControlled) DashDamageEnemies();
                break;
        }
    }

    /// <summary>
    /// 友军版冲刺撞击伤害：对身边敌人造成伤害（带 0.35s 内部 CD 防止一次冲刺反复结算）。
    /// 敌对状态下走 enemy 基类的 OnCollisionEnter，不经过本方法。
    /// </summary>
    private float _allyDashHitCd = 0f;
    private void DashDamageEnemies()
    {
        if (_allyDashHitCd > 0f) { _allyDashHitCd -= Time.fixedDeltaTime; return; }

        Transform host = GameObject.Find("enemylayer")?.transform;
        if (host == null) return;
        const float HIT_R = 2.2f;
        float r2 = HIT_R * HIT_R;
        int n = host.childCount;
        for (int i = 0; i < n; i++)
        {
            Transform t = host.GetChild(i);
            if (t == null) continue;
            enemy e = t.GetComponent<enemy>();
            if (e == null || e == this) continue;
            if (e.health <= 0 || e.rolestate == state.dead) continue;
            if (e._mindControlledFlag) continue;   // 不打友军
            if (e is Camp) continue;               // 不打营地
            if ((t.position - transform.position).sqrMagnitude > r2) continue;

            int d = Mathf.Max(1, (int)atk - (int)e.def);
            e.health -= d;
            MindControlled.SpawnAllyDamageNumber(e, d);
            e.startturnred();
            TombDomainHook.MarkAllyDamage(e);
            if (e.health <= 0) e.Destroy1();
            _allyDashHitCd = 0.35f;
        }
    }

    private IEnumerator DashRoutine()
    {
        if (role == null || isDashing) yield break;
        isDashing = true;
        bossState = BossState.dash;

        // 锁定冲刺方向（朝向当前目标）
        Vector3 toTarget = role.transform.position - transform.position;
        dashDir = new Vector3(toTarget.x, 0, toTarget.z).normalized;

        // ── 预警阶段：站立不动 ──
        bossState = BossState.warning;
        ShowWarning(dashDir);
        AudioManager.PlaySfx(AudioManager.SfxKey.BossCharge);   // 冲刺预警：紧张上扬蓄力音
        yield return new WaitForSeconds(warningDuration);
        HideWarning();

        // ── 冲刺阶段 ──
        AudioManager.PlaySfx(AudioManager.SfxKey.BossDive);     // 冲刺突进：下压 whoosh
        float traveled = 0f;
        while (traveled < dashDistance)
        {
            float step = dashSpeed * Time.fixedDeltaTime;
            transform.position += dashDir * step;
            traveled += step;
            yield return new WaitForFixedUpdate();
        }

        // 冲刺结束，回到追踪
        isDashing = false;
        bossState = BossState.move;
    }

    private void ShowWarning(Vector3 dir)
    {
        if (dashWarningLine == null) return;
        dashWarningLine.gameObject.SetActive(true);
        dashWarningLine.SetPosition(0, transform.position);
        dashWarningLine.SetPosition(1, transform.position + dir * dashDistance);
    }

    private void HideWarning()
    {
        if (dashWarningLine != null)
            dashWarningLine.gameObject.SetActive(false);
    }

    /// <summary>
    /// 关底 Boss 自然回血：每帧按 fixedDeltaTime 累积 `healthmax × naturalHealPctPerSecond × dt`，
    /// 累积 ≥1 时回填整数到 health（不超过 healthmax）。
    ///
    /// 失效条件（在调用方已生效，不需要这里再判）：
    ///   • 已死亡：FixedUpdate 顶部 bossState==dead 已 return；
    ///   • 被亡者领域操控：MindControlled 存在时短路 return（"失去自然回血词条"语义）。
    /// </summary>
    private void TickNaturalHeal()
    {
        if (naturalHealPctPerSecond <= 0f) return;
        if (health <= 0 || health >= healthmax) return;
        _healAccum += healthmax * naturalHealPctPerSecond * Time.fixedDeltaTime;
        if (_healAccum >= 1f)
        {
            int gain = (int)_healAccum;
            _healAccum -= gain;
            health = Mathf.Min(healthmax, health + gain);
        }
    }

    // 覆盖死亡，隐藏预警线
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被孢子领域伤害过，统一复活拦截（BossMushroomMan 不调 base.Destroy1，必须在此自行拦截。
        // WorldBossMushroomMan 调 base.Destroy1() 经此入口；命中时，外层子类会继续执行 OnWorldBossDefeated——
        // 故 WorldBossMushroomMan.Destroy1 自身也已加前置拦截，提前 return。这里只服务普通 BossMushroomMan。）
        // _reviveAttempted 防重入：WorldBossMushroomMan 已在外层投过一次，进入这里就不能再投第二次。
        if (!_reviveAttempted)
        {
            _reviveAttempted = true;
            if (TombDomainHook.TryReviveAsAlly(this))
            {
                Debug.Log($"[亡者领域] 蘑菇王 {gameObject.name} 被复活为友军");
                return;
            }
        }

        HideWarning();
        bossState = BossState.dead;
        rolestate = state.dead;
        StopAllCoroutines();
        // 蘑菇王在 OnEnable 设置 bossScale=20，理论不会启用孢子变异（IsMushroomEnemy 仍可能命中），
        // 这里保险地清掉彩色 overlay，避免万一启用时挡住死亡动画的尸体帧。
        ClearSporeMutationColor();
        var animator = GetAnimator();
        if (animator != null)
        {
            animator.SetBool("ismove", false);
            animator.SetTrigger("dead");
        }
        // 禁用碰撞体
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;
        Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        // 首次击败蘑菇王 → 解锁成就装备2（蘑菇滑板）+ 蘑菇社群好感度 +10
        if (EquipmentSystem.Instance != null)
        {
            bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 2);
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 2);
            if (!alreadyUnlocked)
            {
                ToastManager.Show("成就装备2「蘑菇滑板」已解锁！");
                FavorManager.Instance?.AddFavor(FactionType.Mushroom, 10);
                ToastManager.Show("蘑菇社群好感度 +10");
            }
        }

        // 每次击败蘑菇Boss → 好感度 +1
        if (FavorManager.Instance != null)
        {
            FavorManager.Instance.AddFavor(FactionType.Mushroom, 1);
            int newFavor = FavorManager.Instance.GetFavor(FactionType.Mushroom);
            ToastManager.Show($"蘑菇社群好感度 +1（当前：{newFavor}）");
        }
        else
        {
            // FavorManager 未初始化时直接操作 PlayerPrefs
            string key = "Favor_Mushroom";
            int cur = PlayerPrefs.GetInt(key, 0);
            int next = Mathf.Clamp(cur + 1, 0, 100);
            PlayerPrefs.SetInt(key, next);
            PlayerPrefs.Save();
            ToastManager.Show($"蘑菇社群好感度 +1（当前：{next}）");
        }

        battleUI?.OnBossDefeated();
        StartCoroutine(Destroy2());
    }

    // 反射获取父类私有 Animator（父类 ani 是 private）
    private Animator cachedAni;
    private Animator GetAnimator()
    {
        if (cachedAni != null) return cachedAni;
        cachedAni = GetComponent<Animator>();
        return cachedAni;
    }
}
