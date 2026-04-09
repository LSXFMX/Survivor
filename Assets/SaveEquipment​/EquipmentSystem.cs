using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 装备解锁系统（单例，DontDestroyOnLoad）
/// 使用 PlayerPrefs 持久化，key 格式：EQ_{typeInt}_{id}
/// </summary>
public class EquipmentSystem : MonoBehaviour
{
    public static EquipmentSystem Instance { get; private set; }

    // 内存缓存，避免每次都读 PlayerPrefs
    private readonly Dictionary<string, bool> _cache = new Dictionary<string, bool>();

    // 事件
    public delegate void EquipmentUnlockedHandler(EquipmentType type, int id);
    public event EquipmentUnlockedHandler OnEquipmentUnlocked;

    public delegate void AllEquipmentsResetHandler();
    public event AllEquipmentsResetHandler OnAllEquipmentsReset;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── 公开 API ──────────────────────────────────────────

    /// <summary>解锁装备。已解锁则忽略。</summary>
    public void UnlockEquipment(EquipmentType type, int id)
    {
        string key = Key(type, id);
        if (_cache.TryGetValue(key, out bool v) && v) return; // 已解锁

        _cache[key] = true;
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();

        Debug.Log($"[装备] 解锁 {type}_{id}");
        OnEquipmentUnlocked?.Invoke(type, id);
    }

    /// <summary>检查装备是否已解锁（先查内存缓存，再查 PlayerPrefs）</summary>
    public bool IsEquipmentUnlocked(EquipmentType type, int id)
    {
        string key = Key(type, id);
        if (_cache.TryGetValue(key, out bool v)) return v;

        bool saved = PlayerPrefs.GetInt(key, 0) == 1;
        _cache[key] = saved;
        return saved;
    }

    /// <summary>重置所有装备（清内存 + 扫描删除所有 EQ_ 开头的 PlayerPrefs key）</summary>
    public void ResetAllEquipments()
    {
        _cache.Clear();

        // 枚举所有类型 × id 0~29，覆盖所有可能的 key
        foreach (EquipmentType type in System.Enum.GetValues(typeof(EquipmentType)))
        {
            for (int id = 0; id < 30; id++)
            {
                string key = Key(type, id);
                if (PlayerPrefs.HasKey(key))
                    PlayerPrefs.DeleteKey(key);
            }
        }
        PlayerPrefs.Save();

        Debug.Log("[装备] 所有装备已重置");
        OnAllEquipmentsReset?.Invoke();
    }

    /// <summary>获取当前已解锁的装备列表（仅内存缓存中的）</summary>
    public List<(EquipmentType type, int id)> GetUnlockedEquipments()
    {
        var result = new List<(EquipmentType type, int id)>();
        foreach (var kvp in _cache)
        {
            if (!kvp.Value) continue;
            var parts = kvp.Key.Split('_');
            if (parts.Length == 3 &&
                int.TryParse(parts[1], out int t) &&
                int.TryParse(parts[2], out int i))
            {
                result.Add(((EquipmentType)t, i));
            }
        }
        return result;
    }

    // ── 内部 ──────────────────────────────────────────────

    private static string Key(EquipmentType type, int id) => $"EQ_{(int)type}_{id}";
}
