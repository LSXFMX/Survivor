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
    public float       activateRange = 25f;
    public FactionType faction       = FactionType.Mushroom;

    [Header("世界Boss属性（v2：原关底Boss属性×2）")]
    [Tooltip("激活时自动应用：原关底Boss血量×2 = 1000，攻击×2 = 100；世界Boss额外：+5%/s自然回血 + 0.1%吸血")]
    public int   doubledHealthMax     = 1000;
    public int   doubledAttack         = 100;
    [Range(0f, 0.2f)] public float worldBossHealPctPerSecond = 0.05f; // +5%/s 回血
    [Range(0f, 0.05f)] public float worldBossLifestealPct = 0.001f;  // 0.1% 吸血

    [Header("自然回血（兼容旧字段）")]
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

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 501f; // 世界Boss质量略高于玩家(500)

        // 世界Boss属性应用：原关底Boss属性×2
        // prefab 里保留原值（用于退回关底使用），世界Boss激活时强制覆盖为 ×2
        healthmax = doubledHealthMax;
        health    = healthmax;
        atk       = doubledAttack;
        // 世界Boss额外能力：+5%/s 自然回血
        naturalHealPctPerSecond = worldBossHealPctPerSecond;

        // UI Boss 血条由 BossHealthBarUI 管理（激活后注册），不再挂世界空间小血条
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
        BossHealthBarUI.Register(this);
    }

    // 接触伤害：覆写以附加 0.1% 吸血
    protected override void OnCollisionEnter(Collision collision)
    {
        if (rolestate == state.dead) return;
        if (_mindControlledFlag) return;
        if (GetComponent<MindControlled>() != null) return;
        if (!collision.gameObject.CompareTag("Player")) { base.OnCollisionEnter(collision); return; }

        Player p = collision.gameObject.GetComponent<Player>();
        if (p == null || p.health <= 0) return;
        if (p.IsDashInvincibleActive) return;
        // 闪避
        if (p.EVA > Random.value * 100f)
        {
            MissNumber.Show(atknumber, collision.transform.position);
            return;
        }
        // 减伤
        int dmg = Mathf.Max(1, (int)(atk - p.def));
        p.health -= dmg;
        if (DamageNumberSettings.Visible)
        {
            GameObject number = Instantiate(atknumber, collision.transform.position, default);
            number.transform.localScale *= DamageNumberSettings.SizeScale;
            number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = dmg.ToString();
        }
        AudioManager.PlaySfx(AudioManager.SfxKey.Hit);
        p.startturnred();
        if (p.health <= 0) p.death();

        // 0.1% 吸血
        if (worldBossLifestealPct > 0f)
        {
            int heal = Mathf.Max(1, Mathf.RoundToInt(dmg * worldBossLifestealPct));
            health = Mathf.Min(healthmax, health + heal);
        }
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 2026-06-12：不管是否会被亡者领域复活，只要击败世界Boss就立即给予
        // 局内成长和源奖励（OnWorldBossDefeated）。复活检查放在之后执行。
        worldBossManager?.OnWorldBossDefeated(faction);

        // 亡者领域：复活检查。成功则 Boss 转为友军，不执行后续死亡流程。
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
        ClearSporeMutationColor();
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        if (expstone != null)
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        StartCoroutine(Destroy2());
    }
}
