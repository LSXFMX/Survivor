using UnityEngine;

/// <summary>
/// 游戏时长管理器（单例）
/// 累计记录所有局的游戏时间（分钟），用 PlayerPrefs 永久保存。
/// 每分钟自动保存一次。
/// </summary>
public class PlayTimeManager : MonoBehaviour
{
    public static PlayTimeManager Instance { get; private set; }

    private const string KEY = "TotalPlayMinutes";

    private float _secondAccum = 0f; // 本局累计秒数（未满1分钟的部分）

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        _secondAccum += Time.unscaledDeltaTime;

        // 每满60秒写一次 PlayerPrefs
        if (_secondAccum >= 60f)
        {
            _secondAccum -= 60f;
            int total = PlayerPrefs.GetInt(KEY, 0) + 1;
            PlayerPrefs.SetInt(KEY, total);
            PlayerPrefs.Save();
            Debug.Log($"[游戏时长] 累计 {total} 分钟");

            // 通知装备4解锁检查
            if (total >= 30 && EquipmentSystem.Instance != null)
            {
                bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(
                    EquipmentType.AchievementEquipment, 4);
                EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 4);
                if (!alreadyUnlocked)
                    ToastManager.Show("成就装备4「沙漏」已解锁！");
            }
        }
    }

    /// <summary>获取累计游戏时长（分钟）</summary>
    public int GetTotalMinutes() => PlayerPrefs.GetInt(KEY, 0);

    /// <summary>删除存档时是否清除时长（通常不清除，时长是成就性数据）</summary>
    public void ResetPlayTime()
    {
        PlayerPrefs.DeleteKey(KEY);
        PlayerPrefs.Save();
    }

    [ContextMenu("测试：设置时长为30分钟")]
    void Test_Set30() { PlayerPrefs.SetInt(KEY, 30); PlayerPrefs.Save(); }

    [ContextMenu("测试：打印当前时长")]
    void Test_Print() => Debug.Log($"[游戏时长] 累计 {GetTotalMinutes()} 分钟");
}
