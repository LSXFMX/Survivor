using System;
using System.Collections.Generic;
using UnityEngine;

// ════════════════════════════════════════════════════════════════════════════
//  继承装备 · 数据模型
// ────────────────────────────────────────────────────────────────────────────
//  策划案要点对应：
//    1. 暗黑破坏神式：稀有度 + 随机数值 + 可替换 + 择优穿戴，终局玩法
//    2. 世界 Boss 掉落，掉率与难度相关，N9+ 为 100%
//    3. 六档稀有度：原子/质子/中子/电子/无限超弦/奇点（绿/蓝/紫/金/红/星河）
//    4. 稀有度与主词条强度由「当局难度加成」决定 → 无尽模式可无限刷强
//    5. 六个槽位：头盔/衣服/靴子/武器/手镋/项链 → 血量/防御/闪避/攻击/暴击/暴伤
//    6. 副词条池：暴伤/暴击/攻击力/防御力/闪避；奇点可带5 条
//    7. 主词条：暴击上限 20~30%，闪避上限约 10%，其余无上限；除血量外保留 2 位小数
//    8. 副词条：攻击力 1~3、暴击 1~3、暴伤 1~5、防御力 1~5、闪避 1~2
//    9. 分解按主词条与稀有度返还材料；材料用于重铸副词条，同装备重铸费用递增
//   10. 仓库自动分解：稀有度或主词条低于在穿装备则直接分解（完全相同不分解）
// ════════════════════════════════════════════════════════════════════════════

/// <summary>继承装备稀有度。顺序即强度，索引直接用于各种倍率表查询。</summary>
public enum InheritRarity
{
    Atom        = 0, // 原子   · 绿
    Proton      = 1, // 质子   · 蓝
    Neutron     = 2, // 中子   · 紫
    Electron    = 3, // 电子   · 金
    Superstring = 4, // 无限超弦 · 红
    Singularity = 5, // 奇点   · 宇宙星河
}

/// <summary>装备槽位。顺序与UI 上「左三右三」的排布一致。</summary>
public enum InheritSlot
{
    Helmet= 0, // 头盔 → 血量
    Armor    = 1, // 衣服 → 防御
    Boots    = 2, // 靴子 → 闪避
    Bracelet = 3, // 手镯 → 暴击
    Necklace = 4, // 项链 → 暴伤
    Weapon   = 5, // 武器 → 攻击
}

/// <summary>词条可影响的属性。主词条与副词条共用这套枚举。</summary>
public enum InheritStat
{
    Health   = 0, // 血量（唯一取整的属性）
    Defense  = 1, // 防御力
    Evade    = 2, // 闪避
    Attack   = 3, // 攻击力
    CritRate = 4, // 暴击率
    CritDmg  = 5, // 暴击伤害
}

/// <summary>一条副词条。</summary>
[Serializable]
public class InheritSubStat
{
    public InheritStat stat;
    public float value;

    public InheritSubStat() { }
    public InheritSubStat(InheritStat s, float v) { stat = s; value = v; }
}

/// <summary>
/// 一件继承装备实例。
///
/// 用 [Serializable] + JsonUtility 存档（项目已有 JsonUtility 先例），
/// 因此这里只用 JsonUtility 支持的类型：基础类型、枚举、List&lt;T&gt;、[Serializable] 类。
/// 特别注意不能用 Dictionary（JsonUtility 不支持）。
/// </summary>
[Serializable]
public class InheritItem
{
    /// <summary>全局唯一 id，用于"在穿装备"引用与仓库查找。</summary>
    public string uid;

    public InheritSlot   slot;
    public InheritRarity rarity;

    /// <summary>主词条属性（由槽位唯一决定，冗余存储便于 UI 直接读）。</summary>
    public InheritStat mainStat;
    /// <summary>主词条数值。血量为整数，其余保留 2 位小数。</summary>
    public float mainValue;

    public List<InheritSubStat> subStats = new List<InheritSubStat>();

    /// <summary>已重铸次数。用于计算下一次重铸的材料消耗（递增）。</summary>
    public int reforgeCount;

    /// <summary>掉落时的难度力量值，用于显示"这件装备出自多难的一局"。</summary>
    public float dropPower;

    /// <summary>美术分组（0/1/2），由稀有度推导：每两档稀有度共用一套素材。</summary>
    public int ArtTier => ((int)rarity) / 2;

    /// <summary>装备名 = 稀有度前缀 + 槽位名，例如「奇点·星河战刃」。</summary>
    public string DisplayName =>
        $"{InheritEquipmentDefs.RarityName(rarity)}·{InheritEquipmentDefs.SlotItemName(slot, rarity)}";
}

/// <summary>
/// 继承装备的静态定义表：名称、颜色、槽位↔主词条映射、各类系数。
/// 全部集中在这里，避免数值散落在 UI / 生成器 / 管理器三处而对不上。
/// </summary>
public static class InheritEquipmentDefs
{
    // ─────────────────────────── 稀有度 ───────────────────────────

    public const int RARITY_COUNT = 6;

    /// <summary>稀有度中文名（策划案指定）。</summary>
    public static string RarityName(InheritRarity r) => r switch
    {
        InheritRarity.Atom        => "原子",
        InheritRarity.Proton      => "质子",
        InheritRarity.Neutron     => "中子",
        InheritRarity.Electron    => "电子",
        InheritRarity.Superstring => "无限超弦",
        InheritRarity.Singularity => "奇点",
        _                         => "未知",
    };

    /// <summary>
    /// 稀有度主色（边框 / 文字着色用）。
    /// 奇点是"宇宙星河色"——单色无法表达，这里给一个基色，
    /// 实际边框由 InheritRarityBorder 叠加流动星河渲染。
    /// </summary>
    public static Color RarityColor(InheritRarity r) => r switch
    {
        InheritRarity.Atom        => new Color(0.42f, 0.87f, 0.40f), // 绿
        InheritRarity.Proton      => new Color(0.36f, 0.62f, 1.00f), // 蓝
        InheritRarity.Neutron     => new Color(0.72f, 0.42f, 1.00f), // 紫
        InheritRarity.Electron    => new Color(1.00f, 0.80f, 0.25f), // 金
        InheritRarity.Superstring => new Color(1.00f, 0.30f, 0.28f), // 红
        InheritRarity.Singularity => new Color(0.62f, 0.88f, 1.00f), // 星河（基色偏冷白）
        _                         => Color.white,
    };

    /// <summary>富文本用的十六进制色（不含 #）。</summary>
    public static string RarityHex(InheritRarity r)
    {
        Color c = RarityColor(r);
        return ColorUtility.ToHtmlStringRGB(c);
    }

    /// <summary>奇点是否使用星河动效。</summary>
    public static bool IsCosmic(InheritRarity r) => r == InheritRarity.Singularity;

    // ─────────────────────────── 槽位 ───────────────────────────

    public const int SLOT_COUNT = 6;

    public static string SlotName(InheritSlot s) => s switch
    {
        InheritSlot.Helmet   => "头盔",
        InheritSlot.Armor    => "衣服",
        InheritSlot.Boots    => "靴子",
        InheritSlot.Bracelet => "手镯",
        InheritSlot.Necklace => "项链",
        InheritSlot.Weapon   => "武器",
        _                    => "未知",
    };

    /// <summary>
    /// 槽位 → 主词条属性（策划案第 5 条硬性规定）。
    /// 头盔=血量 / 衣服=防御 / 靴子=闪避 / 武器=攻击 / 手镯=暴击 / 项链=暴伤
    /// </summary>
    public static InheritStat MainStatOf(InheritSlot s) => s switch
    {
        InheritSlot.Helmet   => InheritStat.Health,
        InheritSlot.Armor    => InheritStat.Defense,
        InheritSlot.Boots    => InheritStat.Evade,
        InheritSlot.Bracelet => InheritStat.CritRate,
        InheritSlot.Necklace => InheritStat.CritDmg,
        InheritSlot.Weapon   => InheritStat.Attack,
        _                    => InheritStat.Attack,
    };

    /// <summary>不同美术分组下的物件名，让同槽位在不同稀有度有不同称呼。</summary>
    public static string SlotItemName(InheritSlot s, InheritRarity r)
    {
        int tier = ((int)r) / 2;
        switch (s)
        {
            case InheritSlot.Helmet:   return tier == 0 ? "铁盔"   : tier == 1 ? "电子头冠" : "星河王冕";
            case InheritSlot.Armor:    return tier == 0 ? "铁甲"   : tier == 1 ? "电子护胸" : "星河圣衣";
            case InheritSlot.Boots:    return tier == 0 ? "皮靴"   : tier == 1 ? "悬浮战靴" : "星河踏云靴";
            case InheritSlot.Bracelet: return tier == 0 ? "铁腕环" : tier == 1 ? "电子腕环" : "星河臂轮";
            case InheritSlot.Necklace: return tier == 0 ? "原子坠" : tier == 1 ? "中子核链" : "奇点吊坠";
            case InheritSlot.Weapon:   return tier == 0 ? "铁剑"   : tier == 1 ? "电子长刃" : "星河战刃";
            default: return "遗物";
        }
    }

    // ─────────────────────────── 属性 ───────────────────────────

    public static string StatName(InheritStat s) => s switch
    {
        InheritStat.Health   => "血量",
        InheritStat.Defense  => "防御力",
        InheritStat.Evade    => "闪避",
        InheritStat.Attack   => "攻击力",
        InheritStat.CritRate => "暴击",
        InheritStat.CritDmg  => "暴伤",
        _                    => "未知",
    };

    /// <summary>百分比类属性（显示时带 %）。暴击 / 暴伤 / 闪避在本项目里都是百分点。</summary>
    public static bool IsPercent(InheritStat s) =>
        s == InheritStat.CritRate || s == InheritStat.CritDmg || s == InheritStat.Evade;

    /// <summary>血量是唯一取整的属性（策划案第 7 条）。</summary>
    public static bool IsInteger(InheritStat s) => s == InheritStat.Health;

    /// <summary>格式化词条数值：血量取整，其余保留 2 位小数。</summary>
    public static string FormatValue(InheritStat s, float v)
    {
        if (IsInteger(s)) return Mathf.RoundToInt(v).ToString();
        return v.ToString("0.##") + (IsPercent(s) ? "%" : "");
    }

    /// <summary>格式化整条词条，例如「攻击力 +1.45」。</summary>
    public static string FormatStatLine(InheritStat s, float v)
        => $"{StatName(s)} +{FormatValue(s, v)}";
}
