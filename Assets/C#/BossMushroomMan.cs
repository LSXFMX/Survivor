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
        var playerlayer = GameObject.Find("playerlayer")?.transform;
        typeof(enemy).GetField("playerlayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(this, playerlayer);

        cachedAni = GetComponent<Animator>();

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

    // 覆盖死亡，隐藏预警线
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
        HideWarning();
        bossState = BossState.dead;
        rolestate = state.dead;
        StopAllCoroutines();
        GetAnimator()?.SetBool("ismove", false);
        GetAnimator()?.SetTrigger("dead");
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
