using UnityEngine;
using TMPro;

/// <summary>
/// 世界狼人Boss：继承 WolfBoss，待机→激活，属性翻倍（prefab=1000/100）+20%/s回血+0.1%吸血。
/// </summary>
public class WorldBossWolf : WolfBoss
{
    [Header("世界Boss设置")]
    public float       activateRange            = 15f;
    public FactionType faction                  = FactionType.Wolf;
    [Range(0f, 0.5f)] public float naturalHealPctPerSecond = 0.2f;
    [Range(0f, 0.01f)]public float lifestealPct           = 0.001f;
    private float _healAccum;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;
        if (!_activated)
        {
            if (role == null) getrole();
            if (role != null && Vector3.Distance(transform.position, role.transform.position) <= activateRange)
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
        worldBossManager?.OnWorldBossDefeated(faction);
        FavorManager.Instance?.AddFavor(FactionType.Wolf, 1);
        var saved = battleUI; battleUI = null;
        base.Destroy1();
        battleUI = saved;
    }
}
