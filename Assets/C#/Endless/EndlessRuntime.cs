using UnityEngine;

/// <summary>
/// 无尽模式 = **无尽之塔**（Endless Tower）的运行时状态与配置（单一数据源）。
///
/// ════════════════════════ 设计 ════════════════════════
///
/// 参考暗黑破坏神「大秘境」：层数无上限，每层显著更难，玩家靠刷更好的装备去挑战更高层，
/// 高层又反过来掉更强的装备 —— 形成正反馈的无限循环。
///
/// 【1】每层「每波（5 分钟）血量倍率增量」=<see cref="HpStepOfFloor"/>
///     前三层严格按需求给的15 / 30 / 60 翻倍，第 4 层起改成 ×1.6 递增，
///     否则纯 2ⁿ 到十几层就会把数值推到几十万、显示与手感都崩：
///
///       ┌──────┬────────────┬──────────────────────────────────────┐
///       │ 层   │ 每波 +     │ 半小时(6波)后的总血量倍率(含开局奖励)│
///       ├──────┼────────────┼──────────────────────────────────────┤
///       │  1   │     15     │ 25 +30 + 90   = ×145│
///       │  2   │     30     │ 25 + 60 + 180  = ×265                │
///       │  3   │     60     │ 25 + 120 + 360 = ×505                │
///       │  4   │     96     │ ×793 │
///       │  5   │    154     │ ×1256                                │
///       │  8   │    629     │ ×5057                                │
///       │ 12   │   4123│ ×33.1k                               │
///       │ 20   │  177k      │ ×1.42M                               │
///       └──────┴────────────┴──────────────────────────────────────┘
///
///     为什么这条曲线是"好曲线"：继承装备的数值走
///     <c>1 + 0.12·power^0.85</c>（power = 总血量倍率 × 0.5），
///     也就是**装备强度 ≈ 血量^0.85**。每层血量 ×1.6 时装备只×1.48，
///     差距每层缓慢拉大 —— 于是「换一批更好的装备」能让你多撑住一层左右，
///     但永远不可能一路平推到底，这正是暗黑大秘境的爬层手感。
///
/// 【2】开局奖励倍率 <see cref="InitialHpBonus"/> = 每波增量 × 2
///     否则高层开局也是从 ×25 慢慢爬，前 10 分钟毫无压力、纯浪费时间。
///     相当于"进门就已经打过 2 波"。
///
/// 【3】解锁条件：在某层**局内计时**撑满 <see cref="UNLOCK_SECONDS"/>（30 分钟）
///     即永久解锁下一层。用局内计时而不是击杀数/波次，理由是无尽本身就是计时模式，
///     玩家一眼能看懂进度；也不受倍速影响的争议（倍速会加速局内计时，
///     那是玩家自己选的加速通道，不作限制）。
///
/// 【4】Stage（波次）由 battleUI 直接写入，**绝不用endlessHpMultiplier 反推**——
///     每波增量随层数变化，反推必错，继承装备的稀有度权重会跟着一起错。
/// </summary>
public static class EndlessRuntime
{
    // ─────────────────────────── 常量 ───────────────────────────

    /// <summary>第 1 层的每波血量倍率增量。</summary>
    public const float BASE_STEP = 15f;

    /// <summary>前 3 层的翻倍段（15 → 30 → 60）。</summary>
    private const int  DOUBLE_FLOORS = 3;

    /// <summary>第 4 层起的层间增长比（1.6 倍）。</summary>
    private const float GROWTH = 1.6f;

    /// <summary>解锁下一层所需的**局内**存活时长（秒）。</summary>
    public const float UNLOCK_SECONDS = 1800f;   // 30 分钟

    /// <summary>
    /// 层数硬上限。设200纯粹是防溢出用的（第 200 层每波增量已是天文数字，
    /// 现实中不可能有人打到），不是玩法上的封顶。
    /// </summary>
    public const int MAX_FLOOR = 200;

    private const string KEY_FLOOR      = "EndlessTower.Floor";
    private const string KEY_UNLOCKED   = "EndlessTower.MaxUnlocked";
    private const string KEY_BEST_PREFIX = "EndlessTower.Best.";

    // ─────────────────────────── 层数配置 ───────────────────────────

    /// <summary>
    /// 第<paramref name="floor"/> 层每波（5 分钟）的血量倍率增量。
    /// 1→15、2→30、3→60，之后每层 ×1.6。
    /// </summary>
    public static float HpStepOfFloor(int floor)
    {
        floor = Mathf.Clamp(floor, 1, MAX_FLOOR);

        if (floor <= DOUBLE_FLOORS)
            return BASE_STEP * Mathf.Pow(2f, floor - 1);

        float atDouble = BASE_STEP * Mathf.Pow(2f, DOUBLE_FLOORS - 1);   // 第 3 层 = 60
        return atDouble * Mathf.Pow(GROWTH, floor - DOUBLE_FLOORS);
    }

    /// <summary>
    /// 进入该层时预先叠加的血量倍率（= 每波增量 × 2）。
    /// 让高层从一开始就有对应的压迫感，不必再从 ×25 慢慢爬。
    /// </summary>
    public static float InitialHpBonus(int floor) => HpStepOfFloor(floor) * 2f;

    /// <summary>该层的开局总血量倍率（难度基础 + 开局奖励），用于 UI 预览。</summary>
    public static float StartTotalHpOfFloor(int floor)
    {
        float baseHp = DifficultyManager.Instance != null
            ? DifficultyManager.Instance.Current.hpMultiplier : 25f;
        return baseHp + InitialHpBonus(floor);
    }

    // ─────────────────────────── 当前层 / 解锁进度 ───────────────────────────

    /// <summary>玩家选中的挑战层（持久化，自动钳到已解锁范围）。</summary>
    public static int CurrentFloor
    {
        get
        {
            int v = PlayerPrefs.GetInt(KEY_FLOOR, 1);
            return Mathf.Clamp(v, 1, MaxUnlockedFloor);
        }
        set
        {
            PlayerPrefs.SetInt(KEY_FLOOR, Mathf.Clamp(value, 1, MaxUnlockedFloor));
            PlayerPrefs.Save();
        }
    }

    /// <summary>已解锁的最高层（至少 1）。</summary>
    public static int MaxUnlockedFloor =>
        Mathf.Clamp(PlayerPrefs.GetInt(KEY_UNLOCKED, 1), 1, MAX_FLOOR);

    /// <summary>某层的历史最长存活时间（秒）。</summary>
    public static float BestTimeOfFloor(int floor) =>
        PlayerPrefs.GetFloat(KEY_BEST_PREFIX + floor, 0f);

    /// <summary>
    /// 上报本层当前的局内存活时长。
    ///
    /// 做两件事：①刷新该层最佳记录；② 满<see cref="UNLOCK_SECONDS"/> 就解锁下一层。
    /// 由 battleUI 在无尽计时里每秒调一次即可（内部自己做节流与幂等）。
    /// </summary>
    /// <returns>本次调用是否**刚刚**解锁了新层（用于弹一次 Toast）。</returns>
    public static bool ReportElapsed(int floor, float elapsed)
    {
        floor = Mathf.Clamp(floor, 1, MAX_FLOOR);
        RunElapsed = elapsed;   // 顺手把局内计时暴露出去（奇遇·愚弄的 120 分钟门槛要用）

        // ① 最佳记录（每 5 秒写一次盘，避免每帧 IO）
        if (elapsed > BestTimeOfFloor(floor) && elapsed - _lastBestWrite >= 5f)
        {
            _lastBestWrite = elapsed;
            PlayerPrefs.SetFloat(KEY_BEST_PREFIX + floor, elapsed);
        }

        // ② 解锁下一层
        if (elapsed < UNLOCK_SECONDS) return false;
        if (floor >= MAX_FLOOR) return false;
        if (MaxUnlockedFloor > floor) return false;      // 早就解锁过了

        PlayerPrefs.SetInt(KEY_UNLOCKED, floor + 1);
        PlayerPrefs.Save();
        return true;
    }

    private static float _lastBestWrite;

    // ─────────────────────────── 本局运行时状态 ───────────────────────────

    /// <summary>本局实际挑战的层（进战斗时从<see cref="CurrentFloor"/> 快照，中途不受菜单改动影响）。</summary>
    public static int RunFloor { get; private set; } = 1;

    /// <summary>当前无尽波次（每 5 分钟 +1）。由 battleUI.OnEndlessStage 写入。</summary>
    public static int Stage { get; set; }

    /// <summary>
    /// 本局**局内计时**已过秒数（由 <see cref="ReportElapsed"/> 每秒刷新）。
    ///
    /// 为什么单独暴露：奇遇「愚弄」在无尽模式要按"每 120 分钟多一次选择机会"放行，
    /// 而 <c>Time.time</c> 是现实时间 —— 无尽支持倍速，两者会严重不一致；
    /// 而且暂停/奇遇面板期间 <c>Time.time</c> 仍在走。统一用 battleUI 那份
    /// 受 <c>Time.deltaTime</c> 驱动的局内计时，玩家看到的分钟数才和判定一致。
    /// </summary>
    public static float RunElapsed { get; private set; }

    /// <summary>本局每波的血量倍率增量（= 本局层数对应的增量）。</summary>
    public static float HpStepPerStage => HpStepOfFloor(RunFloor);

    /// <summary>层数显示名，例如「第 3 层」。</summary>
    public static string FloorName(int floor) => $"第 {floor} 层";

    /// <summary>本局层数显示名。</summary>
    public static string RunFloorName => FloorName(RunFloor);

    /// <summary>新一局无尽开始时调用：快照层数、清波次。</summary>
    public static void ResetRun()
    {
        RunFloor       = CurrentFloor;
        Stage          = 0;
        RunElapsed     = 0f;
        _lastBestWrite = 0f;
    }

    /// <summary>
    /// 当前"敌人总血量倍率"（难度基础倍率 + 无尽累加）。
    /// 这就是难度面板上那个「敌人血量：×25.0」在无尽推进后的实时值，
    /// 继承装备的掉落强度直接挂在它上面。
    /// </summary>
    public static float TotalHpMultiplier()
    {
        float baseHp = DifficultyManager.Instance != null
            ? DifficultyManager.Instance.Current.hpMultiplier : 1f;
        return baseHp + enemy.endlessHpMultiplier;
    }
}
