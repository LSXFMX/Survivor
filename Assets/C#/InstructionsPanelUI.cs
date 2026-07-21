using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗暂停菜单中的「操作说明」面板（PPT 翻页式）。
///
/// 双模式：
///   - **图片模式**：若 slidePaths 指定了 Sprite 资源且加载成功，显示 1536×1024 插画。
///   - **文字模式**：若 slidePaths 中某页 Sprite 为 null，自动降级为 slideTexts 对应页的富文本。
///     Color tag 使用 #RRGGBB 格式，统一用 project 已安装的中文字体 heiti SDF 渲染。
///
/// 自动弹出：
///   - N1 开局时自动调用 <see cref="ShowAuto"/> 作为新手引导。
///   - <see cref="WasN1TutorialShown"/> / <see cref="MarkN1TutorialShown"/> 存储 PlayerPrefs。
/// </summary>
public class InstructionsPanelUI : MonoBehaviour
{
    [Header("控件")]
    public Button closeButton;
    public Button prevButton;
    public Button nextButton;
    public Image   slideImage;
    public TextMeshProUGUI pageIndicator;
    /// <summary>文字模式幻灯片（当图片加载失败时使用的富文本内容）</summary>
    public TextMeshProUGUI slideText;

    [Header("自动构建样式")]
    public TMP_FontAsset font;
    public Vector2 autoBuildSize = new Vector2(1100f, 760f);

    [Header("幻灯片（Resources 路径）")]
    public string[] slidePaths = new string[]
    {
        "InstructionsSlides/slide_01_move",
        "InstructionsSlides/slide_02_levelup",
        "InstructionsSlides/slide_03_gacha",
        "InstructionsSlides/slide_04_difficulty",
        "InstructionsSlides/slide_05_affinity",
        "InstructionsSlides/slide_06_events",
    };

    /// <summary>文字模式：slidePaths 对应页 Sprite 加载失败时使用的文字内容。
    /// 使用 &lt;color=#RRGGBB&gt; 标签渲染彩色标题。</summary>
    [TextArea(3, 20)]
    public string[] slideTexts;

    // 兼容旧场景
    [HideInInspector] public TextMeshProUGUI contentText;
    [HideInInspector] public string[] difficultyGoals;
    [HideInInspector] public string[] difficultyUnlocks;

    // ─── 持久化 ───────────────────────────
    private const string PREF_LAST_SEEN   = "InstructionsLastSeenUnlockCount";
    private const string PREF_EVER_VIEWED = "InstructionsEverViewed";
    private const string PREF_N1_SHOWN    = "TutorialN1Shown";

    private Sprite[] _loadedSlides;
    private int _pageIndex;
    private bool _built;
    private bool _autoMode;

    void Awake()
    {
        EnsureBuilt();
        EnsureDefaultSlideTexts();
    }

    /// <summary>若 Inspector 未设置 slideTexts，则填充 12 页内置新手教程文字。</summary>
    private void EnsureDefaultSlideTexts()
    {
        if (slideTexts != null && slideTexts.Length > 0) return;
        slidePaths = new string[12]; // 12 页空路径 → 全部走文字模式
        slideTexts = new string[]
        {
            "<size=36><color=#FFD24A>欢迎来到 Survivor</color></size>\n\n"
            + "这是一款自动战斗 + Roguelite 的像素风生存游戏。\n\n"
            + "<color=#80FFC0>核心目标</color>：在限时内生存在于击败关底 Boss。\n"
            + "<color=#80FFC0>核心循环</color>：击杀敌人 → 获取经验 → 升级选择强化 → 更强 → 击败 Boss。\n\n"
            + "本教程共 <color=#FFD24A>12 页</color>，用左右按钮翻页阅读。祝你生存愉快！",

            "<size=36><color=#FFD24A>1. 移动与自动战斗</color></size>\n\n"
            + "<color=#80FFC0>移动</color>：WASD 或 方向键，或 <color=#FF80C0>鼠标左键点击地面</color>。\n"
            + "<color=#80FFC0>自动释放技能</color>：你装备的技能会自动向最近的敌人释放，无需手动操作。\n\n"
            + "每个技能有独立的 <color=#FFD24A>冷却时间 (CD)</color>，冷却完毕后自动触发。\n"
            + "技能图标下方会显示冷却进度。\n\n"
            + "<color=#80FFC0>拾取经验</color>：靠近经验石会自动飞向你，拾取后增加经验。\n"
            + "蓝色圆圈范围即拾取范围，可被装备和加成扩大。",

            "<size=36><color=#FFD24A>2. 升级三选一</color></size>\n\n"
            + "经验满后升级，弹出 <color=#FFD24A>三选一卡牌</color>：\n\n"
            + "<color=#80FFC0>学习新技能</color>：获得一个全新的自动攻击技能。\n"
            + "开局第一次升级 <color=#FF80C0>保底三张全是学习卡</color>。\n\n"
            + "<color=#80FFC0>技能升级</color>：增强已有技能的伤害、冷却、数量、范围等。\n"
            + "每项属性有独立升级次数上限。\n\n"
            + "<color=#80FFC0>人物升级</color>：提升基础属性（攻/防/血/速/暴击等）。\n\n"
            + "若三张都不想要，可按 <color=#FF80C0>刷新按钮</color>（需有 R 级抽卡装备）。",

            "<size=36><color=#FFD24A>3. 装备系统</color></size>\n\n"
            + "共四种装备类型，持久化解锁，永久生效：\n\n"
            + "<color=#FFD24A>成就装备</color>：达成特定条件自动解锁（如冲刺、三倍速等）。\n"
            + "<color=#FF80C0>好感度装备</color>：各社群好感度达标解锁，提供强力技能。\n"
            + "<color=#C0C0FF>抽卡装备 (SSR)</color>：消耗金币抽取，提供独特全局效果。\n"
            + "<color=#80FFC0>通关装备</color>：按难度通关数量解锁。\n\n"
            + "在存档界面可查看已解锁装备及获得条件。",

            "<size=36><color=#FFD24A>4. 好感度系统</color></size>\n\n"
            + "游戏内有多个 <color=#FF80C0>社群</color>。击败 <color=#FFD24A>世界 Boss</color> 后解锁对应社群。\n\n"
            + "每个社群都会：\n"
            + "  • <color=#80FFC0>强化特定技能</color>，让该技能大幅变强\n"
            + "  • 提供 <color=#80FFC0>额外属性加成</color>（攻/防/暴击/闪避/经验等）\n"
            + "  • 好感度达到 <color=#FFD24A>10 / 50 / 100</color> 分三档解锁装备\n\n"
            + "社群数量、对应技能及奖励请在主菜单社群面板查看。\n"
            + "好感度 <color=#FF80C0>跨局持久保存</color>。",

            "<size=36><color=#FFD24A>5. 冲刺</color></size>\n\n"
            + "<color=#80FFC0>成就装备 2 解锁</color>后即可使用。\n\n"
            + "操作：<color=#FF80C0>按住方向 + 空格键</color>\n"
            + "向移动方向冲刺固定距离，有冷却。\n\n"
            + "冲刺可突破包围、躲避 Boss 技能、快速走位，是保命核心。",

            "<size=36><color=#FFD24A>6. 源木与奇遇</color></size>\n\n"
            + "<color=#FFD24A>源木</color>：局内货币，击杀敌人获取。\n\n"
            + "消耗源木可触发 <color=#FF80C0>奇遇事件</color>：\n"
            + "  • 随机抽取若干一次性效果供你选择\n"
            + "  • 效果包括：临时增益 / 永久加成 / 技能强化\n"
            + "  • 某些奇遇有难度门槛\n\n"
            + "开局后按 <color=#FF80C0>奇遇按钮</color> 触发；某些 SSR 可解锁更多选项。",

            "<size=36><color=#FFD24A>7. 门挑战 (N5+)</color></size>\n\n"
            + "位于主界面 <color=#FFD24A>门按钮</color>，N5+ 难度解锁。\n\n"
            + "共 <color=#FFD24A>13</color> 层递增难度，每层生成强化敌人。\n"
            + "每层通关奖励：<color=#80FFC0>所有技能升级上限永久 +1</color>。\n"
            + "额外随机获得攻、防、经验效率、闪避 +2。\n\n"
            + "通关全部 13 层额外获得经验效率 +10。\n"
            + "敌人自带回血，需要足够输出才能攻克。",

            "<size=36><color=#FFD24A>8. 技能进化系统</color></size>\n\n"
            + "部分基础技能在满足条件后，会进化为更强大的 <color=#FF80C0>UR 形态</color>。\n\n"
            + "进化需要：\n"
            + "  • 学会前置基础技能\n"
            + "  • 满足特定学习条件（部分需好感度或 SSR 解锁）\n"
            + "  • 通过三选一卡牌出现时选择进化卡\n\n"
            + "进化后的技能会有全新的攻击模式、视觉效果和成长曲线。\n"
            + "具体哪些技能可进化，进入游戏后自己探索。",

            "<size=36><color=#FFD24A>9. 皮肤与 UR 角色</color></size>\n\n"
            + "目前共 <color=#FFD24A>4</color> 位可选角色，在主菜单切换：\n\n"
            + "默认角色始终解锁。\n"
            + "UR 角色（其余 3 位）通过 <color=#FFD24A>抽卡 (SSR)</color> 解锁。\n\n"
            + "每位 UR 角色都有独特的：\n"
            + "  • 本命技能加成\n"
            + "  • 开局自带技能\n"
            + "  • 风箭染色 / 视觉风格\n\n"
            + "选好角色后，整局战斗都会体验不同。",

            "<size=36><color=#FFD24A>10. 难度体系</color></size>\n\n"
            + "共 <color=#FFD24A>N1 ~ N13</color> 共 13 个难度 + <color=#FF80C0>无尽模式</color>。\n\n"
            + "随难度提升：\n"
            + "  • 敌人血量与攻击大幅增长\n"
            + "  • 关底 Boss 越来越强\n"
            + "  • 对局时长逐渐增加\n\n"
            + "通关当前难度解锁下一难度。\n"
            + "<color=#FF80C0>无尽模式</color> 永远上涨，永远没有上限。\n\n"
            + "不同难度的具体 Boss 等你亲自去揭开。",

            "<size=36><color=#FFD24A>11. 倍速与暂停</color></size>\n\n"
            + "<color=#FFD24A>倍速</color>：点击倍速按钮在 1x / 2x / 3x 间切换。\n"
            + "3x 倍速需成就装备 4 解锁。\n\n"
            + "<color=#FFD24A>暂停</color>：按 <color=#FF80C0>ESC</color> 打开暂停菜单。\n"
            + "  包含：继续 / 设置 / 操作说明 / 返回主菜单\n\n"
            + "设置面板可调整：\n"
            + "  • 攻击范围显示\n"
            + "  • 伤害数字开关与大小\n"
            + "  • BGM 与 SFX 音量\n"
            + "  • 全屏 / 窗口化与分辨率\n"
            + "  • 后台运行\n\n"
            + "右键点击面板可快速关闭。",

            "<size=36><color=#80FFC0>祝你在 Survivor 中生存愉快！</color></size>\n\n"
            + "提示：操作说明随时可从暂停菜单 (ESC) 重新打开。\n\n"
            + "新手建议：\n"
            + "  1. 先尝试 N1 ~ N5，熟悉基础操作\n"
            + "  2. 优先升级伤害与数量类属性\n"
            + "  3. 好感度装备可大幅改变技能体验\n"
            + "  4. 冲刺是保命核心，尽早点出\n\n"
            + "<color=#FFD24A>每局都是新的开始，积累实力，步步为营。</color>"
        };
        _loadedSlides = null; // 强制重新加载
    }

    void OnEnable()
    {
        EnsureBuilt();
        LoadSlidesIfNeeded();
        if (contentText != null && contentText.gameObject != null)
            contentText.gameObject.SetActive(false);

        if (closeButton != null) { closeButton.onClick.RemoveListener(Close); closeButton.onClick.AddListener(Close); }
        if (prevButton  != null) { prevButton.onClick.RemoveListener(PrevPage);  prevButton.onClick.AddListener(PrevPage);  }
        if (nextButton  != null) { nextButton.onClick.RemoveListener(NextPage);  nextButton.onClick.AddListener(NextPage);  }
        _pageIndex = 0;
        Refresh();
        PlayerPrefs.SetInt(PREF_LAST_SEEN, GetCurrentUnlockedCount());
        PlayerPrefs.SetInt(PREF_EVER_VIEWED, 1);
        PlayerPrefs.Save();
    }

    public void Close()
    {
        gameObject.SetActive(false);
        if (_autoMode) { _autoMode = false; Time.timeScale = 1f; }
    }
    public void PrevPage() { if (_loadedSlides == null || _loadedSlides.Length == 0) return; _pageIndex = (_pageIndex - 1 + _loadedSlides.Length) % _loadedSlides.Length; Refresh(); }
    public void NextPage() { if (_loadedSlides == null || _loadedSlides.Length == 0) return; _pageIndex = (_pageIndex + 1) % _loadedSlides.Length; Refresh(); }

    private void Refresh()
    {
        int total = _loadedSlides?.Length ?? 0;
        bool hasSprite = total > 0 && _pageIndex < total && _loadedSlides[_pageIndex] != null;
        bool hasText   = slideTexts != null && _pageIndex < slideTexts.Length && !string.IsNullOrEmpty(slideTexts[_pageIndex]);

        if (slideImage != null)
        {
            slideImage.sprite = hasSprite ? _loadedSlides[_pageIndex] : null;
            slideImage.enabled = hasSprite;
        }
        if (slideText != null)
        {
            slideText.text = hasText ? slideTexts[_pageIndex] : "";
            slideText.gameObject.SetActive(hasText && !hasSprite); // 文字模式仅在没有图片时显示
        }
        if (pageIndicator != null)
        {
            pageIndicator.text = total > 0 ? $"{_pageIndex + 1} / {total}" : "0 / 0";
            if (font != null && pageIndicator.font != font) pageIndicator.font = font;
        }
    }

    private void LoadSlidesIfNeeded()
    {
        if (_loadedSlides != null && _loadedSlides.Length == (slidePaths?.Length ?? 0)) return;
        if (slidePaths == null) { _loadedSlides = new Sprite[0]; return; }
        _loadedSlides = new Sprite[slidePaths.Length];
        for (int i = 0; i < slidePaths.Length; i++)
        {
            if (string.IsNullOrEmpty(slidePaths[i])) continue;
            _loadedSlides[i] = Resources.Load<Sprite>(slidePaths[i]);
        }
    }

    /// <summary>自动弹出模式：暂停游戏 → 显示 → 关闭时恢复。</summary>
    public void ShowAuto()
    {
        _autoMode = true;
        Time.timeScale = 0f;
        gameObject.SetActive(true);
    }

    // ─── 红点 / 首次检测 ─────────────────

    public static bool HasNewUnlockToShow()
    {
        if (PlayerPrefs.GetInt(PREF_EVER_VIEWED, 0) == 0) return true;
        return GetCurrentUnlockedCount() > PlayerPrefs.GetInt(PREF_LAST_SEEN, 0);
    }
    public static int GetCurrentUnlockedCount()
    {
        if (DifficultyManager.Instance?.configs == null) return 1;
        int count = 1;
        var cfgs = DifficultyManager.Instance.configs;
        for (int i = 1; i < cfgs.Length; i++)
        {
            if (ClearRecordManager.Instance?.GetClearCount(cfgs[i - 1].label) <= 0) break;
            count++;
        }
        return count;
    }
    public static bool WasN1TutorialShown() => PlayerPrefs.GetInt(PREF_N1_SHOWN, 0) == 1;
    public static void MarkN1TutorialShown() { PlayerPrefs.SetInt(PREF_N1_SHOWN, 1); PlayerPrefs.Save(); }

    // ─── 自动构建 ─────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = autoBuildSize;

        if (GetComponent<Image>() == null) { var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.9f); bg.raycastTarget = true; }

        if (closeButton == null)
            closeButton = UIBuilder.CreateButton(rt, "CloseButton", "X", new Vector2(1f,1f), new Vector2(1f,1f), new Vector2(1f,1f), new Vector2(-30f,-30f), new Vector2(60f,60f), font);

        if (slideImage == null) // 图片模式 Slide
        {
            var go = new GameObject("Slide", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var srt = (RectTransform)go.transform;
            srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, 20f);
            float w = autoBuildSize.x - 200f, h = w * 2f / 3f;
            if (h > autoBuildSize.y - 180f) { h = autoBuildSize.y - 180f; w = h * 3f / 2f; }
            srt.sizeDelta = new Vector2(w, h);
            slideImage = go.AddComponent<Image>();
            slideImage.preserveAspect = true;
            slideImage.raycastTarget = false;
        }

        if (slideText == null) // 文字模式 Slide
        {
            var tgo = new GameObject("SlideText", typeof(RectTransform));
            tgo.transform.SetParent(transform, false);
            var trt = (RectTransform)tgo.transform;
            // 左右各留 90px 给翻页按钮（按钮 60 + 8 边距 + 22 缓冲）
            trt.anchorMin = new Vector2(0f, 0.1f);
            trt.anchorMax = new Vector2(1f, 0.92f);
            trt.offsetMin = new Vector2(90f, 0f);
            trt.offsetMax = new Vector2(-90f, 0f);
            slideText = tgo.AddComponent<TextMeshProUGUI>();
            slideText.fontSize = 20;
            slideText.color   = new Color(0.9f, 0.9f, 0.9f);
            slideText.alignment = TextAlignmentOptions.TopLeft;
            slideText.enableWordWrapping = true;
            slideText.raycastTarget = false;
            if (font != null) slideText.font = font;
        }

        if (prevButton == null)
        {
            // 左翻页按钮：贴面板最左侧 8px，居中高度
            prevButton = UIBuilder.CreateButton(rt, "PrevBtn", "<", new Vector2(0f,0.5f), new Vector2(0f,0.5f), new Vector2(0f,0.5f), new Vector2(8f,0f), new Vector2(60f,60f), font);
            if (font != null)
            {
                var tmp = prevButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) { tmp.font = font; tmp.fontSize = 32; tmp.fontStyle = FontStyles.Bold; }
            }
        }
        if (nextButton == null)
        {
            // 右翻页按钮：贴面板最右侧 8px，居中高度
            nextButton = UIBuilder.CreateButton(rt, "NextBtn", ">", new Vector2(1f,0.5f), new Vector2(1f,0.5f), new Vector2(1f,0.5f), new Vector2(-8f,0f), new Vector2(60f,60f), font);
            if (font != null)
            {
                var tmp = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) { tmp.font = font; tmp.fontSize = 32; tmp.fontStyle = FontStyles.Bold; }
            }
        }
        if (pageIndicator == null)
        {
            // 页码指示：底部居中，距离底部 16px
            pageIndicator = UIBuilder.CreateText(rt, "PageIndicator", "1 / 1", 24, FontStyles.Bold, new Vector2(0.5f,0f), new Vector2(0.5f,0f), new Vector2(0.5f,0f), new Vector2(0f,16f), new Vector2(180f,36f), font);
            pageIndicator.alignment = TextAlignmentOptions.Center;
            pageIndicator.color = Color.white;
        }
    }
}
