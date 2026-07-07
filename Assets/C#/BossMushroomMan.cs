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

        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier);
            health    = healthmax;
            atk       = Mathf.RoundToInt(atk * cfg.atkMultiplier);
        }

        Sca = bossScale;
        transform.localScale = new Vector3(Sca, Sca, Sca);

        _rb = GetComponent<Rigidbody>();
        if (_rb != null) _rb.mass = 10f;
    }

    // 覆盖父类 FixedUpdate，加入 Boss 状态机
    protected override void FixedUpdate()
    {
        if (bossState == BossState.dead) return;

        // 亡者领域：被控制为友军后，行为完全交给 MindControlled
        if (GetComponent<MindControlled>() != null) return;

        // 自然回血：放在 MindControlled 短路之后，等价于"被亡者领域操控后失去自然回血词条"。
        TickNaturalHeal();

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
                break;
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
        yield return new WaitForSeconds(warningDuration);
        HideWarning();

        // ── 冲刺阶段 ──
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
