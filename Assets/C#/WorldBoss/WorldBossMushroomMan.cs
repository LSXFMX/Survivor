using System.Collections;
using UnityEngine;

/// <summary>
/// 世界蘑菇Boss：继承 BossMushroomMan，增加待机/激活逻辑。
/// - 生成后原地待机，不追玩家
/// - 玩家进入 activateRange 后激活，开始正常Boss行为
/// - 死亡后通知 WorldBossManager（而非 battleUI）
///
/// Inspector 配置：
/// - activateRange     : 激活距离，默认 15
/// - faction           : 对应社群
/// - worldBossManager  : 由 WorldBossManager 赋值
/// </summary>
public class WorldBossMushroomMan : BossMushroomMan
{
    [Header("世界Boss设置")]
    public float       activateRange   = 15f;
    public FactionType faction         = FactionType.Mushroom;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private int  _lastHealth;

    private void Start()
    {
        _lastHealth = health;
    }

    // 覆盖父类 FixedUpdate
    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后，行为完全交给 MindControlled（不再追玩家、不再走激活逻辑）
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

    // 覆盖死亡：通知 WorldBossManager 而非 battleUI
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：先在最外层拦截。命中则不能让 base.Destroy1 之后的
        // worldBossManager.OnWorldBossDefeated 执行——那会错误地把"被控制"当成"被击败"。
        // _reviveAttempted 设为 true 后，base.Destroy1（→ BossMushroomMan.Destroy1）就不会再
        // 重复调一次 hook（避免双重判定）。
        if (!_reviveAttempted)
        {
            _reviveAttempted = true;
            if (TombDomainHook.TryReviveAsAlly(this))
            {
                Debug.Log($"[亡者领域] 世界蘑菇Boss {gameObject.name} 被永久控制为友军");
                return;
            }
        }

        // 调用父类死亡流程（播动画、生成经验石等）
        // 但不触发 battleUI.OnBossDefeated()，所以先把 battleUI 置空
        var savedBattleUI = battleUI;
        battleUI = null;
        base.Destroy1();
        battleUI = savedBattleUI;

        // 通知世界Boss管理器
        worldBossManager?.OnWorldBossDefeated(faction);
    }
}
