using UnityEngine;

/// <summary>
/// 测试模式开关（DontDestroyOnLoad 单例 + PlayerPrefs 持久化）。
/// 主菜单上有一个"测试模式"按钮，点开后切换 Enabled，进入战斗时
/// EquipmentInitializer.Start 末尾会把玩家 healthmax/health/atk 全部拉到 99999。
///
/// 设计要点：
///  - 持久化用 PlayerPrefs（key = "TestMode"），跨次启动保留玩家选择；
///    与 DifficultyManager（仅内存）不一样——测试模式更应"记住"。
///  - 不需要在 SampleScene 里手动挂这个组件——主菜单 title 脚本启动时
///    会调用 EnsureInstance() 兜底创建。这样即便没有把组件拖进场景，
///    单例也能保证存在；少改场景文件，便于回滚。
///  - 不接管难度逻辑：玩家数值在 EquipmentInitializer 应用所有装备
///    加成之后再被强制拉到 99999，敌人侧的难度倍率不动，依然按当前
///    难度生成；这样"测试模式"更像 god-mode 而不是"无脑卡难度"。
/// </summary>
public class TestModeManager : MonoBehaviour
{
    public static TestModeManager Instance { get; private set; }

    private const string PREF_KEY = "TestMode";

    /// <summary>测试模式是否启用（true = 玩家进入战斗时 hp/atk = 99999）。</summary>
    public bool Enabled { get; private set; }

    /// <summary>由代码兜底创建：主菜单启动时调用，保证 Instance 一定存在。</summary>
    public static TestModeManager EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[TestModeManager]");
        Instance = go.AddComponent<TestModeManager>();
        return Instance;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // 从 PlayerPrefs 恢复上次选择
        Enabled = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        Debug.Log($"[TestMode] 启动加载持久化状态：Enabled={Enabled}");
    }

    /// <summary>切换测试模式开关，并立即写回 PlayerPrefs。</summary>
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        PlayerPrefs.SetInt(PREF_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"[TestMode] SetEnabled = {enabled}（已写入 PlayerPrefs）");
    }

    /// <summary>反转开关。</summary>
    public void Toggle() => SetEnabled(!Enabled);
}
