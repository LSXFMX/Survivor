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

    // ── 测试用 ContextMenu（右键组件标题 → 选择测试项）────────────

    [ContextMenu("测试：蘑菇好感度 +10")]
    void Test_Mushroom_Add10() { AddFavor(FactionType.Mushroom, 10); Print(FactionType.Mushroom); }

    [ContextMenu("测试：蘑菇好感度设为100")]
    void Test_Mushroom_Set100() { SetFavor(FactionType.Mushroom, 100); Print(FactionType.Mushroom); }

    [ContextMenu("测试：蘑菇好感度设为0")]
    void Test_Mushroom_Reset() { SetFavor(FactionType.Mushroom, 0); Print(FactionType.Mushroom); }

    [ContextMenu("测试：蝙蝠好感度 +10")]
    void Test_Bat_Add10() { AddFavor(FactionType.Bat, 10); Print(FactionType.Bat); }

    [ContextMenu("测试：蝙蝠好感度设为100")]
    void Test_Bat_Set100() { SetFavor(FactionType.Bat, 100); Print(FactionType.Bat); }

    [ContextMenu("测试：蝙蝠好感度设为0")]
    void Test_Bat_Reset() { SetFavor(FactionType.Bat, 0); Print(FactionType.Bat); }

    [ContextMenu("测试：狼人好感度 +10")]
    void Test_Wolf_Add10() { AddFavor(FactionType.Wolf, 10); Print(FactionType.Wolf); }

    [ContextMenu("测试：狼人好感度设为100")]
    void Test_Wolf_Set100() { SetFavor(FactionType.Wolf, 100); Print(FactionType.Wolf); }

    [ContextMenu("测试：狼人好感度设为0")]
    void Test_Wolf_Reset() { SetFavor(FactionType.Wolf, 0); Print(FactionType.Wolf); }

    [ContextMenu("测试：史莱姆好感度 +10")]
    void Test_Slime_Add10() { AddFavor(FactionType.Slime, 10); Print(FactionType.Slime); }

    [ContextMenu("测试：史莱姆好感度设为100")]
    void Test_Slime_Set100() { SetFavor(FactionType.Slime, 100); Print(FactionType.Slime); }

    [ContextMenu("测试：史莱姆好感度设为0")]
    void Test_Slime_Reset() { SetFavor(FactionType.Slime, 0); Print(FactionType.Slime); }

    [ContextMenu("测试：打印所有好感度")]
    void Test_PrintAll()
    {
        foreach (FactionType f in System.Enum.GetValues(typeof(FactionType)))
            Debug.Log($"[好感度] {f} = {GetFavor(f)}");
    }

    private void Print(FactionType f) => Debug.Log($"[好感度-测试] {f} = {GetFavor(f)}");
}
