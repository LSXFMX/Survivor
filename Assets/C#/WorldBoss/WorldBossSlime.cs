using UnityEngine;
using TMPro;

/// <summary>
/// 世界史莱姆Boss：继承 SlimeBoss，待机→激活，包含世界Boss属性加成。
/// 属性翻倍由预制体提供（health=1000,atk=100），难度倍率由基类OnEnable应用。
/// 20%/s自然回血 + 0.1%全能吸血。
/// </summary>
public class WorldBossSlime : SlimeBoss
{
    [Header("世界Boss设置")]
    public float       activateRange            = 15f;
    public FactionType faction                  = FactionType.Slime;
    [Range(0f, 0.01f)] public float naturalHealPctPerSecond = 0.0005f; // 0.05%/s 回血
    [Range(0f, 0.01f)]public float lifestealPct           = 0.001f; // 0.1% 全能吸血
    private float _healAccum;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private int  _lastHealth;

    private void Start() { _lastHealth = health; }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;
        if (GetComponent<MindControlled>() != null) return;

        if (!_activated)
        {
            if (_lastHealth > health || (role != null && Vector3.Distance(transform.position, role.transform.position) <= activateRange))
            {
                _activated = true;
                ToastManager.Show("世界Boss已激活！");
                BossHealthBarUI.Register(this);
            }
            _lastHealth = health;
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
        // 记录碰撞前血量用于吸血
        int hpBefore = health;
        base.OnCollisionEnter(collision);
        int dmgDealt = hpBefore > 0 && lifestealPct > 0f ? Mathf.Max(0, hpBefore - health) : 0;
        if (dmgDealt > 0 && health > 0) health = Mathf.Min(healthmax, health + Mathf.Max(1, Mathf.RoundToInt(dmgDealt * lifestealPct)));
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
        worldBossManager?.OnWorldBossDefeated(faction);
        var saved = battleUI; battleUI = null;
        base.Destroy1();
        battleUI = saved;
    }
}
