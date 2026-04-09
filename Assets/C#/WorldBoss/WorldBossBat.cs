using UnityEngine;

/// <summary>
/// 世界蝙蝠Boss：继承 BossBat，增加待机/激活逻辑。
/// </summary>
public class WorldBossBat : BossBat
{
    [Header("世界Boss设置")]
    public float       activateRange   = 15f;
    public FactionType faction         = FactionType.Bat;

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

        if (!_activated)
        {
            // 受到攻击（血量减少）也激活
            if (_lastHealth > health)
            {
                _activated = true;
                ToastManager.Show("世界Boss已激活！");
            }
            _lastHealth = health;

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

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        var savedBattleUI = battleUI;
        battleUI = null;
        base.Destroy1();
        battleUI = savedBattleUI;

        worldBossManager?.OnWorldBossDefeated(faction);
    }
}
