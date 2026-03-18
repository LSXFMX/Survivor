using UnityEngine;
using UnityEngine.UI;
using System;

public class EquipmentIcon : MonoBehaviour
{
    [Header("装备信息")]
    public EquipmentType equipmentType = EquipmentType.ClearEquipment;
    public int equipmentId = 0;  // 从0开始
    public string equipmentName = "装备名称";
    [TextArea(2, 3)]
    public string description = "装备描述";
    [TextArea(1, 2)]
    public string howToGet = "获得方法";

    [Header("颜色设置")]
    public Color unlockedColor = Color.white;
    public Color lockedColor = Color.gray;

    [Header("组件引用")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    [Header("调试设置")]
    public bool enableDebugLogs = true;

    // 点击回调
    public Action<EquipmentType, int, EquipmentIcon> onClickCallback;

    private bool isInitialized = false;

    // 保存EquipmentSystem引用
    private EquipmentSystem equipmentSystem;

    private void Start()
    {
        Initialize();
    }

    private void OnEnable()
    {
        // 每次激活时重新检查状态
        UpdateDisplay();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (enableDebugLogs)
        {
            Debug.Log($"EquipmentIcon初始化: {equipmentType}_{equipmentId} - {equipmentName}");
        }

        // 获取组件
        if (button == null)
        {
            button = GetComponent<Button>();
            if (button == null)
            {
                Debug.LogError($"EquipmentIcon {name}: 找不到Button组件");
            }
        }

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>();
            if (iconImage == null)
            {
                Debug.LogError($"EquipmentIcon {name}: 找不到Image组件");
            }
        }

        // 设置点击事件
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            // 如果还是没有button，尝试添加一个
            button = gameObject.AddComponent<Button>();
            button.onClick.AddListener(OnButtonClicked);
        }

        // 获取EquipmentSystem引用
        equipmentSystem = EquipmentSystem.Instance;
        if (equipmentSystem == null && enableDebugLogs)
        {
            Debug.LogWarning($"EquipmentIcon {name}: 初始化时EquipmentSystem为null");
        }

        // 初始更新显示
        UpdateDisplay();

        isInitialized = true;
    }

    private void OnButtonClicked()
    {
        if (enableDebugLogs)
        {
            Debug.Log($"点击装备图标: {equipmentType}_{equipmentId} - {equipmentName}");
        }

        onClickCallback?.Invoke(equipmentType, equipmentId, this);
    }

    // 更新显示状态
    public void UpdateDisplay()
    {
        if (iconImage == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"EquipmentIcon {name}: iconImage为空");
            }
            return;
        }

        // 检查是否解锁
        bool isUnlocked = IsUnlocked();

        // 设置颜色
        Color targetColor = isUnlocked ? unlockedColor : lockedColor;

        // 如果颜色与当前不同才更新
        if (iconImage.color != targetColor)
        {
            iconImage.color = targetColor;

            if (enableDebugLogs)
            {
                Debug.Log($"EquipmentIcon更新颜色: {equipmentType}_{equipmentId} = {isUnlocked}, 颜色: {targetColor}");
            }
        }

        // 更新Alpha通道
        iconImage.color = new Color(targetColor.r, targetColor.g, targetColor.b, 1f);
    }

    // 检查是否解锁
    private bool IsUnlocked()
    {
        // 方法1: 从EquipmentSystem检查
        if (equipmentSystem != null)
        {
            return equipmentSystem.IsEquipmentUnlocked(equipmentType, equipmentId);
        }

        // 方法2: 重新获取EquipmentSystem引用
        equipmentSystem = EquipmentSystem.Instance;
        if (equipmentSystem != null)
        {
            return equipmentSystem.IsEquipmentUnlocked(equipmentType, equipmentId);
        }

        // 方法3: 直接查找
        EquipmentSystem system = FindObjectOfType<EquipmentSystem>();
        if (system != null)
        {
            return system.IsEquipmentUnlocked(equipmentType, equipmentId);
        }

        if (enableDebugLogs)
        {
            Debug.LogWarning($"EquipmentIcon {name}: 无法找到EquipmentSystem");
        }

        return false;  // 默认未解锁
    }

    // 强制重新初始化
    [ContextMenu("重新初始化")]
    public void Reinitialize()
    {
        isInitialized = false;
        Initialize();
    }

    [ContextMenu("手动更新显示")]
    public void ManualUpdateDisplay()
    {
        UpdateDisplay();
    }

    [ContextMenu("检查解锁状态")]
    public void DebugCheckUnlockStatus()
    {
        bool isUnlocked = IsUnlocked();
        Debug.Log($"装备 {equipmentType}_{equipmentId} 解锁状态: {isUnlocked}");

        if (equipmentSystem != null)
        {
            Debug.Log($"EquipmentSystem引用有效: {equipmentSystem != null}");
        }
        else
        {
            Debug.LogWarning($"EquipmentSystem引用为空");
        }
    }

    [ContextMenu("测试解锁装备")]
    public void TestUnlockThisEquipment()
    {
        if (EquipmentSystem.Instance != null)
        {
            EquipmentSystem.Instance.UnlockEquipment(equipmentType, equipmentId);
            Debug.Log($"测试解锁装备: {equipmentType}_{equipmentId}");
            UpdateDisplay();
        }
        else
        {
            Debug.LogError("EquipmentSystem未找到");
        }
    }

    [ContextMenu("测试重置装备")]
    public void TestResetThisEquipment()
    {
        if (EquipmentSystem.Instance != null)
        {
            // 手动重置这个装备
            string key = $"EQ_{(int)equipmentType}_{equipmentId}";
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();

            // 清除内存中的状态
            EquipmentSystem.Instance.GetType().GetField("equipmentUnlockStates",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(EquipmentSystem.Instance, new System.Collections.Generic.Dictionary<string, bool>());

            Debug.Log($"测试重置装备: {equipmentType}_{equipmentId}");
            UpdateDisplay();
        }
    }

    // 设置装备信息
    public void SetEquipmentInfo(EquipmentType type, int id, string name, string desc, string howToGetText)
    {
        equipmentType = type;
        equipmentId = id;
        equipmentName = name;
        description = desc;
        howToGet = howToGetText;

        if (enableDebugLogs)
        {
            Debug.Log($"设置装备信息: {type}_{id} - {name}");
        }
    }

    // 设置颜色
    public void SetColors(Color unlocked, Color locked)
    {
        unlockedColor = unlocked;
        lockedColor = locked;
        UpdateDisplay();
    }
}