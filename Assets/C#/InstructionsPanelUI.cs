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

    [Header("幻灯片（Resources 路径，留空=纯文字模式）")]
    /// <summary>
    /// 图片幻灯片的 Resources 路径。默认留空 → 纯文字模式。
    ///
    /// 【背景说明 · 2026-08】
    ///   本字段的默认值原本是 6 条 "InstructionsSlides/slide_0X_*" 路径，
    ///   对应的 6 张 PNG **确实存在**于 Resources/InstructionsSlides/
    ///   （slide_01_move / 02_levelup / 03_gacha / 04_difficulty / 05_affinity / 06_events）。
    ///
    ///   之所以现在改成留空，不是因为图片缺失，而是两个实际问题：
    ///     1. Refresh() 的规则是"某页有图就显示图、并隐藏该页文字"
    ///        （slideText.SetActive(hasText && !hasSprite)），
    ///        所以只要这 6 条路径在，前 6 页就永远显示图片、文字永不可见；
    ///     2. 这 6 张图是按**旧版 6 个主题**渲染出来的静态位图，内容已过时
    ///        （抽卡消耗金币、门挑战在主界面、装备四类…），而位图无法像文字那样改写。
    ///        既然本次要求"移除所有过时内容"，就不能再让它们作为正文呈现。
    ///
    ///   现行方案：正文统一走 slideTexts（14 页，内容最新且可维护）。
    ///   若将来重新绘制了与新文案匹配的插画，只需在 Inspector 里逐页填回路径即可 ——
    ///   Refresh() 支持"有图的页显示图、无图的页回落文字"的混合模式，无需改代码。
    /// </summary>
    public string[] slidePaths = new string[0];

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

    /// <summary>若 Inspector 未设置 slideTexts，则填充内置新手教程文字。
    ///
    /// 【2026-08 全面重写】移除过时内容 + 补充最新功能：
    ///   移除/修正：
    ///     • "抽卡装备(SSR)：消耗金币抽取" —— 项目里抽卡不消耗金币，表述错误；
    ///     • "UR 角色通过抽卡 (SSR) 解锁" —— 实际是 UR 档位，不是 SSR；
    ///     • "门挑战位于主界面门按钮" —— 实际入口在战斗场景内；
    ///     • "装备共四种类型" —— 实际有五类（漏了继承装备）；
    ///     • 难度页只笼统说"敌人变强"，未提 N3/N4/N5/N6/N7/N8 的关键解锁节点。
    ///   新增（此前完全没有文档的系统）：
    ///     • 亡者领域（三条复活链路 / 友军机制）
    ///     • 世界 Boss、营地攻占、对局结算面板、读档币复活
    ///     • 伤害公式与各项属性的实际作用
    ///     • 四位角色的具体定位、装备积分、技能进化的真实条件
    /// </summary>
    private void EnsureDefaultSlideTexts()
    {
        if (slideTexts != null && slideTexts.Length > 0) return;

        slideTexts = new string[]
        {
            // ── 0. 欢迎 ──
            "<size=34><color=#FFD24A>欢迎来到 Survivor</color></size>\n\n"
            + "这是一款 <color=#80FFC0>自动战斗 + Roguelite</color> 的像素生存游戏。\n\n"
            + "<color=#80FFC0>核心目标</color>：在限时内生存，并击败关底首领。\n"
            + "<color=#80FFC0>核心循环</color>：击杀敌人 → 获取经验 → 升级强化 → 变强 → 击败首领。\n\n"
            + "你唯一需要手动做的事是 <color=#FF80C0>走位</color>，技能会自动释放。\n\n"
            + "本教程共 <color=#FFD24A>15 页</color>，可用 <color=#FF80C0>← →</color> 按钮翻页。",

            // ── 1. 移动与自动战斗 ──
            "<size=34><color=#FFD24A>1. 移动与自动战斗</color></size>\n\n"
            + "<color=#80FFC0>移动</color>：WASD / 方向键，或 <color=#FF80C0>鼠标左键点击地面</color>。\n"
            + "<color=#80FFC0>自动施法</color>：所有技能自动瞄准最近敌人释放，无需手动。\n\n"
            + "每个技能有独立 <color=#FFD24A>冷却 (CD)</color>，冷却结束即自动触发；\n"
            + "技能图标下方显示冷却进度。\n\n"
            + "<color=#80FFC0>拾取经验</color>：靠近经验石会自动吸附。\n"
            + "脚下的圆圈就是技能范围，可通过升级和装备扩大。\n\n"
            + "<color=#888>提示：范围圈显示可在设置面板中开关。</color>",

            // ── 2. 升级三选一 ──
            "<size=34><color=#FFD24A>2. 升级三选一</color></size>\n\n"
            + "经验条满后升级，弹出 <color=#FFD24A>三张卡牌</color> 供你选择：\n\n"
            + "<color=#80FFC0>学习新技能</color>：获得一个全新的自动攻击技能。\n"
            + "  <color=#888>开局第一次升级保底三张全是学习卡。</color>\n\n"
            + "<color=#80FFC0>技能升级</color>：提升已有技能的伤害 / 冷却 / 数量 / 范围 / 穿透。\n"
            + "  <color=#888>每项属性都有独立的升级次数上限。</color>\n\n"
            + "<color=#80FFC0>人物升级</color>：提升基础属性（攻 / 防 / 血 / 速 / 暴击 / 闪避）。\n\n"
            + "三张都不想要时，可点 <color=#FF80C0>刷新按钮</color> 重抽（需对应抽卡装备）。",

            // ── 3. 属性与战斗公式 ──
            "<size=34><color=#FFD24A>3. 属性与战斗公式</color></size>\n\n"
            + "<color=#FFD24A>伤害公式</color>：\n"
            + "  最终伤害 = 技能伤害 × (1 + 攻击力 × 0.1) − 目标防御\n"
            + "  触发暴击时再乘以暴击伤害倍率。\n\n"
            + "<color=#80FFC0>攻击力</color>：对所有技能生效的通用乘区，优先堆。\n"
            + "<color=#80FFC0>防御力</color>：直接减免每次受到的伤害。\n"
            + "<color=#80FFC0>暴击率 / 暴击伤害</color>：暴击时伤害数字显示为金色。\n"
            + "<color=#80FFC0>闪避</color>：成功闪避会弹出青色 <color=#40E0D0>Miss</color>。\n"
            + "<color=#80FFC0>经验效率</color>：提升每颗经验石收益，加快升级节奏。\n\n"
            + "<color=#888>敌人同样拥有防御与闪避，高难度下需要足够攻击力才能破防。</color>",

            // ── 4. 技能进化 (UR) ──
            "<size=34><color=#FFD24A>4. 技能进化 (UR)</color></size>\n\n"
            + "部分基础技能满足条件后，可进化为更强大的 <color=#FF80C0>UR 形态</color>。\n\n"
            + "进化的通用条件：\n"
            + "  • 学会全部 <color=#80FFC0>前置基础技能</color>\n"
            + "  • 前置技能的关键属性（范围 / 数量等）达到门槛\n"
            + "  • 已通过 <color=#FF80C0>UR 抽卡</color> 解锁该进化资格\n"
            + "  • 满足难度门槛，并在三选一里选择进化卡\n\n"
            + "进化后拥有全新攻击模式与视觉效果，成长曲线也完全不同。\n\n"
            + "<color=#888>默认进化会消耗（移除）前置技能；\n"
            + "「不忘初心」类装备可让前置技能保留。</color>",

            // ── 5. 亡者领域 ──
            "<size=34><color=#C080FF>5. 亡者领域</color></size>\n\n"
            + "「孢子领域」的 UR 进化，也是角色 <color=#C080FF>无罪</color> 的本命技能。\n\n"
            + "效果：敌人死亡时有概率 <color=#FF80C0>复活为你的友军</color> 替你作战。\n"
            + "概率取决于「是谁击杀了它」：\n"
            + "  • 被 <color=#C080FF>领域(孢子)</color> 击杀 → <color=#80FF80>100%</color>\n"
            + "  • 被 <color=#C080FF>已复活的友军</color> 击杀 → <color=#FFD24A>25%</color>\n"
            + "  • 被其余技能击杀 → <color=#FF8080>5%</color>\n\n"
            + "<color=#80FFC0>友军小怪</color>：存活数秒并持续掉血，之后自然消亡。\n"
            + "<color=#80FFC0>友军世界 Boss</color>：<color=#FF80C0>永久</color> 跟随，且你每次受伤都会治疗它；\n"
            + "  屏幕右侧会显示它的头像与血条。\n\n"
            + "<color=#888>注意：关底 Boss 无法被复活，只有世界 Boss 可以。</color>",

            // ── 6. 装备系统 ──
            "<size=34><color=#FFD24A>6. 装备系统</color></size>\n\n"
            + "装备一律 <color=#FF80C0>持久化解锁、跨局永久生效</color>，共五类：\n\n"
            + "<color=#FFD24A>成就装备</color>：达成条件自动解锁（冲刺、三倍速、自动模式…）。\n"
            + "<color=#FF80C0>好感度装备</color>：各社群好感度达标解锁，多为强力技能。\n"
            + "<color=#C0C0FF>抽卡装备</color>：抽卡获得，SSR / UR 直接影响局内战斗。\n"
            + "<color=#80FFC0>通关装备</color>：按难度通关解锁，主要提供面板属性。\n"
            + "<color=#C0A060>继承装备</color>：特殊继承奖励。\n\n"
            + "在 <color=#FFD24A>存档界面</color> 可查看每件装备的效果与获得条件。\n\n"
            + "<color=#888>重复获得的抽卡装备会转为「装备积分」，可兑换通关装备。</color>",

            // ── 7. 抽卡系统 ──
            "<size=34><color=#C0C0FF>7. 抽卡系统</color></size>\n\n"
            + "共 <color=#FFD24A>R / SR / SSR / UR</color> 四档，带 <color=#FF80C0>软保底</color>：\n"
            + "连续不中会逐步提升出率，避免长期空手。\n\n"
            + "<color=#C0C0FF>R</color>：消耗品（Remake、量子源木、<color=#FFD24A>读档币</color>）。\n"
            + "<color=#80FFC0>SR</color>：各类灵果，永久提升某项面板属性。\n"
            + "<color=#FFD24A>SSR</color>：独特全局效果（开局资金、分身翻倍、全能吸血…）。\n"
            + "<color=#FF80C0>UR</color>：解锁 <color=#FF80C0>技能进化路线</color> 与对应 UR 角色。\n\n"
            + "<color=#FFD24A>读档币</color>：死亡时可消耗 1 张原地满血复活，每局限一次。\n\n"
            + "<color=#888>UR 有难度硬门槛，需先通关对应难度才会进池。</color>",

            // ── 8. 好感度与社群 ──
            "<size=34><color=#FF80C0>8. 好感度与社群</color></size>\n\n"
            + "游戏内有四个 <color=#FF80C0>社群</color>：蘑菇 / 蝙蝠 / 狼人 / 史莱姆。\n\n"
            + "击败对应的 <color=#FFD24A>世界 Boss</color> 即可解锁社群并累积好感度。\n"
            + "首次击败关底首领<color=#80FFC0>+10</color>，之后每次击败 <color=#80FFC0>+1</color>（上限 100）。\n\n"
            + "每个社群会：\n"
            + "  • <color=#80FFC0>解锁一个专属技能</color>，并随好感度不断强化它\n"
            + "  • 提供额外面板加成（攻 / 防 / 速 / 闪避 / 经验/ 回血）\n"
            + "  • 在好感度 <color=#FFD24A>10 / 50 / 100</color> 三档各解锁一件装备\n"
            + "  • 好感度 <color=#FFD24A>100</color> 时赠予该社群的 <color=#FF80C0>宠物</color>\n\n"
            + "好感度 <color=#FF80C0>跨局永久保存</color>，是长线养成的核心。\n\n"
            + "<color=#888>四社群专属技能：孢子领域 / 血族血统 / 命途:寄生 / 阴·阳史莱姆。</color>",

            // ── 9. 太极史莱姆（史莱姆社群） ──
            "<size=34><color=#9BE8FF>9. 太极史莱姆</color></size>\n\n"
            + "史莱姆社群的专属技能，由<color=#C0C0FF>两个独立技能</color> 组成：\n\n"
            + "<color=#B278FF>阴史莱姆</color>（好感 10）：召唤太极阴鱼，向四周齐射黑色能量灵弹。\n"
            + "<color=#FFF5C8>阳史莱姆</color>（好感 50）：召唤太极阳鱼，齐射白色能量灵弹。\n"
            + "  <color=#888>单发伤害低，但数量极多（初始每轮 6 发）。</color>\n\n"
            + "<color=#9BE8FF>★ 同时持有两者时，自动合体为「太极史莱姆」</color>，\n"
            + "两种攻击方式<color=#FFD24A>轮流</color>切换（合体与拆分都有演出）：\n"
            + "  <color=#FFD24A>①太极印</color> — 从敌人头顶威压压制，连续落下多次；\n"
            + "<color=#FF80C0>被压制的敌人无法移动</color>，是强力控场手段。\n"
            + "  <color=#FFD24A>②阴阳齐射</color> — 拆分为双鱼，同时向四周倾泻黑白灵弹。\n\n"
            + "<color=#80FFC0>升级卡共享</color>：写作「阴/阳史莱姆」，一张卡同时强化两支。\n"
            + "可升级<color=#C0C0FF>伤害 / 冷却 / 数量 / 范围</color>；其中 <color=#FFD24A>数量</color>同时决定\n"
            + "每轮射弹数与太极印次数，是最核心的成长项。\n\n"
            + "<color=#888>好感 100「太极两仪」：开局直接自带太极史莱姆 + 太极图宠物。</color>",

            // ── 10. 世界 Boss 与营地 ──
            "<size=34><color=#FFD24A>10. 世界 Boss 与营地</color></size>\n\n"
            + "<color=#FF6060>世界 Boss</color>（N6 起解锁）：\n"
            + "  • 属性为同名关底 Boss 的 <color=#FF6060>两倍</color>，并自带每秒回血\n"
            + "  • 击败后解锁对应社群，好感度 +1\n"
            + "  • 血厚且持续恢复，必须有足够持续输出才能击杀\n\n"
            + "<color=#80FFC0>中立营地</color>：\n"
            + "  • 战场上会出现不会攻击你的营地\n"
            + "  • 打掉它即可 <color=#FFD24A>攻占</color>，之后每秒自动产出源木\n"
            + "  • 累计攻占达标可解锁成就装备\n\n"
            + "<color=#888>无尽模式每 5 分钟随机刷出一只已解锁社群的 Boss。</color>",

            // ── 11. 源木与奇遇 ──
            "<size=34><color=#C0A060>11. 源木与奇遇</color></size>\n\n"
            + "<color=#C0A060>源木</color>：局内货币，来自击杀敌人与已占领的营地。\n\n"
            + "<color=#FF80C0>奇遇事件</color>（N3 起开放）：\n"
            + "  • 消耗源木触发，随机给出若干效果供你选择其一\n"
            + "  • 效果包含临时增益、永久面板加成、技能强化等\n"
            + "  • 部分强力奇遇有难度门槛，低难度不会出现\n\n"
            + "点击战斗界面的 <color=#FF80C0>奇遇按钮</color> 即可触发。\n"
            + "特定 SSR 可让可选项从二选一变为三选一。\n\n"
            + "<color=#888>无尽模式下每分钟会扣除 10% 源木，需持续补充。</color>",

            // ── 12. 门挑战 ──
            "<size=34><color=#FFD24A>12. 门挑战 (N5+)</color></size>\n\n"
            + "N5 及以上难度的战斗中会出现 <color=#FFD24A>门</color>，点击即可进入挑战。\n\n"
            + "共 <color=#FFD24A>13 层</color>，逐层递增，每层生成强化过的守门敌人。\n\n"
            + "每层通关奖励：\n"
            + "  • <color=#80FFC0>所有技能的升级上限永久 +1</color>\n"
            + "  • 随机获得 攻击 / 防御 / 经验效率 / 闪避 +2\n\n"
            + "全部 13 层通关：额外获得 <color=#FFD24A>经验效率 +10</color>。\n\n"
            + "<color=#888>守门人自带回血，输出不足会陷入僵持，建议中后期再挑战。</color>",

            // ── 13. 角色与冲刺 ──
            "<size=34><color=#FFD24A>13. 角色与冲刺</color></size>\n\n"
            + "共 <color=#FFD24A>4</color> 位角色，在主菜单切换。默认角色始终可用，\n"
            + "其余 3 位通过 <color=#FF80C0>UR 抽卡</color> 解锁：\n\n"
            + "  • <color=#80FFC0>琪诺露</color> — 默认角色，均衡，享受难度攻击加成\n"
            + "  • <color=#40E0D0>南筱风</color> — 风系特化，风箭范围与数量大幅领先\n"
            + "  • <color=#FF6060>夏  无</color> — 火系特化，倾向火球进化路线\n"
            + "  • <color=#C080FF>无  罪</color> — 亡者领域本命，领域范围随时间持续扩张\n\n"
            + "<color=#FFD24A>冲刺</color>（成就装备解锁）：<color=#FF80C0>方向键 + 空格</color>\n"
            + "向移动方向瞬移一段距离，有独立冷却，是核心保命手段。\n\n"
            + "<color=#888>可通过升级为冲刺附加无敌 / 穿怪效果。</color>",

            // ── 14. 难度、结算与设置 ──
            "<size=34><color=#FFD24A>14. 难度 · 结算 · 设置</color></size>\n\n"
            + "<color=#FFD24A>难度</color>：N1 ~ N13 + <color=#FF80C0>无尽模式</color>，通关当前解锁下一档。\n"
            + "  关键节点：<color=#80FFC0>N3</color> 奇遇 · <color=#80FFC0>N4</color> 蝙蝠 · <color=#80FFC0>N5</color> 门挑战\n"
            + "  <color=#80FFC0>N6</color> 世界 Boss · <color=#80FFC0>N7</color> 血攻翻倍 · <color=#80FFC0>N8</color> 社群挑战\n"
            + "  <color=#FF80C0>无尽</color>（通关 N8 解锁）：难度无上限持续上涨。\n\n"
            + "<color=#FFD24A>对局结算</color>：结束后弹出总结面板，共 4 页 ——\n"
            + "  概览（伤害 / DPS / 击杀 / 承伤 / 治疗）、技能伤害占比、\n"
            + "  击败首领、本局技能与新解锁装备。\n"
            + "  可用 <color=#FF80C0>← →</color> 按钮、方向键或 <color=#FF80C0>鼠标滚轮</color> 翻页。\n\n"
            + "<color=#FFD24A>倍速</color>：1x / 2x / 3x 切换（3x 需成就装备）。\n"
            + "<color=#FFD24A>暂停</color>：<color=#FF80C0>ESC</color> — 继续 / 设置 / 操作说明 / 返回主菜单。\n\n"
            + "<color=#888>右键点击面板可快速关闭。祝你生存愉快！</color>"
        };

        // 全部走文字模式（不加载图片幻灯片）。
        // 【关键】长度必须由 slideTexts.Length 推导，不能像旧版那样硬编码 12：
        //   Refresh() 用 _loadedSlides.Length 作为总页数，一旦文案条数多于这里的长度，
        //   末尾几页将永远无法翻到（旧版正是 12 vs 13 条，最后一页玩家从未见过）。
        slidePaths = new string[slideTexts.Length];

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
    // 翻页用 TotalPages（图片/文字页数取大者），而不是只看 _loadedSlides.Length，
    // 否则纯文字模式下若 slidePaths 比 slideTexts 短，末尾页数将无法翻到。
    public void PrevPage() { int n = TotalPages; if (n == 0) return; _pageIndex = (_pageIndex - 1 + n) % n; Refresh(); }
    public void NextPage() { int n = TotalPages; if (n == 0) return; _pageIndex = (_pageIndex + 1) % n; Refresh(); }

    /// <summary>【2026-08 新增】方向键 / 滚轮翻页，与结算面板交互保持一致。</summary>
    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PrevPage();
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) NextPage();

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f) PrevPage();
        else if (scroll < -0.1f) NextPage();

        // 右键快速关闭（原注释里承诺过此行为，但代码从未实现）
        if (Input.GetMouseButtonDown(1)) Close();
    }

    private void Refresh()
    {
        // 【2026-08 修复】总页数改为「图片页数 与 文字页数 取较大值」。
        //   旧版只用 _loadedSlides.Length（= slidePaths.Length）作为总页数，
        //   若场景 Inspector 里的 slidePaths 残留着旧版 6 条路径，
        //   而 slideTexts 有 14 条，玩家就只能翻到第 6 页 —— 后 8 页永远看不到。
        int spriteCount = _loadedSlides?.Length ?? 0;
        int textCount   = slideTexts?.Length ?? 0;
        int total = Mathf.Max(spriteCount, textCount);

        // 页码越界保护（slideTexts 被外部改短时 _pageIndex 可能超界）
        if (total > 0) _pageIndex = Mathf.Clamp(_pageIndex, 0, total - 1);
        else _pageIndex = 0;

        bool hasSprite = _pageIndex < spriteCount && _loadedSlides[_pageIndex] != null;
        bool hasText   = _pageIndex < textCount && !string.IsNullOrEmpty(slideTexts[_pageIndex]);

        if (slideImage != null)
        {
            slideImage.sprite = hasSprite ? _loadedSlides[_pageIndex] : null;
            slideImage.enabled = hasSprite;
        }
        if (slideText != null)
        {
            slideText.text = hasText ? slideTexts[_pageIndex] : "";
            slideText.gameObject.SetActive(hasText && !hasSprite); // 文字模式仅在没有图片时显示
            if (font != null && slideText.font != font) slideText.font = font;
        }
        if (pageIndicator != null)
        {
            pageIndicator.text = total > 0 ? $"{_pageIndex + 1} / {total}" : "0 / 0";
            if (font != null && pageIndicator.font != font) pageIndicator.font = font;
        }
    }

    /// <summary>翻页可用的总页数（图片页与文字页取较大值）。</summary>
    private int TotalPages
    {
        get
        {
            int sc = _loadedSlides?.Length ?? 0;
            int tc = slideTexts?.Length ?? 0;
            return Mathf.Max(sc, tc);
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
