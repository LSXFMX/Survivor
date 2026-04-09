using UnityEngine;

/// <summary>
/// 难度通关记录管理器（单例）
/// 使用 PlayerPrefs 永久保存各难度通关次数和积分。
/// </summary>
public class ClearRecordManager : MonoBehaviour
{
    public static ClearRecordManager Instance { get; private set; }

    private const string KEY_PREFIX = "ClearCount_";
    private const string KEY_POINTS = "ClearEquipmentPoints";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>记录当前难度通关一次，并触发对应奖励</summary>
    public void RecordClear()
    {
        if (DifficultyManager.Instance == null) return;
        string label = DifficultyManager.Instance.Current.label;
        string key = KEY_PREFIX + label;
        int count = PlayerPrefs.GetInt(key, 0) + 1;
        PlayerPrefs.SetInt(key, count);
        PlayerPrefs.Save();
        Debug.Log($"[通关记录] {label} 通关次数：{count}");

        // 通关获得【源】（难度数字=获得数量）
        GachaManager.Instance?.GrantYuanFromClear(label);

        switch (label)
        {
            case "N2": GrantN2Reward(); break;
            case "N3": GrantN3Reward(); break;
            case "N4": GrantN4Reward(); break;
            case "N5": GrantN5Reward(); break;
            case "N6": GrantN6Reward(); break;
            case "N7": GrantN7Reward(); break;
            case "N8": GrantN8Reward(); break;
            // N1 无通关装备奖励
        }
    }

    private void GrantN2Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(0, 3); // id 0/1/2
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 20;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N2通关奖励：获得20积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N2通关奖励：解锁通关装备{roll}号！");
        }
    }

    private void GrantN3Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(3, 6); // id 3/4/5
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 40;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N3通关奖励：获得40积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N3通关奖励：解锁通关装备{roll}号！");
        }
    }

    private void GrantN4Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(6, 9); // id 6/7/8
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 60;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N4通关奖励：获得60积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N4通关奖励：解锁通关装备{roll}号！");
        }
    }

    private void GrantN5Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(9, 12); // id 9/10/11
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 80;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N5通关奖励：获得80积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N5通关奖励：解锁通关装备{roll}号！");
        }
    }

    private void GrantN6Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(12, 15); // id 12/13/14
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 100;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N6通关奖励：获得100积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N6通关奖励：解锁通关装备{roll}号！");
        }
    }
    private void GrantN7Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(15, 18); 
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 120;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N7通关奖励：获得120积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N7通关奖励：解锁通关装备{roll}号！");
        }
    }
    private void GrantN8Reward()
    {
        if (EquipmentSystem.Instance == null) return;
        int roll = Random.Range(18, 21); // id 12/13/14
        bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, roll);
        if (alreadyUnlocked)
        {
            int pts = PlayerPrefs.GetInt(KEY_POINTS, 0) + 140;
            PlayerPrefs.SetInt(KEY_POINTS, pts);
            PlayerPrefs.Save();
            ToastManager.Show($"N8通关奖励：获得140积分（当前{pts}）");
        }
        else
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.ClearEquipment, roll);
            ToastManager.Show($"N8通关奖励：解锁通关装备{roll}号！");
        }
    }

    /// <summary>获取通关装备积分</summary>
    public int GetEquipmentPoints() => PlayerPrefs.GetInt(KEY_POINTS, 0);

    /// <summary>测试用：直接设置积分为10000</summary>
    [ContextMenu("测试：设置积分为10000")]
    public void TestSetPoints100()
    {
        PlayerPrefs.SetInt(KEY_POINTS, 10000);
        PlayerPrefs.Save();
        Debug.Log("[测试] 积分已设置为10000");
    }

    /// <summary>获取指定难度通关次数</summary>
    public int GetClearCount(string label) => PlayerPrefs.GetInt(KEY_PREFIX + label, 0);

    /// <summary>删除存档时清除所有通关记录和积分</summary>
    public void DeleteAllRecords()
    {
        string[] labels = { "N1", "N2", "N3", "N4", "N5" ,"N6" ,"N7" ,"N8"};
        foreach (var label in labels)
            PlayerPrefs.DeleteKey(KEY_PREFIX + label);
        PlayerPrefs.DeleteKey(KEY_POINTS);
        PlayerPrefs.Save();
        Debug.Log("[通关记录] 所有通关记录及积分已清除");
    }
}
