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
    public string emptyTypeText = "[请选择装备]";
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

    [Header("积分解锁")]
    public Button unlockByPointsButton;         // 积分解锁按钮
    public TextMeshProUGUI currentPointsText;   // 当前积分显示
    private EquipmentType _pendingType;
    private int _pendingId;
    private EquipmentIcon _pendingIcon;

    /// <summary>根据通关装备 id 返回兑换所需积分</summary>
    private static int GetUnlockCost(int id)
    {
        if (id <= 2)  return 60;   // N2
        if (id <= 5)  return 120;  // N3
        if (id <= 8)  return 180;  // N4
        if (id <= 11) return 240;  // N5
        if (id <= 14) return 300;  // N6 (id 12~14)
        if (id <= 17) return 360;  // N7 (id 15~17)
        if (id <= 20) return 420;  // N8 (id 18~20)
        if (id <= 23) return 480;  // N9 (id 21~23)
        if (id <= 26) return 540;  // N10 (id 24~26)
        if (id <= 29) return 600;  // N11 (id 27~29)
        if (id <= 32) return 660;  // N12 (id 30~32)
        return 720;                // N13 (id 33~35)
    }

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

        // 初始化积分解锁按钮
        if (unlockByPointsButton != null)
        {
            unlockByPointsButton.onClick.AddListener(OnUnlockByPoints);
            unlockByPointsButton.gameObject.SetActive(false);
        }
        RefreshPointsDisplay();

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
        {
            equipmentContainers.Add(EquipmentType.AchievementEquipment, achievementEquipmentContainer);
        }

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
        // ── 在挂回调前先补全 SSR8 / SSR9 两个新增抽卡装备图标 ─────────────────
        // 场景里历史上只手工拖了 SSR0~SSR7 共 8 个 EquipmentIcon，新增的
        // 「我与我与我」(equipmentSystemId=11) / 「三清化一」(equipmentSystemId=12)
        // 没有对应图标。这里在运行时用现有 SSR EquipmentIcon 做模板 Instantiate 出
        // 缺失的两个图标，挂到原 SSR 容器（GridLayoutGroup 会自动排版）。
        // 之后 EquipmentIcon.Start → Initialize → ApplyForcedGachaSsrOverrides
        // 会自动注入名字/描述/howToGet/Sprite。
        EnsureGachaSsrIconsExist();

        // 同样补全 R_2 读档币 / SR_6 速度灵果 两个新增 R/SR 抽卡装备图标。
        // 直接在 gachaEquipmentContainer 下扫描全部 EquipmentIcon，按 gachaRarity 分组，
        // 取首个同稀有度图标做模板克隆（保留 parent 一致以兼容 GridLayoutGroup）。
        // 文本与 Sprite 由 EquipmentIcon.ApplyForcedGachaRSrOverrides 注入。
        EnsureGachaRSrIconsExist();

        // ── 同样地补全 N8 通关装备 18/19/20（和平之剑/甲/心）三个图标 ──────────
        // 场景里通关装备容器历史上只挂到 N7（id 0~17），新增的 N8 三件没有 EquipmentIcon。
        // 这里在运行时用 N7 任一 EquipmentIcon 做模板 Instantiate，文本和图标
        // 由 EquipmentIcon.ApplyForcedClearEquipmentN8Overrides 自动注入。
        EnsureClearEquipmentN8IconsExist();

        // ── 同样地补全 N9~N13 通关装备 21~35（利爪/月牙/粘液/暗影/龙鳞 系列）十五个图标 ──
        // 场景里没有这些图标，运行时用现有 ClearEquipment 图标做模板克隆，
        // 文本和图标由 EquipmentIcon.ApplyForcedClearEquipmentN9toN13Overrides 自动注入。
        EnsureClearEquipmentN9toN13IconsExist();

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

    /// <summary>
    /// 在 GachaEquipment 容器下的 "SSR" 子节点中，按需补出 SSR8 / SSR9（equipmentId 11/12）的图标。
    /// 已存在则跳过，幂等。模板来自首个 SSR 抽卡 EquipmentIcon。
    /// </summary>
    private void EnsureGachaSsrIconsExist()
    {
        if (gachaEquipmentContainer == null) return;

        // 在 GachaEquipment 容器下递归查找名字为 "SSR" 的子节点
        Transform ssrParent = FindChildByNameRecursive(gachaEquipmentContainer.transform, "SSR");
        if (ssrParent == null) return;

        // 收集 SSR 容器下现有 SSR EquipmentIcon
        EquipmentIcon[] existingSsr = ssrParent.GetComponentsInChildren<EquipmentIcon>(true);
        if (existingSsr == null || existingSsr.Length == 0) return;

        EquipmentIcon template = null;
        var existingIds = new HashSet<int>();
        foreach (var icon in existingSsr)
        {
            if (icon.equipmentType != EquipmentType.GachaEquipment) continue;
            if (icon.gachaRarity   != GachaRarity.SSR)              continue;
            existingIds.Add(icon.equipmentId);
            if (template == null) template = icon;
        }
        if (template == null) return;

        // 需要补出的四个 SSR：equipmentId = 11 / 12 / 13 / 14
        TryCloneSsrIcon(template, ssrParent, 11, existingIds);
        TryCloneSsrIcon(template, ssrParent, 12, existingIds);
        TryCloneSsrIcon(template, ssrParent, 13, existingIds);
        TryCloneSsrIcon(template, ssrParent, 14, existingIds);
    }

    /// <summary>
    /// 在 GachaEquipment 容器下按 gachaRarity 分组，按需补出 R_2 / SR_6 的图标。
    /// 已存在则跳过，幂等。模板来自首个同稀有度抽卡 EquipmentIcon。
    ///
    /// 与 EnsureGachaSsrIconsExist 同套路；文本/图标由
    /// EquipmentIcon.ApplyForcedGachaRSrOverrides 自动注入。
    ///
    /// 实现注意：不再依赖容器名（如 "R" / "SR"）做查找——历史上场景容器命名可能不一致，
    /// 容易导致克隆失败。改为直接在 gachaEquipmentContainer 下扫描全部 EquipmentIcon，
    /// 按 gachaRarity 分组取首个作为模板，并复用它的 parent（Grid 布局父节点）。
    /// </summary>
    private void EnsureGachaRSrIconsExist()
    {
        if (gachaEquipmentContainer == null) return;

        TryEnsureRarityIcon(GachaRarity.R, 2);
        TryEnsureRarityIcon(GachaRarity.SR, 6);
    }

    private void TryEnsureRarityIcon(GachaRarity rarity, int targetId)
    {
        // 直接在整个抽卡容器下扫描；不再依赖容器名（兼容历史命名差异）。
        EquipmentIcon[] all = gachaEquipmentContainer.GetComponentsInChildren<EquipmentIcon>(true);
        if (all == null || all.Length == 0) return;

        EquipmentIcon template = null;
        var existingIds = new HashSet<int>();
        foreach (var icon in all)
        {
            if (icon == null) continue;
            if (icon.equipmentType != EquipmentType.GachaEquipment) continue;
            if (icon.gachaRarity   != rarity)                       continue;
            existingIds.Add(icon.equipmentId);
            if (template == null) template = icon;
        }
        if (template == null)
        {
            Debug.LogWarning($"[ArchiveManager] 未在 GachaEquipment 容器下找到任何 {rarity} 模板，无法克隆 {rarity}_{targetId}");
            return;
        }
        if (existingIds.Contains(targetId)) return;

        Transform parent = template.transform.parent;
        if (parent == null) parent = gachaEquipmentContainer.transform;

        GameObject clone = Instantiate(template.gameObject, parent);
        clone.name = $"{rarity}_{targetId} (auto)";

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.GachaEquipment;
        cloneIcon.gachaRarity   = rarity;
        cloneIcon.equipmentId   = targetId;
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;

        Debug.Log($"[ArchiveManager] 已克隆 {rarity}_{targetId} 图标（父节点 = {parent.name}）");
    }

    /// <summary>
    /// 在 ClearEquipment 容器下按需补出 N8 通关装备 18/19/20（和平之剑/甲/心）的图标。
    /// 已存在则跳过，幂等。模板优先选 N7 EquipmentIcon（id 15/16/17 中第一个找到的），
    /// 找不到 N7 则退而求其次用容器内任一 ClearEquipment 图标。
    /// </summary>
    private void EnsureClearEquipmentN8IconsExist()
    {
        if (clearEquipmentContainer == null) return;

        EquipmentIcon[] existing = clearEquipmentContainer.GetComponentsInChildren<EquipmentIcon>(true);
        if (existing == null || existing.Length == 0) return;

        EquipmentIcon template = null;
        var existingIds = new HashSet<int>();
        foreach (var icon in existing)
        {
            if (icon.equipmentType != EquipmentType.ClearEquipment) continue;
            existingIds.Add(icon.equipmentId);
            // 优先用 N7（id 15/16/17）做模板，确保 RectTransform / 字体 / Image 结构与新加的 N8 同款
            if (template == null || (icon.equipmentId >= 15 && icon.equipmentId <= 17))
                template = icon;
        }
        if (template == null) return;

        // 模板父节点（一般就是 clearEquipmentContainer，或它下面某个 GridLayoutGroup 容器）
        Transform parent = template.transform.parent != null ? template.transform.parent : clearEquipmentContainer.transform;

        TryCloneClearN8Icon(template, parent, 18, existingIds);
        TryCloneClearN8Icon(template, parent, 19, existingIds);
        TryCloneClearN8Icon(template, parent, 20, existingIds);
    }

    /// <summary>
    /// 在 ClearEquipment 容器下按需补出 N9~N13 通关装备 21~35 的图标。
    /// 已存在则跳过，幂等。模板优先选 N7/N8 EquipmentIcon，找不到则用容器内任一 ClearEquipment 图标。
    /// 文本和图标由 EquipmentIcon.ApplyForcedClearEquipmentN9toN13Overrides 在 Initialize 时注入。
    /// </summary>
    private void EnsureClearEquipmentN9toN13IconsExist()
    {
        if (clearEquipmentContainer == null) return;

        EquipmentIcon[] existing = clearEquipmentContainer.GetComponentsInChildren<EquipmentIcon>(true);
        if (existing == null || existing.Length == 0) return;

        EquipmentIcon template = null;
        var existingIds = new HashSet<int>();
        foreach (var icon in existing)
        {
            if (icon.equipmentType != EquipmentType.ClearEquipment) continue;
            existingIds.Add(icon.equipmentId);
            // 优先用 N7（id 15~17）或 N8（id 18~20）做模板
            if (template == null || (icon.equipmentId >= 15 && icon.equipmentId <= 20))
                template = icon;
        }
        if (template == null) return;

        Transform parent = template.transform.parent != null ? template.transform.parent : clearEquipmentContainer.transform;

        // N9: 21-23
        for (int id = 21; id <= 23; id++)
            TryCloneClearN9toN13Icon(template, parent, id, existingIds);
        // N10: 24-26
        for (int id = 24; id <= 26; id++)
            TryCloneClearN9toN13Icon(template, parent, id, existingIds);
        // N11: 27-29
        for (int id = 27; id <= 29; id++)
            TryCloneClearN9toN13Icon(template, parent, id, existingIds);
        // N12: 30-32
        for (int id = 30; id <= 32; id++)
            TryCloneClearN9toN13Icon(template, parent, id, existingIds);
        // N13: 33-35
        for (int id = 33; id <= 35; id++)
            TryCloneClearN9toN13Icon(template, parent, id, existingIds);
    }

    private static void TryCloneClearN9toN13Icon(EquipmentIcon template, Transform parent, int targetId, HashSet<int> existingIds)
    {
        if (existingIds.Contains(targetId)) return;

        GameObject clone = Instantiate(template.gameObject, parent);
        string diffPrefix = targetId <= 23 ? "N9" : targetId <= 26 ? "N10" : targetId <= 29 ? "N11" : targetId <= 32 ? "N12" : "N13";
        clone.name = $"{diffPrefix}_{targetId} (auto)";

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.ClearEquipment;
        cloneIcon.equipmentId   = targetId;
        cloneIcon.gachaRarity   = GachaRarity.R;
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;

        existingIds.Add(targetId);
    }

    private static Transform FindChildByNameRecursive(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform r = FindChildByNameRecursive(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private static void TryCloneSsrIcon(EquipmentIcon template, Transform parent, int targetId, HashSet<int> existingIds)
    {
        if (existingIds.Contains(targetId)) return;

        GameObject clone = Instantiate(template.gameObject, parent);
        clone.name = $"SSR_{targetId} (auto)";

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.GachaEquipment;
        cloneIcon.gachaRarity   = GachaRarity.SSR;
        cloneIcon.equipmentId   = targetId;
        // 名字 / 描述 / howToGet / Sprite 都会在同帧内由
        // EquipmentIcon.Start → Initialize → ApplyForcedGachaSsrOverrides 自动注入。
        // 这里把文本字段清空避免视觉上闪现模板内容；iconImage 是 private 字段，
        // 不能从外部访问，但因为 Initialize 内部会重新 SetIconFromAssetPath
        // 覆盖 sprite / overrideSprite，所以无需手动清空也不会残留模板贴图。
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;

        existingIds.Add(targetId);
    }

    /// <summary>
    /// 克隆一个 N7 EquipmentIcon 模板出来作为 N8 通关装备图标。逻辑与 TryCloneSsrIcon 同源，
    /// 区别在于：稀有度 / equipmentType 不同，且名字/描述/sprite 由
    /// EquipmentIcon.ApplyForcedClearEquipmentN8Overrides 在 Initialize 时注入。
    /// </summary>
    private static void TryCloneClearN8Icon(EquipmentIcon template, Transform parent, int targetId, HashSet<int> existingIds)
    {
        if (existingIds.Contains(targetId)) return;

        GameObject clone = Instantiate(template.gameObject, parent);
        clone.name = $"N8_{targetId} (auto)";

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.ClearEquipment;
        cloneIcon.equipmentId   = targetId;
        cloneIcon.gachaRarity   = GachaRarity.R; // 默认值，ClearEquipment 不依赖此字段
        // 文本清空，等 Initialize 在 ApplyForcedClearEquipmentN8Overrides 注入
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;

        existingIds.Add(targetId);
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
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
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

        if (unlockByPointsButton != null)
            unlockByPointsButton.gameObject.SetActive(false);

        Debug.Log("已清空装备详情显示");
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

        // R/SR 抽卡装备：有叠加数量就算解锁（用 icon 上的 gachaRarity 精确判断）
        if (!isUnlocked && type == EquipmentType.GachaEquipment && GachaManager.Instance != null
            && icon != null)
        {
            int count = GachaManager.Instance.GetItemCount(icon.gachaRarity, id);
            if (count > 0) isUnlocked = true;
        }

        string progressStr = "";
        if (!isUnlocked && type == EquipmentType.AchievementEquipment)
        {
            switch (id)
            {
                case 0:
                    progressStr = " (0/1)";
                    break;
                case 1:
                case 2:
                case 3:
                    progressStr = " (0/1)";
                    break;
                case 4:
                    int tMin = PlayerPrefs.GetInt("TotalPlayMinutes", 0);
                    progressStr = $" ({Mathf.Min(30, tMin)}/30分钟)";
                    break;
                case 5:
                    int cCount = PlayerPrefs.GetInt("CampCapturedCount", 0);
                    progressStr = $" ({Mathf.Min(100, cCount)}/100)";
                    break;
                case 6:
                    int mCount = PlayerPrefs.GetInt("MushroomDefeatedCount", 0);
                    progressStr = $" ({Mathf.Min(500, mCount)}/500)";
                    break;
                case 7:
                    int bestLevel = PlayerPrefs.GetInt("BestSingleRunLevel", 1);
                    progressStr = $" ({Mathf.Min(50, bestLevel)}/50级)";
                    break;
            }
        }

        if (isUnlocked)
        {
            if (nameText != null)
                nameText.text = icon.equipmentName;

            // R/SR 抽卡装备额外显示叠加数量（用 icon 上的 gachaRarity 精确查询）
            string extraInfo = "";
            if (type == EquipmentType.GachaEquipment && GachaManager.Instance != null)
            {
                int count = GachaManager.Instance.GetItemCount(icon.gachaRarity, id);
                if (count > 0) extraInfo = $"\n持有数量：×{count}";
            }

            if (descriptionText != null)
                descriptionText.text = icon.description + extraInfo;

            if (unlockByPointsButton != null)
                unlockByPointsButton.gameObject.SetActive(false);
        }
        else
        {
            if (nameText != null)
                nameText.text = lockedNamePrefix;

            if (descriptionText != null)
                descriptionText.text = lockedDescription;

            // 未解锁且是通关装备 0~35（N2~N13 全部），显示积分解锁按钮
            bool canUnlockByPoints = type == EquipmentType.ClearEquipment && id >= 0 && id <= 35;
            if (unlockByPointsButton != null)
            {
                unlockByPointsButton.gameObject.SetActive(canUnlockByPoints);
                if (canUnlockByPoints)
                {
                    _pendingType = type;
                    _pendingId   = id;
                    _pendingIcon = icon;
                    int cost = GetUnlockCost(id);
                    int pts = ClearRecordManager.Instance != null ? ClearRecordManager.Instance.GetEquipmentPoints() : 0;
                    bool canAfford = pts >= cost;
                    unlockByPointsButton.interactable = canAfford;
                    if (currentPointsText != null) currentPointsText.text = $"现有积分：{pts} / 所需积分：{cost}";
                }
            }
        }

        if (typeText != null)
            typeText.text = GetEquipmentTypeName(type);

        if (idText != null)
            idText.text = $"编号: {GetDisplayId(type, id, icon):D3}";

        if (howToGetText != null)
            howToGetText.text = "获得方式：" + icon.howToGet + progressStr;

        Debug.Log($"显示装备信息: {icon.equipmentName} (已解锁: {isUnlocked})");
    }

    // 获取装备类型的中文名称
    public string GetEquipmentTypeName(EquipmentType type)
    {
        switch (type)
        {
            case EquipmentType.ClearEquipment: return "[通关装备]";
            case EquipmentType.AchievementEquipment: return "[成就装备]";
            case EquipmentType.FavorEquipment: return "[好感度装备]";
            case EquipmentType.GachaEquipment: return "[抽卡装备]";
            case EquipmentType.InheritEquipment: return "[继承装备]";
            default: return "[未知类型]";
        }
    }

    /// <summary>
    /// 计算"编号"显示用的 id。
    /// 抽卡装备的 SSR/UR 在场景里用 EquipmentSystem 命名空间下的 equipmentId（避开 0~9 SSR 名空间冲突，
    /// 例如亡者领域 equipmentId=10），与玩家在 UI 上看到的"该稀有度第几个"概念不一致。
    /// 因此对 SSR/UR 抽卡装备，用 GachaManager 中匹配的 rarityId 作为显示编号——
    /// 即"UR 第 0/1/2 个" → 显示 "000/001/002"，亡者领域显示 "002" 而不是 "010"。
    /// 其它装备保持显示 EquipmentIcon.equipmentId（已经是该类型内的连续序号）。
    /// </summary>
    private int GetDisplayId(EquipmentType type, int id, EquipmentIcon icon)
    {
        if (type != EquipmentType.GachaEquipment || icon == null || GachaManager.Instance == null)
            return id;

        // 仅对 SSR/UR 做 equipmentId → rarityId 的转换；R/SR 的 equipmentId 本身就等于 rarityId
        if (icon.gachaRarity != GachaRarity.SSR && icon.gachaRarity != GachaRarity.UR)
            return id;

        var item = GachaManager.Instance.FindItemByEquipmentSystemId(icon.gachaRarity, id);
        return item != null ? item.rarityId : id;
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

    [ContextMenu("测试：打开删除存档面板")]
    public void TestOpenDeleteArchivePanel()
    {
        if (deleteArchiveConfirm != null)
        {
            deleteArchiveConfirm.OpenConfirmPanel();
        }
    }

    // ── 【Editor 工具】静态化新增抽卡 / 通关装备图标 ───────────────────────────
    //
    // 背景：历史上新增的 R_2 读档币 / SR_6 速度灵果 / SSR_11(8) / SSR_12(9) / SSR_13(10) /
    //       N8 通关装备 18/19/20 都是「运行时 Instantiate 模板克隆」出来的（见上面
    //       EnsureGachaSsrIconsExist / EnsureGachaRSrIconsExist / EnsureClearEquipmentN8IconsExist），
    //       这导致两个问题：
    //         1) 非 Play 模式打开场景看不到这些图标，不利于策划在编辑器里直接调整布局；
    //         2) 每次启动都要走克隆 + SetIconFromAssetPath（File.ReadAllBytes）流程，性能/稳定性差。
    //
    // 解决方案：提供 Editor 菜单，让开发者在编辑器里点一次即可把所有缺失图标
    //          以"真实场景 GameObject"的形式生成出来，并立即把 equipmentName /
    //          description / howToGet / Sprite 写入 SerializedField，保存场景后
    //          它们就成为静态资源，下次进入场景（无论 Editor 或 Play 模式）直接可见，
    //          运行时 EnsureXxxIconsExist 检测到已存在会自动跳过（幂等）。
    //
    // 使用步骤：
    //   1) 打开 SampleScene；
    //   2) 选中挂着 ArchiveManager 的 GameObject；
    //   3) Inspector 里 ArchiveManager 组件右上角齿轮菜单 → "静态化生成所有缺失装备图标"；
    //   4) 检查 Scene 视图里 GachaEquipment / ClearEquipment 容器下确实新增了图标；
    //   5) Ctrl+S 保存场景。
    //
    // 之后每次新增装备时，再次点这个菜单即可——已存在的会跳过。
    [ContextMenu("静态化生成所有缺失装备图标（Editor 工具）")]
    public void EditorStaticGenerateMissingEquipmentIcons()
    {
#if UNITY_EDITOR
        if (!Application.isEditor || Application.isPlaying)
        {
            Debug.LogWarning("[ArchiveManager] 静态化生成图标必须在非 Play 模式的 Editor 中调用");
            return;
        }

        // 初始化容器字典（Editor 模式 Start 未跑过）
        InitializeContainers();

        int beforeCount = CountAllIconsInContainers();

        // 跑四套补全逻辑——和运行时完全相同
        EnsureGachaSsrIconsExist();
        EnsureGachaRSrIconsExist();
        EnsureClearEquipmentN8IconsExist();
        EnsureClearEquipmentN9toN13IconsExist();

        // 关键差异：Editor 模式下 EquipmentIcon.Start 不会触发，
        // 需要主动调用 EditorApplyForcedOverrides() 把文本/Sprite 立即写入。
        // 这样保存场景后这些字段就以静态值持久化进 .unity 文件。
        foreach (var container in equipmentContainers.Values)
        {
            if (container == null) continue;
            var icons = container.GetComponentsInChildren<EquipmentIcon>(true);
            foreach (var icon in icons)
            {
                if (icon == null) continue;
                icon.EditorApplyForcedOverrides();
                // 标记 SerializedObject 脏，确保保存场景时这次 Editor 注入的字段真的被持久化
                UnityEditor.EditorUtility.SetDirty(icon);
                if (icon.gameObject.scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(icon.gameObject.scene);
            }
        }

        int afterCount = CountAllIconsInContainers();
        Debug.Log($"[ArchiveManager] 静态化生成完成。新增图标 {afterCount - beforeCount} 个（{beforeCount} → {afterCount}）。");
        Debug.Log($"[ArchiveManager] 请 Ctrl+S 保存场景，新增的图标即成为永久静态对象。");
#else
        Debug.LogError("[ArchiveManager] 该工具仅在 Unity Editor 中可用");
#endif
    }

    private int CountAllIconsInContainers()
    {
        int n = 0;
        foreach (var c in equipmentContainers.Values)
        {
            if (c == null) continue;
            n += c.GetComponentsInChildren<EquipmentIcon>(true).Length;
        }
        return n;
    }

    // 积分解锁按钮点击
    private void OnUnlockByPoints()
    {
        if (ClearRecordManager.Instance == null || EquipmentSystem.Instance == null) return;
        int cost = GetUnlockCost(_pendingId);
        int pts = ClearRecordManager.Instance.GetEquipmentPoints();
        if (pts < cost) return;

        // 扣除积分
        PlayerPrefs.SetInt("ClearEquipmentPoints", pts - cost);
        PlayerPrefs.Save();

        // 解锁装备
        EquipmentSystem.Instance.UnlockEquipment(_pendingType, _pendingId);
        ToastManager.Show($"已消耗{cost}积分，解锁装备{_pendingId}号！");

        // 刷新显示
        RefreshPointsDisplay();
        if (_pendingIcon != null)
            ShowEquipmentInfo(_pendingType, _pendingId, _pendingIcon);
    }

    private void RefreshPointsDisplay()
    {
        if (currentPointsText == null || ClearRecordManager.Instance == null) return;
        int pts = ClearRecordManager.Instance.GetEquipmentPoints();
        currentPointsText.text = $"现有积分：{pts}";
    }
}