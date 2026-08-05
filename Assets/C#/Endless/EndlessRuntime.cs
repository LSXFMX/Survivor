using UnityEngine;

/// <summary>
/// 无尽模式「难度速度」档位：决定每 5 分钟血量倍率的**加法**增量。
/// 之前固定 +15，现在开放给玩家选，越高档越快滚雪球。
/// </summary>
public enum EndlessSpeedMode
{
    /// <summary>标准：每波 +15（原有手感，25 → 40 → 55…）</summary>
    Standard = 0,
    /// <summary>加速：每波 +50（25 → 75 → 125…）</summary>
    Fast = 1,
    /// <summary>狂暴：每波 +100（25 → 125 → 225…）</summary>
    Frenzy = 2,
}

/// <summary>
/// 无尽模式的运行时状态与配置（单一数据源）。
///
/// 为什么要单独抽出来：
///   1. 波次数（Stage）以前是靠 <c>endlessHpMultiplier / 15</c> **反推**的，
///  一旦每波增量可配（15 / 50 / 100），反推立刻算错 → 继承装备的稀有度权重跟着错。
///      现在由 battleUI 直接写入，谁都不用猜。
///   2. 每波增量（<see cref="HpStepPerStage"/>）要同时被 battleUI（涨怪物血）
///      和 InheritEquipmentGenerator（算装备强度）读到。
///   3. 玩家选的速度档要在主菜单→战斗之间传递，用 PlayerPrefs 持久化最省事，
///      顺便下次进游戏还记得上次的选择。
/// </summary>
public static class EndlessRuntime
{
    private const string KEY_SPEED = "EndlessSpeedMode";

    /// <summary>各档位每波的血量倍率增量。</summary>
    private static readonly float[] HP_STEP = { 15f, 50f, 100f };

    /// <summary>各档位的显示名。</summary>
    private static readonly string[] MODE_NAME = { "标准", "加速", "狂暴" };

 /// <summary>玩家选择的难度速度档（持久化）。</summary>
    public static EndlessSpeedMode SpeedMode
    {
        get
        {
   int v = PlayerPrefs.GetInt(KEY_SPEED, 0);
            return (EndlessSpeedMode)Mathf.Clamp(v, 0, HP_STEP.Length - 1);
        }
        set
        {
PlayerPrefs.SetInt(KEY_SPEED, (int)value);
            PlayerPrefs.Save();
        }
    }

    /// <summary>当前档位每波（5 分钟）的血量倍率增量。</summary>
    public static float HpStepPerStage => HP_STEP[(int)SpeedMode];

    /// <summary>当前档位显示名（标准 / 加速 / 狂暴）。</summary>
    public static string SpeedModeName => MODE_NAME[(int)SpeedMode];

    public static string NameOf(EndlessSpeedMode m) => MODE_NAME[(int)m];
public static float  StepOf(EndlessSpeedMode m) => HP_STEP[(int)m];
    public static int    ModeCount => HP_STEP.Length;

    /// <summary>
    /// 当前无尽波次（每 5 分钟 +1）。由 battleUI.OnEndlessStage 写入，
    /// 存档恢复时也会一并还原，避免反推带来的误差。
    /// </summary>
    public static int Stage { get; set; }

    /// <summary>新一局无尽开始 / 离开无尽时调用。</summary>
public static void ResetRun() => Stage = 0;

    /// <summary>
    /// 当前"敌人总血量倍率"（难度基础倍率 + 无尽累加）。
    /// 这就是玩家在难度面板上看到的那个「敌人血量：×25.0」在无尽推进后的实时值，
 /// 继承装备的掉落强度直接挂在它上面。
    /// </summary>
    public static float TotalHpMultiplier()
    {
   float baseHp = DifficultyManager.Instance != null
         ? DifficultyManager.Instance.Current.hpMultiplier : 1f;
        return baseHp + enemy.endlessHpMultiplier;
    }
}
