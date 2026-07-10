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
    private const string KEY_TRINITY_FUSION_ENABLED = "TrinityFusion.Enabled";

    private bool            isInitialized  = false;
    private EquipmentSystem equipmentSystem;
    private Button          sporeToggleButton;
    private TextMeshProUGUI sporeToggleText;
    private Button          trinityToggleButton;
    private TextMeshProUGUI trinityToggleText;

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
        SetupTrinityFusionToggle();
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

        // N4~N7 通关装备 7/10/13/16（正常铁甲/源木轻甲/蘑菇之甲/熟练者之甲）的文本覆盖。
        // 场景里这些装备的 description 可能落后于策划最新数值，这里强制同步。
        ApplyForcedClearEquipmentN4toN7Overrides();

        // N8 通关装备 18/19/20（和平之剑/甲/心）的文本兜底。
        // 这三件图标是 ArchiveManager.EnsureClearEquipmentN8IconsExist() 在运行时
        // 用 N7 EquipmentIcon 做模板克隆出来的，初始字段都被清空，
        // 必须在这里按 equipmentId 注入名字/描述/howToGet/Sprite。
        ApplyForcedClearEquipmentN8Overrides();

        // N9~N13 通关装备 21~35（利爪/月牙/粘液/暗影/龙鳞 系列）的文本 + 图标兜底。
        // 这十五件图标由 ArchiveManager.EnsureClearEquipmentN9toN13IconsExist() 在运行时克隆，
        // 必须在这里按 equipmentId 注入完整信息。
        ApplyForcedClearEquipmentN9toN13Overrides();

        if (equipmentType != EquipmentType.AchievementEquipment) return;

        if (equipmentId == 1)
        {
            equipmentName = "大手子";
            description = "初始经验石吸取距离 +50%\n每通关一个更高的难度，额外 +10%\n" +
                "（通关 N1 后 60%，N2 后 70%，N3 后 80%……）\n\n" +
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
        else if (equipmentId == 4)
        {
            equipmentName = "沙漏";
            description = "解锁三倍速\n\n无限流玩家的必需品，血族血脉的最爱。";
            howToGet = "累计游玩 30 分钟游戏";
        }
        else if (equipmentId == 7)
        {
            equipmentName = "万象天引";
            description = "每隔一分钟，将全图的经验石吸引到自己周围\n\n真是方便的能力啊";
            howToGet = "在单局内达到五十级";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/成就装备/007空白绘卷/7.万象天引.png");
        }
        else if (equipmentId == 8)
        {
            equipmentName = "不可视之手";
            description = "解锁自动选取升级功能\n\n有一只手在帮你选择升级，还不快谢他";
            howToGet = "累计选择两百次升级";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/成就装备/008不可视之手/8.不可视之手.png");
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
        else if (equipmentId == 36)
        {
            equipmentName = "气运之子";
            description = "奇遇可选数量+1（二选一变三选一）\n\n气运加身，抉择无忧。";
            howToGet = "累计抽卡 550 次后加入卡池";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/SSR/011.png");
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

        // UR_0 风之形（之前缺失描述，完全来自场景 prefab → 补充兜底）
        if (equipmentId == 4)
        {
            equipmentName = "风之形";
            description = "解锁飓风技能进化路线\n\n" +
                "进化条件：已学习风箭\n\n" +
                "技能效果：将风箭进化为飓风，大幅提升范围伤害与击退\n\n" +
                "风的形状，由你来定义。";
            howToGet = "抽卡获得（N3 加入卡池）";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/UR/000.png");
        }
        else if (equipmentId == 10)
        {
            equipmentName = "亡者领域";
            description = "解锁孢子领域 → 亡者领域 的技能进化路线\n\n" +
                "进化条件：已学习风箭 + 已学习孢子领域，且二者攻击范围 >= 15\n\n" +
                "技能效果：在孢子领域范围内复活被击杀的敌人成为友军\n" +
                "友军小怪死亡时回复 0.5% 最大生命值\n" +
                "（无罪专属：每分钟攻击范围 +1，上限 20）\n\n" +
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
                "• 持有[不忘初心]时保留火球术，数量/伤害/冷却全部实时同步火球";
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
        else if (gachaRarity == GachaRarity.R && equipmentId == 1)
        {
            equipmentName = "量子源木";
            description = "每件开局源木 +1\n\n量子态的源木在开局时具现为可用资源，扩充冒险的资本。";
            howToGet = "累计抽卡 200 次后加入卡池（共 100 个）";
            if (!TrySetIconFromGachaManager(GachaRarity.R, 1))
                SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/R/001.png");
            howToGet = "累计抽卡 200 次后加入卡池（每 20 抽追加 1 张）";
            // 优先用挂在 GachaManager 上的静态 Sprite（走标准 Unity 资源管线，避免运行时 LoadImage 偏色）
            if (!TrySetIconFromGachaManager(GachaRarity.R, 2))
                SetIconFromAssetPath("像素幸存者资源包/存档装备图标/抽卡装备/R/002.png");
        }
        else if (gachaRarity == GachaRarity.SR && equipmentId == 6)
        {
            equipmentName = "速度灵果";
            description = "每件移动速度 +0.03（每累计 34 件 = +1 移速，满池 100 件 = +3）\n\n步履匆匆，无人能追。";
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
    /// N4~N7 通关装备 7/10/13/16 的文本强制覆盖。
    /// 这些装备的 description 设在场景 prefab 里，可能落后策划最新数值，
    /// 这里按 equipmentId 无条件覆盖，保证显示与 EquipmentInitializer 实际效果一致。
    /// </summary>
    private void ApplyForcedClearEquipmentN4toN7Overrides()
    {
        if (equipmentType != EquipmentType.ClearEquipment) return;

        if (equipmentId == 7)
        {
            description = "防御力＋1\n\n重，但是很安心。";
        }
        else if (equipmentId == 10)
        {
            description = "防御力＋2\n\n轻便又有韧劲。";
        }
        else if (equipmentId == 13)
        {
            description = "生命值＋200\n\n经典外观，实在防护。";
        }
        else if (equipmentId == 16)
        {
            description = "防御力＋2\n\n经历过磨练的铠甲。";
        }
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

    /// <summary>
    /// N9~N13 通关装备 21~35（利爪/月牙/粘液/暗影/龙鳞 系列）的文本 + 图标兜底。
    /// 这十五件图标由 ArchiveManager.EnsureClearEquipmentN9toN13IconsExist() 在运行时克隆，
    /// 必须在这里按 equipmentId 注入完整信息。
    ///
    /// 策划数值（与 EquipmentInitializer.ApplyClearEquipments 保持一致）：
    ///   21 利爪之剑：攻击力+20     | 22 皮毛之甲：防御力+2      | 23 野兽之心：经验效率+2
    ///   24 月牙之剑：攻击力+20     | 25 月圆之甲：防御力+2      | 26 月球之心：自然回血+2
    ///   27 粘液之剑：攻击力+20     | 28 粘液之甲：生命值+300    | 29 粘液之心：经验效率+2
    ///   30 暗影之剑：攻击力+20     | 31 暗影之甲：防御力+2      | 32 暗影之心：暴击伤害+20
    ///   33 龙鳞之剑：攻击力+30     | 34 龙鳞之甲：防御力+10     | 35 黄金睛：暴击伤害+20
    /// </summary>
    private void ApplyForcedClearEquipmentN9toN13Overrides()
    {
        if (equipmentType != EquipmentType.ClearEquipment) return;

        // ── N9 (id 21-23) ─────────────────────────────────────
        if (equipmentId == 21)
        {
            equipmentName = "利爪之剑";
            description = "攻击力＋20\n\n为什么非要做成剑？！！";
            howToGet = "通关N9有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/021.png");
        }
        else if (equipmentId == 22)
        {
            equipmentName = "皮毛之甲";
            description = "防御力＋2\n\n没有皮草的家伙！";
            howToGet = "通关N9有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/022.png");
        }
        else if (equipmentId == 23)
        {
            equipmentName = "野兽之心";
            description = "经验效率＋2\n\n欲望略微增加了";
            howToGet = "通关N9有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/023.png");
        }
        // ── N10 (id 24-26) ────────────────────────────────────
        else if (equipmentId == 24)
        {
            equipmentName = "月牙之剑";
            description = "攻击力＋20\n\n月牙露出小尖尖";
            howToGet = "通关N10有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/024.png");
        }
        else if (equipmentId == 25)
        {
            equipmentName = "月圆之甲";
            description = "防御力＋2\n\n唉哆...";
            howToGet = "通关N10有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/025.png");
        }
        else if (equipmentId == 26)
        {
            equipmentName = "月球之心";
            description = "自然回血＋2\n\n我也要上月球吗";
            howToGet = "通关N10有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/026.png");
        }
        // ── N11 (id 27-29) ────────────────────────────────────
        else if (equipmentId == 27)
        {
            equipmentName = "粘液之剑";
            description = "攻击力＋20\n\n这批装备的名字也太好取了吧";
            howToGet = "通关N11有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/027.png");
        }
        else if (equipmentId == 28)
        {
            equipmentName = "粘液之甲";
            description = "生命值＋300\n\n溶解之爱…";
            howToGet = "通关N11有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/028.png");
        }
        else if (equipmentId == 29)
        {
            equipmentName = "粘液之心";
            description = "经验效率＋2\n\n胶粘有感觉吗";
            howToGet = "通关N11有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/029.png");
        }
        // ── N12 (id 30-32) ────────────────────────────────────
        else if (equipmentId == 30)
        {
            equipmentName = "暗影之剑";
            description = "攻击力＋20\n\n物理学圣剑！";
            howToGet = "通关N12有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/030.png");
        }
        else if (equipmentId == 31)
        {
            equipmentName = "暗影之甲";
            description = "防御力＋2\n\n史莱姆的塑性制作而成哦齁齁齁";
            howToGet = "通关N12有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/031.png");
        }
        else if (equipmentId == 32)
        {
            equipmentName = "暗影之心";
            description = "暴击伤害＋20\n\n人形自走核弹来袭";
            howToGet = "通关N12有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/032.png");
        }
        // ── N13 (id 33-35) ────────────────────────────────────
        else if (equipmentId == 33)
        {
            equipmentName = "龙鳞之剑";
            description = "攻击力＋30\n\n好帅的一柄剑";
            howToGet = "通关N13有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/033.png");
        }
        else if (equipmentId == 34)
        {
            equipmentName = "龙鳞之甲";
            description = "防御力＋10\n\n好帅的一套铠甲";
            howToGet = "通关N13有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/034.png");
        }
        else if (equipmentId == 35)
        {
            equipmentName = "黄金睛";
            description = "暴击伤害＋20\n\n感叹没有黄金瞳的照修命运吧！";
            howToGet = "通关N13有概率掉落";
            SetIconFromAssetPath("像素幸存者资源包/存档装备图标/通关装备/035.png");
        }
    }

    private void SetIconFromAssetPath(string relativeToAssets)
    {
        if (iconImage == null) return;

        // 从 editorAssetsRelativePath 自动推导 resourcesRelativePath（去掉 .png 扩展名）
        // 这样 RuntimeAssetLoader 第 2 层 Resources.Load 能命中，不再仅依赖第 3 层编辑器文件读取
        string resourcesPath = null;
        if (!string.IsNullOrEmpty(relativeToAssets) && relativeToAssets.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
            resourcesPath = relativeToAssets.Substring(0, relativeToAssets.Length - 4);

        var tex = RuntimeAssetLoader.LoadTexture(null, resourcesPath, relativeToAssets);
        if (tex == null) return;

        // AI 生成的图标通常带有白色/灰色实心背景（无真正 alpha），需后处理去除
        MakeTextureTransparent(tex);

        Sprite forcedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        iconImage.enabled = true;
        iconImage.material = null;
        iconImage.sprite = forcedSprite;
        iconImage.overrideSprite = forcedSprite;
        iconImage.type = Image.Type.Simple;
        iconImage.preserveAspect = true;
    }

    /// <summary>
    /// 使用边缘泛洪填充（Flood Fill）去除 Texture2D 的实心背景。
    ///
    /// 改进点（相比初版）：
    /// 1. 采样全部边缘像素（不只是四角），用颜色直方图选出前 N 个最频背景色候选
    /// 2. BFS 时只要与任一候选背景色距离 ≤ TOLERANCE 就去除，正确处理棋盘格/渐变背景
    /// 3. TOLERANCE 提升到 80，覆盖 AI 生成图的色差
    /// 4. 二次扫描：对已被去背的区域做"洞填充"（去除物体内部被背景色包围的残留像素）
    /// </summary>
    private static void MakeTextureTransparent(Texture2D tex)
    {
        if (tex == null) return;
        try { var _ = tex.GetPixel(0, 0); }
        catch (System.Exception) { return; }

        int w = tex.width;
        int h = tex.height;
        if (w <= 2 || h <= 2) return;

        var pixels = tex.GetPixels32();

        // ── 步骤1：采样全部边缘像素，建立颜色直方图 ──
        var hist = new System.Collections.Generic.Dictionary<long, int>();
        var histColor = new System.Collections.Generic.Dictionary<long, Color32>();

        // 用 lambda 代替本地函数（C# 6.0 兼容）
        System.Action<Color32> addToHist = (c) =>
        {
            if (c.a < 128) return;
            long key = ((long)(c.r / 8) << 16) | ((long)(c.g / 8) << 8) | (c.b / 8);
            int v; hist.TryGetValue(key, out v); v++;
            hist[key] = v;
            if (!histColor.ContainsKey(key)) histColor[key] = c;
        };

        for (int x = 0; x < w; x++)
        {
            addToHist(pixels[0 * w + x]);
            addToHist(pixels[(h - 1) * w + x]);
        }
        for (int y = 1; y < h - 1; y++)
        {
            addToHist(pixels[y * w + 0]);
            addToHist(pixels[y * w + (w - 1)]);
        }

        if (hist.Count == 0)
        {
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return;
        }

        var sorted = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<long, int>>(hist);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        int candidateCount = System.Math.Min(4, sorted.Count);
        var bgCandidates = new Color32[candidateCount];
        for (int i = 0; i < candidateCount; i++)
            bgCandidates[i] = histColor[sorted[i].Key];

        // ── 步骤2：从所有边缘像素开始 BFS 泛洪填充 ──
        const int TOLERANCE = 80;
        var visited = new bool[w * h];
        var queue = new System.Collections.Generic.Queue<int>();

        // 用 lambda 代替 TryEnqueueEdge / TryEnqueueNeighbor（C# 6.0 兼容）
        System.Action<int, int> tryEnqueue = (ex, ey) =>
        {
            int i = ey * w + ex;
            if (visited[i]) return;
            Color32 c = pixels[i];
            if (c.a == 0) { visited[i] = true; return; }
            if (MatchesAnyBg(c, bgCandidates, TOLERANCE))
                queue.Enqueue(i);
        };

        for (int x = 0; x < w; x++)
        {
            tryEnqueue(x, 0);
            tryEnqueue(x, h - 1);
        }
        for (int y = 1; y < h - 1; y++)
        {
            tryEnqueue(0, y);
            tryEnqueue(w - 1, y);
        }

        // BFS 主循环
        while (queue.Count > 0)
        {
            int idx = queue.Dequeue();
            if (visited[idx]) continue;
            visited[idx] = true;
            pixels[idx] = new Color32(0, 0, 0, 0);

            int px = idx % w;
            int py = idx / w;

            if (px > 0)     tryEnqueue(px - 1, py);
            if (px < w - 1) tryEnqueue(px + 1, py);
            if (py > 0)     tryEnqueue(px, py - 1);
            if (py < h - 1)  tryEnqueue(px, py + 1);
        }

        // ── 步骤3：洞填充 ──
        FillInternalHoles(pixels, visited, w, h, bgCandidates, TOLERANCE);

        // ── 应用结果 ──
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
    }

    /// <summary>
    /// 填充已被去背像素包围的内部"洞"——扫描线算法：
    /// 对每一行，在两个已去背像素之间的区域，若像素颜色接近背景候选则也设为透明。
    /// </summary>
    private static void FillInternalHoles(Color32[] pixels, bool[] visited, int w, int h,
        Color32[] bgCandidates, int tolerance)
    {
        for (int y = 1; y < h - 1; y++)
        {
            int rowStart = -1;
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (visited[idx] || pixels[idx].a == 0)
                {
                    if (rowStart >= 0)
                    {
                        int holeLen = x - rowStart;
                        if (holeLen >= 3)
                        {
                            bool shouldFill = false;
                            for (int kx = rowStart; kx < x && !shouldFill; kx++)
                            {
                                if (MatchesAnyBg(pixels[y * w + kx], bgCandidates, tolerance))
                                    shouldFill = true;
                            }
                            if (shouldFill)
                            {
                                for (int kx = rowStart; kx < x; kx++)
                                {
                                    int kIdx = y * w + kx;
                                    if (!visited[kIdx] && MatchesAnyBg(pixels[kIdx], bgCandidates, tolerance))
                                    {
                                        visited[kIdx] = true;
                                        pixels[kIdx] = new Color32(0, 0, 0, 0);
                                    }
                                }
                            }
                        }
                        rowStart = -1;
                    }
                }
                else
                {
                    if (rowStart < 0) rowStart = x;
                }
            }
        }
    }

    private static bool MatchesAnyBg(Color32 c, Color32[] candidates, int tolerance)
    {
        for (int i = 0; i < candidates.Length; i++)
            if (ColorDist(c, candidates[i]) <= tolerance) return true;
        return false;
    }

    /// <summary>计算两个颜色的 RGB 各通道绝对差值之和（曼哈顿距离）</summary>
    private static int ColorDist(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
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

    // ── SSR9 三清化一 开关（参照孢子异变模式）────────────────────
    private void SetupTrinityFusionToggle()
    {
        if (equipmentType != EquipmentType.GachaEquipment || gachaRarity != GachaRarity.SSR || equipmentId != 12) return;
        if (trinityToggleButton != null) return;

        Transform existing = transform.Find("TrinityFusionToggle");
        GameObject toggleObj = existing != null ? existing.gameObject : new GameObject("TrinityFusionToggle");
        toggleObj.transform.SetParent(transform, false);

        RectTransform rect = toggleObj.GetComponent<RectTransform>();
        if (rect == null) rect = toggleObj.AddComponent<RectTransform>();
        // 定位在装备图标底部居中（不与孢子异变按钮的顶部位置冲突）
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 8f);
        rect.sizeDelta = new Vector2(50f, 30f);

        Image bg = toggleObj.GetComponent<Image>();
        if (bg == null) bg = toggleObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);

        trinityToggleButton = toggleObj.GetComponent<Button>();
        if (trinityToggleButton == null) trinityToggleButton = toggleObj.AddComponent<Button>();
        trinityToggleButton.onClick.RemoveAllListeners();
        trinityToggleButton.onClick.AddListener(ToggleTrinityFusion);

        Transform textTr = toggleObj.transform.Find("Text");
        GameObject textObj = textTr != null ? textTr.gameObject : new GameObject("Text");
        textObj.transform.SetParent(toggleObj.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        if (textRect == null) textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        trinityToggleText = textObj.GetComponent<TextMeshProUGUI>();
        if (trinityToggleText == null) trinityToggleText = textObj.AddComponent<TextMeshProUGUI>();
        trinityToggleText.alignment = TextAlignmentOptions.Center;
        trinityToggleText.fontSize = 18;
        trinityToggleText.fontStyle = FontStyles.Bold;
        trinityToggleText.raycastTarget = false;
    }

    private void ToggleTrinityFusion()
    {
        bool enabled = TrinityFusionToggle.Enabled;
        TrinityFusionToggle.Enabled = !enabled;
        UpdateTrinityFusionToggleDisplay(IsUnlocked());
        ToastManager.Show(enabled ? "三清化一：已关闭" : "三清化一：已开启");
    }

    private void UpdateTrinityFusionToggleDisplay(bool isUnlocked)
    {
        if (equipmentType != EquipmentType.GachaEquipment || gachaRarity != GachaRarity.SSR || equipmentId != 12) return;
        SetupTrinityFusionToggle();
        if (trinityToggleButton == null) return;

        trinityToggleButton.gameObject.SetActive(isUnlocked);
        if (!isUnlocked) return;

        bool enabled = TrinityFusionToggle.Enabled;
        if (trinityToggleText != null) trinityToggleText.text = enabled ? "开" : "关";
        Image bg = trinityToggleButton.GetComponent<Image>();
        if (bg != null) bg.color = enabled ? new Color(0.35f, 0.15f, 0.55f, 0.95f) : new Color(0.45f, 0.1f, 0.1f, 0.95f);
    }

    public void UpdateDisplay()
    {
        if (iconImage == null) return;

        ApplyForcedAchievementOverrides();

        bool isUnlocked = IsUnlocked();
        iconImage.color = isUnlocked ? unlockedColor : lockedColor;
        UpdateSporeMutationToggleDisplay(isUnlocked);
        UpdateTrinityFusionToggleDisplay(isUnlocked);

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
