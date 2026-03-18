using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

// 装备类型枚举
public enum EquipmentType
{
    ClearEquipment,     // 通关装备
    AchievementEquipment, // 成就装备
    FavorEquipment,     // 好感度装备
    GachaEquipment,     // 抽卡装备
    InheritEquipment    // 继承装备
}

public class ArchiveManager : MonoBehaviour
{
    [Header("装备容器 - 存放不同类型的装备")]
    public GameObject clearEquipmentContainer;       // 通关装备容器
    public GameObject achievementEquipmentContainer; // 成就装备容器
    public GameObject favorEquipmentContainer;       // 好感度装备容器
    public GameObject gachaEquipmentContainer;       // 抽卡装备容器
    public GameObject inheritEquipmentContainer;     // 继承装备容器

    [Header("UI显示引用")]
    public TextMeshProUGUI nameText;         // 名称显示
    public TextMeshProUGUI descriptionText;  // 描述显示
    public TextMeshProUGUI howToGetText;     // 获得方法显示
    public TextMeshProUGUI typeText;         // 类型显示
    public TextMeshProUGUI idText;           // 编号显示

    [Header("未解锁时显示")]
    [TextArea(1, 2)]
    public string lockedNamePrefix = "未解锁装备";
    [TextArea(2, 3)]
    public string lockedDescription = "？？？";

    [Header("类型切换按钮")]
    public Button clearTabButton;             // 通关装备标签按钮
    public Button achievementTabButton;       // 成就装备标签按钮
    public Button favorTabButton;             // 好感度装备标签按钮
    public Button gachaTabButton;             // 抽卡装备标签按钮
    public Button inheritTabButton;           // 继承装备标签按钮

    [Header("按钮选中状态")]
    public Color selectedTabColor = Color.blue;      // 选中时的颜色
    public Color normalTabColor = Color.white;        // 未选中时的颜色

    [Header("清空时显示")]
    [TextArea(1, 2)]
    public string emptyTypeText = "【请选择装备】";
    [TextArea(1, 2)]
    public string emptyNameText = "点击左侧装备查看详情";
    [TextArea(2, 3)]
    public string emptyDescriptionText = "选择装备后，这里会显示装备的详细信息";
    [TextArea(1, 2)]
    public string emptyHowToGetText = "这里会显示装备的获得方式";
    [TextArea(1, 2)]
    public string emptyIdText = "编号: ---";

    [Header("删除存档")]
    public DeleteArchiveConfirm deleteArchiveConfirm;  // 删除存档确认面板

    // 装备容器字典
    private Dictionary<EquipmentType, GameObject> equipmentContainers = new Dictionary<EquipmentType, GameObject>();

    // 当前选中的装备类型
    private EquipmentType currentSelectedType = EquipmentType.ClearEquipment;

    // 所有类型切换按钮
    private Dictionary<EquipmentType, Button> tabButtons = new Dictionary<EquipmentType, Button>();

    void Start()
    {
        // 初始化装备容器字典
        InitializeContainers();

        // 设置所有EquipmentIcon的点击回调
        SetupEquipmentIcons();

        // 初始化类型切换按钮
        InitializeTabButtons();

        // 监听EquipmentSystem重置事件
        SetupEquipmentSystemListeners();

        // 设置删除存档确认面板
        SetupDeleteArchiveConfirm();

        // 默认显示通关装备
        ShowEquipmentContainer(EquipmentType.ClearEquipment);

        // 启动时清空显示
        ClearAllDisplay();
    }

    // 初始化装备容器字典
    private void InitializeContainers()
    {
        equipmentContainers.Clear();

        if (clearEquipmentContainer != null)
            equipmentContainers.Add(EquipmentType.ClearEquipment, clearEquipmentContainer);

        if (achievementEquipmentContainer != null)
            equipmentContainers.Add(EquipmentType.AchievementEquipment, achievementEquipmentContainer);

        if (favorEquipmentContainer != null)
            equipmentContainers.Add(EquipmentType.FavorEquipment, favorEquipmentContainer);

        if (gachaEquipmentContainer != null)
            equipmentContainers.Add(EquipmentType.GachaEquipment, gachaEquipmentContainer);

        if (inheritEquipmentContainer != null)
            equipmentContainers.Add(EquipmentType.InheritEquipment, inheritEquipmentContainer);

        // 初始时全部隐藏
        HideAllContainers();
    }

    // 设置所有EquipmentIcon的点击回调
    private void SetupEquipmentIcons()
    {
        foreach (var container in equipmentContainers.Values)
        {
            if (container == null) continue;

            EquipmentIcon[] icons = container.GetComponentsInChildren<EquipmentIcon>(true);
            foreach (var icon in icons)
            {
                // 设置点击回调
                icon.onClickCallback = OnEquipmentClicked;
            }
        }
    }

    // 隐藏所有装备容器
    private void HideAllContainers()
    {
        foreach (var container in equipmentContainers.Values)
        {
            if (container != null)
            {
                container.SetActive(false);
            }
        }
    }

    // 初始化类型切换按钮
    private void InitializeTabButtons()
    {
        tabButtons.Clear();

        if (clearTabButton != null)
        {
            tabButtons[EquipmentType.ClearEquipment] = clearTabButton;
            clearTabButton.onClick.AddListener(() => OnTabButtonClick(EquipmentType.ClearEquipment));
        }

        if (achievementTabButton != null)
        {
            tabButtons[EquipmentType.AchievementEquipment] = achievementTabButton;
            achievementTabButton.onClick.AddListener(() => OnTabButtonClick(EquipmentType.AchievementEquipment));
        }

        if (favorTabButton != null)
        {
            tabButtons[EquipmentType.FavorEquipment] = favorTabButton;
            favorTabButton.onClick.AddListener(() => OnTabButtonClick(EquipmentType.FavorEquipment));
        }

        if (gachaTabButton != null)
        {
            tabButtons[EquipmentType.GachaEquipment] = gachaTabButton;
            gachaTabButton.onClick.AddListener(() => OnTabButtonClick(EquipmentType.GachaEquipment));
        }

        if (inheritTabButton != null)
        {
            tabButtons[EquipmentType.InheritEquipment] = inheritTabButton;
            inheritTabButton.onClick.AddListener(() => OnTabButtonClick(EquipmentType.InheritEquipment));
        }

        UpdateTabButtonsAppearance();
    }

    // 设置EquipmentSystem事件监听
    private void SetupEquipmentSystemListeners()
    {
        if (EquipmentSystem.Instance != null)
        {
            // 监听装备重置事件
            EquipmentSystem.Instance.OnAllEquipmentsReset += OnEquipmentsReset;
            // 监听单个装备解锁事件
            EquipmentSystem.Instance.OnEquipmentUnlocked += OnEquipmentUnlocked;

            Debug.Log("已注册EquipmentSystem事件监听");
        }
        else
        {
            Debug.LogWarning("EquipmentSystem未找到，无法注册事件监听");
        }
    }

    // 设置删除存档确认面板
    private void SetupDeleteArchiveConfirm()
    {
        if (deleteArchiveConfirm != null)
        {
            deleteArchiveConfirm.SetArchiveManager(this);
            Debug.Log("已设置删除存档确认面板");
        }
    }

    // 标签按钮点击事件
    private void OnTabButtonClick(EquipmentType type)
    {
        // 切换到新类型时清空显示
        ClearAllDisplay();

        // 显示对应装备容器
        ShowEquipmentContainer(type);

        Debug.Log($"切换到装备类型: {GetEquipmentTypeName(type)}");
    }

    // 显示指定类型的装备容器
    public void ShowEquipmentContainer(EquipmentType type)
    {
        HideAllContainers();

        if (equipmentContainers.ContainsKey(type))
        {
            equipmentContainers[type].SetActive(true);
        }

        currentSelectedType = type;
        UpdateTabButtonsAppearance();
    }

    // 更新标签按钮外观
    private void UpdateTabButtonsAppearance()
    {
        foreach (var kvp in tabButtons)
        {
            EquipmentType type = kvp.Key;
            Button button = kvp.Value;

            if (button != null)
            {
                Image buttonImage = button.GetComponent<Image>();
                if (buttonImage != null)
                {
                    buttonImage.color = (type == currentSelectedType) ? selectedTabColor : normalTabColor;
                }

                button.interactable = (type != currentSelectedType);
            }
        }
    }

    // 装备点击回调
    public void OnEquipmentClicked(EquipmentType type, int id, EquipmentIcon icon)
    {
        ShowEquipmentInfo(type, id, icon);
    }

    // 清空所有显示
    public void ClearAllDisplay()
    {
        if (typeText != null)
            typeText.text = emptyTypeText;

        if (nameText != null)
            nameText.text = emptyNameText;

        if (descriptionText != null)
            descriptionText.text = emptyDescriptionText;

        if (howToGetText != null)
            howToGetText.text = emptyHowToGetText;

        if (idText != null)
            idText.text = emptyIdText;

        Debug.Log("已清空装备信息显示");
    }

    // 显示装备信息
    public void ShowEquipmentInfo(EquipmentType type, int id, EquipmentIcon icon)
    {
        if (icon == null)
        {
            Debug.LogError("EquipmentIcon为空");
            return;
        }

        // 从EquipmentSystem检查是否解锁
        bool isUnlocked = false;
        if (EquipmentSystem.Instance != null)
        {
            isUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(type, id);
        }
        else
        {
            Debug.LogWarning("EquipmentSystem未找到，使用本地检查");
        }

        if (isUnlocked)
        {
            if (nameText != null)
                nameText.text = icon.equipmentName;

            if (descriptionText != null)
                descriptionText.text = icon.description;
        }
        else
        {
            if (nameText != null)
                nameText.text = lockedNamePrefix;

            if (descriptionText != null)
                descriptionText.text = lockedDescription;
        }

        if (typeText != null)
            typeText.text = GetEquipmentTypeName(type);

        if (idText != null)
            idText.text = $"编号: {id:D3}";

        if (howToGetText != null)
            howToGetText.text = "获得方式：" + icon.howToGet;

        Debug.Log($"显示装备信息: {icon.equipmentName} (已解锁: {isUnlocked})");
    }

    // 获取装备类型的中文名称
    public string GetEquipmentTypeName(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.ClearEquipment: return "【通关装备】";
            case EquipmentType.AchievementEquipment: return "【成就装备】";
            case EquipmentType.FavorEquipment: return "【好感度装备】";
            case EquipmentType.GachaEquipment: return "【抽卡装备】";
            case EquipmentType.InheritEquipment: return "【继承装备】";
            default: return "【未知类型】";
        }
    }

    // 更新所有装备图标显示
    public void UpdateAllEquipmentIcons()
    {
        foreach (var container in equipmentContainers.Values)
        {
            if (container != null)
            {
                EquipmentIcon[] icons = container.GetComponentsInChildren<EquipmentIcon>(true);
                foreach (var icon in icons)
                {
                    icon.UpdateDisplay();
                }
            }
        }

        Debug.Log($"已更新所有装备图标显示");
    }

    // 装备重置事件处理
    private void OnEquipmentsReset()
    {
        Debug.Log("收到装备重置事件，更新显示");

        // 更新所有图标显示
        UpdateAllEquipmentIcons();

        // 清空信息显示
        ClearAllDisplay();

        Debug.Log("所有装备已重置，显示已更新");
    }

    // 单个装备解锁事件处理
    private void OnEquipmentUnlocked(EquipmentType type, int id)
    {
        Debug.Log($"收到装备解锁事件: {type}_{id}");

        // 如果当前显示的是这个类型，更新对应的图标
        if (type == currentSelectedType)
        {
            UpdateEquipmentIcon(type, id);
        }
    }

    // 更新单个装备图标
    private void UpdateEquipmentIcon(EquipmentType type, int id)
    {
        if (equipmentContainers.ContainsKey(type))
        {
            GameObject container = equipmentContainers[type];
            if (container != null)
            {
                EquipmentIcon[] icons = container.GetComponentsInChildren<EquipmentIcon>(true);
                foreach (var icon in icons)
                {
                    if (icon.equipmentType == type && icon.equipmentId == id)
                    {
                        icon.UpdateDisplay();
                        Debug.Log($"更新了装备图标: {type}_{id}");
                        break;
                    }
                }
            }
        }
    }

    void OnDestroy()
    {
        // 取消监听EquipmentSystem事件
        if (EquipmentSystem.Instance != null)
        {
            EquipmentSystem.Instance.OnAllEquipmentsReset -= OnEquipmentsReset;
            EquipmentSystem.Instance.OnEquipmentUnlocked -= OnEquipmentUnlocked;

            Debug.Log("已取消EquipmentSystem事件监听");
        }
    }

    // 调试方法

    [ContextMenu("测试更新所有图标")]
    public void TestUpdateAllIcons()
    {
        UpdateAllEquipmentIcons();
    }

    [ContextMenu("打开删除存档面板")]
    public void TestOpenDeleteArchivePanel()
    {
        if (deleteArchiveConfirm != null)
        {
            deleteArchiveConfirm.OpenConfirmPanel();
        }
    }
}