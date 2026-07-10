using UnityEngine;

/// <summary>
/// 「失去焦点继续运行」设置（PlayerPrefs 持久化）。
/// 由设置面板的「后台运行」Toggle 读写，开局时 title.cs 调用 <see cref="Apply"/> 生效。
/// </summary>
public static class BackgroundRun
{
    private const string KEY = "BackgroundRun.Enabled";

    /// <summary>是否启用后台运行（默认关）。写入即持久化并立刻生效。</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(KEY, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(KEY, value ? 1 : 0);
            PlayerPrefs.Save();
            Application.runInBackground = value;
        }
    }

    /// <summary>开局时调用一次，把持久化值读到 Application.runInBackground。</summary>
    public static void Apply()
    {
        Application.runInBackground = Enabled;
    }
}
