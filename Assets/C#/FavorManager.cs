using UnityEngine;

/// <summary>
/// 好感度管理器（单例）
/// 每个社群拥有独立的好感度槽（0~100），用 PlayerPrefs 永久保存。
/// 
/// 用法：
///   FavorManager.Instance.AddFavor(FactionType.Mushroom, 10);
///   int val = FavorManager.Instance.GetFavor(FactionType.Bat);
/// </summary>
public class FavorManager : MonoBehaviour
{
    public static FavorManager Instance { get; private set; }

    private const string KEY_PREFIX = "Favor_";
    private const int MAX_FAVOR = 100;
    private const int MIN_FAVOR = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>获取指定社群的好感度</summary>
    public int GetFavor(FactionType faction)
    {
        return PlayerPrefs.GetInt(KEY_PREFIX + faction.ToString(), 0);
    }

    /// <summary>增加好感度（自动 clamp 到 0~100）</summary>
    public void AddFavor(FactionType faction, int amount)
    {
        int current = GetFavor(faction);
        int newVal  = Mathf.Clamp(current + amount, MIN_FAVOR, MAX_FAVOR);
        PlayerPrefs.SetInt(KEY_PREFIX + faction.ToString(), newVal);
        PlayerPrefs.Save();
        Debug.Log($"[好感度] {faction} : {current} → {newVal}");
    }

    /// <summary>直接设置好感度</summary>
    public void SetFavor(FactionType faction, int value)
    {
        int newVal = Mathf.Clamp(value, MIN_FAVOR, MAX_FAVOR);
        PlayerPrefs.SetInt(KEY_PREFIX + faction.ToString(), newVal);
        PlayerPrefs.Save();
    }

    /// <summary>删除存档时清除所有好感度</summary>
    public void DeleteAllFavor()
    {
        foreach (FactionType f in System.Enum.GetValues(typeof(FactionType)))
            PlayerPrefs.DeleteKey(KEY_PREFIX + f.ToString());
        PlayerPrefs.Save();
        Debug.Log("[好感度] 所有好感度已清除");
    }

    // ── 测试用 ContextMenu ────────────────────────────────

    [ContextMenu("测试：蘑菇好感度 +10")]
    void Test_Mushroom_Add10() => AddFavor(FactionType.Mushroom, 10);

    [ContextMenu("测试：蘑菇好感度设为100")]
    void Test_Mushroom_Set100() => SetFavor(FactionType.Mushroom, 100);

    [ContextMenu("测试：蘑菇好感度设为0")]
    void Test_Mushroom_Reset() => SetFavor(FactionType.Mushroom, 0);

    [ContextMenu("测试：蝙蝠好感度 +10")]
    void Test_Bat_Add10() => AddFavor(FactionType.Bat, 10);

    [ContextMenu("测试：蝙蝠好感度设为100")]
    void Test_Bat_Set100() => SetFavor(FactionType.Bat, 100);

    [ContextMenu("测试：打印所有好感度")]
    void Test_PrintAll()
    {
        foreach (FactionType f in System.Enum.GetValues(typeof(FactionType)))
            Debug.Log($"[好感度] {f} = {GetFavor(f)}");
    }
}
