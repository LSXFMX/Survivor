using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 继承装备随机生成器（数值核心）。
///
/// ══════════════════════════════════════════════════════════════════════════
///  数值体系设计说明（与现有装备体系 / 关卡难度曲线的对齐依据）
/// ══════════════════════════════════════════════════════════════════════════
///
/// 【1】先量化"现有装备给多少"，作为平衡基线
///     统计 EquipmentInitializer 里所有既有装备的加成，典型量级为：
///       攻击力 atk : +1 ~ +30（最常见 +20/ +15）
///       防御力 def : +1 ~ +10（最常见 +2）
///       暴击   CR  : +2（灵果 +0.1/个）
///       暴伤   CD  : +20
///       闪避   EVA : +1
///       血量       : +10 / +200 / +300
///     其中 **攻击力是最强乘区** —— 伤害公式为
///       最终伤害 = 技能伤害 × (1 + atk × 0.1) −目标防御
///     即每 1 点 atk = +10% 全技能伤害。所以 atk 的随机上限必须比 def/CD 更保守，
///     否则一件武器就能顶掉整套既有装备。
///
/// 【2】难度力量值 DifficultyPower（把"关卡难度曲线"折进随机区间）
///       非无尽：power = 难度编号 n（N1→1 … N13→13）
///       无尽  ：power = 敌人总血量倍率 × 0.5（下限 13，**无上限**）
///                → ×25(开局) = 13、×125 = 62、×525 = 262、×2000 = 1000 …
///     这样做的理由：
///       • 策划案第 4 条要求"稀有度与主词条由当局难度加成决定，无尽更好且无限制"；
///       • 直接挂在玩家能看见的那个「敌人血量：×25.0」上，
///         "怪越硬 → 掉的装备越强"因果直观，且玩家选了更快的无尽难度速度
///         （每波 +15/+50/+100）时装备强度会自动跟着加速，不用额外调参。
///
/// 【3】主词条 = 基础区间 × 稀有度系数 × 成长曲线（**全部无硬上限，可无限刷**）
///       稀有度系数 RARITY_MUL = [1.0, 1.6, 2.5, 4.0, 6.5, 10.0]（约每档×1.6，六档共 ×10）
///       成长曲线   StatScale(stat, power)：按属性分三档，见该方法注释
///         · 攻击/防御/血量：1 + 0.12·power^0.85（近线性，无顶）
///         · 暴伤          ：1 + 0.35·ln(power) （纯乘区，对数增长）
///         · 暴击/闪避     ：1 + 0.15·ln(power) （有 100% 语义天花板，最缓）
///
///     奇点级（×10 稀有度系数）取区间上限时的实际数值：
///┌────────┬──────────┬───────────┬───────────┬────────────┐
///       │ 主词条│ power 13 │ power 62  │ power 262 │ power 1000 │
///       │        │ (血×25)  │ (血×125)  │ (血×525)  │ (血×2000)  │
///       ├────────┼──────────┼───────────┼───────────┼────────────┤
///       │ 攻击   │  +12.4   │   +30.1   │   +87.8   │   +262     │
///       │ 防御   │  +11.3   │   +27.6   │   +80.5   │   +240     │
///       │ 血量   │  +515    │   +1253   │   +3658   │   +10900   │
///       │ 暴伤   │  +47%    │   +61%    │   +74%    │   +86%     │
///       │ 暴击   │  +28%    │   +32%    │   +37%    │   +41%     │
///       │ 闪避   │  +9.7%   │   +11.3%  │   +12.8%  │   +14.3%   │
///       └────────┴──────────┴───────────┴───────────┴────────────┘
///     对照同一时刻的敌人血量：×25 → ×125 → ×525 → ×2000（线性）。
///     攻击用 power^0.85 略慢于线性，配合暴击/暴伤的对数增益，
///     以及玩家自身的技能升级 / 奇遇 / 其它装备（这些都是独立乘区），
///     总战力大致跟得上难度，越往后越依赖"刷到更好的装备"——
///     既满足"无限刷无尽换更强装备"，又不会某一件装备就直接开挂。
///
/// 【4】为什么不设硬上限
///     早期版本把暴击/闪避/暴伤做成"不吃难度系数"的硬上限属性，
///     结果无尽刷再久这三项也一动不动，与"无限成长"的目标冲突。
///     现在改为**全部无顶、按边际收益分层用不同增长速度**：
///     乘区越强的属性涨得越慢（对数），加算数值涨得快（幂 0.85）。
///
/// 【5】稀有度权重分布（难度越高越容易出高稀有度）
///     以"目标档位"为中心做离散高斯：
///       targetTier = (power − 1) / 12 × 5   （N1→0.0、N13→5.0、无尽 >5 后钳到 5）
///       weight[i]  = exp( −(i − targetTier)² / (2σ²) )，σ = 1.15
///     无尽阶段额外给高档位加成：endlessBonus = 无尽阶段 × 0.15，
///叠加到 targetTier 上（已钳制在 5），并对 i≥4 的权重再乘 (1 + endlessBonus)，
///     实现"无尽刷得越久，奇点越常见"（策划案第 4 条的"循环往复得到无限强装备"）。
///
/// 【6】副词条：条数吃稀有度，数值区间固定
///     条数 SUB_COUNT = [1, 2, 3, 4, 4, 5]（奇点 5 条，对应策划案第 6 条）
///     数值区间（策划案第 8 条原文照抄，不做稀有度缩放）：
///       攻击力 1~3 / 暴击 1~3 / 暴伤 1~5 / 防御力 1~5 / 闪避 1~2
///     只缩放条数而不缩放数值，好处是"高稀有度的价值主要体现在主词条与词条数量"，
///     副词条保持可预期，便于玩家判断重铸收益。
///     同一件装备内副词条**允许重复**（只有五种属性，但可重复 roll 出两条暴击率等）。
///
/// 【7】小数精度
///     除血量外全部 Round到 2 位小数（策划案第 7 条"攻击力可以是 1.45"）。
/// ══════════════════════════════════════════════════════════════════════════
/// </summary>
public static class InheritEquipmentGenerator
{
    // ───────────────────────── 系数表 ─────────────────────────

    /// <summary>稀有度系数：约每档 ×1.6，六档合计 ×10。</summary>
    private static readonly float[] RARITY_MUL = { 1.0f, 1.6f, 2.5f, 4.0f, 6.5f, 10.0f };

    /// <summary>各稀有度的副词条条数（策划案：奇点可5 条）。</summary>
    private static readonly int[] SUB_COUNT = { 1, 2, 3, 4, 4, 5 };

    /// <summary>稀有度高斯分布的标准差。越大越平均，越小越集中在目标档。</summary>
    private const float RARITY_SIGMA = 1.15f;

    // ───────────────────────── 主词条基础区间 ─────────────────────────
    // 索引与 InheritStat 对齐；(min, max) 为「原子级 @ power=1」的区间。

    private static (float min, float max) MainBaseRange(InheritStat s) => s switch
    {
        InheritStat.Attack   => (0.30f, 0.60f),   // 最强乘区，压得最保守
        InheritStat.Defense  => (0.25f, 0.55f),   // 防御是减法减伤，高了直接免伤
        InheritStat.Health   => (10f,   25f),
        // 下面三项是百分比乘区，走对数曲线。基础区间按"奇点级起步值"反推：
        //   暴伤 2.5×10×1.898 ≈ 47%、暴击 2.0×10×1.385 ≈ 28%、闪避 0.7×10×1.385 ≈ 10%
        //与策划案第 7 条"暴击 20~30%、闪避约 10%"对齐，再往上靠刷无尽慢慢涨。
        InheritStat.CritDmg  => (1.2f,  2.5f),
        InheritStat.CritRate => (1.2f,  2.0f),
        InheritStat.Evade    => (0.35f, 0.70f),
        _                    => (1f,    2f),
    };

    // ═══════════════ 成长曲线：三档，**全部无硬上限，可无限成长** ═══════════════
    //
    // 设计目标（玩家明确要求）：「无限刷无尽模式来获得更好的装备」
    //   → 任何属性都不设封顶，power 越高数值越高，永远有追求空间。
    //
    // 但不同属性对战力的**边际收益差别极大**，用同一条曲线必然出事
    // （上一版暴伤跟着线性涨到 +523%，一件项链让伤害翻好几倍）。
    // 所以按"这属性乘区有多强"分成三档，用不同增长速度：
    //
    // ┌──────────────┬──────────────────────┬────────────────────────────────┐
    // │ 属性         │ 曲线                 │ 为什么                         │
    // ├──────────────┼──────────────────────┼────────────────────────────────┤
    // │ 攻击/防御/血量│ 1 + 0.12·power^0.85 │ 加算数值，边际收益递减，        │
    // │              │ （近线性，无顶）     │ 可以放开涨才追得上线性血量       │
    // │ 暴伤         │ 1 + 0.35·ln(power)   │ 纯乘区，线性涨必失控，对数增长│
    // │ 暴击/闪避│ 1 + 0.15·ln(power)   │ 有100% 语义天花板，涨得最慢     │
    // └──────────────┴──────────────────────┴────────────────────────────────┘
    //
    // 实际数值（奇点级、稀有度系数 ×10、取区间上限；已按公式核算）：
    //   power        13(血×25)  62(血×125)  262(血×525)  1000(血×2000)
    //   成长系数 num    2.06       5.01        14.63        43.6
    //   成长系数 cd     1.90       2.44        2.95         3.42
    //   成长系数 pct    1.39       1.62        1.84         2.04
    //   ──────────────────────────────────────────────────────────────
    //   攻击          +12.4      +30.1       +87.8        +262
    //   防御          +11.3      +27.6       +80.5        +240
    //   血量          +515       +1253       +3658        +10900
    //   暴伤          +47%       +61%        +74%+86%
    //   暴击          +28%       +32%        +37%         +41%
    //   闪避          +9.7%      +11.3%      +12.8%       +14.3%
    //
    // 对照：敌人血量在同一时刻是 ×25 → ×125 → ×525 → ×2000（线性）。
    // 攻击的 power^0.85 略慢于线性，配合暴击/暴伤的对数增益与玩家自身的
    // 技能升级/奇遇/其它装备（这些是独立乘区），总战力大致能跟上难度、
    // 且越往后越需要靠"刷到更好的装备"来续命 —— 既有无限成长，也保留挑战。
    private const float NUM_COEF = 0.12f;   // 数值类系数
    private const float NUM_EXP  = 0.85f;   // 数值类幂次（<1 = 边际递减，但无顶）
    private const float CD_COEF  = 0.35f;   // 暴伤（对数）
    private const float PCT_COEF = 0.15f;   // 暴击 / 闪避（对数，最缓）

    /// <summary>
    /// 按属性类别取成长系数（作用在稀有度系数之上）。**无任何硬上限。**
    /// </summary>
    public static float StatScale(InheritStat s, float power)
    {
        if (power <= 1f) return 1f;
        switch (s)
        {
            case InheritStat.CritRate:
            case InheritStat.Evade:
                return 1f + PCT_COEF * Mathf.Log(power);
            case InheritStat.CritDmg:
                return 1f + CD_COEF * Mathf.Log(power);
            default:   // Attack / Defense / Health
                return 1f + NUM_COEF * Mathf.Pow(power, NUM_EXP);
        }
    }

    // ───────────────────────── 副词条区间 ─────────────────────────
    // 策划案第 8 条原文：攻击力1~3，暴击1~3，暴伤1~5，防御力1~5，闪避1~2

    private static readonly InheritStat[] SUB_POOL =
    {
        InheritStat.Attack, InheritStat.CritRate, InheritStat.CritDmg,
        InheritStat.Defense, InheritStat.Evade,
    };

    private static (float min, float max) SubRange(InheritStat s) => s switch
    {
        InheritStat.Attack   => (1f, 3f),
        InheritStat.CritRate => (1f, 3f),
        InheritStat.CritDmg  => (1f, 5f),
        InheritStat.Defense  => (1f, 5f),
        InheritStat.Evade    => (1f, 2f),
        _                    => (1f, 2f),
    };

    // ═════════════════════════ 对外 API ═════════════════════════

    /// <summary>
    /// 当前这一局的「难度力量值」。
    /// 非无尽 = 难度编号（N1→1 … N13→13）。
    ///
    /// 无尽 = **直接挂在敌人总血量倍率上**（难度面板上那个「敌人血量：×25.0」的实时值）：
    ///   power = 总血量倍率 × POWER_PER_HP_MULT
    ///   ×25（开局）→ 12.5 → 钳到 13（与 N13 齐平）
    ///   ×100     → 50
    ///   ×500       → 250
    /// 这样"敌人变多硬 → 掉的装备就变多强"完全同步，且玩家选了更快的难度速度
    /// （每波 +50/+100）时，装备强度自然跟着一起加速，不需要额外调参。
    /// </summary>
 private const float POWER_PER_HP_MULT = 0.5f;

    public static float CurrentPower()
    {
var dm = DifficultyManager.Instance;
        if (dm == null) return 1f;

        if (dm.IsEndless)
      {
      float totalHp = EndlessRuntime.TotalHpMultiplier();
   // 下限 13：无尽本身就是 N13 之后的内容，装备强度不该低于 N13
            return Mathf.Max(13f, totalHp * POWER_PER_HP_MULT);
  }

        return dm.CurrentIndex + 1;
    }

    /// <summary>当前无尽波次（每 5 分钟 +1）。非无尽返回 0。</summary>
    public static int EndlessStage()
    {
 // 【2026-08】改为读 EndlessRuntime.Stage（battleUI 直接写入）。
        // 以前是用 endlessHpMultiplier / 15 反推的，但每波增量现在可由玩家选择
        //（标准 15 / 加速 50 / 狂暴 100），反推必然算错。
        var dm = DifficultyManager.Instance;
      if (dm == null || !dm.IsEndless) return 0;
        return Mathf.Max(0, EndlessRuntime.Stage);
    }

    /// <summary>
    /// 单次世界 Boss 掉落的**件数**：无尽模式随敌人血量倍率递增。
    /// 每 ×125 血量倍率 +1 件，上限 5 件
    /// （留个上限只是为了别一次刷爆 120 格仓库，不是强度上限）。
    /// 非无尽恒 1 件。
    /// </summary>
    public static int DropCount()
    {
        var dm = DifficultyManager.Instance;
        if (dm == null || !dm.IsEndless) return 1;

        float totalHp = EndlessRuntime.TotalHpMultiplier();
        return Mathf.Clamp(1 + Mathf.FloorToInt(totalHp / 125f), 1, 5);
    }

    /// <summary>
    /// 世界 Boss 掉落继承装备的概率。
    /// 策划案第 2 条：与难度相关，N9 起（含 N9）为 100%。
    /// 公式 chance = n / 9 → N1 = 11%、N4 = 44%、N8 = 89%、N9+ = 100%；无尽恒 100%。
    /// </summary>
    public static float DropChance()
    {
        var dm = DifficultyManager.Instance;
        if (dm == null) return 1f;
        if (dm.IsEndless) return 1f;

        int n = dm.CurrentIndex + 1;
        return Mathf.Clamp01(n / 9f);
    }

    /// <summary>按当前难度掷一次稀有度。</summary>
    public static InheritRarity RollRarity(float power)
    {
        // 目标档位：N1 → 0，N13 → 5
        float targetTier = Mathf.Clamp((power - 1f) / 12f * 5f, 0f, 5f);

        // 无尽额外偏移：刷得越久越容易出高档
        int stage = EndlessStage();
        float endlessBonus = stage * 0.15f;
        targetTier = Mathf.Clamp(targetTier + endlessBonus, 0f, 5f);

        var weights = new float[InheritEquipmentDefs.RARITY_COUNT];
        float total = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            float d = i - targetTier;
            float w = Mathf.Exp(-(d * d) / (2f * RARITY_SIGMA * RARITY_SIGMA));
            // 无尽后期进一步拉高顶档出现率
            if (i >= 4 && endlessBonus > 0f) w *= 1f + endlessBonus;
            weights[i] = w;
            total += w;
        }

        float roll = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (roll <= acc) return (InheritRarity)i;
        }
        return InheritRarity.Atom;
    }

    /// <summary>生成一件随机继承装备（掉落入口）。</summary>
    public static InheritItem Generate()
    {
        float power = CurrentPower();
        InheritSlot slot = (InheritSlot)Random.Range(0, InheritEquipmentDefs.SLOT_COUNT);
        return Generate(slot, RollRarity(power), power);
    }

    /// <summary>生成指定槽位 / 稀有度的装备（调试与重铸复用）。</summary>
    public static InheritItem Generate(InheritSlot slot, InheritRarity rarity, float power)
    {
        var item = new InheritItem
        {
            uid       = System.Guid.NewGuid().ToString("N").Substring(0, 12),
            slot      = slot,
            rarity    = rarity,
            mainStat  = InheritEquipmentDefs.MainStatOf(slot),
            dropPower = power,
        };

        item.mainValue = RollMainValue(item.mainStat, rarity, power);
        item.subStats  = RollSubStats(rarity, power);
        return item;
    }

    /// <summary>掷主词条数值。</summary>
    public static float RollMainValue(InheritStat stat, InheritRarity rarity, float power)
    {
        var (lo, hi) = MainBaseRange(stat);
        // 稀有度系数 × 该属性对应的成长曲线（无硬上限，见 StatScale）
        float mul = RARITY_MUL[(int)rarity] * StatScale(stat, power);

        float v = Random.Range(lo, hi) * mul;
        return Quantize(stat, v);
    }

    /// <summary>
    /// 掷副词条。条数由稀有度决定。
    ///
    /// 【副词条允许重复】
    ///   副词条只有五种属性（SUB_POOL：暴击/暴伤/攻击/防御/闪避），
    ///   但每条独立从池中随机抽取、**允许出现相同属性**——
    ///   比如同件装备可以 roll 出两条暴击率副词条（玩家明确要求的设定）。
    ///   条数上限由稀有度决定（SUB_COUNT），与属性种类无关。
    ///
    /// 【2026-08 副词条也随难度成长】
    ///   之前副词条是**固定区间**、完全不吃 power，于是无尽刷到血量 ×2000 时
    ///   奇点装备的 5 条副词条依然只有 +1%~3%，形同摆设，
    ///   与"无限刷无尽换更强装备"的目标背道而驰。
    ///   现在同样套用 StatScale，但只取其<see cref="SUB_SCALE_SHARE"/>（60%）的增益：
    ///   主词条依然是装备的核心，副词条作为补充跟着涨、不抢主词条的戏。
    /// </summary>
    private const float SUB_SCALE_SHARE = 0.60f;

    public static List<InheritSubStat> RollSubStats(InheritRarity rarity, float power = 1f)
    {
        int count = SUB_COUNT[(int)rarity];
        var result = new List<InheritSubStat>(count);
        for (int i = 0; i < count; i++)
        {
            InheritStat s = SUB_POOL[Random.Range(0, SUB_POOL.Length)];
            var (lo, hi) = SubRange(s);
            // 只吃 60% 的成长曲线：1 + (scale − 1) × 0.6
            float scale = 1f + (StatScale(s, power) - 1f) * SUB_SCALE_SHARE;
            result.Add(new InheritSubStat(s, Quantize(s, Random.Range(lo, hi) * scale)));
        }
        return result;
    }

    /// <summary>血量取整，其余保留 2 位小数（策划案第 7 条）。</summary>
    public static float Quantize(InheritStat stat, float v)
    {
        if (InheritEquipmentDefs.IsInteger(stat)) return Mathf.Round(v);
        return Mathf.Round(v * 100f) / 100f;
    }

    // ═════════════════════════ 分解 / 重铸 ═════════════════════════

    /// <summary>各稀有度分解返还的基础材料量（约每档翻倍）。</summary>
    private static readonly int[] SALVAGE_BASE = { 1, 2, 4, 8, 16, 32 };

    /// <summary>
    /// 分解返还材料。
    /// 策划案第 9 条："根据其主属性和稀有度返还材料"。
    ///   材料 = 稀有度基数 × (1 + 主词条品质系数)
    /// 主词条品质系数 = 该数值在「同稀有度同难度理论区间」内的归一化位置（0~1），
    /// 因此"主词条越接近满值的装备，分解返还越多"，玩家分解垃圾装备不会亏、
    /// 分解极品会心疼——这正是暗黑式取舍的一部分。
    /// </summary>
    public static int SalvageValue(InheritItem item)
    {
        if (item == null) return 0;

        var (lo, hi) = MainBaseRange(item.mainStat);
        float mul = RARITY_MUL[(int)item.rarity] * StatScale(item.mainStat, item.dropPower);

        float rangeLo = lo * mul, rangeHi = hi * mul;
        float quality = rangeHi > rangeLo
            ? Mathf.Clamp01((item.mainValue - rangeLo) / (rangeHi - rangeLo))
            : 0.5f;

        int baseV = SALVAGE_BASE[(int)item.rarity];
        return Mathf.Max(1, Mathf.RoundToInt(baseV * (1f + quality)));
    }

    /// <summary>
    /// 重铸消耗。
    /// 策划案第 9 条："同一装备每次重铸会增加消耗的材料"。
    ///   消耗 = 稀有度基数 × 2 × (已重铸次数 + 1)
    /// 线性递增而非指数：指数会让第 5 次重铸直接不可能，
    /// 线性能让玩家持续投入、又始终感到成本压力。
    /// </summary>
    public static int ReforgeCost(InheritItem item)
    {
        if (item == null) return 0;
        int baseV = SALVAGE_BASE[(int)item.rarity];
        return Mathf.Max(1, baseV * 2 * (item.reforgeCount + 1));
    }

    /// <summary>
    /// 执行重铸：重掷全部副词条（种类与数值都会变），主词条与稀有度不变。
    /// 策划案第 9 条："重铸可以改变副属性的种类和数值"。
    /// </summary>
    public static void ApplyReforge(InheritItem item)
    {
        if (item == null) return;
        // 用装备**掉落时**的 power 重掷：重铸是改词条，不该顺便把装备"升级"到当前难度，
        // 否则玩家会拿低难度装备反复重铸来白嫖高难度数值。
        item.subStats = RollSubStats(item.rarity, item.dropPower);
        item.reforgeCount++;
    }
}
