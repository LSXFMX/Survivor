using UnityEngine;

/// <summary>
/// 奇遇3：蚂蚁召唤人类
/// 效果：撤销本次奇遇，返还源木（数量与触发阈值相同）
/// </summary>
public class AdventureAntSummon : AdventureOptionBase
{
    /// <summary>无尽模式下不出现该奇遇。</summary>
    public override bool IsAvailableInCurrentDifficulty()
    {
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsEndless)
            return false;
        return base.IsAvailableInCurrentDifficulty();
    }

    public override void Execute()
    {
        int refund = AdventureEventManager.Instance != null
            ? AdventureEventManager.Instance.TriggerThreshold
            : 0;

        if (refund > 0)
            YuanMuManager.Instance?.Add(refund);

        base.Execute();
    }
}
