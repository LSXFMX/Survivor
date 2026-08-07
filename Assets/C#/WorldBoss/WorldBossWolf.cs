using UnityEngine;

public class WorldBossWolf : WolfBoss
{
    /// <summary>世界 Boss（覆写父类 WolfBoss 的 StageBoss）。</summary>
    public override BossTag bossTag => BossTag.WorldBoss;

    [Header("世界Boss设置")]
    public float       activateRange            = 25f;
    public FactionType faction                  = FactionType.Wolf;
    [Range(0f, 0.5f)] public float naturalHealPctPerSecond = 0.0001f;
    [Range(0f, 0.01f)]public float lifestealPct           = 0.001f;
    private float _healAccum;

    [HideInInspector] public WorldBossManager worldBossManager;

    private bool _activated = false;
    private bool _wasHit = false;

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后跳过"激活判定"（未激活时 _wasHit 为 false 会 return，
        //   友军 Boss 永远等不到"被玩家攻击"这个条件，会一直站着不动）。
        //   直接进入 WolfBoss 的技能状态机；同时不再自然回血。
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

        // 【2026-08 修复·无尽模式狼人 boss 二阶段】
        //   症状：无尽模式打到世界狼人 boss，半血变身进 Phase.Wolf 后既不显示血条又无敌。
        //   根因：TransformRoutine 的协程在无尽模式（busy 监控阈值 9s）边界条件下
        //   可能未走到 finally 块 —— phase 进入 Wolf 但 invincible 仍为 true，
        //   WolfBoss.LateUpdate 每帧 `if (invincible) health = lockedHealth` 把它锁死，
        //   子弹命中即"无效"= 真正无敌；血条条目虽然已注册但 health 不变 + 中途重建易丢。
        //   修复：Wolf 形态每帧兜底 —— 强制 invincible=false（解锁锁血），
        //   并幂等重新注册血条（DoRegister 内部 _entries.Exists 去重，无副作用）。
        if (IsWolfPhase)
        {
            if (IsTransformInvincible) ClearTransformInvincible();
            BossHealthBarUI.Register(this);
        }
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

        // 【2026-08 修复】变身无敌期免死：此时命中只是被 WolfBoss.Destroy1 的
        //   `if (invincible) { health = lockedHealth; return; }` 锁血救活，Boss 实际不会死。
        //   若在此处直接触发 OnWorldBossDefeated（掉落），会出现：
        //     变身期间掉一次（但Boss没死） + 狼形态死亡再掉一次 = **双掉落**。
        //   无敌期直接返回，掉落在狼形态真正死亡时才结算一次。
        //   （WolfBoss.LateUpdate 每帧 `if (invincible) health = lockedHealth` 保证血量锁死。）
        if (IsTransformInvincible) return;

        // 亡者领域复活检查（与WorldBossBat/WorldBossMushroomMan一致）
        if (!_reviveAttempted) { _reviveAttempted = true; if (TombDomainHook.TryReviveAsAlly(this)) { Debug.Log("[亡者领域] 世界狼人Boss被永久控制为友军"); return; } }
        worldBossManager?.OnWorldBossDefeated(faction);
        FavorManager.Instance?.AddFavor(FactionType.Wolf, 1);
        var saved = battleUI; battleUI = null;
        base.Destroy1();
        battleUI = saved;
    }
}
