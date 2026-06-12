using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 「亡者领域」全局钩子：集中提供"被孢子伤害过"的敌人标记、复活为友军、玩家受伤时治疗等逻辑，
/// 避免污染 enemy/Player 主流程。
/// </summary>
public static class TombDomainHook
{
    /// <summary>「死亡前 windowSeconds 秒内吃过亡者领域(孢子)伤害」就视为"被领域击败"。</summary>
    private const float SporeDamageWindow = 5f;

    /// <summary>「死亡前 windowSeconds 秒内被已复活的友军打过」就视为"被友军击败"。</summary>
    private const float AllyDamageWindow = 5f;

    // ============================================================
    //  亡者领域 · 三条独立的复活判定（按优先级从高到低）
    //  优先级 1（最高）：被领域(孢子)击败 → 100%   复活为友军
    //  优先级 2        ：被已复活的友军击败 → 25%  复活为友军
    //  优先级 3（兜底）：被玩家其余技能击败 → 5%   复活为友军
    //  ※ 同一只敌人的死亡只会命中其中一条（先匹配的链路生效）；
    //    后两条仅在前面链路都未命中时才被尝试，保证"链路独立、不相互重投"。
    // ============================================================

    /// <summary>链路 A：被亡者领域(孢子/领域 tick)击败 → 100% 复活。</summary>
    public const float ReviveChanceFromDomain    = 1.00f;

    /// <summary>链路 B：被已复活的友军击败 → 25% 复活。</summary>
    public const float ReviveChanceFromAllyKill  = 0.25f;

    /// <summary>链路 C：被玩家其余技能击败（与领域、友军都无关）→ 5% 复活。</summary>
    public const float ReviveChanceFromOtherSkill = 0.05f;

    /// <summary>(已弃用) 历史 50% 字段，仅为外部老代码兼容保留；新逻辑用 ReviveChanceFromDomain。</summary>
    public const float ReviveChance = ReviveChanceFromDomain;

    /// <summary>被复活的友军继承玩家攻击力的比例（0~1）。例：0.10 表示 +Player.atk × 10%。</summary>
    public const float AllyAtkInheritFromPlayer = 0.10f;

    private static readonly Dictionary<enemy, float> _lastSporeHitTime = new Dictionary<enemy, float>();
    // 同一只敌人最后一次"被友军打"的时间。Destroy1 时如果命中窗口，视为友军杀死。
    private static readonly Dictionary<enemy, float> _lastAllyHitTime  = new Dictionary<enemy, float>();

    /// <summary>BulletSporeField 在造成伤害时调用：记录"曾经被孢子打过"。</summary>
    public static void MarkSporeDamage(enemy en)
    {
        if (en == null) return;
        _lastSporeHitTime[en] = Time.time;
    }

    public static bool WasRecentlySporeDamaged(enemy en)
    {
        if (en == null) return false;
        return _lastSporeHitTime.TryGetValue(en, out float t) && (Time.time - t) <= SporeDamageWindow;
    }

    /// <summary>MindControlled.DealMeleeHit / Bat.isAllyMode 命中敌人时调用：标记"被友军打过"。</summary>
    public static void MarkAllyDamage(enemy en)
    {
        if (en == null) return;
        _lastAllyHitTime[en] = Time.time;
    }

    public static bool WasRecentlyKilledByAlly(enemy en)
    {
        if (en == null) return false;
        return _lastAllyHitTime.TryGetValue(en, out float t) && (Time.time - t) <= AllyDamageWindow;
    }

    /// <summary>
    /// 尝试把这个敌人复活为友军。三条**独立、优先级明确**的触发链路：
    ///
    ///   优先级 1 —— 链路 A：被亡者领域(孢子/领域 tick)击败
    ///                        → 5s 内被孢子打过 → 100%   复活
    ///   优先级 2 —— 链路 B：被已复活的友军击败
    ///                        → 5s 内被友军打过、且**未**被孢子打过 → 25%  复活
    ///   优先级 3 —— 链路 C：被玩家其余技能击败（与领域、友军都无关）
    ///                        → **既未被孢子打过、也未被友军打过** → 5%   复活
    ///
    /// 判定规则：
    ///   - 三条链路按 A > B > C 顺序匹配，**先命中的链路独占判定**（不会同一只敌人在多条链路上各投一次）；
    ///   - 这样能保证"被领域 100%"、"被友军 25%"、"其余 5%" 的概率不会因为链路重叠而虚高/被稀释。
    ///
    /// 通用前置：玩家学过亡者领域、目标尚未被控制。
    /// 复活后会额外继承玩家攻击力的 AllyAtkInheritFromPlayer(10%) 作为加成。
    /// 返回 true 表示已复活，调用方应跳过原本的 Destroy1。
    /// </summary>
    public static bool TryReviveAsAlly(enemy en)
    {
        if (en == null) return false;
        if (en.GetComponent<MindControlled>() != null) return false; // 已是友军

        Player p = ResolveAnyPlayer();
        if (p == null)
        {
            Debug.Log($"[亡者领域·复活] 未找到玩家，跳过 ({en.gameObject.name})");
            return false;
        }
        SkillTombDomain td = SkillTombDomain.ResolveOnPlayer(p);
        if (td == null)
        {
            Debug.Log($"[亡者领域·复活] 玩家未学习亡者领域，跳过 ({en.gameObject.name})");
            return false;
        }

        // === 按优先级 A > B > C 选定**唯一**的链路，再投一次骰 ===
        bool sporeHit = WasRecentlySporeDamaged(en);
        bool allyHit  = WasRecentlyKilledByAlly(en);

        float  chance;
        string reason;
        if (sporeHit)
        {
            // 链路 A：领域击败 → 100%
            chance = ReviveChanceFromDomain;
            reason = "领域(孢子)击败";
        }
        else if (allyHit)
        {
            // 链路 B：友军击败（且未被领域打过） → 25%
            chance = ReviveChanceFromAllyKill;
            reason = "友军击败";
        }
        else
        {
            // 链路 C：玩家其余技能击败 → 5%
            // 兜底链路，仅当死亡前 5s 内既没吃过孢子也没被友军打过——
            // 视为"玩家用其余技能直接打死的敌人"。
            chance = ReviveChanceFromOtherSkill;
            reason = "玩家其余技能击败";
        }

        float roll = Random.value;
        if (roll > chance)
        {
            // 投掷未通过：清掉两个标记，避免重入误判
            _lastSporeHitTime.Remove(en);
            _lastAllyHitTime.Remove(en);
            Debug.Log($"[亡者领域·复活] {en.gameObject.name} 概率判定未通过 [{reason}] (roll={roll:F2} > {chance:F2})，正常死亡");
            return false;
        }
        Debug.Log($"[亡者领域·复活] {en.gameObject.name} 概率判定通过 [{reason}] (roll={roll:F2} <= {chance:F2})");

        // 世界 Boss 判定：注意 WorldBossBat/WorldBossMushroomMan 实际继承自 BossBat/BossMushroomMan，
        // 与孤立的 WorldBossBase 没有继承关系——不能用 `is WorldBossBase` 来判定。
        bool isWorldBoss = (en is WorldBossBat) || (en is WorldBossMushroomMan) || (en is WorldBossBase);

        // === 2026-06-12：亡者领域对关底 Boss 无效，只对世界 Boss 有效 ===
        // 关底 Boss（BossBat / BossMushroomMan 实例但不是世界 Boss 版本）不能被复活。
        bool isStageBoss = !isWorldBoss && ((en is BossBat) || (en is BossMushroomMan));
        if (isStageBoss)
        {
            _lastSporeHitTime.Remove(en);
            _lastAllyHitTime.Remove(en);
            Debug.Log($"[亡者领域·复活] {en.gameObject.name} 是关底Boss，亡者领域对其无效，跳过复活");
            return false;
        }

        // === 复活：MindControlled 接管 → Boss 级特效 Spawn ===
        // ★ 顺序修复（v10.1）：必须**先** AddComponent + Setup + SetReviveFreeze(true)，**再** Spawn
        //   特效。原因：
        //     1) ReviveBossEffect.Spawn 内部要 `targetTransform.GetComponent<MindControlled>()` 取
        //        mc 引用以调 SetReviveFreeze 和每帧 ForceSyncOverlayNow。如果先 Spawn 再 AddComponent，
        //        mc 永远是 null → 冻结失败 → MindControlled.FixedUpdate 立刻接管 `ismove`，把
        //        Animator 切回 idle/move，把我们手动 SampleAnimation 写入的 dead 第 0 帧全部覆盖掉，
        //        玩家看不到"先死后复活"的任何过程。
        //     2) Setup 内部会 `_baseRenderer.enabled = false` 并创建 overlay 子物体，Spawn 之后的
        //        协程必须能拿到这个 overlay 才能让反向死亡视觉显现在友军紫色色调上。
        //
        //   注意：Setup 把 `rolestate = idle` 后会立即开始走 FixedUpdate 逻辑，但 _frozenForRevive
        //   在 Setup 后立刻被置 true，FixedUpdate 第一行就 return，不会干扰特效。
        MindControlled mc = en.gameObject.AddComponent<MindControlled>();
        mc.Setup(en, isWorldBoss,
                 lifetime: td.minionLifetime,
                 decayPerSec: td.minionDecayPerSecond,
                 leash: td.bossLeashDistance);

        // === Boss 级别复活特效（仅 Boss 触发，小怪复活不放，避免战场视觉污染）===
        // 判定口径：
        //   • 世界 Boss（isWorldBoss）必触；
        //   • 关底 Boss：BossBat / BossMushroomMan 链路（WorldBossBat / WorldBossMushroomMan 已经被
        //     isWorldBoss 涵盖，所以这里只额外关心"非世界版"的关底 boss 实例）；
        //   • 普通敌人（小蝙蝠、小蘑菇等）走 enemy.Destroy1 复活链路时此判定为 false → 不放特效，
        //     避免大量小怪同时被孢子团灭时屏幕被符文阵刷屏。
        // 特效是顶层独立 GameObject（不挂目标身上），避免被 MindControlled 改写
        // SpriteRenderer.enabled 后被连带"隐藏"。Spawn 内部已做资源缺失兜底，不会阻断复活主流程。
        bool isBossLevel = isWorldBoss || (en is BossBat) || (en is BossMushroomMan);
        if (isBossLevel)
        {
            // 先冻结 MindControlled，再 Spawn 特效（特效内部也会再调一次 SetReviveFreeze(true)，幂等）
            mc.SetReviveFreeze(true);
            ReviveBossEffect.Spawn(en.transform, isWorldBoss);
        }

        // === 继承玩家 10% 攻击力 ===
        // 在 Setup 之后追加加成（Setup 不动签名，加成单独可观测、便于将来调系数）。
        // enemy.atk 是 float，可以直接累加；最低保证 +1，避免极低 player.atk 下的"假加成"。
        float bonus = Mathf.Max(1f, p.atk * AllyAtkInheritFromPlayer);
        en.atk += bonus;

        // 清掉两个标记：成功复活后，新生友军不应再因为残留标记被二次复活循环
        _lastSporeHitTime.Remove(en);
        _lastAllyHitTime.Remove(en);

        Debug.Log($"[亡者领域·复活] 成功！{en.gameObject.name} 转为友军（链路={reason}, isWorldBoss={isWorldBoss}, hp={en.healthmax}, atk={en.atk:F1}（含 +{bonus:F1} 来自玩家）)");
        return true;
    }

    /// <summary>玩家受伤时由 Player.startturnred / 受伤管线调用：治疗所有被控制的世界 Boss。</summary>
    public static void OnPlayerTookDamage()
    {
        Player p = ResolveAnyPlayer();
        if (p == null) return;
        SkillTombDomain td = SkillTombDomain.ResolveOnPlayer(p);
        if (td == null) return;
        MindControlled.HealAllControlledBosses(td.worldBossHealOnPlayerDamage);
    }

    private static Player ResolveAnyPlayer()
    {
        Transform pl = GameObject.Find("playerlayer")?.transform;
        if (pl == null) return null;
        foreach (Transform t in pl)
        {
            if (t == null || !t.CompareTag("Player")) continue;
            Player p = t.GetComponent<Player>();
            if (p != null) return p;
        }
        return pl.childCount > 0 ? pl.GetChild(0).GetComponent<Player>() : null;
    }

    /// <summary>场景重载时调用以清理静态字典。</summary>
    public static void ResetSceneCaches()
    {
        _lastSporeHitTime.Clear();
        _lastAllyHitTime.Clear();
    }
}
