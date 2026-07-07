using System.Collections;
using UnityEngine;

public class WorldBossMushroomMan : BossMushroomMan
{
    [Header("世界Boss设置")]
    public float       activateRange   = 15f;
    public FactionType faction         = FactionType.Mushroom;
    [Range(0f, 0.01f)]public float lifestealPct = 0.001f;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private bool _wasHit = false;

    private void Start()
    {
        // 初始血量即为满血，受击标记为false
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;
        if (GetComponent<MindControlled>() != null) return;

        if (!_activated)
        {
            // 受击检测：血量减少即标记"玩家在攻击"
            if (health < healthmax) { _wasHit = true; health = healthmax; } // 无敌：回满血

            // 距离检测
            if (role == null) getrole();
            if (role != null && Vector3.Distance(transform.position, role.transform.position) <= activateRange && _wasHit)
            {
                _activated = true;
                ToastManager.Show("世界Boss已激活！");
                BossHealthBarUI.Register(this);
            }
            if (!_activated) return;
        }

        base.FixedUpdate();
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
        worldBossManager?.OnWorldBossDefeated(faction);
        if (!_reviveAttempted) { _reviveAttempted = true; if (TombDomainHook.TryReviveAsAlly(this)) { Debug.Log($"[亡者领域] 世界蘑菇Boss被永久控制为友军"); return; } }
        var savedBattleUI = battleUI; battleUI = null;
        base.Destroy1();
        battleUI = savedBattleUI;
    }
}
