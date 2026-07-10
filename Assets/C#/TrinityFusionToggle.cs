using UnityEngine;

/// <summary>
/// SSR9「三清化一」开关：设置面板 Toggle → PlayerPrefs 持久化。
/// 关闭后即使已解锁 SSR9，人格解离奇遇也不会触发"分身技能合并到本体"效果。
/// </summary>
public static class TrinityFusionToggle
{
    private const string KEY = "TrinityFusion.Enabled";

    /// <summary>是否启用三清化一（默认开）。写入即持久化。</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(KEY, 1) != 0;
        set { PlayerPrefs.SetInt(KEY, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
