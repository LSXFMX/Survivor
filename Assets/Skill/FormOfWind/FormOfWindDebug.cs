using UnityEngine;

/// <summary>
/// 风之形 / 风箭命中链诊断：默认用 Debug.LogError（不受 Console「仅 Warning」过滤影响）。查完后把 Enabled 设为 false。
/// </summary>
public static class FormOfWindDebug
{
    public static bool Enabled = false;
    static int _count;

    public static void Err(string tag, string msg)
    {
        if (!Enabled) return;
        if (_count >= 200) return;
        _count++;
        Debug.LogError($"[风之形诊断·{tag}] {msg}");
    }

    public static void ResetCounter()
    {
        _count = 0;
    }
}
