using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 三存档槽管理器。
///
/// ═════════════════════════ 为什么这样实现 ═════════════════════════
///
/// 项目里所有进度都存在 <see cref="PlayerPrefs"/>（43 个脚本、上百个键，
/// 到处都是直写直读）。要做"多存档槽"，理论上最干净的办法是给每个键加槽位前缀，
/// 但那意味着把 43 个脚本的 PlayerPrefs 调用全部改掉 —— 风险极高、收益不成正比。
///
/// 所以这里采用**搬箱子**方案，对现有代码零侵入：
///· PlayerPrefs 里永远只放"当前槽"的数据，所有老代码照原样跑，完全不用改；
///   · 切槽时把当前 PlayerPrefs 中的进度键**导出**成 JSON 落盘
///     （<c>persistentDataPath/SaveSlots/slot{N}.json</c>），删掉这些键，
///     再把目标槽的 JSON **导入**回 PlayerPrefs。
///
/// PlayerPrefs 无法枚举键，所以进度键必须显式列清单（见 <see cref="ProgressKeys"/>）。
/// 清单由全项目 grep 得到，动态键（EQ_x_y、ClearCount_Nx、GachaPool_xx_y …）
/// 按取值范围穷举后用 HasKey 过滤 —— 几百次 HasKey 的开销可以忽略。
///
/// 【全局设置不跟着切换】音量、伤害数字、后台运行、控制台、测试模式这些属于
/// "客户端偏好"而非"游戏进度"，见 <see cref="GlobalKeys"/>，切槽时原样保留。
/// </summary>
public static class SaveSlotManager
{
    public const int SLOT_COUNT = 3;

    /// <summary>当前槽位号。**本身是全局键**，不参与槽位搬运。</summary>
    private const string KEY_CURRENT = "SaveSlot.Current";

    private enum PrefType { Int, Float, Str }

    // ─────────────────────────── 当前槽 ───────────────────────────

    public static int CurrentSlot
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KEY_CURRENT, 1), 1, SLOT_COUNT);
        private set
        {
            PlayerPrefs.SetInt(KEY_CURRENT, Mathf.Clamp(value, 1, SLOT_COUNT));
            PlayerPrefs.Save();
        }
    }

    private static string SlotDir => Path.Combine(Application.persistentDataPath, "SaveSlots");
    private static string SlotFile(int slot) => Path.Combine(SlotDir, $"slot{slot}.json");

    // ─────────────────────────── 切槽 ───────────────────────────

    /// <summary>
    /// 切换到指定槽位：把当前进度落盘到当前槽 → 清空 PlayerPrefs 里的进度键
    /// → 载入目标槽（不存在就是一个全新的空档，玩家可以从零开荒）。
    /// </summary>
    /// <returns>是否真的切换了（点当前槽返回 false）。</returns>
    public static bool SwitchTo(int slot)
    {
        slot = Mathf.Clamp(slot, 1, SLOT_COUNT);
        int from = CurrentSlot;
        if (slot == from) return false;

        SaveCurrentSlot();          // ① 存当前
        ClearProgressKeys();        // ② 清场
        CurrentSlot = slot;         // ③ 记住新槽（放在 Clear 之后，Clear 不会碰这个键）
        LoadSlotIntoPrefs(slot);    // ④ 载入目标（文件不存在 → 保持空白= 新档）
        PlayerPrefs.Save();

        ReloadRuntimeCaches();      // ⑤ 让缓存了PlayerPrefs 的单例重新读盘

        Debug.Log($"[SaveSlot] 已从存档 {from} 切换到存档 {slot}");
        return true;
    }

    /// <summary>把当前 PlayerPrefs 里的进度写回当前槽文件（切槽、退出游戏前调用）。</summary>
    public static void SaveCurrentSlot()
    {
        try
        {
            var blob = new SlotBlob();
            foreach (var (key, type) in ProgressKeys())
            {
                if (!PlayerPrefs.HasKey(key)) continue;
                switch (type)
                {
                    case PrefType.Int:
                        blob.intKeys.Add(key);
                        blob.intVals.Add(PlayerPrefs.GetInt(key, 0));
                        break;
                    case PrefType.Float:
                        blob.floatKeys.Add(key);
                        blob.floatVals.Add(PlayerPrefs.GetFloat(key, 0f));
                        break;
                    default:
                        blob.strKeys.Add(key);
                        blob.strVals.Add(PlayerPrefs.GetString(key, ""));
                        break;
                }
            }

            Directory.CreateDirectory(SlotDir);
            File.WriteAllText(SlotFile(CurrentSlot), JsonUtility.ToJson(blob));
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSlot] 保存槽 {CurrentSlot} 失败：{ex.Message}");
        }
    }

    private static void LoadSlotIntoPrefs(int slot)
    {
        try
        {
            string file = SlotFile(slot);
            if (!File.Exists(file)) return;      // 新档：什么都不写，全部走默认值

            var blob = JsonUtility.FromJson<SlotBlob>(File.ReadAllText(file));
            if (blob == null) return;

            for (int i = 0; i < blob.intKeys.Count && i < blob.intVals.Count; i++)
                PlayerPrefs.SetInt(blob.intKeys[i], blob.intVals[i]);
            for (int i = 0; i < blob.floatKeys.Count && i < blob.floatVals.Count; i++)
                PlayerPrefs.SetFloat(blob.floatKeys[i], blob.floatVals[i]);
            for (int i = 0; i < blob.strKeys.Count && i < blob.strVals.Count; i++)
                PlayerPrefs.SetString(blob.strKeys[i], blob.strVals[i]);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSlot] 载入槽 {slot} 失败（按空档处理）：{ex.Message}");
        }
    }

    private static void ClearProgressKeys()
    {
        foreach (var (key, _) in ProgressKeys())
            if (PlayerPrefs.HasKey(key)) PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
    }

    /// <summary>清空某个槽（删档重开，不影响其它槽）。</summary>
    public static void EraseSlot(int slot)
    {
        slot = Mathf.Clamp(slot, 1, SLOT_COUNT);
        try
        {
            string file = SlotFile(slot);
            if (File.Exists(file)) File.Delete(file);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SaveSlot] 删除槽 {slot} 文件失败：{ex.Message}");
        }

        // 删的是当前槽 → 连内存里的 PlayerPrefs 一起清掉
        if (slot == CurrentSlot)
        {
            ClearProgressKeys();
            ReloadRuntimeCaches();
        }
    }

    /// <summary>
    /// 让那些把 PlayerPrefs 读进内存的DontDestroyOnLoad 单例重新读盘。
    /// 不做这一步的话，切槽后装备解锁表/ 继承装备仓库还是上一个档的内容。
    /// </summary>
    private static void ReloadRuntimeCaches()
    {
        EquipmentSystem.Instance?.ReloadFromPrefs();
        InheritEquipmentManager.Instance?.ReloadFromPrefs();
        GachaManager.Instance?.ReloadFromPrefs();
        // ClearRecordManager / FavorManager / EndlessRuntime 都是直读 PlayerPrefs，无缓存
    }

    // ─────────────────────────── 摘要（给 UI 用）───────────────────────────

    public struct SlotSummary
    {
        public bool   exists;        // 有没有进度
        public int    maxClearN;     // 最高通关难度编号（0 = 尚无通关）
        public int    playMinutes;   // 累计游戏时长（分钟）
        public int    unlockedCount; // 已解锁装备数
        public int    towerFloor;    // 无尽之塔已解锁最高层
        public int    yuan;          // 源（抽卡货币）
    }

    /// <summary>读取某槽摘要。当前槽直接读 PlayerPrefs，其它槽读它的 JSON 文件。</summary>
    public static SlotSummary GetSummary(int slot)
    {
        slot = Mathf.Clamp(slot, 1, SLOT_COUNT);

        if (slot == CurrentSlot) return SummaryFromPrefs();

        var s = new SlotSummary();
        try
        {
            string file = SlotFile(slot);
            if (!File.Exists(file)) return s;

            var blob = JsonUtility.FromJson<SlotBlob>(File.ReadAllText(file));
            if (blob == null) return s;

            for (int i = 0; i < blob.intKeys.Count && i < blob.intVals.Count; i++)
            {
                string k = blob.intKeys[i];
                int    v = blob.intVals[i];

                if (k == "TotalPlayMinutes")           s.playMinutes = v;
                else if (k == "GachaYuan")             s.yuan = v;
                else if (k == "EndlessTower.MaxUnlocked") s.towerFloor = v;
                else if (k.StartsWith("EQ_") && v == 1)s.unlockedCount++;
                else if (k.StartsWith("ClearCount_N") && v > 0)
                {
                    if (int.TryParse(k.Substring("ClearCount_N".Length), out int n))
                        s.maxClearN = Mathf.Max(s.maxClearN, n);
                }
            }
            s.exists = s.playMinutes > 0 || s.unlockedCount > 0||
                       s.maxClearN > 0 || s.yuan > 0 ||
                       blob.strKeys.Count > 0;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveSlot] 读取槽 {slot} 摘要失败：{ex.Message}");
        }
        return s;
    }

    private static SlotSummary SummaryFromPrefs()
    {
        var s = new SlotSummary();
        s.playMinutes = PlayerPrefs.GetInt("TotalPlayMinutes", 0);
        s.yuan        = PlayerPrefs.GetInt("GachaYuan", 0);
        s.towerFloor  = PlayerPrefs.GetInt("EndlessTower.MaxUnlocked", 1);

        for (int n = 1; n <= 13; n++)
            if (PlayerPrefs.GetInt("ClearCount_N" + n, 0) > 0) s.maxClearN = Mathf.Max(s.maxClearN, n);

        int typeCount = System.Enum.GetValues(typeof(EquipmentType)).Length;
        for (int t = 0; t < typeCount; t++)
            for (int id = 0; id <= EQ_MAX_ID; id++)
                if (PlayerPrefs.GetInt($"EQ_{t}_{id}", 0) == 1) s.unlockedCount++;

        s.exists = s.playMinutes > 0 || s.unlockedCount > 0 || s.maxClearN > 0 || s.yuan > 0;
        return s;
    }

    // ─────────────────────────── 键清单 ───────────────────────────

    /// <summary>装备 id 上限（当前最高36 = SSR_11 气运之子，留足余量）。</summary>
    private const int EQ_MAX_ID = 80;

    /// <summary>抽卡道具 rarityId 上限。</summary>
    private const int GACHA_MAX_ID = 60;

    /// <summary>
    /// **全局偏好键**：客户端设置，不属于游戏进度，切槽时保持不变。
    /// 单独列出来是为了让"漏掉某个进度键"这种错误更容易被发现 ——
    /// 只要不在这张表里，就应该考虑要不要加进 ProgressKeys。
    /// </summary>
    private static readonly string[] GlobalKeys =
    {
        KEY_CURRENT,
        "AudioManager.bgmVolume",
        "AudioManager.sfxVolume",
        "Settings.AttackRangeVisible",
        "DamageNumber.Visible",
        "DamageNumber.Size",
        "BackgroundRun.Enabled",
        "Console.Enabled",
        "TestMode",
    };

    /// <summary>
    /// 全部"游戏进度"键。清单来源：对Assets 下所有 .cs 做PlayerPrefs 调用 grep。
    /// 新增存档项时**记得加到这里**，否则它会在所有槽之间共享。
    /// </summary>
    private static IEnumerable<(string key, PrefType type)> ProgressKeys()
    {
        // ── 通关 / 积分 ──
        yield return ("ClearEquipmentPoints", PrefType.Int);
        for (int n = 1; n <= 13; n++)
        {
            yield return ("ClearCount_N" + n, PrefType.Int);
            yield return ("FirstClearChestClaimed_N" + n, PrefType.Int);
        }

        // ── 装备解锁（EQ_{typeInt}_{id}）──
        int typeCount = System.Enum.GetValues(typeof(EquipmentType)).Length;
        for (int t = 0; t < typeCount; t++)
            for (int id = 0; id <= EQ_MAX_ID; id++)
                yield return ($"EQ_{t}_{id}", PrefType.Int);

        // ── 社群好感度 ──
        foreach (FactionType f in System.Enum.GetValues(typeof(FactionType)))
            yield return ("Favor_" + f, PrefType.Int);

        // ── 抽卡 ──
        yield return ("GachaYuan", PrefType.Int);
        yield return ("GachaTotalDraws", PrefType.Int);
        yield return ("GachaPity_SSR", PrefType.Int);
        yield return ("GachaPity_UR", PrefType.Int);
        yield return ("TitleGrassRewarded", PrefType.Int);
        foreach (GachaRarity r in System.Enum.GetValues(typeof(GachaRarity)))
            for (int id = 0; id <= GACHA_MAX_ID; id++)
            {
                yield return ($"GachaPool_{r}_{id}", PrefType.Int);
                yield return ($"GachaCount_{r}_{id}", PrefType.Int);
                yield return ($"GachaPoolMilestone_{r}_{id}", PrefType.Int);
            }

        // ── 继承装备（整包 JSON）──
        yield return ("InheritEquipSave_v1", PrefType.Str);

        // ── 皮肤 / 角色 ──
        yield return ("SelectedSkin", PrefType.Int);
        for (int i = 0; i <= 12; i++)
            yield return ($"SkinUnlocked_{i}", PrefType.Int);

        // ── 无尽之塔 ──
        yield return ("EndlessTower.Floor", PrefType.Int);
        yield return ("EndlessTower.MaxUnlocked", PrefType.Int);
        for (int f = 1; f <= EndlessRuntime.MAX_FLOOR; f++)
            yield return ($"EndlessTower.Best.{f}", PrefType.Float);

        // ── 统计 / 成就类计数 ──
        yield return ("TotalPlayMinutes", PrefType.Int);
        yield return ("TotalUpgradeChoices", PrefType.Int);
        yield return ("CampCapturedCount", PrefType.Int);
        yield return ("MushroomDefeatedCount", PrefType.Int);
        yield return ("BestSingleRunLevel", PrefType.Int);
        yield return ("ReachedLevel50Once", PrefType.Int);
        yield return ("GateChallengeStartedOnce", PrefType.Int);

        // ── 玩法开关（属于进度：由解锁/剧情推进而来，不是客户端偏好）──
        yield return ("SporeMutationEnabled", PrefType.Int);
        yield return ("TrinityFusion.Enabled", PrefType.Int);

        // ── 教程/ 说明书阅读进度 ──
        yield return ("InstructionsLastSeenUnlockCount", PrefType.Int);
        yield return ("InstructionsEverViewed", PrefType.Int);
        yield return ("TutorialN1Shown", PrefType.Int);
        yield return ("WorldBossHintShown", PrefType.Int);   // N6 首次世界Boss提示（每档独立）
    }

    [System.Serializable]
    private class SlotBlob
    {
        public List<string> intKeys   = new List<string>();
        public List<int>    intVals   = new List<int>();
        public List<string> floatKeys = new List<string>();
        public List<float>  floatVals = new List<float>();
        public List<string> strKeys   = new List<string>();
        public List<string> strVals   = new List<string>();
    }
}
