using UnityEngine;

/// <summary>
/// 奇遇4：寻找无尽源木
/// 效果：立刻获得 50 源木；**每个已占领营地**每秒源木 +2。
///
/// 说明：`YuanMuManager.perSecond` 是全局加成字段，被**每个**营地的
/// <see cref="Camp.YuanMuCoroutine"/> 每秒各自读取并叠加（营地基础 1 + 加成）。
/// 因此无论营地是在选奇遇前还是后占领，只要它存在就享受 +2 ——
/// 实际效果 = 每个营地每秒 +2（营地越多总收益越高），属**有意设计**。
/// 文案不能写"此后"（易被理解成只对之后占领的营地生效），应写"每个已占领营地"。
/// </summary>
public class AdventureOption4_InfiniteYuanmu : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "寻找无尽源木";
        optionDescription = "出发雷霆号，寻找无尽源木";
        effectDescription = "立刻获得50源木；每个已占领营地每秒源木+2";
    }

    /// <summary>
    /// Reset() 只在编辑器"新增组件"那一刻跑一次，场景/预制体里已序列化的旧文案
    /// （"每秒源木+2，立刻获得50源木"）不会自动更新 —— 运行时强制纠正一次（幂等）。
    /// </summary>
    private void Awake()
    {
        const string newDesc = "立刻获得50源木；每个已占领营地每秒源木+2";
        if (string.IsNullOrEmpty(effectDescription))
            effectDescription = newDesc;
        else if (!effectDescription.Contains("每个已占领营地"))
            effectDescription = newDesc;
    }

    public override void Execute()
    {
        if (YuanMuManager.Instance != null)
        {
            // 立刻获得50源木
            YuanMuManager.Instance.Add(50);
            // 每个营地每秒 +2：修改全局 perSecond 增量（每个营地的 YuanMuCoroutine 各自读取叠加）
            YuanMuManager.Instance.perSecond += 2;
        }
        base.Execute();
    }
}
