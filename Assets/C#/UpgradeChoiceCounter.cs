using UnityEngine;

/// <summary>
/// 累计升级选择计数器（跨对局持久化）。
/// 玩家每在三选一升级面板确认一次选择 → +1；
/// 累计达到 200 次 → 解锁成就装备 8「不可视之手」（解锁自动选取升级功能）。
/// </summary>
public static class UpgradeChoiceCounter
{
    private const string KEY = "TotalUpgradeChoices";
    public const int UNLOCK_THRESHOLD = 200;

    /// <summary>当前累计升级选择次数。</summary>
    public static int Count => PlayerPrefs.GetInt(KEY, 0);

    /// <summary>成就装备8「不可视之手」是否已达成解锁条件（累计选择 200 次升级）。</summary>
    public static bool AutoUnlocked =>
        PlayerPrefs.GetInt("EQ_1_8", 0) == 1 || Count >= UNLOCK_THRESHOLD;

    /// <summary>记录一次升级选择，并在达到阈值时解锁成就装备 8。</summary>
    public static void RecordChoice()
    {
        int c = PlayerPrefs.GetInt(KEY, 0) + 1;
        PlayerPrefs.SetInt(KEY, c);
        PlayerPrefs.Save();

        if (c >= UNLOCK_THRESHOLD && EquipmentSystem.Instance != null &&
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 8))
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 8);
            ToastManager.Show("<color=#B0E0FF>成就达成：不可视之手（累计选择200次升级，自动选取已解锁）</color>");
        }
    }
}
