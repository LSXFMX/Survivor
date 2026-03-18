using UnityEngine;
using System.Collections.Generic;

public class EquipmentSystem : MonoBehaviour
{
    // 单例实例
    private static EquipmentSystem _instance;
    public static EquipmentSystem Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EquipmentSystem>();

                if (_instance == null)
                {
                    GameObject obj = new GameObject("EquipmentSystem");
                    _instance = obj.AddComponent<EquipmentSystem>();
                }
            }
            return _instance;
        }
    }

    // 装备解锁状态字典
    private Dictionary<string, bool> equipmentUnlockStates = new Dictionary<string, bool>();

    // 装备数据缓存
    private Dictionary<string, EquipmentData> equipmentDataCache = new Dictionary<string, EquipmentData>();

    [System.Serializable]
    public class EquipmentData
    {
        public EquipmentType type;
        public int id;
        public string name;
        public string description;
        public string howToGet;
    }

    private bool isInitialized = false;

    // 存储所有装备Key的列表，用于重置
    private List<string> allEquipmentKeys = new List<string>();

    [Header("调试设置")]
    public bool enableDetailedLogs = true;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (enableDetailedLogs)
        {
            Debug.Log("EquipmentSystem 初始化");
        }

        // 加载所有已保存的解锁状态
        LoadAllUnlockStates();

        isInitialized = true;
    }

    // 获取装备的唯一Key
    public string GetEquipmentKey(EquipmentType type, int id)
    {
        return $"EQ_{(int)type}_{id}";
    }

    // 解锁装备
    public void UnlockEquipment(EquipmentType type, int id)
    {
        string key = GetEquipmentKey(type, id);

        // 记录这个装备Key
        if (!allEquipmentKeys.Contains(key))
        {
            allEquipmentKeys.Add(key);
        }

        if (!equipmentUnlockStates.ContainsKey(key))
        {
            equipmentUnlockStates[key] = false;
        }

        if (!equipmentUnlockStates[key])
        {
            equipmentUnlockStates[key] = true;
            SaveUnlockState(key, true);

            if (enableDetailedLogs)
            {
                Debug.Log($"✅ 装备解锁: {type}_{id}");
            }

            // 通知解锁事件
            OnEquipmentUnlocked?.Invoke(type, id);
        }
    }

    // ✅ 修复的：检查装备是否解锁
    public bool IsEquipmentUnlocked(EquipmentType type, int id)
    {
        string key = GetEquipmentKey(type, id);

        if (enableDetailedLogs)
        {
            Debug.Log($"检查装备解锁: {type}_{id} (Key: {key})");
        }

        // 1. 首先检查内存中的状态
        if (equipmentUnlockStates.ContainsKey(key))
        {
            bool unlocked = equipmentUnlockStates[key];

            if (enableDetailedLogs)
            {
                Debug.Log($"  ✅ 从内存获取: {unlocked}");
            }

            return unlocked;
        }

        // 2. ✅ 重要修改：不从PlayerPrefs自动加载！
        // 重置后，我们不应该自动重新加载已删除的状态
        // 而是返回false，表示未解锁

        if (enableDetailedLogs)
        {
            Debug.Log($"  ⚠️ 内存中无记录，返回false（不自动加载）");
        }

        return false;
    }

    // 保存解锁状态
    private void SaveUnlockState(string key, bool isUnlocked)
    {
        PlayerPrefs.SetInt(key, isUnlocked ? 1 : 0);
        PlayerPrefs.Save();

        if (enableDetailedLogs)
        {
            Debug.Log($"保存解锁状态: {key} = {isUnlocked}");
        }
    }

    // 加载解锁状态
    private bool LoadUnlockState(string key)
    {
        bool exists = PlayerPrefs.HasKey(key);
        int value = PlayerPrefs.GetInt(key, 0);
        bool isUnlocked = value == 1;

        if (enableDetailedLogs && exists)
        {
            Debug.Log($"从PlayerPrefs加载: {key} = {value} -> {isUnlocked}");
        }

        return isUnlocked;
    }

    // 加载所有解锁状态
    private void LoadAllUnlockStates()
    {
        // 延迟加载，当需要时再从PlayerPrefs加载
        equipmentUnlockStates.Clear();

        if (enableDetailedLogs)
        {
            Debug.Log("已清空内存中的装备状态");
        }
    }

    // 批量解锁装备
    public void UnlockEquipments(List<(EquipmentType type, int id)> equipments)
    {
        foreach (var equipment in equipments)
        {
            UnlockEquipment(equipment.type, equipment.id);
        }
    }

    // ✅ 最终修复版：重置所有装备解锁状态
    public void ResetAllEquipments()
    {
        Debug.Log("=== 开始重置所有装备 ===");

        // 记录开始时间
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();

        // 1. 检查当前状态
        int beforeUnlockedCount = GetUnlockedEquipments().Count;
        Debug.Log($"删除前已解锁装备: {beforeUnlockedCount} 个");

        // 2. 清空内存中的解锁状态
        int memoryCount = equipmentUnlockStates.Count;
        equipmentUnlockStates.Clear();
        Debug.Log($"清除了内存中的 {memoryCount} 个装备状态");

        // 3. 删除PlayerPrefs中的所有装备Key
        int deletedCount = 0;
        List<string> deletedKeys = new List<string>();

        foreach (string key in allEquipmentKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                // 记录要删除的key
                deletedKeys.Add(key);
                PlayerPrefs.DeleteKey(key);
                deletedCount++;

                if (deletedCount <= 5)  // 只显示前5个
                {
                    Debug.Log($"删除存档键: {key}");
                }
            }
        }

        // 4. ✅ 重要：立即保存
        PlayerPrefs.Save();

        // 5. ✅ 重要：验证删除是否成功
        int verifiedDeleted = 0;
        foreach (string key in deletedKeys)
        {
            if (!PlayerPrefs.HasKey(key))
            {
                verifiedDeleted++;
            }
            else
            {
                Debug.LogError($"❌ 删除失败: {key} 仍然存在！");
            }
        }

        if (deletedCount > 0)
        {
            Debug.Log($"验证删除: {verifiedDeleted}/{deletedCount} 个键已确认删除");
        }

        // 6. 清空数据缓存
        equipmentDataCache.Clear();

        // 7. ✅ 重要：确保内存状态为空
        // 重新检查并清空
        equipmentUnlockStates.Clear();
        Debug.Log($"最终内存状态数量: {equipmentUnlockStates.Count}");

        // 8. 检查重置后的状态
        int afterUnlockedCount = GetUnlockedEquipments().Count;

        // 9. 记录时间
        stopwatch.Stop();

        // 10. 触发重置完成事件
        OnAllEquipmentsReset?.Invoke();

        // 11. 最终报告
        Debug.Log($"✅ 装备重置完成");
        Debug.Log($"   耗时: {stopwatch.ElapsedMilliseconds}ms");
        Debug.Log($"   删除前: {beforeUnlockedCount} 个已解锁装备");
        Debug.Log($"   删除后: {afterUnlockedCount} 个已解锁装备");
        Debug.Log($"   删除了: {deletedCount} 个PlayerPrefs键");
        Debug.Log($"   总装备数: {allEquipmentKeys.Count} 个");

        if (afterUnlockedCount > 0)
        {
            Debug.LogError($"❌ 警告：删除后仍有 {afterUnlockedCount} 个已解锁装备！");
            Debug.LogError("可能原因：IsEquipmentUnlocked方法有缓存问题");

            // 显示哪些装备仍然显示为已解锁
            var stillUnlocked = GetUnlockedEquipments();
            foreach (var equip in stillUnlocked)
            {
                Debug.LogError($"  仍然解锁: {equip.type}_{equip.id}");
            }
        }
        else
        {
            Debug.Log($"✅ 完美！所有装备都已正确重置");
        }

        Debug.Log("=== 重置结束 ===");
    }

    // 重置指定类型的装备
    public void ResetEquipmentByType(EquipmentType type)
    {
        Debug.Log($"开始重置 {type} 类型的所有装备");

        List<string> keysToRemove = new List<string>();

        foreach (string key in allEquipmentKeys)
        {
            if (key.StartsWith($"EQ_{(int)type}_"))
            {
                keysToRemove.Add(key);
            }
        }

        int deletedCount = 0;
        foreach (string key in keysToRemove)
        {
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                deletedCount++;
            }

            if (equipmentUnlockStates.ContainsKey(key))
            {
                equipmentUnlockStates.Remove(key);
            }
        }

        PlayerPrefs.Save();
        Debug.Log($"✅ 重置 {type} 类型的所有装备，删除了 {deletedCount} 个");
    }

    // 获取已解锁的装备列表
    public List<(EquipmentType type, int id)> GetUnlockedEquipments()
    {
        List<(EquipmentType type, int id)> unlocked = new List<(EquipmentType type, int id)>();

        foreach (var kvp in equipmentUnlockStates)
        {
            if (kvp.Value)  // 已解锁
            {
                // 解析Key: "EQ_type_id"
                string[] parts = kvp.Key.Split('_');
                if (parts.Length == 3 &&
                    parts[0] == "EQ" &&
                    int.TryParse(parts[1], out int typeInt) &&
                    int.TryParse(parts[2], out int id))
                {
                    unlocked.Add(((EquipmentType)typeInt, id));
                }
            }
        }

        return unlocked;
    }

    // 获取指定类型已解锁的装备
    public List<int> GetUnlockedEquipmentsByType(EquipmentType type)
    {
        List<int> unlockedIds = new List<int>();

        foreach (var kvp in equipmentUnlockStates)
        {
            if (kvp.Value)  // 已解锁
            {
                string key = kvp.Key;
                // 解析Key: "EQ_type_id"
                string[] parts = key.Split('_');
                if (parts.Length == 3 &&
                    parts[0] == "EQ" &&
                    int.TryParse(parts[1], out int typeInt) &&
                    int.TryParse(parts[2], out int id))
                {
                    if ((EquipmentType)typeInt == type)
                    {
                        unlockedIds.Add(id);
                    }
                }
            }
        }

        return unlockedIds;
    }

    // 事件：当装备解锁时
    public delegate void EquipmentUnlockedHandler(EquipmentType type, int id);
    public event EquipmentUnlockedHandler OnEquipmentUnlocked;

    // ✅ 新增：当所有装备重置时
    public delegate void AllEquipmentsResetHandler();
    public event AllEquipmentsResetHandler OnAllEquipmentsReset;

    // 注册装备数据
    public void RegisterEquipment(EquipmentType type, int id, string name, string description, string howToGet)
    {
        string key = GetEquipmentKey(type, id);

        // 记录这个装备Key
        if (!allEquipmentKeys.Contains(key))
        {
            allEquipmentKeys.Add(key);
        }

        EquipmentData data = new EquipmentData
        {
            type = type,
            id = id,
            name = name,
            description = description,
            howToGet = howToGet
        };

        equipmentDataCache[key] = data;

        if (enableDetailedLogs)
        {
            Debug.Log($"注册装备: {type}_{id} - {name}");
        }
    }

    // 获取装备数据
    public EquipmentData GetEquipmentData(EquipmentType type, int id)
    {
        string key = GetEquipmentKey(type, id);

        if (equipmentDataCache.ContainsKey(key))
        {
            return equipmentDataCache[key];
        }

        return null;
    }

    // 手动添加装备Key
    public void AddEquipmentKey(EquipmentType type, int id)
    {
        string key = GetEquipmentKey(type, id);
        if (!allEquipmentKeys.Contains(key))
        {
            allEquipmentKeys.Add(key);
        }
    }

    // 手动添加多个装备Key
    public void AddEquipmentKeys(List<(EquipmentType type, int id)> equipments)
    {
        foreach (var equip in equipments)
        {
            AddEquipmentKey(equip.type, equip.id);
        }
    }

    // ✅ 新增：强制保存所有数据
    public void ForceSaveAll()
    {
        foreach (var kvp in equipmentUnlockStates)
        {
            PlayerPrefs.SetInt(kvp.Key, kvp.Value ? 1 : 0);
        }
        PlayerPrefs.Save();
        Debug.Log($"强制保存了 {equipmentUnlockStates.Count} 个装备状态");
    }

    // ✅ 新增：详细检查状态
    [ContextMenu("详细检查装备状态")]
    public void DetailedCheckEquipmentStatus()
    {
        Debug.Log("=== 详细装备状态检查 ===");

        // 检查单例
        Debug.Log($"EquipmentSystem单例: {_instance != null}");
        Debug.Log($"EquipmentSystem.Instance: {Instance != null}");
        Debug.Log($"当前实例: {this.name}");

        // 检查内存状态
        var unlocked = GetUnlockedEquipments();
        Debug.Log($"内存中已解锁装备: {unlocked.Count} 个");

        if (unlocked.Count > 0)
        {
            Debug.Log("已解锁装备列表:");
            for (int i = 0; i < Mathf.Min(unlocked.Count, 5); i++)
            {
                Debug.Log($"  {unlocked[i].type}_{unlocked[i].id}");
            }
        }

        // 检查PlayerPrefs
        int prefKeysCount = 0;
        foreach (string key in allEquipmentKeys)
        {
            if (PlayerPrefs.HasKey(key))
            {
                prefKeysCount++;
                if (prefKeysCount <= 5)
                {
                    int value = PlayerPrefs.GetInt(key, -1);
                    Debug.Log($"PlayerPrefs键: {key} = {value}");
                }
            }
        }

        Debug.Log($"PlayerPrefs中的装备键: {prefKeysCount} 个");
        Debug.Log($"总装备记录: {allEquipmentKeys.Count} 个");

        // ✅ 重要：检查特定装备的状态
        Debug.Log("检查常见装备状态:");
        for (int i = 0; i < 3; i++)
        {
            string key = GetEquipmentKey(EquipmentType.ClearEquipment, i);
            bool hasKey = PlayerPrefs.HasKey(key);
            int value = hasKey ? PlayerPrefs.GetInt(key, 0) : 0;

            // 检查内存状态
            bool memoryState = false;
            if (equipmentUnlockStates.ContainsKey(key))
            {
                memoryState = equipmentUnlockStates[key];
            }

            Debug.Log($"  {EquipmentType.ClearEquipment}_{i}:");
            Debug.Log($"    PlayerPrefs: 有键={hasKey}, 值={value}");
            Debug.Log($"    内存状态: {memoryState}");
            Debug.Log($"    IsEquipmentUnlocked返回: {IsEquipmentUnlocked(EquipmentType.ClearEquipment, i)}");
        }

        Debug.Log("=== 检查结束 ===");
    }

    // 调试方法
    [ContextMenu("打印所有解锁状态")]
    public void PrintAllUnlockStates()
    {
        Debug.Log("=== 所有装备解锁状态 ===");
        foreach (var kvp in equipmentUnlockStates)
        {
            Debug.Log($"{kvp.Key}: {kvp.Value}");
        }
    }

    [ContextMenu("打印所有装备Key")]
    public void PrintAllEquipmentKeys()
    {
        Debug.Log($"=== 所有装备Key ({allEquipmentKeys.Count}个) ===");
        foreach (string key in allEquipmentKeys)
        {
            Debug.Log(key);
        }
    }

    [ContextMenu("清除所有装备数据")]
    public void ClearAllEquipmentData()
    {
        ResetAllEquipments();
        equipmentDataCache.Clear();
        allEquipmentKeys.Clear();
        Debug.Log("✅ 所有装备数据已清除");
    }

    [ContextMenu("测试解锁几个装备")]
    public void TestUnlockSomeEquipments()
    {
        Debug.Log("测试解锁装备...");
        UnlockEquipment(EquipmentType.ClearEquipment, 0);
        UnlockEquipment(EquipmentType.ClearEquipment, 1);
        UnlockEquipment(EquipmentType.AchievementEquipment, 0);
        Debug.Log("✅ 测试解锁完成");
    }

    [ContextMenu("测试重置装备")]
    public void TestResetEquipments()
    {
        Debug.Log("测试重置装备...");
        ResetAllEquipments();
    }

    [ContextMenu("立即测试装备0状态")]
    public void TestEquipment0Status()
    {
        bool unlocked = IsEquipmentUnlocked(EquipmentType.ClearEquipment, 0);
        Debug.Log($"装备0状态: {unlocked}");

        // 手动检查PlayerPrefs
        string key = GetEquipmentKey(EquipmentType.ClearEquipment, 0);
        bool hasKey = PlayerPrefs.HasKey(key);
        int value = hasKey ? PlayerPrefs.GetInt(key, 0) : 0;

        Debug.Log($"PlayerPrefs检查:");
        Debug.Log($"  键: {key}");
        Debug.Log($"  是否存在: {hasKey}");
        Debug.Log($"  值: {value}");
    }

    [ContextMenu("手动设置装备0为未解锁")]
    public void ManualSetEquipment0Locked()
    {
        string key = GetEquipmentKey(EquipmentType.ClearEquipment, 0);

        // 1. 从内存中移除
        if (equipmentUnlockStates.ContainsKey(key))
        {
            equipmentUnlockStates.Remove(key);
        }

        // 2. 从PlayerPrefs中删除
        if (PlayerPrefs.HasKey(key))
        {
            PlayerPrefs.DeleteKey(key);
        }

        PlayerPrefs.Save();

        Debug.Log($"  PlayerPrefs: {PlayerPrefs.HasKey(key)}");
    }
}