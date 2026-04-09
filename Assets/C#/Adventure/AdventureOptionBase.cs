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

    /// <summary>是否可出现在当前难度的奇遇池中（默认可用）</summary>
    public virtual bool IsAvailableInCurrentDifficulty() => true;

    /// <summary>执行奇遇效果（子类重写，调用 base.Execute() 可恢复时间）</summary>
    public virtual void Execute()
    {
        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null) bui.ResumeTime();
        else Time.timeScale = 1;
    }
}
