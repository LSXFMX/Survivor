using UnityEngine;

/// <summary>
/// 世界史莱姆Boss：继承 SlimeBoss，增加"待机 → 激活"逻辑（世界Boss通用范式）。
/// - 生成后原地待机，不追玩家、不放技能
/// - 玩家进入 activateRange 或受到攻击后激活，开始正常史莱姆Boss行为
/// - 死亡后通知 WorldBossManager（而非 battleUI 关底结算）
/// </summary>
public class WorldBossSlime : SlimeBoss
{
    [Header("世界Boss设置")]
    public float       activateRange = 15f;
    public FactionType faction       = FactionType.Slime;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private int  _lastHealth;

    private void Start()
    {
        _lastHealth = health;
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后，行为完全交给 MindControlled
        if (GetComponent<MindControlled>() != null) return;

        if (!_activated)
        {
            // 受到攻击（血量减少）也激活
            if (_lastHealth > health)
            {
                _activated = true;
                ToastManager.Show("世界Boss已激活！");
            }
            _lastHealth = health;

            // 玩家靠近也激活
            if (role == null) getrole();
            if (role != null)
            {
                float dist = Vector3.Distance(transform.position, role.transform.position);
                if (dist <= activateRange)
                {
                    _activated = true;
                    ToastManager.Show("世界Boss已激活！");
                }
            }
            if (!_activated) return;
        }

        base.FixedUpdate();
    }

    // 覆盖死亡：通知 WorldBossManager 而非 battleUI（世界Boss被击败不等于关底通关）
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        worldBossManager?.OnWorldBossDefeated(faction);

        // 临时置空 battleUI，避免 SlimeBoss.Destroy1 触发关底胜利结算
        var savedBattleUI = battleUI;
        battleUI = null;
        base.Destroy1();
        battleUI = savedBattleUI;
    }
}
