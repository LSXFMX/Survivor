using UnityEngine;

/// <summary>
/// 世界狼人Boss：继承 WolfBoss，增加"待机 → 激活"逻辑（世界Boss通用范式）。
/// - 生成后原地待机，不追玩家
/// - 玩家进入 activateRange 或受到攻击后激活，开始正常 WolfBoss 行为
/// - 死亡后通知 WorldBossManager（而非 battleUI 关底结算）
/// </summary>
public class WorldBossWolf : WolfBoss
{
    [Header("世界Boss设置")]
    public float       activateRange = 15f;
    public FactionType faction       = FactionType.Wolf;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;

    private void Start()
    {
        // WolfBoss 在 OnEnable 已经初始化过各字段，这里仅标记初始血量用于受击激活
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // WolfBoss 内部有 phase==Human/Wolf/Transforming/Dead 状态机，
        // 这里在外层额外加一道"未激活 → 原地待机"的拦截。
        if (!_activated)
        {
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

    // 覆盖死亡：通知 WorldBossManager 而非 battleUI
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        worldBossManager?.OnWorldBossDefeated(faction);

        // 也通知 FavorManager：每次击败狼人世界Boss好感度+1（与 WolfBoss 普通击败一致）
        if (FavorManager.Instance != null)
        {
            FavorManager.Instance.AddFavor(FactionType.Wolf, 1);
        }

        // 临时置空 battleUI，避免 WolfBoss.Destroy1 触发关底胜利结算
        var savedBattleUI = battleUI;
        battleUI = null;
        base.Destroy1();
        battleUI = savedBattleUI;
    }
}
