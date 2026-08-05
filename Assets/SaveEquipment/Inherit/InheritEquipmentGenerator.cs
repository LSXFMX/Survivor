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
///       无尽  ：power = 13 + 无尽阶段数 × 1.5（每 5 分钟一阶段，无上限）
///     这样做的理由：
///       • 策划案第 4 条要求"稀有度与主词条由当局难度加成决定，无尽更好且无限制"；
///       • 直接用敌人血量倍率（N7 已经 ×20）会让曲线爆炸，
///         而难度编号是线性且玩家可感知的，更适合做装备强度轴。
///
/// 【3】主词条 = 基础区间 × 稀有度系数 × 难度系数
///       稀有度系数 RARITY_MUL = [1.0, 1.6, 2.5, 4.0, 6.5, 10.0]（约每档×1.6，六档共 ×10）
///       难度系数   diffScale  = 1 + 0.12 × (power − 1)
///                  → N1 ×1.0、N13 ×2.44、无尽第 10 阶段 ×4.24（持续增长）
///
///     基础区间（原子级@N1）与推导出的奇点级 @N13 上限：
///       ┌────────┬──────────────┬───────────────┬──────────────────────┐
///       │ 主词条│ 原子@N1      │ 奇点@N13      │ 对照既有装备          │
///       ├────────┼──────────────┼───────────────┼──────────────────────┤
///       │ 攻击   │ 0.30 ~ 0.60  │ 7.3 ~ 14.6    │ 单件最强 +30→ 未超  │
///       │ 防御   │ 0.40 ~ 0.90  │ 9.8 ~ 22.0    │ 单件最强 +10 → 略超  │
///       │ 血量   │ 15 ~ 40      │ 366 ~ 976     │ 单件最强 +300 → 略超 │
///       │ 暴伤   │ 2.0 ~ 4.0│ 48.8 ~ 97.6│ 单件最强 +20 → 超│
///       └────────┴──────────────┴───────────────┴──────────────────────┘
///     "略超/超"是**有意为之**：继承装备是终局玩法，必须比一次性解锁的装备更有吸引力，
///     且需要玩家刷到奇点 + 高难度才能拿到上限值（概率极低），属于长线追求目标。
///     攻击力刻意压到不超过既有上限，因为它是 ×10% 伤害的乘区，超了会直接破坏平衡。
///
/// 【4】暴击 / 闪避是**硬上限**属性，不吃难度系数
///     策划案第 7 条明确："暴击最高 20%~30%，闪避最高约 10%"。
///     这两项若跟着难度无限涨会直接失控（暴击 >100% 无意义、闪避 100% 则无敌），
///     因此：
///       暴击：基础 2.0~3.0，×稀有度系数10 → 奇点恰好 20~30，**不乘难度系数**
///       闪避：基础 0.5~1.0，×稀有度系数10 → 奇点恰好  5~10，**不乘难度系数**
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
///     同一件装备内副词条**不重复**（避免出现两条攻击力挤掉其他属性的退化情况）。
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

    /// <summary>难度系数的每级增量。</summary>
    private const float DIFF_SCALE_PER_POWER = 0.12f;

    /// <summary>稀有度高斯分布的标准差。越大越平均，越小越集中在目标档。</summary>
    private const float RARITY_SIGMA = 1.15f;

    // ───────────────────────── 主词条基础区间 ─────────────────────────
    // 索引与 InheritStat 对齐；(min, max) 为「原子级 @ power=1」的区间。

    private static (float min, float max) MainBaseRange(InheritStat s) => s switch
    {
        InheritStat.Attack   => (0.30f, 0.60f),   // 最强乘区，压得最保守
        InheritStat.Defense  => (0.40f, 0.90f),
        InheritStat.Health   => (15f,   40f),
        InheritStat.CritDmg  => (2.0f,  4.0f),
        InheritStat.CritRate => (2.0f,  3.0f),    // ×10 → 奇点 20~30（硬上限）
        InheritStat.Evade    => (0.5f,  1.0f),    // ×10 → 奇点  5~10（硬上限）
        _                    => (1f,    2f),
    };

    /// <summary>暴击 / 闪避不吃难度系数（硬上限属性）。</summary>
    private static bool IsCappedStat(InheritStat s) =>
        s == InheritStat.CritRate || s == InheritStat.Evade;

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
    /// 非无尽 = 难度编号（N1→1 … N13→13）；无尽 = 13 + 阶段数 × 1.5（无上限）。
    /// </summary>
    public static float CurrentPower()
    {
        var dm = DifficultyManager.Instance;
        if (dm == null) return 1f;

        if (dm.IsEndless)
            return 13f + EndlessStage() * 1.5f;

        return dm.CurrentIndex + 1;
    }

    /// <summary>当前无尽阶段数（每 5 分钟 +1）。非无尽返回 0。</summary>
    public static int EndlessStage()
    {
        // 无尽阶段直接由累积的血量倍率反推：每阶段 +5~10，取中位 7.5
        // （battleUI 没有对外暴露 stage，用倍率反推足够精确地驱动稀有度权重）
        float extra = enemy.endlessHpMultiplier - 1f;
        if (extra <= 0f) return 0;
        return Mathf.Max(0, Mathf.RoundToInt(extra / 7.5f));
    }

    /// <summary>难度系数（只作用于非硬上限属性）。</summary>
    public static float DiffScale(float power) => 1f + DIFF_SCALE_PER_POWER * (power - 1f);

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
        item.subStats  = RollSubStats(rarity, item.mainStat);
        return item;
    }

    /// <summary>掷主词条数值。</summary>
    public static float RollMainValue(InheritStat stat, InheritRarity rarity, float power)
    {
        var (lo, hi) = MainBaseRange(stat);
        float mul = RARITY_MUL[(int)rarity];

        // 暴击 / 闪避是硬上限属性，不乘难度系数
        if (!IsCappedStat(stat)) mul *= DiffScale(power);

        float v = Random.Range(lo, hi) * mul;
        return Quantize(stat, v);
    }

    /// <summary>
    /// 掷副词条。条数由稀有度决定。
    ///
    /// 【2026-08 修正】副词条**不允许与主词条重复**：
    ///   副词条池 SUB_POOL 里有暴击/暴伤，而手镯的主词条就是暴击——
    ///   以前会出现「主词条暴击 + 副词条暴击」的观感重复（玩家反馈
    ///   "一件装备有两个暴击率"）。现在生成时把主词条从池里剔除，
    ///   副词条之间再用不放回抽签保证彼此也不重复。
    ///   若剔除后可用属性数少于条数上限，条数自动钳到可用数
    ///   （例：手镯主词条=暴击，副词条只剩 4 种 → 奇点最多 4 条而非 5 条）。
    /// </summary>
    public static List<InheritSubStat> RollSubStats(InheritRarity rarity, InheritStat mainStat = InheritStat.Attack)
    {
        // 副词条池剔除主词条 → 主/副词条永不同属性
        var pool = new List<InheritStat>(SUB_POOL);
        pool.Remove(mainStat);

        // 不放回抽签（Fisher-Yates 洗牌 + 取前 count）→ 副词条彼此不重复
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Min(SUB_COUNT[(int)rarity], pool.Count);
        var result = new List<InheritSubStat>(count);
        for (int i = 0; i < count; i++)
        {
            InheritStat s = pool[i];
            var (lo, hi) = SubRange(s);
            result.Add(new InheritSubStat(s, Quantize(s, Random.Range(lo, hi))));
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
        float mul = RARITY_MUL[(int)item.rarity];
        if (!IsCappedStat(item.mainStat)) mul *= DiffScale(item.dropPower);

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
        item.subStats = RollSubStats(item.rarity, item.mainStat);
        item.reforgeCount++;
    }
}
