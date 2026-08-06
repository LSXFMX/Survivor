using UnityEngine;

public class WorldBossSlime : SlimeBoss
{
    [Header("世界Boss设置")]
    public float       activateRange            = 25f;
    public FactionType faction                  = FactionType.Slime;
    [Range(0f, 0.001f)] public float naturalHealPctPerSecond = 0.0001f;
    [Range(0f, 0.01f)] public float lifestealPct = 0.001f;
    private float _healAccum;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private bool _wasHit = false;

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后跳过"激活判定"（激活语义只针对玩家接近/受击），
        //   直接进入 SlimeBoss 的技能状态机（role 已由 MindControlled 喂成最近的敌人）。
        //   同时不再自然回血（"被操控后失去自然回血词条"）。
        //   原实现在这里直接 return，导致友军世界史莱姆 Boss 完全不动、技能永不释放。
        if (GetComponent<MindControlled>() != null)
        {
            base.FixedUpdate();
            return;
        }

        if (!_activated)
        {
            if (health < healthmax) { _wasHit = true; health = healthmax; }
            if (role == null) getrole();
            if (role != null && Vector3.Distance(transform.position, role.transform.position) <= activateRange && _wasHit)
            {
                _activated = true;
                ToastManager.Show("世界Boss已激活！");
                BossHealthBarUI.Register(this);
            }
            if (!_activated) return;
        }
        TickNaturalHeal();
        base.FixedUpdate();
    }

    private void TickNaturalHeal()
    {
        if (naturalHealPctPerSecond <= 0f || health <= 0 || health >= healthmax) return;
        _healAccum += healthmax * naturalHealPctPerSecond * Time.fixedDeltaTime;
        if (_healAccum >= 1f) { int g = (int)_healAccum; _healAccum -= g; health = Mathf.Min(healthmax, health + g); }
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        int hpBefore = health;
        base.OnCollisionEnter(collision);
        int d = hpBefore > 0 && lifestealPct > 0f ? Mathf.Max(0, hpBefore - health) : 0;
        if (d > 0 && health > 0) health = Mathf.Min(healthmax, health + Mathf.Max(1, Mathf.RoundToInt(d * lifestealPct)));
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 【2026-08 修复】史莱姆→巨龙变身演出期间免死（SlimeBoss.Destroy1 对 Transforming
        //   直接 return）。若在此处触发 OnWorldBossDefeated（掉落），会出现：
        //     变身期间掉一次（但Boss没死） + 龙形态死亡再掉一次 = **双掉落**。
        //   变身期直接返回，掉落在龙形态真正死亡时才结算一次。
        if (IsTransformingPhase) return;

        // 亡者领域复活检查（与WorldBossBat/WorldBossMushroomMan一致）
        if (!_reviveAttempted) { _reviveAttempted = true; if (TombDomainHook.TryReviveAsAlly(this)) { Debug.Log("[亡者领域] 世界史莱姆Boss被永久控制为友军"); return; } }
        worldBossManager?.OnWorldBossDefeated(faction);
        // 世界Boss 击败 → 好感度 +1（与 WorldBossWolf.Destroy1 一致；
        // 此前史莱姆世界 Boss 漏了这一行，导致打世界 Boss 不涨好感度）
        FavorManager.Instance?.AddFavor(FactionType.Slime, 1);
        var saved = battleUI; battleUI = null;
        base.Destroy1();
        battleUI = saved;
    }

    /// <summary>
    /// 世界 Boss 不走关底 Boss 的「首杀解锁阴史莱姆 +10 / 每杀 +1」流程 ——
    /// 它的社群解锁与好感度由 WorldBossManager.OnWorldBossDefeated +
    /// 本类 Destroy1 里的 AddFavor(1) 负责，否则一次击败会重复加两次好感度。
    /// </summary>
    protected override void GrantSlimeFactionFavor() { }
}
