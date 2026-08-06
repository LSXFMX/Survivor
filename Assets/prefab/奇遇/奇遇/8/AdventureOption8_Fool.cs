using UnityEngine;

/// <summary>
/// 奇遇8：愚弄
/// 效果：本局门挑战进度重置到第 1 层，**但门挑战难度永久翻倍（×2 累乘）**。
/// 数值实现统一交给 <see cref="GateChallengeManager.ResetAndDouble"/>，
/// 这里不再走反射改私有字段（之前的写法只重置进度、不翻难度）。
///
/// ════════════════════ 无尽模式：按局内计时解锁额外选择机会 ════════════════════
///
/// 普通难度仍是 oneShot（单局只能选一次）。
/// 无尽模式改为「每 <see cref="INTERVAL_MINUTES"/> 分钟局内计时 +1 次机会」：
///
///   ┌────────────────┬────────────┬────────────────────┐
///   │ 局内计时       │ 累计可选次 │ 选完后门挑战难度│
///   ├────────────────┼────────────┼────────────────────┤
///   │ 0（开局）      │     1      │ ×2                 │
///   │ 120 分钟       │     2      │ ×4                 │
///   │ 240 分钟       │     3      │ ×8                 │
///   │ 360 分钟       │     4      │ ×16                │
///   └────────────────┴────────────┴────────────────────┘
///
/// 为什么用「局内计时」而不是 <c>Time.time</c>：无尽支持倍速，而且暂停/选奇遇
/// 期间现实时间仍在流逝 —— 用现实时间会和玩家看到的计时器严重不一致。
/// 这里统一读 <see cref="EndlessRuntime.RunElapsed"/>（由 battleUI 的无尽计时驱动）。
///
/// 为什么门槛定120 分钟（而不是像女娲补天那样 30 分钟）：愚弄是**成倍**放大门挑战
/// 难度的，×2 累乘 4 次就是 ×16；而门挑战每层通关会给「全技能升级上限 +1」+ 随机属性，
/// 是无尽后期最主要的 build 成长来源。间隔太短会让玩家在 1 小时内把倍率堆到爆、
/// 再也打不动门挑战，等于自断成长线。120 分钟差不多是"上一档倍率的门挑战刚好被
/// 新装备和技能追平"的节奏。
///
/// 注意配套改动：敌人闪避被难度倍率一起放大，×8 时 15% 会变成 120% → 完全无敌。
/// 所以怪物闪避统一钳在 <see cref="enemy.EVA_CAP"/>（50%）。
/// </summary>
public class AdventureOption8_Fool : AdventureOptionBase
{
    /// <summary>无尽模式下，每多少**分钟局内计时**追加一次「愚弄」选择机会。</summary>
    private const int INTERVAL_MINUTES = 120;

    /// <summary>无尽模式开局自带的选择次数。</summary>
    private const int BASE_CHANCES = 1;

    // ── 无尽模式多选追踪（静态：跨奇遇面板存活，按局重置）──
    private static int _foolSelectedCount = 0;

    /// <summary>新一局开始时清零。由 AdventureEventManager.Awake 调用。</summary>
    public static void ResetRunCounter() => _foolSelectedCount = 0;

    /// <summary>本局已选择的愚弄次数（调试/UI 用）。</summary>
    public static int SelectedCount => _foolSelectedCount;

    /// <summary>
    /// 当前累计允许的选择次数 = 1 + 局内计时分钟 / 120。
    /// </summary>
    public static int MaxChancesNow()
    {
        int elapsedMin = (int)(Mathf.Max(0f, EndlessRuntime.RunElapsed) / 60f);
        return BASE_CHANCES + elapsedMin / INTERVAL_MINUTES;
    }

    public override bool IsAvailableInCurrentDifficulty()
    {
        if (DifficultyManager.Instance == null) return false;

        bool endless = DifficultyManager.Instance.IsEndless;

        if (endless)
        {
            // 无尽：不走 base 的 oneShot 去重（否则第一次选完就永远消失），
            // 改为按"已选次数 < 当前允许次数"放行。
            return _foolSelectedCount < MaxChancesNow();
        }

        // 普通难度：先过 base 的 oneShot 去重，再要求 N5+
        if (!base.IsAvailableInCurrentDifficulty()) return false;

        string label = DifficultyManager.Instance.Current.label;
        if (!label.StartsWith("N")) return false;
        if (!int.TryParse(label.Substring(1), out int n)) return false;
        return n >= 5;
    }

    private void Reset()
    {
        optionName        = "愚弄";
        optionDescription = "你欺骗了那扇灰白色的门，假装自己未收到赐福";
        effectDescription = "门挑战进度重置至第1层，但门挑战难度翻倍（无尽模式每120分钟可再选一次）";
    }

    /// <summary>
    /// Reset() 只在编辑器里"新增组件"那一刻跑一次，场景/预制体里已经序列化好的
    /// effectDescription 不会因为改了 Reset() 而更新 —— 玩家看到的还是旧文案。
    /// 所以这里在运行时补一句无尽规则说明（幂等，已包含就不重复追加）。
    /// </summary>
    private void Awake()
    {
        const string hint = "（无尽模式每120分钟可再选一次，难度依次 ×2 → ×4 → ×8）";
        if (string.IsNullOrEmpty(effectDescription))
            effectDescription = "门挑战进度重置至第1层，但门挑战难度翻倍" + hint;
        else if (!effectDescription.Contains("120分钟"))
            effectDescription += hint;
    }

    public override void Execute()
    {
        bool endless = DifficultyManager.Instance != null && DifficultyManager.Instance.IsEndless;

        if (GateChallengeManager.Instance != null)
        {
            // 同时完成「重置到第1层」+「难度倍率 ×2」+「提示」。
            GateChallengeManager.Instance.ResetAndDouble();
        }

        if (endless)
        {
            _foolSelectedCount++;

            // 告知玩家下一次机会什么时候到（否则玩家会以为愚弄没了）
            int nextAtMin = _foolSelectedCount * INTERVAL_MINUTES;
            ToastManager.Show($"<color=#9BE8FF>愚弄已用 {_foolSelectedCount} 次" +
                              $"，下一次机会：局内计时 {nextAtMin} 分钟</color>");

            // 无尽模式不走 base.Execute() 的 oneShot 去重（改由上面的计数控制），
            // 但仍必须手动恢复时间流，否则游戏停在暂停状态。
            battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
            if (bui != null) bui.ResumeTime();
            else Time.timeScale = 1;
            return;
        }

        base.Execute();
    }
}
