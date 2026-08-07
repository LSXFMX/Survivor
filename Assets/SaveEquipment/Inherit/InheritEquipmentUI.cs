using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 继承装备界面（挂在存档界面的「继承装备」容器下，运行时全量构建）。
///
/// 布局（策划案第 5 条：人形轮廓 + 左右各三件 + 仓库）：
/// ┌──────────────────────────────────────────────────────────────┐
/// │  材料: 128   [自动分解:开]   [一键分解劣质]       │
/// ├────────────────────────┬─────────────────────────────────────┤
/// │  ┌──┐   ┌──┐  │ 仓库 (23/120)  │
/// │  │头│  ╭────╮    │手│  │  ┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐ │
/// │  └──┘  │人形│    └──┘  │  └──┘└──┘└──┘└──┘└──┘└──┘│
/// │  ┌──┐  │轮廓│    ┌──┐  │  ┌──┐┌──┐┌──┐┌──┐┌──┐┌──┐ │
/// │  │衣│  ╰────╯    │项│  │  └──┘└──┘└──┘└──┘└──┘└──┘          │
/// │  └──┘ └──┘  │  …（滚动）         │
/// │  ┌──┐      ┌──┐  ├─────────────────────────────────────┤
/// │  │靴│      │武│  │  详情：名称 / 主词条 / 副词条  │
/// │  └──┘            └──┘  │  [穿戴] [分解] [重铸(消耗 N)]│
/// │  总加成：atk+.. def+..  │         │
/// └────────────────────────┴─────────────────────────────────────┘
///
/// 为什么整套 UI 用代码构建而不是做 prefab：
///   场景里的「继承装备」容器是个空节点（1030×851），一个子物体都没有。
///   项目里同类面板（GameSummaryPanel / 存档图标补齐）也都是运行时构建的，
///   保持一致；且代码构建能保证任何分辨率下布局都由比例算出，不会错位。
/// </summary>
public class InheritEquipmentUI : MonoBehaviour
{
    // ── 布局比例（相对容器尺寸，保证任意分辨率一致）──
    private const float LEFT_PANEL_RATIO = 0.42f; // 左侧装备栏占宽
    private const float DETAIL_H_RATIO   = 0.30f;   // 右下详情区占高
    private const int   WAREHOUSE_COLS   = 6;       // 仓库每行格数
    private const float CELL_GAP         = 8f;
    /// <summary>面板与容器边缘的留白：存档界面的黑框有一圈装饰边，铺到底会被压住。</summary>
    private const float PANEL_PADDING    = 20f;

    // ── 运行时按容器尺寸算出的实际值（容器是 1030×851 的 UI 单位，
    //    写死 46/78/74 这种"屏幕像素直觉值"会显得极小，所以全部按比例推导）──
    private float _topBarH  = 56f;
    private float _slotSize = 120f;
    private float _cellSize = 84f;
    /// <summary>基准字号（容器越大字越大）。</summary>
    private float _fs = 20f;

    private RectTransform _root;
    private TextMeshProUGUI _materialText;
    private TextMeshProUGUI _warehouseTitle;
    private TextMeshProUGUI _totalsText;
    private TextMeshProUGUI _detailText;
    private Button _autoBtn, _salvageAllBtn, _equipBestBtn;
    private TextMeshProUGUI _autoBtnLabel;

    // 顶栏右上角 "i" 系统说明（hover tooltip，仿 SkinChanger / GachaUI 的 "i"）。
    private GameObject      _infoTooltip;
    private TextMeshProUGUI _infoTooltipText;

    /// <summary>"i" 悬停弹出的继承装备系统介绍文案。</summary>
    private static readonly string InheritSystemInfoText =
        "<b><color=#FFE066>继承装备系统</color></b>\n\n" +
        "<color=#9BE8FF>什么是继承装备：</color>击败世界Boss / 无尽之塔Boss掉落的装备，\n" +
        "穿戴后**永久**提供属性加成，且在本局内随波次/塔层成长。\n\n" +
        "<color=#9BE8FF>稀有度（由低到高）：</color>\n" +
        "  <color=#6BDF66>原子</color> < <color=#5C9EFF>质子</color> < <color=#B86BFF>中子</color> < " +
        "<color=#FFCC40>电子</color> < <color=#FF4D47>无限超弦</color> < <color=#9FE1FF>奇点</color>\n" +
        "稀有度越高，主/副词条数值越强；难度越高、塔层越高，掉落越高级。\n\n" +
        "<color=#9BE8FF>六个部位：</color>头盔 / 衣服 / 靴子 / 手镯 / 项链 / 武器\n\n" +
        "<color=#9BE8FF>操作说明：</color>\n" +
        "· <color=#80FF80>一键装备</color>：自动穿上稀有度最高、主词条最大的装备\n" +
        "· <color=#80FF80>分解</color>：装备 → 材料（用于重铸）\n" +
        "· <color=#80FF80>重铸</color>：消耗材料随机重掷副词条\n" +
        "· <color=#80FF80>自动分解</color>：新掉落中自动分解劣质装备";

    private Button _equipBtn, _salvageBtn, _reforgeBtn;
    private TextMeshProUGUI _equipLabel, _salvageLabel, _reforgeLabel;

    private RectTransform _warehouseViewport;
    private RectTransform _warehouseContent;

    /// <summary>六个装备槽的格子（索引 = InheritSlot）。</summary>
    private readonly SlotCell[] _slotCells = new SlotCell[InheritEquipmentDefs.SLOT_COUNT];
    /// <summary>仓库格子池（复用，避免频繁 Instantiate/Destroy）。</summary>
    private readonly List<SlotCell> _warehouseCells = new List<SlotCell>();

    /// <summary>当前在详情区选中的装备。</summary>
    private InheritItem _selected;
    /// <summary>选中的是否为"在穿装备槽"（决定按钮显示"卸下"还是"穿戴"）。</summary>
    private bool _selectedIsEquipped;

    private bool _built;

    // ══════════════════════ 格子（装备槽 / 仓库共用）══════════════════════

    /// <summary>一个装备格子：底框 + 稀有度边框 + 图标 + 角标（在穿标记）。</summary>
    private class SlotCell
    {
        public GameObject go;
        public Image bg;
        public Image border;
        public Image icon;
        public TextMeshProUGUI tag;// 左上角小字：槽位名 或 "E"（已装备）
        public Button button;
        public InheritItem item;
        public InheritSlot slot;
        public bool isEquipSlot;
    }

    // ══════════════════════ 生命周期 ══════════════════════

    private void OnEnable()
    {
        InheritEquipmentManager.Ensure();
        EnsureFullRect();      // 每次显示都自愈一次（父级布局组可能被别处重新启用）
        EnsureBuilt();
        Subscribe();
        Refresh();
        // 【2026-08】切到继承装备 tab 时，让存档界面的右侧"请选择装备"信息板整体隐藏。
        // 继承装备有自己的详情区（见 BuildDetailPanel），不再需要那个通用空状态提示。
        NotifyArchiveManager(true);
        // 首帧 RectTransform.rect 可能还没被 Canvas 算好（宽度读到 0），
        // 那样仓库格子会退回估算尺寸。延迟一帧再排一次，保证严格贴合视口。
        StartCoroutine(RelayoutNextFrame());
    }

    private System.Collections.IEnumerator RelayoutNextFrame()
    {
        yield return null;
        if (!_built) yield break;
        EnsureFullRect();
        var m = InheritEquipmentManager.Instance;
        if (m != null) LayoutWarehouseCells(m.GetWarehouse().Count);
    }

    private void OnDisable()
    {
        Unsubscribe();
        // 离开继承装备 tab 时恢复右侧信息板（防止切回其他 tab 看到空板）。
        NotifyArchiveManager(false);
        // 顺带隐藏系统说明 tooltip（防切 tab 后它孤悬在 OverlayLayer 上）。
        if (_infoTooltip != null) _infoTooltip.SetActive(false);
    }
    private void OnDestroy()
    {
        Unsubscribe();
        if (_infoTooltip != null) _infoTooltip.SetActive(false);
    }

    /// <summary>
    /// 通知 <see cref="ArchiveManager"/> 切到 / 离开「继承装备」tab。
    /// 用 FindObjectOfType 而不是序列化引用，是因为这个面板是运行时挂到空容器上的，
    /// 跟 ArchiveManager 的关系只在 UI 启用时短暂出现，没必要把引用写进场景。
    /// </summary>
    private static ArchiveManager _amCache;
    private static void NotifyArchiveManager(bool inheritMode)
    {
        if (_amCache == null) _amCache = Object.FindObjectOfType<ArchiveManager>(true);
        if (_amCache != null) _amCache.SetInheritEquipmentMode(inheritMode);
    }

    private void Subscribe()
    {
        var m = InheritEquipmentManager.Instance;
        if (m == null) return;
        m.OnChanged -= Refresh;
        m.OnChanged += Refresh;
    }

    private void Unsubscribe()
    {
        var m = InheritEquipmentManager.Instance;
        if (m != null) m.OnChanged -= Refresh;
    }

    // ══════════════════════ 铺满 & 尺寸自适应 ══════════════════════

    /// <summary>
    /// 关掉一个 RectTransform 上的自动布局组件。
    ///
    /// 存档界面 5 个分类容器互为复制体，都挂着 HorizontalLayoutGroup
    /// （childControlWidth/Height = 0），那是给 EquipmentIcon 图标横排用的。
    /// LayoutGroup 会强行把子物体 anchor 改成 (0,1) 并只写 anchoredPosition、
    /// **不写 size** —— 于是"anchor 铺满 + sizeDelta=0"的整块面板被压成 0×0，
    /// 界面上所有文字溢出成一条竖线。继承装备是整块自绘面板，直接禁用。
    /// </summary>
    public static void DisableParentAutoLayout(RectTransform container)
    {
        if (container == null) return;
        foreach (var g in container.GetComponents<LayoutGroup>())
        if (g != null && g.enabled) g.enabled = false;
        foreach (var f in container.GetComponents<ContentSizeFitter>())
        if (f != null && f.enabled) f.enabled = false;
    }

    /// <summary>把自身强制铺满父容器（留一圈 <see cref="PANEL_PADDING"/> 避开黑框装饰边）。</summary>
    private void EnsureFullRect()
    {
        var rt = transform as RectTransform;
        if (rt == null) return;

        DisableParentAutoLayout(rt.parent as RectTransform);

        // 双保险：即使 LayoutGroup 又被谁启用，ignoreLayout 也能让本面板免于被接管
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.pivot  = new Vector2(0.5f, 0.5f);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2( PANEL_PADDING,  PANEL_PADDING);
        rt.offsetMax = new Vector2(-PANEL_PADDING, -PANEL_PADDING);
        rt.anchoredPosition = Vector2.zero;
    }

    /// <summary>面板自身的可用尺寸（首帧 rect 可能还没算好，退回用父容器尺寸推导）。</summary>
    private Vector2 PanelSize()
    {
        var rt = transform as RectTransform;
        Vector2 s = rt != null ? rt.rect.size : Vector2.zero;
        if (s.x < 1f || s.y < 1f)
        {
            var p = rt != null ? rt.parent as RectTransform : null;
            if (p != null) s = p.rect.size - new Vector2(PANEL_PADDING * 2f, PANEL_PADDING * 2f);
        }
        if (s.x < 1f || s.y < 1f) s = new Vector2(990f, 810f);   // 兜底：容器 1030×851 减留白
        return s;
    }

    /// <summary>按容器尺寸推导顶栏高 / 格子边长 / 基准字号。</summary>
    private void ComputeMetrics()
    {
        Vector2 s = PanelSize();

        _topBarH = Mathf.Clamp(s.y * 0.075f, 44f, 78f);
        _fs      = Mathf.Clamp(s.y * 0.026f, 14f, 30f);

        // 左栏：三行装备槽，槽边长取"栏宽的 30%"与"栏高的 22%"较小者
        float leftW = s.x * LEFT_PANEL_RATIO;
        float leftH = s.y - _topBarH;
        _slotSize = Mathf.Clamp(Mathf.Min(leftW * 0.30f, leftH * 0.22f), 56f, 160f);

        // 右栏：仓库按列数把宽度均分（正好排满，不溢出也不留大片空白）
        float rightW = s.x * (1f - LEFT_PANEL_RATIO) - 20f;
        _cellSize = Mathf.Max(40f, (rightW - (WAREHOUSE_COLS + 1) * CELL_GAP) / WAREHOUSE_COLS);
    }

    // ══════════════════════ 构建 ══════════════════════

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        _root = transform as RectTransform;
        if (_root == null)
        {
            Debug.LogError("[Inherit] UI 根节点不是 RectTransform，无法构建");
            return;
        }

        ComputeMetrics();

        BuildTopBar();
        BuildEquipPanel();
        BuildWarehousePanel();
        BuildDetailPanel();
    }

    // ── 顶栏：材料 / 自动分解开关 / 一键分解 ──
    private void BuildTopBar()
    {
        var bar = NewRect("TopBar", _root);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.sizeDelta = new Vector2(0f, _topBarH);
        bar.anchoredPosition = Vector2.zero;

        _materialText = NewText("MaterialText", bar, _fs * 1.15f, TextAlignmentOptions.MidlineLeft);
        var mt = _materialText.rectTransform;
        mt.anchorMin = new Vector2(0f, 0f); mt.anchorMax = new Vector2(0.22f, 1f);
        mt.offsetMin = new Vector2(10f, 0f); mt.offsetMax = Vector2.zero;
        _materialText.enableWordWrapping = false;

        // 系统说明 "i" 按钮：悬停弹出继承装备系统介绍（仿 SkinChanger / GachaUI 的 "i"）。
        // 缩小材料文本宽度（0.24→0.22），在它右侧塞一个小的圆形提示钮。
        BuildInfoButton(bar);

        // 一键装备：为6 个槽位各挑稀有度最高、主词条最大的那件穿上
        _equipBestBtn = NewButton("EquipBestBtn", bar, out var ebl, "一键装备");
        var eb = _equipBestBtn.transform as RectTransform;
        eb.anchorMin = new Vector2(0.25f, 0.14f); eb.anchorMax = new Vector2(0.48f, 0.86f);
        eb.offsetMin = Vector2.zero; eb.offsetMax = Vector2.zero;
        var ebImg = _equipBestBtn.GetComponent<Image>();
        if (ebImg != null) ebImg.color = new Color(0.16f, 0.30f, 0.20f, 0.95f);  // 绿：正向操作
        _equipBestBtn.onClick.AddListener(() =>
        {
            var m = InheritEquipmentManager.Instance;
            if (m == null) return;
            int n = m.EquipBestAll();
            ToastManager.Show(n > 0
                ? $"<color=#80FF80>一键装备完成，换上 {n} 件</color>"
                : "<color=#999999>当前已是最优配置</color>");
        });

        _autoBtn = NewButton("AutoBtn", bar, out _autoBtnLabel, "自动分解: 关");
        var ab = _autoBtn.transform as RectTransform;
        ab.anchorMin = new Vector2(0.50f, 0.14f); ab.anchorMax = new Vector2(0.73f, 0.86f);
        ab.offsetMin = Vector2.zero; ab.offsetMax = Vector2.zero;
        _autoBtn.onClick.AddListener(() =>
        {
            var m = InheritEquipmentManager.Instance;
            if (m == null) return;
            m.AutoSalvage = !m.AutoSalvage;
        });

        _salvageAllBtn = NewButton("SalvageAllBtn", bar, out var sal, "一键分解劣质");
        var sb = _salvageAllBtn.transform as RectTransform;
        sb.anchorMin = new Vector2(0.75f, 0.14f); sb.anchorMax = new Vector2(1f, 0.86f);
        sb.offsetMin = Vector2.zero; sb.offsetMax = Vector2.zero;
        _salvageAllBtn.onClick.AddListener(() =>
        {
            var m = InheritEquipmentManager.Instance;
            if (m == null) return;
            int gain = m.SalvageAllInferior();
            ToastManager.Show(gain > 0
            ? $"<color=#9BE8FF>一键分解完成，材料 +{gain}</color>"
            : "<color=#999999>没有可分解的劣质装备</color>");
        });
    }

    /// <summary>顶栏 "i" 说明按钮：鼠标悬停弹出系统介绍，移开消失。</summary>
    private void BuildInfoButton(RectTransform bar)
    {
        var go = NewRect("InfoBtn", bar);
        go.anchorMin = new Vector2(0.225f, 0.22f);
        go.anchorMax = new Vector2(0.245f, 0.78f);
        go.offsetMin = Vector2.zero; go.offsetMax = Vector2.zero;

        var img = go.gameObject.AddComponent<Image>();
        img.color = new Color(0.22f, 0.25f, 0.40f, 1f);
        img.raycastTarget = true;

        var btn = go.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        var t = NewText("i", go, _fs * 0.85f, TextAlignmentOptions.Center);
        Stretch(t.rectTransform);
        t.text = "i";
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(1f, 0.92f, 0.4f, 1f);
        t.raycastTarget = false;

        var trigger = go.gameObject.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowInfoTooltip(true));
        trigger.triggers.Add(enter);
        var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => ShowInfoTooltip(false));
        trigger.triggers.Add(exit);
    }

    /// <summary>显示/隐藏系统说明 tooltip。</summary>
    private void ShowInfoTooltip(bool show)
    {
        if (show)
        {
            EnsureInfoTooltip();
            if (_infoTooltip != null)
            {
                _infoTooltip.SetActive(true);
                _infoTooltip.transform.SetAsLastSibling();
            }
        }
        else
        {
            if (_infoTooltip != null) _infoTooltip.SetActive(false);
        }
    }

    /// <summary>
    /// 创建系统说明 tooltip。
    /// 必须挂到 OverlayLayer（sortingOrder=10000）：继承装备 UI 在存档面板（主菜单 Canvas）内，
    /// 若 tooltip 也挂在主菜单 Canvas 下，会被存档面板的深色底板/装饰边盖住，或与其它面板层级冲突。
    /// </summary>
    private void EnsureInfoTooltip()
    {
        if (_infoTooltip != null) return;

        Transform overlay = UIOverlayLayer.Get();
        Transform parent = overlay != null ? overlay : transform;

        var go = new GameObject("InheritInfoTooltip", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620f, 430f);
        rt.anchoredPosition = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.12f, 0.97f);
        bg.raycastTarget = false;

        _infoTooltipText = NewText("Content", rt, _fs * 0.8f, TextAlignmentOptions.TopLeft);
        var tr = _infoTooltipText.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(20f, 16f); tr.offsetMax = new Vector2(-20f, -16f);
        _infoTooltipText.text = InheritSystemInfoText;
        _infoTooltipText.enableAutoSizing = true;
        _infoTooltipText.fontSizeMin = 12f;
        _infoTooltipText.fontSizeMax = _fs;
        _infoTooltipText.lineSpacing = 2f;

        _infoTooltip = go;
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    // ── 左侧：人形轮廓 + 六个装备槽（左三右三）──
    private void BuildEquipPanel()
    {
        var panel = NewRect("EquipPanel", _root);
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(LEFT_PANEL_RATIO, 1f);
        panel.offsetMin = new Vector2(0f, 0f);
        panel.offsetMax = new Vector2(-6f, -_topBarH - 6f);

        var bg = NewImage("BG", panel);
        Stretch(bg.rectTransform);
        // 【2026-08】原来是近乎纯黑（0.05/0.06/0.10），暗色装备图标几乎看不见。整体提亮。
        bg.color = new Color(0.13f, 0.15f, 0.22f, 0.55f);
        bg.raycastTarget = false;

        // 人形轮廓底图（居中，稍微压暗以免抢过装备图标）
        var sil = NewImage("Silhouette", panel);
        sil.sprite = InheritEquipmentAssets.Silhouette();
        sil.color = new Color(1f, 1f, 1f, 0.30f);
        sil.preserveAspect = true;
        sil.raycastTarget = false;
        // 没有轮廓图时必须关掉：Image 无 sprite 会渲染成实心矩形，反而盖住整块左栏
        sil.enabled = sil.sprite != null;
        var sr = sil.rectTransform;
        sr.anchorMin = new Vector2(0.32f, 0.22f);
        sr.anchorMax = new Vector2(0.68f, 0.96f);
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;

        // 六个槽位：左列 头盔/衣服/靴子，右列 手镯/项链/武器（策划案第 5 条的顺序）
        InheritSlot[] leftCol  = { InheritSlot.Helmet,   InheritSlot.Armor,    InheritSlot.Boots  };
        InheritSlot[] rightCol = { InheritSlot.Bracelet, InheritSlot.Necklace, InheritSlot.Weapon };

        for (int i = 0; i < 3; i++)
        {
            _slotCells[(int)leftCol[i]]  = BuildSlotCell(panel, leftCol[i],  true,  i);
            _slotCells[(int)rightCol[i]] = BuildSlotCell(panel, rightCol[i], false, i);
        }

        // 底部总加成（占 0~0.19 高；最低一排槽底边在 0.24 以上，不会压字）
        _totalsText = NewText("TotalsText", panel, _fs, TextAlignmentOptions.Top);
        var tt = _totalsText.rectTransform;
        tt.anchorMin = new Vector2(0f, 0f); tt.anchorMax = new Vector2(1f, 0.19f);
        tt.offsetMin = new Vector2(6f, 4f); tt.offsetMax = new Vector2(-6f, 0f);
        _totalsText.enableAutoSizing = true;
        _totalsText.fontSizeMin = _fs * 0.65f;
        _totalsText.fontSizeMax = _fs;
        _totalsText.lineSpacing = 4f;
    }

    /// <summary>创建一个装备槽格子。row 0/1/2 从上到下。</summary>
    private SlotCell BuildSlotCell(RectTransform parent, InheritSlot slot, bool left, int row)
    {
        var cell = new SlotCell { slot = slot, isEquipSlot = true };

        var rt = NewRect($"Slot_{slot}", parent);
        // 左列 x 中心 0.17，右列 0.83；纵向 0.84 / 0.58 / 0.32
        float cx = left ? 0.17f : 0.83f;
        float cy = 0.84f - row * 0.26f;
        rt.anchorMin = rt.anchorMax = new Vector2(cx, cy);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(_slotSize, _slotSize);
        rt.anchoredPosition = Vector2.zero;
        cell.go = rt.gameObject;

        BuildCellVisual(cell, rt, _fs * 0.68f);
        return cell;
    }

    /// <summary>格子的公共视觉部分：底框 + 图标 + 边框 + 角标 + 点击。</summary>
    private void BuildCellVisual(SlotCell cell, RectTransform rt, float tagFontSize)
    {
        cell.bg = NewImage("BG", rt);
        Stretch(cell.bg.rectTransform);
        // 提亮格子底色：暗红/暗紫的装备图标在近黑底上完全看不清（玩家反馈）
        cell.bg.color = new Color(0.20f, 0.22f, 0.31f, 0.92f);

        cell.icon = NewImage("Icon", rt);
        var ir = cell.icon.rectTransform;
        ir.anchorMin = new Vector2(0.12f, 0.12f); ir.anchorMax = new Vector2(0.88f, 0.88f);
        ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        cell.icon.preserveAspect = true;
        cell.icon.raycastTarget = false;

        // 边框放在图标之后创建 → 渲染在图标之上（中空所以不挡）
        cell.border = NewImage("Border", rt);
        Stretch(cell.border.rectTransform);
        cell.border.type = Image.Type.Sliced;
        cell.border.raycastTarget = false;

        cell.tag = NewText("Tag", rt, tagFontSize, TextAlignmentOptions.TopLeft);
        var gr = cell.tag.rectTransform;
        gr.anchorMin = new Vector2(0f, 0f); gr.anchorMax = new Vector2(1f, 1f);
        gr.offsetMin = new Vector2(4f, 2f); gr.offsetMax = new Vector2(-4f, -2f);
        cell.tag.raycastTarget = false;

        cell.button = rt.gameObject.AddComponent<Button>();
        cell.button.targetGraphic = cell.bg;
        var captured = cell;
        cell.button.onClick.AddListener(() => OnCellClicked(captured));
    }

    // ── 右上：仓库网格（可滚动）──
    private void BuildWarehousePanel()
    {
        var panel = NewRect("WarehousePanel", _root);
        panel.anchorMin = new Vector2(LEFT_PANEL_RATIO, DETAIL_H_RATIO);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.offsetMin = new Vector2(6f, 6f);
        panel.offsetMax = new Vector2(0f, -_topBarH - 6f);

        float titleH = Mathf.Max(24f, _fs * 1.6f);

        _warehouseTitle = NewText("WarehouseTitle", panel, _fs, TextAlignmentOptions.MidlineLeft);
        var wt = _warehouseTitle.rectTransform;
        wt.anchorMin = new Vector2(0f, 1f); wt.anchorMax = new Vector2(1f, 1f);
        wt.pivot = new Vector2(0.5f, 1f);
        wt.sizeDelta = new Vector2(0f, titleH);
        wt.anchoredPosition = Vector2.zero;
        wt.offsetMin = new Vector2(4f, wt.offsetMin.y);
        _warehouseTitle.enableWordWrapping = false;

        // ScrollRect：仓库可能有上百件
        _warehouseViewport = NewRect("Viewport", panel);
        _warehouseViewport.anchorMin = new Vector2(0f, 0f);
        _warehouseViewport.anchorMax = new Vector2(1f, 1f);
        _warehouseViewport.offsetMin = Vector2.zero;
        _warehouseViewport.offsetMax = new Vector2(0f, -titleH - 2f);
        var vpImg = _warehouseViewport.gameObject.AddComponent<Image>();
        vpImg.color = new Color(0.11f, 0.13f, 0.19f, 0.60f);
        _warehouseViewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        _warehouseContent = NewRect("Content", _warehouseViewport);
        _warehouseContent.anchorMin = new Vector2(0f, 1f);
        _warehouseContent.anchorMax = new Vector2(1f, 1f);
        _warehouseContent.pivot = new Vector2(0.5f, 1f);
        _warehouseContent.offsetMin = new Vector2(0f, 0f);
        _warehouseContent.offsetMax = new Vector2(0f, 0f);
        _warehouseContent.sizeDelta = new Vector2(0f, 100f);
        _warehouseContent.anchoredPosition = Vector2.zero;

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.content = _warehouseContent;
        scroll.viewport = _warehouseViewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 30f;
        scroll.movementType = ScrollRect.MovementType.Clamped;
    }

    // ── 右下：详情 + 操作按钮 ──
    private void BuildDetailPanel()
    {
        var panel = NewRect("DetailPanel", _root);
        panel.anchorMin = new Vector2(LEFT_PANEL_RATIO, 0f);
        panel.anchorMax = new Vector2(1f, DETAIL_H_RATIO);
        panel.offsetMin = new Vector2(6f, 0f);
        panel.offsetMax = new Vector2(0f, -4f);

        var bg = NewImage("BG", panel);
        Stretch(bg.rectTransform);
        bg.color = new Color(0.06f, 0.07f, 0.12f, 0.80f);
        bg.raycastTarget = false;

        _detailText = NewText("DetailText", panel, _fs, TextAlignmentOptions.TopLeft);
        var dt = _detailText.rectTransform;
        dt.anchorMin = new Vector2(0f, 0.32f); dt.anchorMax = new Vector2(1f, 1f);
        dt.offsetMin = new Vector2(12f, 4f); dt.offsetMax = new Vector2(-12f, -8f);
        _detailText.enableAutoSizing = true;
        _detailText.fontSizeMin = _fs * 0.72f;
        _detailText.fontSizeMax = _fs * 1.1f;
        _detailText.lineSpacing = 4f;

        _equipBtn   = NewButton("EquipBtn", panel, out _equipLabel,   "穿戴");
        _salvageBtn = NewButton("SalvageBtn", panel, out _salvageLabel, "分解");
        _reforgeBtn = NewButton("ReforgeBtn", panel, out _reforgeLabel, "重铸");

        PlaceBtn(_equipBtn,   0.03f, 0.32f);
        PlaceBtn(_salvageBtn, 0.355f, 0.645f);
        PlaceBtn(_reforgeBtn, 0.68f, 0.97f);

        _equipBtn.onClick.AddListener(OnEquipClicked);
        _salvageBtn.onClick.AddListener(OnSalvageClicked);
        _reforgeBtn.onClick.AddListener(OnReforgeClicked);
    }

    private static void PlaceBtn(Button b, float xMin, float xMax)
    {
        var rt = b.transform as RectTransform;
        rt.anchorMin = new Vector2(xMin, 0.06f);
        rt.anchorMax = new Vector2(xMax, 0.28f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ══════════════════════ 刷新 ══════════════════════

    private void Refresh()
    {
        var m = InheritEquipmentManager.Instance;
        if (m == null || !_built) return;

        // 顶栏
        if (_materialText != null)
        _materialText.text = $"<color=#9BE8FF>重铸材料</color>  <b>{m.Materials}</b>";
        if (_autoBtnLabel != null)
        _autoBtnLabel.text = m.AutoSalvage
        ? "<color=#80FF80>自动分解: 开</color>"
        : "<color=#BBBBBB>自动分解: 关</color>";

        // 装备槽
        for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
        {
            var cell = _slotCells[s];
            if (cell == null) continue;
            BindCell(cell, m.GetEquipped((InheritSlot)s), (InheritSlot)s, true);
        }

        // 总加成
        if (_totalsText != null)
        {
            m.GetTotals(out float a, out float d, out int hp, out float cr, out float cd, out float ev);
            _totalsText.text =
            "<color=#FFD24A>全套加成</color>\n" +
            $"<color=#FF8080>攻击 +{a:0.##}</color>  " +
            $"<color=#80C0FF>防御 +{d:0.##}</color>  " +
            $"<color=#80FF80>血量 +{hp}</color>\n" +
            $"<color=#FFC24A>暴击 +{cr:0.##}%</color>  " +
            $"<color=#FF80C0>暴伤 +{cd:0.##}%</color>  " +
            $"<color=#40E0D0>闪避 +{ev:0.##}%</color>";
        }

        RefreshWarehouse(m);
        RefreshDetail(m);
    }

    private void RefreshWarehouse(InheritEquipmentManager m)
    {
        var list = m.GetWarehouse();

        if (_warehouseTitle != null)
        _warehouseTitle.text = $"<color=#C0C0FF>仓库</color>  " +
        $"{list.Count}/{InheritEquipmentManager.WAREHOUSE_CAP}";

        // 扩容格子池
        while (_warehouseCells.Count < list.Count)
        _warehouseCells.Add(BuildWarehouseCell(_warehouseCells.Count));

        for (int i = 0; i < _warehouseCells.Count; i++)
        {
            var cell = _warehouseCells[i];
            bool used = i < list.Count;
            if (cell.go != null) cell.go.SetActive(used);
            if (used) BindCell(cell, list[i], list[i].slot, false);
        }

        LayoutWarehouseCells(list.Count);
    }

    /// <summary>
    /// 按当前视口宽度重排仓库格子。
    /// 每次刷新都算一遍，这样窗口分辨率变化 / 容器尺寸不同都能自适应，
    /// 不会出现"格子溢出视口"或"右边留一大片空白"。
    /// </summary>
    private void LayoutWarehouseCells(int count)
    {
        if (_warehouseContent == null) return;

        float w = _warehouseViewport != null ? _warehouseViewport.rect.width : 0f;
        float cs = w > 10f
        ? Mathf.Max(36f, (w - (WAREHOUSE_COLS + 1) * CELL_GAP) / WAREHOUSE_COLS)
        : _cellSize;

        for (int i = 0; i < _warehouseCells.Count; i++)
        {
            var rt = _warehouseCells[i].go != null
            ? _warehouseCells[i].go.transform as RectTransform : null;
            if (rt == null) continue;

            int col = i % WAREHOUSE_COLS, row = i / WAREHOUSE_COLS;
            rt.sizeDelta = new Vector2(cs, cs);
            rt.anchoredPosition = new Vector2(
            CELL_GAP + col * (cs + CELL_GAP),
            -(CELL_GAP + row * (cs + CELL_GAP)));
        }

        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)WAREHOUSE_COLS));
        _warehouseContent.sizeDelta = new Vector2(0f, rows * (cs + CELL_GAP) + CELL_GAP);
    }

    private SlotCell BuildWarehouseCell(int index)
    {
        var cell = new SlotCell { isEquipSlot = false };

        var rt = NewRect($"WCell_{index}", _warehouseContent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(_cellSize, _cellSize);
        cell.go = rt.gameObject;

        BuildCellVisual(cell, rt, _fs * 0.62f);
        cell.bg.color = new Color(0.23f, 0.25f, 0.34f, 0.95f);
        return cell;
    }

    /// <summary>把一件装备（可为 null = 空槽）绑定到格子上。</summary>
    private void BindCell(SlotCell cell, InheritItem item, InheritSlot slot, bool isEquipSlot)
    {
        cell.item = item;
        cell.slot = slot;
        cell.isEquipSlot = isEquipSlot;

        if (item == null)
        {
            if (cell.icon != null) { cell.icon.sprite = null; cell.icon.enabled = false; }
            if (cell.border != null)
            {
                cell.border.sprite = InheritEquipmentAssets.Border(InheritRarity.Atom);
                cell.border.color = new Color(1f, 1f, 1f, 0.12f);
                cell.border.enabled = cell.border.sprite != null;   // 无边框图就别画实心块
                RemoveCosmic(cell.border);
            }
            if (cell.tag != null)
            cell.tag.text = isEquipSlot
            ? $"<color=#666677>{InheritEquipmentDefs.SlotName(slot)}</color>"
            : "";
            return;
        }

        if (cell.icon != null)
        {
            cell.icon.sprite = InheritEquipmentAssets.Icon(item.slot, item.rarity);
            cell.icon.enabled = cell.icon.sprite != null;
        }

        if (cell.border != null)
        {
            cell.border.sprite = InheritEquipmentAssets.Border(item.rarity);
            cell.border.enabled = cell.border.sprite != null;
            if (InheritEquipmentDefs.IsCosmic(item.rarity))
            {
                // 奇点：挂星河动效组件（会持续改 color）
                if (cell.border.GetComponent<InheritRarityBorder>() == null)
                cell.border.gameObject.AddComponent<InheritRarityBorder>();
            }
            else
            {
                RemoveCosmic(cell.border);
                cell.border.color = InheritEquipmentDefs.RarityColor(item.rarity);
            }
        }

        if (cell.tag != null)
        {
            string mark = "";
            var m = InheritEquipmentManager.Instance;
            if (!isEquipSlot && m != null && m.IsEquipped(item.uid))
            mark = "<color=#80FF80>已穿 </color>";
            cell.tag.text = $"{mark}<color=#{InheritEquipmentDefs.RarityHex(item.rarity)}>" +
            $"{InheritEquipmentDefs.SlotName(item.slot)}</color>";
        }
    }

    private static void RemoveCosmic(Image img)
    {
        var c = img.GetComponent<InheritRarityBorder>();
        if (c != null) Destroy(c);
    }

    private void RefreshDetail(InheritEquipmentManager m)
    {
        if (_detailText == null) return;

        // 选中的装备可能已被分解 → 清空
        if (_selected != null)
        {
            bool stillExists = false;
            foreach (var it in m.GetWarehouse()) if (it == _selected) { stillExists = true; break; }
            if (!stillExists)
            {
                for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
                if (m.GetEquipped((InheritSlot)s) == _selected) { stillExists = true; break; }
            }
            if (!stillExists) _selected = null;
        }

        if (_selected == null)
        {
            _detailText.text = "<color=#888888>点击左侧装备槽或右上仓库中的装备查看详情。\n" +
            "· 主词条由槽位决定，数值由稀有度与当局难度共同决定\n" +
            "· 分解可获得重铸材料，重铸会重掷全部副词条\n" +
            "· 开启「自动分解」后，弱于在穿装备的掉落会直接转化为材料</color>";
            SetBtn(_equipBtn, _equipLabel,   "穿戴", false);
            SetBtn(_salvageBtn, _salvageLabel, "分解", false);
            SetBtn(_reforgeBtn, _reforgeLabel, "重铸", false);
            return;
        }

        var it2 = _selected;
        bool equipped = m.IsEquipped(it2.uid);
        _selectedIsEquipped = equipped;

        string hex = InheritEquipmentDefs.RarityHex(it2.rarity);
        var sb = new System.Text.StringBuilder(384);
        sb.Append($"<color=#{hex}><b>{it2.DisplayName}</b></color>");
        if (equipped) sb.Append("  <color=#80FF80>(已装备)</color>");
        sb.Append('\n');
        sb.Append($"<color=#AAAAAA>{InheritEquipmentDefs.SlotName(it2.slot)} · " +
        $"{InheritEquipmentDefs.RarityName(it2.rarity)}</color>\n");
        sb.Append($"<color=#FFD24A>主词条</color>  " +
        $"{InheritEquipmentDefs.FormatStatLine(it2.mainStat, it2.mainValue)}\n");

        if (it2.subStats != null && it2.subStats.Count > 0)
        {
            sb.Append("<color=#80FFC0>副词条</color>  ");
            for (int i = 0; i < it2.subStats.Count; i++)
            {
                var sub = it2.subStats[i];
                if (sub == null) continue;
                sb.Append(InheritEquipmentDefs.FormatStatLine(sub.stat, sub.value));
                if (i < it2.subStats.Count - 1) sb.Append("  /  ");
            }
            sb.Append('\n');
        }

        int salvage = InheritEquipmentGenerator.SalvageValue(it2);
        int reforge = InheritEquipmentGenerator.ReforgeCost(it2);
        sb.Append($"<color=#888888>已重铸 {it2.reforgeCount} 次 · 分解可得 {salvage} 材料</color>");

        _detailText.text = sb.ToString();

        SetBtn(_equipBtn, _equipLabel, equipped ? "卸下" : "穿戴", true);
        SetBtn(_salvageBtn, _salvageLabel, $"分解(+{salvage})", true);
        SetBtn(_reforgeBtn, _reforgeLabel,
        $"重铸 (-{reforge})", m.Materials >= reforge);
    }

    private static void SetBtn(Button b, TextMeshProUGUI label, string text, bool enabled)
    {
        if (b != null) b.interactable = enabled;
        if (label != null)
        label.text = enabled ? text : $"<color=#777777>{text}</color>";
    }

    // ══════════════════════ 交互 ══════════════════════

    private void OnCellClicked(SlotCell cell)
    {
        if (cell == null) return;
        _selected = cell.item;
        var m = InheritEquipmentManager.Instance;
        if (m != null) RefreshDetail(m);
    }

    private void OnEquipClicked()
    {
        var m = InheritEquipmentManager.Instance;
        if (m == null || _selected == null) return;

        if (_selectedIsEquipped) m.Unequip(_selected.slot);
        else m.Equip(_selected);
        // Refresh 由 OnChanged 事件触发
    }

    private void OnSalvageClicked()
    {
        var m = InheritEquipmentManager.Instance;
        if (m == null || _selected == null) return;

        string name = _selected.DisplayName;
        int gain = m.Salvage(_selected);
        _selected = null;
        ToastManager.Show($"<color=#9BE8FF>已分解 {name} → 材料 +{gain}</color>");
    }

    private void OnReforgeClicked()
    {
        var m = InheritEquipmentManager.Instance;
        if (m == null || _selected == null) return;

        int cost = InheritEquipmentGenerator.ReforgeCost(_selected);
        if (!m.Reforge(_selected))
        {
            ToastManager.Show($"<color=#FF8080>材料不足（需要 {cost}）</color>");
            return;
        }
        ToastManager.Show($"<color=#C080FF>重铸完成，消耗 {cost} 材料</color>");
    }

    // ══════════════════════ UI 构建小工具 ══════════════════════

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static Image NewImage(string name, Transform parent)
    {
        var rt = NewRect(name, parent);
        return rt.gameObject.AddComponent<Image>();
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, float size,
    TextAlignmentOptions align)
    {
        var rt = NewRect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        var f = ResolveChineseFont();
        if (f != null) t.font = f;
        return t;
    }

    /// <summary>
    /// 解析中文字体 —— 统一走<see cref="InheritEquipmentAssets.ChineseFont"/>，
    /// 与局内掉落展示（InheritDropDisplay）共用同一份缓存，避免两处各写一套回退逻辑。
    /// </summary>
    private static TMP_FontAsset ResolveChineseFont() => InheritEquipmentAssets.ChineseFont();

    private static Button NewButton(string name, Transform parent,
    out TextMeshProUGUI label, string text)
    {
        var rt = NewRect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.16f, 0.17f, 0.28f, 1f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        label = NewText("Label", rt, 18f, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        label.text = text;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f; label.fontSizeMax = 22f;

        return btn;
    }
}
