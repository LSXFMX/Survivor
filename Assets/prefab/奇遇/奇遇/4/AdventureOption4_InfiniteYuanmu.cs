using UnityEngine;

/// <summary>
/// 奇遇4：寻找无尽源木
/// 效果：每秒源木+2，立刻获得50源木
/// </summary>
public class AdventureOption4_InfiniteYuanmu : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "寻找无尽源木";
        optionDescription = "出发雷霆号，寻找无尽源木";
        effectDescription = "每秒源木+2，立刻获得50源木";
    }

    public override void Execute()
    {
        if (YuanMuManager.Instance != null)
        {
            // 立刻获得50源木
            YuanMuManager.Instance.Add(50);
            // 每秒+2：修改 perSecond 增量
            YuanMuManager.Instance.perSecond += 2;
        }
        base.Execute();
    }
}
