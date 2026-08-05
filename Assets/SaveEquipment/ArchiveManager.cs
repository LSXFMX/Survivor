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
        {
            equipmentContainers.Add(EquipmentType.InheritEquipment, inheritEquipmentContainer);
            // 继承装备走一套完全独立的 UI（人形轮廓 + 六槽位 + 仓库 + 分解/重铸），
            // 不使用 EquipmentIcon 那套"固定图标格子"机制，因此在这里单独挂载面板。
            // 场景里这个容器是个空节点，面板整体由代码构建。
            SafeRun(() => InheritEquipmentHooks.AttachUIToArchiveContainer(inheritEquipmentContainer),
                    "AttachInheritEquipmentUI");
        }

        // 【2026-08 修复"所有装备都挤在第一排"】
        //   4 个图标容器都挂着 HorizontalLayoutGroup —— 它只会把子物体沿水平轴一字排开，
        //   **永远不会换行**。装备数量少的时候看不出来，后来通关/抽卡/好感度装备各扩到
        //   10 个以上，就全部溢出到面板右边（甚至跑出黑框外）。
        //   这里把它们替换成 GridLayoutGroup（按容器宽度算列数、自动换行）。
        //   继承装备容器不参与 —— 它是整块自绘面板，见 InheritEquipmentUI。
        SafeRun(() => ApplyGridLayout(clearEquipmentContainer), "GridLayout-Clear");
        SafeRun(() => ApplyGridLayout(achievementEquipmentContainer), "GridLayout-Achievement");
        SafeRun(() => ApplyGridLayout(favorEquipmentContainer), "GridLayout-Favor");
        SafeRun(() => ApplyGridLayout(gachaEquipmentContainer), "GridLayout-Gacha");

        // 初始时全部隐藏
        HideAllContainers();
    }

    /// <summary>
    /// 把一个装备图标容器的横排布局换成自动换行的网格布局。
    /// 单元格尺寸取容器内现有图标的实际大小（场景里配好的 ≈99），列数按容器宽度推导。
    /// </summary>
    private static void ApplyGridLayout(GameObject container)
    {
        if (container == null) return;
        var rt = container.transform as RectTransform;
        if (rt == null) return;

        // 关掉横排/竖排布局组
        foreach (var g in container.GetComponents<HorizontalOrVerticalLayoutGroup>())
            if (g != null) g.enabled = false;

        // 单元格边长：优先沿用现有图标的尺寸，保证观感不变
        float cell = 99f;
        var firstIcon = container.GetComponentInChildren<EquipmentIcon>(true);
        if (firstIcon != null)
        {
            var irt = firstIcon.transform as RectTransform;
            if (irt != null && irt.rect.width > 10f) cell = irt.rect.width;
        }

        const float space = 12f;
        const int pad = 24;

        float w = rt.rect.width > 10f ? rt.rect.width : 1030f;
        int cols = Mathf.Max(1, Mathf.FloorToInt((w - pad * 2f + space) / (cell + space)));

        var grid = container.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = container.AddComponent<GridLayoutGroup>();
        grid.enabled = true;
        grid.padding = new RectOffset(pad, pad, pad, pad);
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(space, space);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = cols;

        Debug.Log($"[Archive] {container.name} 改用网格布局：{cols} 列 / 格子 {cell:0}");
    }

    /// <summary>
    /// 安全执行一个补全函数：捕获任何异常并记录日志，绝不让其向上传播炸掉整个初始化流程。
    /// </summary>
    private static void SafeRun(System.Action action, string label)
    {
        try
        {
            action();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ArchiveManager] {label} 执行失败，已跳过（不影响其它装备分类正常显示）：{ex}");
        }
    }

    // 设置所有EquipmentIcon的点击回调
    private void SetupEquipmentIcons()
    {
        // 【关键修复】以下每个 Ensure* 补全函数都会克隆图标并触发 EquipmentIcon.Initialize
        // （里面含贴图抠背景等可能失败的操作，如纹理压缩格式不支持 SetPixels32）。
        // 之前任意一个函数抛出未捕获异常，会导致本方法直接中断——后面绑定所有装备图标
        // onClickCallback 的循环、以及 Start() 里 InitializeTabButtons/ShowEquipmentContainer
        // 全部被跳过，表现为“存档界面什么都没有、全部无法点击”。
        // 现在给每一步单独 try-catch，任意一步失败只影响该分类，其余分类仍可正常显示。
        SafeRun(EnsureGachaSsrIconsExist, "EnsureGachaSsrIconsExist");
        SafeRun(EnsureGachaRSrIconsExist, "EnsureGachaRSrIconsExist");
        SafeRun(EnsureClearEquipmentN8IconsExist, "EnsureClearEquipmentN8IconsExist");
        SafeRun(EnsureClearEquipmentN9toN13IconsExist, "EnsureClearEquipmentN9toN13IconsExist");
        SafeRun(EnsureAchievementIcon8Exists, "EnsureAchievementIcon8Exists");
        SafeRun(EnsureFavorEquipmentWolfIconsExist, "EnsureFavorEquipmentWolfIconsExist");

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

        // 需要补出的四个 SSR：equipmentId = 11 / 12 / 13 / 36
        //   注：14 已被 N6「夯子之心」占用，故 SSR_11 用 36
        TryCloneSsrIcon(template, ssrParent, 11, existingIds);
        TryCloneSsrIcon(template, ssrParent, 12, existingIds);
        TryCloneSsrIcon(template, ssrParent, 13, existingIds);
        TryCloneSsrIcon(template, ssrParent, 36, existingIds);
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
        // 关键修复：Instantiate 会把模板的 isInitialized=true 也复制过来，
        // 导致克隆体自己的 Start() 直接跳过 Initialize，点击和图标覆盖都不会生效。
        cloneIcon.ForceReinitializeAfterClone();

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

    /// <summary>
    /// 在成就装备容器下按需补出成就装备 8「不可视之手」的图标。
    /// 已存在则跳过，幂等。用现有成就装备图标（优先 id 7）做模板克隆，
    /// 文本与图标由 EquipmentIcon.Initialize（equipmentId==8 分支）自动注入。
    /// </summary>
    private void EnsureAchievementIcon8Exists()
    {
        if (achievementEquipmentContainer == null) return;

        EquipmentIcon[] existing = achievementEquipmentContainer.GetComponentsInChildren<EquipmentIcon>(true);
        if (existing == null || existing.Length == 0) return;

        EquipmentIcon template = null;
        var existingIds = new HashSet<int>();
        foreach (var icon in existing)
        {
            if (icon.equipmentType != EquipmentType.AchievementEquipment) continue;
            existingIds.Add(icon.equipmentId);
            if (template == null || icon.equipmentId == 7) template = icon;
        }
        if (template == null || existingIds.Contains(8)) return;

        Transform parent = template.transform.parent != null ? template.transform.parent : achievementEquipmentContainer.transform;
        GameObject clone = Instantiate(template.gameObject, parent);
        clone.name = "Achievement_8 (auto)";

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.AchievementEquipment;
        cloneIcon.equipmentId   = 8;
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;
        cloneIcon.ForceReinitializeAfterClone();
    }

    /// <summary>
    /// 在 FavorEquipment 容器下按需补出**狼人 6/7/8 与史莱姆 9/10/11** 六件图标。
    /// 场景里历史上只挂了蘑菇 0/1/2 + 蝙蝠 3/4/5，后加的两个社群都需要运行时克隆补齐。
    /// 名字 / 描述 / howToGet / Sprite 由
    /// <see cref="EquipmentIcon.ApplyForcedFavorEquipmentWolfOverrides"/> 与
    /// <see cref="EquipmentIcon.ApplyForcedFavorEquipmentSlimeOverrides"/> 在 Initialize 时自动注入。
    /// 与 EnsureGachaSsrIconsExist / EnsureAchievementIcon8Exists 同套路，幂等。
    /// </summary>
    private void EnsureFavorEquipmentWolfIconsExist()
    {
        if (favorEquipmentContainer == null) return;

        EquipmentIcon[] existing = favorEquipmentContainer.GetComponentsInChildren<EquipmentIcon>(true);
        if (existing == null || existing.Length == 0) return;

        EquipmentIcon template = null;
        EquipmentIcon rightmost = null; // 场景里 x 坐标最大的现有图标，用于推算下一个图标的排列位置
        var existingIds = new HashSet<int>();
        foreach (var icon in existing)
        {
            if (icon == null) continue;
            if (icon.equipmentType != EquipmentType.FavorEquipment) continue;
            existingIds.Add(icon.equipmentId);
            // 优先选 id 5（最靠近狼人系列）做模板，其次任意
            if (template == null || icon.equipmentId == 5) template = icon;

            var rt = icon.transform as RectTransform;
            if (rt != null)
            {
                var rightRt = rightmost != null ? rightmost.transform as RectTransform : null;
                if (rightRt == null || rt.anchoredPosition.x > rightRt.anchoredPosition.x)
                    rightmost = icon;
            }
        }
        if (template == null)
        {
            Debug.LogWarning("[ArchiveManager] 未在 FavorEquipment 容器下找到任何模板，无法克隆狼人 6/7/8");
            return;
        }

        Transform parent = template.transform.parent != null
            ? template.transform.parent
            : favorEquipmentContainer.transform;

        // 计算图标间距：用现有图标里 x 坐标差值最小的正数间距，找不到则回退用图标自身宽度
        float spacing = 0f;
        var templateRt = template.transform as RectTransform;
        if (templateRt != null) spacing = templateRt.sizeDelta.x + 22f; // 图标宽度 + 一点间隙作为兜底
        {
            var xs = new List<float>();
            foreach (var icon in existing)
            {
                if (icon == null || icon.equipmentType != EquipmentType.FavorEquipment) continue;
                var rt = icon.transform as RectTransform;
                if (rt != null) xs.Add(rt.anchoredPosition.x);
            }
            xs.Sort();
            for (int i = 1; i < xs.Count; i++)
            {
                float d = xs[i] - xs[i - 1];
                if (d > 1f) { spacing = d; break; }
            }
        }

        float baseX = 0f, baseY = 0f;
        var rightmostRt = rightmost != null ? rightmost.transform as RectTransform : null;
        if (rightmostRt != null) { baseX = rightmostRt.anchoredPosition.x; baseY = rightmostRt.anchoredPosition.y; }

        int offsetIndex = 1;
        // 狼人社群 6/7/8
        TryCloneFavorWolfIcon(template, parent, 6, existingIds, baseX + spacing * offsetIndex++, baseY);
        TryCloneFavorWolfIcon(template, parent, 7, existingIds, baseX + spacing * offsetIndex++, baseY);
        TryCloneFavorWolfIcon(template, parent, 8, existingIds, baseX + spacing * offsetIndex++, baseY);
        // 史莱姆社群 9/10/11（阴史莱姆 / 阳史莱姆 / 太极两仪）
        // 场景里同样没有这三个节点，必须一起克隆出来，否则存档界面「好感度装备」
        // 永远只显示到狼人的 8 号，新加的三件在图鉴里根本看不到。
        TryCloneFavorWolfIcon(template, parent, 9,  existingIds, baseX + spacing * offsetIndex++, baseY);
        TryCloneFavorWolfIcon(template, parent, 10, existingIds, baseX + spacing * offsetIndex++, baseY);
        TryCloneFavorWolfIcon(template, parent, 11, existingIds, baseX + spacing * offsetIndex++, baseY);
    }

    private static void TryCloneFavorWolfIcon(EquipmentIcon template, Transform parent,
        int targetId, HashSet<int> existingIds, float posX, float posY)
    {
        if (existingIds.Contains(targetId)) return;

        GameObject clone = Instantiate(template.gameObject, parent);
        // 6~8 = 狼人，9~11= 史莱姆
        clone.name = targetId <= 8
            ? $"Favor_Wolf_{targetId} (auto)"
            : $"Favor_Slime_{targetId} (auto)";

        // 修复：Instantiate 会完整复制模板的 RectTransform.anchoredPosition，
        // 导致克隆图标与模板图标完全重叠、互相遮挡点击区域。这里显式重新定位。
        var rt = clone.transform as RectTransform;
        if (rt != null) rt.anchoredPosition = new Vector2(posX, posY);

        EquipmentIcon cloneIcon = clone.GetComponent<EquipmentIcon>();
        if (cloneIcon == null) { Destroy(clone); return; }

        cloneIcon.equipmentType = EquipmentType.FavorEquipment;
        cloneIcon.equipmentId   = targetId;
        // 名字/描述/howToGet/Sprite 由 EquipmentIcon 的
        // ApplyForcedFavorEquipmentWolfOverrides / ...SlimeOverrides 注入，
        // 这里清空避免视觉上闪现模板内容（蘑菇/蝙蝠字样）
        cloneIcon.equipmentName = string.Empty;
        cloneIcon.description   = string.Empty;
        cloneIcon.howToGet      = string.Empty;
        // 关键修复：template 若已跑过 Start()（isInitialized=true），Instantiate 会把这个
        // 值也复制给克隆体，导致克隆体自己的 Start() 直接跳过 Initialize，
        // button.onClick 从未挂监听器（点击无反应）、图标也未被重新覆盖为狼人系列贴图。
        cloneIcon.ForceReinitializeAfterClone();

        existingIds.Add(targetId);
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
        cloneIcon.ForceReinitializeAfterClone();

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
        cloneIcon.ForceReinitializeAfterClone();

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
        cloneIcon.ForceReinitializeAfterClone();

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

    // ════════════════════════════════════════════════════════════════
    //  继承装备页签：右侧信息板切换
    // ════════════════════════════════════════════════════════════════

    /// <summary>右侧 5 个 TMP 的缓存，避免每次遍历找引用。</summary>
    private TextMeshProUGUI[] _rightInfoTexts;
    /// <summary>右侧信息板的根节点（含蓝色底框/装饰），解析一次后缓存。</summary>
    private GameObject _detailPanelRoot;
    private bool _detailPanelRootResolved;

    /// <summary>
    /// 继承装备 tab 切换：<c>true</c> = 隐藏右侧「请选择装备」信息板，
    /// <c>false</c> = 切回其它装备类型时恢复显示。
    ///
    /// 继承装备有自己的详情区（画在容器内部），右边那块通用的
    /// 「[请选择装备] 点击左侧装备查看详情 / 获得方式」是多余的。
    /// 只隐藏文字不够 —— 蓝色底框和装饰还留在屏幕上（玩家反馈"无用的装备描述栏"），
    /// 所以这里优先隐藏**整块面板根节点**，解析失败才退回逐个隐藏文本。
    /// </summary>
    public void SetInheritEquipmentMode(bool active)
    {
    var root = ResolveDetailPanelRoot();
        if (root != null)
      {
  root.SetActive(!active);
       return;
 }

        // 回退方案：至少把文字藏掉
        if (_rightInfoTexts == null)
        {
      _rightInfoTexts = new TextMeshProUGUI[]
          {
    typeText, nameText, descriptionText, howToGetText, idText
            };
        }
        foreach (var t in _rightInfoTexts)
{
            if (t == null) continue;
     t.gameObject.SetActive(!active);
      }
    }

    /// <summary>
    /// 推导右侧信息板的根节点：从 nameText 逐层往上，找到第一个"包含全部信息文本"的祖先。
    /// 同时做安全校验 —— 一旦这个祖先把装备容器 / Canvas 也包进来了，
    /// 说明已经上到整个界面的公共父级，此时宁可放弃（否则会把整个存档界面隐藏掉）。
    /// </summary>
    private GameObject ResolveDetailPanelRoot()
    {
        if (_detailPanelRootResolved) return _detailPanelRoot;
        _detailPanelRootResolved = true;

  var texts = new List<Transform>();
        if (typeText != null) texts.Add(typeText.transform);
     if (nameText != null)        texts.Add(nameText.transform);
        if (descriptionText != null) texts.Add(descriptionText.transform);
        if (howToGetText != null)    texts.Add(howToGetText.transform);
      if (idText != null)          texts.Add(idText.transform);
 if (texts.Count == 0) return null;

 Transform cur = texts[0].parent;
        int guard = 0;
        while (cur != null && guard++ < 8)
        {
   bool containsAll = true;
      foreach (var t in texts)
                if (!t.IsChildOf(cur)) { containsAll = false; break; }

 if (containsAll)
 {
            bool tooBig = cur.GetComponent<Canvas>() != null;
  if (!tooBig && inheritEquipmentContainer != null)
        tooBig = inheritEquipmentContainer.transform.IsChildOf(cur);
       if (!tooBig && favorEquipmentContainer != null)
       tooBig = favorEquipmentContainer.transform.IsChildOf(cur);

        if (!tooBig)
    {
                  _detailPanelRoot = cur.gameObject;
  Debug.Log($"[Archive] 右侧信息板根节点 = {cur.name}");
        return _detailPanelRoot;
        }
         break;  // 再往上只会更大，没必要继续
    }
 cur = cur.parent;
}

      Debug.LogWarning("[Archive] 未能推导右侧信息板根节点，退回逐个隐藏文本");
        return null;
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

        // 【2026-08-05 修复】详情面板文字重叠 / 溢出
        // 现象：名称"太极两仪"大字号，描述字字号略小，两行紧贴重叠；编号"011"被右栏
        // 物理边界裁切；获得方式与下方文字相交。
        // 根因：右栏 5 个 TMP 都是从场景里读引用，配置在 Editor 里被改过（很可能是早期
        //  调整字号时连带动了行高/ RectTransform），改字号后高度未同步增加，于是
        //  TMP 内部的 LineHeight 强行把多行压成同一基线，造成视觉重叠。
        // 修法：每次显示前**运行时校正**一次：
        //   • 名称、描述、获得方式统一收紧字号 + 行高，禁止溢出 RectTransform；
        //   • 编号字号调到 16，永久可放进面板；
        //   • 描述 / 获得方式启用 autoSizing，避免长文案把行间挤崩；
        //   • idText anchor 强制左上对齐并截断到 RectTransform 内（不再溢出到右栏外）。
        // 这种"运行时校正"是不动场景工程的妥协 —— 若改场景能根治但需要你打开工程。
        ApplyDetailPanelLayout();

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
                case 8:
                    int upCount = PlayerPrefs.GetInt("TotalUpgradeChoices", 0);
                    progressStr = $" ({Mathf.Min(200, upCount)}/200)";
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

    // ════════════════════════════════════════════════════════════════
    //  存档装备 · 详情面板运行时校正
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// 一次性把右栏 5 个 TMP 的字号 / 行距 / 对齐方式 / overflow 模式校正到稳定形态。
    /// 见 <see cref="ShowEquipmentInfo"/> 顶部的说明。
    ///
    /// 思路：把 Editor 写死的字号视为"上限"，把 RectTransform 视为"行间距的下限"。
    ///   名称/描述/获得方式/编号 都按面板可用宽度（=右栏 RectTransform 宽度）算字号。
    ///   描述 / 获得方式启用 autoSizing，长文案时字号会自动缩小避免互相压行。
    /// </summary>
    private void ApplyDetailPanelLayout()
    {
        // 取右栏的可用宽度——以最宽的 TMP 为基准（一般是 nameText）
        float refWidth = 0f;
        if (nameText     != null) refWidth = Mathf.Max(refWidth, GetWidth(nameText.rectTransform));
        if (descriptionText != null) refWidth = Mathf.Max(refWidth, GetWidth(descriptionText.rectTransform));
        if (howToGetText != null) refWidth = Mathf.Max(refWidth, GetWidth(howToGetText.rectTransform));
        if (refWidth <= 0f) return; // 场景里没配任何右栏引用，放弃

   // 名称：单行，固定 26 字号。TMP 默认行高 ≈ 1.2×字号 ≈ 31，
        // 把 LineHeight 调成 1.2 与之吻合，避免 "名称与描述压成同基线"。
     // 【2026-08】改为居中：标题居中比贴左更像"卡片标题"，也和居中的描述/获得方式统一。
        if (nameText != null)
    {
            nameText.enableAutoSizing = false;
            nameText.fontSize = 26;
nameText.lineSpacing = 4f;
          nameText.alignment = TextAlignmentOptions.Top; // 顶部居中
    nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 描述：长文案是常态，启用 autoSizing。Min 设 13 防止被压得太小看不清。
        // 【2026-08】字号整体上调（14~20），并改为顶部居中。
        if (descriptionText != null)
        {
 descriptionText.enableAutoSizing = true;
        descriptionText.fontSizeMin = 14f;
      descriptionText.fontSizeMax = 20f;
        descriptionText.lineSpacing = 6f;
  descriptionText.alignment = TextAlignmentOptions.Top;
        }

        // 获得方式：【2026-08 按需求调整】字号明显加大（原 12~16 → 18~24）并**居中**。
        // 这行是玩家最常看的"怎么拿到"，之前挤在面板底部又小又贴左，很难注意到。
        if (howToGetText != null)
   {
            howToGetText.enableAutoSizing = true;
            howToGetText.fontSizeMin = 18f;
   howToGetText.fontSizeMax = 24f;
  howToGetText.lineSpacing = 6f;
   howToGetText.alignment = TextAlignmentOptions.Center;   // 水平+垂直居中
         howToGetText.enableWordWrapping = true;
     }

        // 编号：单行小字，左上对齐。"011" 最长三字符加"编号:" 也就 7 字，
        // 16 字号 × 7 ≈ 110 px，远小于右栏宽度，绝不会溢出。
        if (idText != null)
        {
            idText.enableAutoSizing = false;
            idText.fontSize = 16;
            idText.lineSpacing = 0f;
            idText.alignment = TextAlignmentOptions.TopLeft;
            idText.enableWordWrapping = false;
            idText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 类型标签（如"[好感度装备]"）：顶部小字，与编号同字号
        if (typeText != null)
        {
            typeText.enableAutoSizing = false;
            typeText.fontSize = 16;
            typeText.lineSpacing = 0f;
            typeText.alignment = TextAlignmentOptions.TopLeft;
            typeText.enableWordWrapping = false;
            typeText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 编号 / 类型标签的 anchor 强制 (1, 1) 右上 —— 避免场景里把它们做成居中
        // 后字号变化会"跳来跳去"。Overflow=Ellipsis 进一步兜底：万一面板极窄，
        // 也只会被省略号截断，而不会溢出到面板外。
        EnsureTopLeftAnchor(idText);
        EnsureTopLeftAnchor(typeText);
    }

    private static float GetWidth(RectTransform rt)
        => rt != null ? rt.rect.width : 0f;

    private static void EnsureTopLeftAnchor(TextMeshProUGUI tmp)
    {
        if (tmp == null) return;
        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
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
        EnsureAchievementIcon8Exists();

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