using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 世界Boss基类：以往关底Boss的克隆体。
/// - 生成在地图固定位置，初始处于待机状态（不追玩家）
/// - 玩家进入激活范围后才开始战斗
/// - 击败后触发社群解锁，提供局内加成
///
/// Inspector 配置：
/// - activateRange  : 玩家靠近多少距离激活，默认 15
/// - faction        : 该Boss对应的社群
/// - battleUI       : 由 WorldBossManager 赋值
/// </summary>
public class WorldBossBase : enemy
{
    [Header("世界Boss设置")]
    public float       activateRange = 15f;
    public FactionType faction       = FactionType.Mushroom;

    [Header("自然回血")]
    [Tooltip("每秒按 healthmax 的百分比自然回血；被亡者领域操控（MindControlled 挂载）后短路失效。仅激活后生效。")]
    public float naturalHealPctPerSecond = 0.02f;
    private float _healAccum;

    [HideInInspector] public battleUI battleUI;
    [HideInInspector] public WorldBossManager worldBossManager;

    protected bool _activated = false;

    private Animator _ani;

    protected new void OnEnable()
    {
        // 父类已将 playerlayer 改为 protected，直接赋值
        playerlayer = GameObject.Find("playerlayer")?.transform;

        _ani = GetComponent<Animator>();

        // 世界Boss不受难度倍率影响，保持 prefab 原始数值
        // （如需倍率可在子类 override）

        // 头顶血条：无论是否被亡者领域控制都要显示。
        //   组件 LateUpdate 自驱动，只读 health/healthmax，不依赖任何"是否被控制"状态——
        //   被 MindControlled 接管后 GameObject 不变、字段仍在，血条照常更新。
        //   用 GetComponent + AddComponent 兜底：避免重复挂（重启场景或子类 OnEnable 二次触发）。
        if (GetComponent<WorldBossHealthBar>() == null)
            gameObject.AddComponent<WorldBossHealthBar>();
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后，行为完全交给 MindControlled（不再追玩家、不再走激活逻辑）
        if (GetComponent<MindControlled>() != null) return;

        // 未激活：检测玩家距离
        if (!_activated)
        {
            if (role == null) getrole();
            if (role != null)
            {
                float dist = Vector3.Distance(transform.position, role.transform.position);
                if (dist <= activateRange)
                    Activate();
            }
            return;
        }

        // 激活后：执行正常 enemy 逻辑
        // 自然回血放在 MindControlled 短路后、激活之后——只有"激活进入战斗的、未被亡者领域操控"的
        // 世界 Boss 才会回血，符合"被亡者领域操控后失去自然回血词条"的设计。
        TickNaturalHeal();
        base.FixedUpdate();
    }

    /// <summary>每秒按 healthmax × naturalHealPctPerSecond 自然回血（累积法防止小数丢失）。</summary>
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

    protected virtual void Activate()
    {
        _activated = true;
        ToastManager.Show($"世界Boss已激活！");
        Debug.Log($"[WorldBoss] {faction} 世界Boss激活");
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：先在最外层拦截。命中则不通知 worldBossManager（它会以为 Boss 真被击败）。
        // _reviveAttempted 防重入：与 BossBat/BossMushroomMan 系列保持一致。
        if (!_reviveAttempted)
        {
            _reviveAttempted = true;
            if (TombDomainHook.TryReviveAsAlly(this))
            {
                Debug.Log($"[亡者领域] 世界Boss {gameObject.name} 被永久控制为友军");
                return;
            }
        }

        rolestate = state.dead;

        _ani?.SetTrigger("dead");
        // 彩色 overlay 在世界 Boss 上几乎不会启用（Boss 体型大，多数不属于 IsMushroomEnemy 名字判定），
        // 但保险地清一下，避免万一启用导致死亡帧不可见。
        ClearSporeMutationColor();
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        if (expstone != null)
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        // 通知世界Boss管理器
        worldBossManager?.OnWorldBossDefeated(faction);

        StartCoroutine(Destroy2());
    }
}
