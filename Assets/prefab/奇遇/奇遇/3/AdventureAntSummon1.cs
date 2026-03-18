using UnityEngine;

/// <summary>
/// 奇遇3：蚂蚁召唤人类
/// 效果：撤销本次奇遇，返还源木（数量与触发阈值相同）
/// </summary>
public class AdventureAntSummon : AdventureOptionBase
{
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
