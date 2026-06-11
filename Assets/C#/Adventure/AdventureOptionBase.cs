using System.Collections.Generic;
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

    [Header("出现规则")]
    [Tooltip("勾选则本局选过一次后不会再次出现。Something for nothing 和 蚂蚁召唤人类等可重复奇遇请取消勾选。")]
    public bool oneShot = true;

    /// <summary>当局已被选择过的奇遇集合（按 optionName 去重）。AdventureEventManager 调用 Reset。</summary>
    public static HashSet<string> usedOptionsThisRun = new HashSet<string>();

    /// <summary>是否可出现在当前难度的奇遇池中（默认可用）</summary>
    public virtual bool IsAvailableInCurrentDifficulty()
    {
        if (oneShot && !string.IsNullOrEmpty(optionName) && usedOptionsThisRun.Contains(optionName))
            return false;
        return true;
    }

    /// <summary>执行奇遇效果（子类重写，调用 base.Execute() 可恢复时间）</summary>
    public virtual void Execute()
    {
        if (oneShot && !string.IsNullOrEmpty(optionName))
            usedOptionsThisRun.Add(optionName);

        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null) bui.ResumeTime();
        else Time.timeScale = 1;
    }
}
