using UnityEngine;

/// <summary>
/// 伤害飘字显示开关 + 大小档位：全局静态状态 + PlayerPrefs 持久化。
/// 大小档位：0=小 (×0.45), 1=中 (×0.7), 2=大 (×1.0, 默认)
/// </summary>
public static class DamageNumberSettings
{
    private const string KEY_VISIBLE = "DamageNumber.Visible";
    private const string KEY_SIZE    = "DamageNumber.Size";
    private static bool _visible;
    private static bool _loaded;
    private static int  _size = -1;

    public static bool Visible
    {
        get { Load(); return _visible; }
        set { _visible = value; _loaded = true; PlayerPrefs.SetInt(KEY_VISIBLE, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>0=小, 1=中, 2=大（默认）</summary>
    public static int Size
    {
        get { Load(); return _size >= 0 ? _size : 2; }
        set { _size = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetInt(KEY_SIZE, _size); PlayerPrefs.Save(); }
    }

    /// <summary>0 → ×0.45, 1 → ×0.7, 2 → ×1.0</summary>
    public static float SizeScale => Size switch { 0 => 0.45f, 1 => 0.7f, _ => 1.0f };

    public static string SizeLabel => Size switch { 0 => "小", 1 => "中", _ => "大" };

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _visible = PlayerPrefs.GetInt(KEY_VISIBLE, 1) != 0;
        _size    = PlayerPrefs.GetInt(KEY_SIZE, 2);
    }
}
