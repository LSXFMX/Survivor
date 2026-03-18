using UnityEngine;

/// <summary>
/// 奇遇选项基类，结构对标 Upgradeoptionsbase。
/// 子类重写 Execute() 实现具体效果。
/// </summary>
public class AdventureOptionBase : MonoBehaviour
{
    [Header("基础信息")]
    public string optionName;
    public string optionDescription;
    public string effectDescription; // 效果信息，如"回复50点生命值"
    public Sprite icon;

    /// <summary>执行奇遇效果（子类重写，调用 base.Execute() 可恢复时间）</summary>
    public virtual void Execute()
    {
        Time.timeScale = 1;
    }
}
