using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;

public class EquipmentIcon : MonoBehaviour
{
    [Header("装备信息")]
    public EquipmentType equipmentType = EquipmentType.ClearEquipment;
    public int    equipmentId   = 0;
    public string equipmentName = "装备名称";
    [TextArea(2, 3)] public string description = "装备描述";
    [TextArea(1, 2)] public string howToGet    = "获得方法";

    [Header("颜色设置")]
    public Color unlockedColor = Color.white;
    public Color lockedColor   = Color.gray;

    [Header("组件引用")]
    [SerializeField] private Image  iconImage;
    [SerializeField] private Button button;

    [Header("叠加数量显示（R/SR抽卡装备用，可选）")]
    public TextMeshProUGUI countText;
    public GachaRarity     gachaRarity = GachaRarity.R; // 设置该图标对应的稀有度

    [Header("调试选项")]
    public bool enableDebugLogs = false;

    public Action<EquipmentType, int, EquipmentIcon> onClickCallback;

    private const string KEY_SPORE_MUTATION_ENABLED = "SporeMutationEnabled";

    private bool            isInitialized  = false;
    private EquipmentSystem equipmentSystem;
    private Button          sporeToggleButton;
    private TextMeshProUGUI sporeToggleText;

    private void Start()    => Initialize();
    private void OnEnable() => UpdateDisplay();

    /// <summary>
    /// 【仅供 Editor / ArchiveManager 静态化工具使用】
    /// 在不依赖运行时（Start / OnEnable）的情况下，立刻把名字/描述/howToGet/Sprite 注入到本图标。
    ///
    /// 使用场景：在 Unity Editor 里通过 ArchiveManager 的 ContextMenu 静态生成抽卡图标后，
    /// 需要把"自动注入文本与 Sprite"立刻同步进 SerializedField，保存场景后这些字段就被持久化。
    /// 否则克隆出来的 GameObject 上 equipmentName / description / howToGet / iconImage.sprite
    /// 都还是模板内容（或空），重新打开场景或运行游戏才会被 Start 注入。
    /// </summary>
    public void EditorApplyForcedOverrides()
    {
        if (iconImage == null) iconImage = GetComponentInChildren<Image>(true);
        ApplyForcedAchievementOverrides();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (button == null) button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>();

        ApplyForcedAchievementOverrides();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnButtonClicked);

        equipmentSystem = EquipmentSystem.Instance;
        SetupSporeMutationToggle();
        UpdateDisplay();
        isInitialized = true;
    }

    private void OnButtonClicked()
    {
        if (enableDebugLogs) Debug.Log($"点击装备图标: {equipmentType}_{equipmentId}");
        onClickCallback?.Invoke(equipmentType, equipmentId, this);
    }

    private void ApplyForcedAchievementOverrides()
    {
        // 同时把抽卡 SSR 8 / 9（新增的「我与我与我」/「三清化一」）的文本兜底
        // 写在这里，避免场景里手工漏配。即使该图标根本没在场景里挂出来，UI 显示也不报错。
        ApplyForcedGachaSsrOverrides();

        // UR 抽卡装备的图标 / 文本兜底（目前仅 UR_2 亡者领域）：
        // 场景里 UR_2 GameObject 的子 Image.m_Sprite 历史上是 Unity 内置占位（guid 0000…f0000），
        // 从未挂入真正的 UR/002.png；这里在 Start 时主动注入 sprite + 文本，
        // 与 SSR 8/9 同样套路。改动彻底不依赖场景手工拖拽。
        ApplyForcedGachaUrOverrides();

        // R / SR 新增抽卡装备的图标 + 文本兜底（R_2 读档币 / SR_6 速度灵果）。
        // 思路同上：场景里没手工拖图标节点也能正常显示。
        ApplyForcedGachaRSrOverrides();

        // N8 通关装备 18/19/20（和平之剑/甲/心）的文本兜底。
        // 这三件图标是 ArchiveManager.EnsureClearEquipmentN8IconsExist() 在运行时
        // 用 N7 EquipmentIcon 做模板克隆出来的，初始字段都被清空，
        // 必须在这里按 equipmentId 注入名字/描述/howToGet/Sprite。
        ApplyForcedClearEquipmentN8Overrides();

        if (equipmentType != EquipmentType.AchievementEquipment) return;

        if (equipmentId == 1)
        {
            equipmentName = "大手子";
            description = "初始经验石吸取距离 +30%\n每通关一个更高的难度，额外 +5%\n" +
                "（通关 N1 后 35%，N2 后 40%，N3 后 45%……）\n\n" +
                "那些闪闪发光的东西，一个也别想逃。";
            howToGet = "首次点击主页面的草";
        }
        else if (equipmentId == 3)
        {
            equipmentName = "钥匙剑";
            description = "解锁世界boss奖励\n\n不止可以开门，还可以让boss摆脱宿命，或许他们会感激你？";
            howToGet = "初次进行门挑战";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/成就装备/003钥匙剑/3.钥匙剑_最终.png");
        }
        else if (equipmentId == 7)
        {
            equipmentName = "万象天引";
            description = "每隔一分钟，将全图的经验石吸引到自己周围\n\n真是方便的能力啊";
            howToGet = "在单局内达到五十级";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/成就装备/007空白绘卷/7.万象天引.png");
        }
    }

    /// <summary>
    /// 抽卡 SSR 8 / 9 文本与图标兜底。
    /// 这两个新增 SSR 的 equipmentSystemId 是 11 / 12（避开 UR「亡者领域」占用的 10）。
    /// 在场景里手工拖图标时也只需把 equipmentType=GachaEquipment / gachaRarity=SSR / equipmentId=11或12，
    /// 文本与 icon 都会在 Start/UpdateDisplay 时被这里自动覆盖。
    /// </summary>
    private void ApplyForcedGachaSsrOverrides()
    {
        if (equipmentType != EquipmentType.GachaEquipment) return;
        if (gachaRarity != GachaRarity.SSR) return;

        if (equipmentId == 11)
        {
            equipmentName = "我与我与我";
            description = "分身事件可选择第二次，场上最多存在2个分身\n\n你不再是一个人在战斗。";
            howToGet = "累计抽卡 400 次后加入卡池";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/SSR/008.png");
        }
        else if (equipmentId == 12)
        {
            equipmentName = "三清化一";
            description = "分身位置与本体重合且隐身，只保留技能效果\n\n万法归一，独我独存。";
            howToGet = "累计抽卡 450 次后加入卡池";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/SSR/009.png");
        }
        else if (equipmentId == 13)
        {
            equipmentName = "饮血剑";
            description = "全能吸血+1（回复所有来源造成伤害 1% 的血量）\n\n剑刃饥渴，越战越凶。";
            howToGet = "累计抽卡 500 次后加入卡池";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/SSR/010.png");
        }
    }

    /// <summary>
    /// 抽卡 UR 文本与图标兜底。目前只有 UR_2「亡者领域」(equipmentSystemId=10)。
    /// 场景里 UR_2 GameObject 子节点 Image.m_Sprite 历史上一直是 Unity 内置占位 UISprite
    /// （guid 0000000000000000f000000000000000），从未引用过磁盘上的 UR/002.png；
    /// GachaItemData / GachaManager / GachaUI 都不负责存档界面图标显示，所以改它们没用。
    /// 这里在 Initialize 时无条件按 equipmentId 注入正确 sprite，跟 SSR8/9 是同一套路。
    ///
    /// 注：UR_1「地狱火」(equipmentSystemId=9) 的 sprite 在场景里已正确手工拖入，
    /// 但保留这里的代码路径也无害（等价二次覆盖），便于今后再加 UR_3+ 时统一扩展。
    /// </summary>
    private void ApplyForcedGachaUrOverrides()
    {
        if (equipmentType != EquipmentType.GachaEquipment) return;
        if (gachaRarity != GachaRarity.UR) return;

        if (equipmentId == 10)
        {
            equipmentName = "亡者领域";
            description = "解锁孢子领域 → 亡者领域 的技能进化路线\n\n" +
                "进化条件：已学习风箭 + 已学习孢子领域，且二者攻击范围 >= 15\n\n" +
                "技能效果：在孢子领域范围内复活被击杀的敌人成为友军，友军小怪死亡时回复 0.5% 最大生命值\n\n" +
                "墓园的呢喃，是给你的低语。";
            howToGet = "通关 N6 后加入卡池";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/UR/002.png");
        }
        else if (equipmentId == 9)
        {
            equipmentName = "地狱火";
            description = "解锁火球术技能进化：每轮在最近敌人头顶召唤地狱三叉戟下劈\n\n" +
                "进化条件：风箭多重 >= 2 且 火球多重 >= 2 且 已学习火球术\n\n" +
                "属性继承：\n" +
                "• 攻击次数 = 风箭多重 + 火球多重\n" +
                "• 伤害 & 冷却 = 学习瞬间继承火球术的值\n" +
                "• 持有【不忘初心】时保留火球术，数量/伤害/冷却全部实时同步火球";
            howToGet = "抽卡获得（N3 加入卡池）";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/UR/001.png");
        }
    }

    /// <summary>
    /// R / SR 新增抽卡装备的文本与图标兜底。
    ///   R_2  读档币  : equipmentType=GachaEquipment, gachaRarity=R,  equipmentId=2
    ///   SR_6 速度灵果: equipmentType=GachaEquipment, gachaRarity=SR, equipmentId=6
    /// 与 ApplyForcedGachaSsrOverrides / ApplyForcedGachaUrOverrides 同套路：
    /// 场景里只要把 EquipmentIcon 放对类型/稀有度/equipmentId，名字描述图标都会被这里覆盖。
    /// </summary>
    private void ApplyForcedGachaRSrOverrides()
    {
        if (equipmentType != EquipmentType.GachaEquipment) return;

        if (gachaRarity == GachaRarity.R && equipmentId == 2)
        {
            equipmentName = "读档币";
            description = "死亡时可消耗 1 张原地满血复活，本局只能使用 1 次\n\n这一切，仿佛只是一场可以读档重来的梦。";
            howToGet = "累计抽卡 200 次后加入卡池（每 20 抽追加 1 张）";
            // 优先用挂在 GachaManager 上的静态 Sprite（走标准 Unity 资源管线，避免运行时 LoadImage 偏色）
            if (!TrySetIconFromGachaManager(GachaRarity.R, 2))
                SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/R/002.png");
        }
        else if (gachaRarity == GachaRarity.SR && equipmentId == 6)
        {
            equipmentName = "速度灵果";
            description = "每件移动速度 +0.05（每累计 20 件 = +1 移速，满池 100 件 = +5）\n\n步履匆匆，无人能追。";
            howToGet = "累计抽卡 300 次后加入卡池（共 100 个）";
            if (!TrySetIconFromGachaManager(GachaRarity.SR, 6))
                SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/SR/6.png");
        }
    }

    /// <summary>
    /// 尝试从 GachaManager 上挂载的静态 Sprite 字段取图标。
    /// 找到则直接赋给 iconImage，返回 true；找不到（场景未配 / GachaManager 不在）返回 false 走文件路径兜底。
    /// </summary>
    private bool TrySetIconFromGachaManager(GachaRarity rarity, int rarityId)
    {
        if (iconImage == null) return false;
        var gm = GachaManager.Instance;
        if (gm == null) return false;

        Sprite sp = null;
        if (rarity == GachaRarity.R && rarityId == 2) sp = gm.reviveCoinIcon;
        else if (rarity == GachaRarity.SR && rarityId == 6) sp = gm.speedFruitIcon;
        if (sp == null) return false;

        iconImage.enabled = true;
        iconImage.material = null;
        iconImage.sprite = sp;
        iconImage.overrideSprite = sp;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
        return true;
    }

    /// <summary>
    /// N8 通关装备 18/19/20「和平之剑 / 和平之甲 / 和平之心」的文本 + 图标兜底。
    /// 这三件图标节点 **不存在于场景文件**，由 ArchiveManager.SetupEquipmentIcons() →
    /// EnsureClearEquipmentN8IconsExist() 在运行时用 N7 EquipmentIcon 做模板克隆而来，
    /// 克隆完会把字段清空，因此必须在 Initialize 时按 equipmentId 注入完整信息。
    ///
    /// 策划数值（与 EquipmentInitializer.ApplyClearEquipments 保持一致）：
    ///   18 和平之剑：攻击力 +20  （比 N7 熟练者之剑 +15 高一档，对应终局武器加成）
    ///   19 和平之甲：闪避率 +1
    ///   20 和平之心：经验效率 +3 （比 N7 熟练者之心 +1 高 2 档，对应终局养成加速）
    /// 重复获得积分：140 / 兑换积分：420（ClearRecordManager + ArchiveManager 已对齐）
    /// </summary>
    private void ApplyForcedClearEquipmentN8Overrides()
    {
        if (equipmentType != EquipmentType.ClearEquipment) return;

        if (equipmentId == 18)
        {
            equipmentName = "和平之剑";
            description = "攻击力＋20\n\n些许疲软";
            howToGet = "通关N8有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/018.png");
        }
        else if (equipmentId == 19)
        {
            equipmentName = "和平之甲";
            description = "闪避率＋1\n\n这个，不需要了";
            howToGet = "通关N8有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/019.png");
        }
        else if (equipmentId == 20)
        {
            equipmentName = "和平之心";
            description = "经验效率＋3\n\n或许，我能为这里带来和平";
            howToGet = "通关N8有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/020.png");
        }
    }

    private void SetIconFromAssetPath(string relativeToAssets)
    {
        if (iconImage == null) return;

        // 通过 RuntimeAssetLoader 走三层兜底：Inspector 引用（此处无） → Resources.Load → dataPath（仅编辑器）
        // 编辑器内能从 Application.dataPath 直接读 Assets 下的 PNG 调试；
        // 打包后这条路径不可用，但装备图标的主路径已经走 GachaManager 上挂的 Sprite 引用 + 装备 Inspector 字段，
        // 这个 SetIconFromAssetPath 仅作为最后的兜底；走不到时 iconImage 静默不显示，不会 crash。
        var tex = RuntimeAssetLoader.LoadTexture(null, null, relativeToAssets);
        if (tex == null) return;
        Sprite forcedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        iconImage.enabled = true;
        iconImage.material = null;
        iconImage.sprite = forcedSprite;
        iconImage.overrideSprite = forcedSprite;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
    }

    private void SetupSporeMutationToggle()
    {
        if (equipmentType != EquipmentType.AchievementEquipment || equipmentId != 6) return;
        if (sporeToggleButton != null) return;

        Transform existing = transform.Find("SporeMutationToggle");
        GameObject toggleObj = existing != null ? existing.gameObject : new GameObject("SporeMutationToggle");
        toggleObj.transform.SetParent(transform, false);

        RectTransform rect = toggleObj.GetComponent<RectTransform>();
        if (rect == null) rect = toggleObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 8f);
        rect.sizeDelta = new Vector2(52f, 34f);

        Image bg = toggleObj.GetComponent<Image>();
        if (bg == null) bg = toggleObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        sporeToggleButton = toggleObj.GetComponent<Button>();
        if (sporeToggleButton == null) sporeToggleButton = toggleObj.AddComponent<Button>();
        sporeToggleButton.onClick.RemoveAllListeners();
        sporeToggleButton.onClick.AddListener(ToggleSporeMutationFeature);

        Transform textTr = toggleObj.transform.Find("Text");
        GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("Text");
        textObj.transform.SetParent(toggleObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        sporeToggleText = textObj.GetComponent<TextMeshProUGUI>();
        if (sporeToggleText == null) sporeToggleText = textObj.AddComponent<TextMeshProUGUI>();
        sporeToggleText.alignment = TextAlignmentOptions.Center;
        sporeToggleText.fontSize = 18;
        sporeToggleText.fontStyle = FontStyles.Bold;
        sporeToggleText.raycastTarget = false;
    }

    private void ToggleSporeMutationFeature()
    {
        bool enabled = PlayerPrefs.GetInt(KEY_SPORE_MUTATION_ENABLED, 1) == 1;
        PlayerPrefs.SetInt(KEY_SPORE_MUTATION_ENABLED, enabled ? 0 : 1);
        PlayerPrefs.Save();
        UpdateSporeMutationToggleDisplay(IsUnlocked());
        ToastManager.Show(enabled ? "孢子异变：已关闭七彩蘑菇" : "孢子异变：已开启七彩蘑菇");
    }

    private void UpdateSporeMutationToggleDisplay(bool isUnlocked)
    {
        if (equipmentType != EquipmentType.AchievementEquipment || equipmentId != 6) return;
        SetupSporeMutationToggle();
        if (sporeToggleButton == null) return;

        sporeToggleButton.gameObject.SetActive(isUnlocked);
        if (!isUnlocked) return;

        bool enabled = PlayerPrefs.GetInt(KEY_SPORE_MUTATION_ENABLED, 1) == 1;
        if (sporeToggleText != null) sporeToggleText.text = enabled ? "开" : "关";
        Image bg = sporeToggleButton.GetComponent<Image>();
        if (bg != null) bg.color = enabled ? new Color(0.1f, 0.55f, 0.15f, 0.95f) : new Color(0.45f, 0.1f, 0.1f, 0.95f);
    }

    public void UpdateDisplay()
    {
        if (iconImage == null) return;

        ApplyForcedAchievementOverrides();

        bool isUnlocked = IsUnlocked();
        iconImage.color = isUnlocked ? unlockedColor : lockedColor;
        UpdateSporeMutationToggleDisplay(isUnlocked);

        // R/SR 抽卡装备：按稀有度单独显示叠加数量
        if (countText != null && equipmentType == EquipmentType.GachaEquipment
            && GachaManager.Instance != null
            && (gachaRarity == GachaRarity.R || gachaRarity == GachaRarity.SR))
        {
            int count = GachaManager.Instance.GetItemCount(gachaRarity, equipmentId);
            countText.text = count > 0 ? $"×{count}" : "";
            countText.gameObject.SetActive(count > 0);
        }
    }

    private bool IsUnlocked()
    {
        // R/SR 抽卡装备：有叠加数量就算解锁
        if (equipmentType == EquipmentType.GachaEquipment && GachaManager.Instance != null
            && (gachaRarity == GachaRarity.R || gachaRarity == GachaRarity.SR))
        {
            return GachaManager.Instance.GetItemCount(gachaRarity, equipmentId) > 0;
        }

        if (equipmentSystem == null) equipmentSystem = EquipmentSystem.Instance;
        if (equipmentSystem == null) equipmentSystem = FindObjectOfType<EquipmentSystem>();
        return equipmentSystem != null && equipmentSystem.IsEquipmentUnlocked(equipmentType, equipmentId);
    }

    public void SetEquipmentInfo(EquipmentType type, int id, string eName, string desc, string howToGetText)
    {
        equipmentType = type; equipmentId = id;
        equipmentName = eName; description = desc; howToGet = howToGetText;
    }

    public void SetColors(Color unlocked, Color locked)
    {
        unlockedColor = unlocked; lockedColor = locked;
        UpdateDisplay();
    }

    [ContextMenu("重新初始化")] public void Reinitialize() { isInitialized = false; Initialize(); }
    [ContextMenu("手动更新显示")] public void ManualUpdateDisplay() => UpdateDisplay();

    [ContextMenu("测试解锁装备")]
    public void TestUnlockThisEquipment()
    {
        EquipmentSystem.Instance?.UnlockEquipment(equipmentType, equipmentId);
        UpdateDisplay();
    }
}
