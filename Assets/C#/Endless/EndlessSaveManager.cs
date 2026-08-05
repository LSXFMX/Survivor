using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 单个技能的存档快照。
/// 反射保存 Skillbase（及子类）的全部 public 数值字段（float / int / bool），
/// 恢复时先按 Skillname 从升级卡池找 prefab 重建（保留 bullet 引用），再反射回写数值。
/// </summary>
[System.Serializable]
public class EndlessSkillSnapshot
{
    public string skillName;
    public string componentType;             // 类名，如 "SkillWindArrow" / "Skillbase"
    public List<string> floatFields = new List<string>();
    public List<float>  floatValues = new List<float>();
    public List<string> intFields   = new List<string>();
    public List<int>    intValues   = new List<int>();
    public List<string> boolFields  = new List<string>();
    public List<bool>   boolValues  = new List<bool>();
}

/// <summary>无尽模式整局存档（JsonUtility 可序列化）。</summary>
[System.Serializable]
public class EndlessSaveData
{
    public string savedAt;   // 存档时间
    public int version = 1;

    // ── 玩家数值（Attribute + Player 特有）──
    public int   health, healthmax, speed, EVA, exp, expmax, level, regen;
    public float atk, def, CR, CD, DR;
    public bool  dashUnlocked, dashInvincibleUnlocked, dashPhaseUnlocked;
    public float dashCooldown;
    public float posX, posZ;

    // ── 无尽计时 ──
    public float endlessElapsed;
    public int   endlessStageCount;
    public int   endlessPointsMinute;
    public float endlessHpMultiplier;
    public float endlessAtkMultiplier;
    /// <summary>无尽难度速度档（0 标准 / 1 加速 / 2 狂暴），读档后保持同一节奏。</summary>
    public int   endlessSpeedMode;

    // ── 货币（源木为局内数值；装备积分天然跨局持久化，无需存）──
    public int yuanmu;

    // ── 门挑战 ──
    public int  gateFloor;
    public int  gateDifficultyMultiplier;

    // ── 奇遇 ──
    public List<string> usedAdventures = new List<string>();
    public int  personalityDissolveCount;
    public int  nuwaSelectedCount;

    // ── 世界Boss 本局已击败（影响通关源奖励）──
    public List<string> defeatedFactions = new List<string>();

    // ── 技能 ──
    public List<EndlessSkillSnapshot> skills = new List<EndlessSkillSnapshot>();
}

/// <summary>
/// 无尽模式「保存本局 / 继续」管理器。
///
/// - <see cref="Save"/>：在无尽模式暂停菜单点「保存本局」时调用，把整局状态写入 PlayerPrefs。
/// - <see cref="ResumePending"/>：主菜单点「继续无尽」时置 true，进入战斗场景后由
///   <see cref="RestoreIntoScene"/> 恢复，恢复完成后自动清除。
/// - <see cref="HasSave"/> / <see cref="Clear"/>：存档存在性 / 删除（通关或放弃后清档）。
///
/// 设计要点：
///  1) 技能按 Skillname 从 ChoiceUI.skillEntries 的学习卡找到 prefab 重建，
///     保留 bullet 引用，再反射回写全部数值字段（含 attackRadius 等子类字段）。
///  2) 玩家数值是"存档时刻的最终值"（已含装备/奇遇/升级加成），恢复时直接覆盖，
///     因此进入战斗时必须跳过 EquipmentInitializer.ApplyAllEquipments 的数值叠加。
///  3) 只存真实局内状态：源木、门挑战层数、奇遇已用集合、无尽计时。
/// </summary>
public static class EndlessSaveManager
{
    public const string KEY = "EndlessRunSaveV1";
    private const string FLAG = "EndlessResumePending";

    /// <summary>是否为"继续无尽"开局（进入战斗后置位，恢复完成或放弃时清除）。</summary>
    public static bool ResumePending
    {
        get => PlayerPrefs.GetInt(FLAG, 0) == 1;
        set => PlayerPrefs.SetInt(FLAG, value ? 1 : 0);
    }

    public static bool HasSave() => PlayerPrefs.HasKey(KEY);

    /// <summary>保存当前无尽模式整局状态。</summary>
    public static void Save()
    {
        var d = new EndlessSaveData { savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") };

        Player p = FindPlayer();
        if (p != null)
        {
            d.health   = p.health;
            d.healthmax = p.healthmax;
            d.atk      = p.atk;
            d.def      = p.def;
            d.speed    = p.speed;
            d.CR       = p.CR;
            d.CD       = p.CD;
            d.EVA      = p.EVA;
            d.DR       = p.DR;
            d.regen    = p.regen;
            d.exp      = p.exp;
            d.expmax   = p.expmax;
            d.level    = p.level;
            d.dashUnlocked         = p.dashUnlocked;
            d.dashInvincibleUnlocked = p.dashInvincibleUnlocked;
            d.dashPhaseUnlocked     = p.dashPhaseUnlocked;
            d.dashCooldown          = p.dashCooldown;
            d.posX = p.transform.position.x;
            d.posZ = p.transform.position.z;

            if (p.SkillList != null)
            {
                foreach (Transform t in p.SkillList)
                {
                    var sb = t != null ? t.GetComponent<Skillbase>() : null;
                    if (sb == null || string.IsNullOrEmpty(sb.Skillname)) continue;
                    d.skills.Add(SnapshotSkill(sb));
                }
            }
        }

        battleUI bui = Object.FindObjectOfType<battleUI>();
        if (bui != null)
        {
            d.endlessElapsed     = bui.GetEndlessElapsedForSave();
            d.endlessStageCount  = bui.GetEndlessStageCountForSave();
            d.endlessPointsMinute = bui.GetEndlessPointsMinuteForSave();
        }
        d.endlessHpMultiplier  = enemy.endlessHpMultiplier;
        d.endlessAtkMultiplier = enemy.endlessAtkMultiplier;
        d.endlessSpeedMode     = (int)EndlessRuntime.SpeedMode;

        if (YuanMuManager.Instance != null) d.yuanmu = YuanMuManager.Instance.Current;

        var gate = GateChallengeManager.Instance;
        if (gate != null)
        {
            d.gateFloor              = gate.CurrentFloor;
            d.gateDifficultyMultiplier = gate.DifficultyMultiplier;
        }

        d.usedAdventures = new List<string>(AdventureOptionBase.usedOptionsThisRun);
        d.personalityDissolveCount = AdventurePersonalityDissolve.GetRunCounterForSave();
        d.nuwaSelectedCount        = AdventureNuwaFailed.GetSelectedCountForSave();

        var wb = WorldBossManager.Instance;
        if (wb != null)
        {
            var factions = wb.GetDefeatedFactionsForSave();
            if (factions != null)
            {
                d.defeatedFactions.Clear();
                foreach (var f in factions) d.defeatedFactions.Add(f.ToString());
            }
        }

        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
        Debug.Log("[EndlessSave] 本局已保存：" + d.savedAt);
    }

    /// <summary>读取存档数据（无存档返回 null）。</summary>
    public static EndlessSaveData Load()
    {
        if (!HasSave()) return null;
        try
        {
            return JsonUtility.FromJson<EndlessSaveData>(PlayerPrefs.GetString(KEY, ""));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[EndlessSave] 存档解析失败，已忽略：" + e.Message);
            return null;
        }
    }

    /// <summary>删除存档（通关、放弃、玩家主动清除时调用）。</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(KEY);
        ResumePending = false;
        PlayerPrefs.Save();
    }

    /// <summary>把存档恢复进当前战斗场景。由战斗场景在初始化完成后（延迟一帧）调用。</summary>
    public static void RestoreIntoScene()
    {
        var d = Load();
        if (d == null) { ResumePending = false; return; }

        Player p = FindPlayer();
        if (p == null) { Debug.LogWarning("[EndlessSave] 找不到 Player，恢复中止"); ResumePending = false; return; }

        // ── 玩家数值（覆盖，最终值）──
        p.health    = d.health;
        p.healthmax = d.healthmax;
        p.atk       = d.atk;
        p.def       = d.def;
        p.speed     = d.speed;
        p.CR        = d.CR;
        p.CD        = d.CD;
        p.EVA       = d.EVA;
        p.DR        = d.DR;
        p.regen     = d.regen;
        p.exp       = d.exp;
        p.expmax    = d.expmax;
        p.level     = d.level;
        p.dashUnlocked           = d.dashUnlocked;
        p.dashInvincibleUnlocked = d.dashInvincibleUnlocked;
        p.dashPhaseUnlocked      = d.dashPhaseUnlocked;
        p.dashCooldown           = d.dashCooldown;
        var pos = p.transform.position;
        pos.x = d.posX; pos.z = d.posZ;
        p.transform.position = pos;

        // ── 技能：清空后按存档重建 ──
        if (p.SkillList != null)
        {
            for (int i = p.SkillList.childCount - 1; i >= 0; i--)
                Object.Destroy(p.SkillList.GetChild(i).gameObject);
            foreach (var snap in d.skills)
                RestoreSkill(p, snap);
        }

        // ── 无尽计时 ──
        var bui = Object.FindObjectOfType<battleUI>();
        if (bui != null)
            bui.RestoreEndlessState(d.endlessElapsed, d.endlessStageCount, d.endlessPointsMinute);
        enemy.endlessHpMultiplier  = d.endlessHpMultiplier;
        enemy.endlessAtkMultiplier = d.endlessAtkMultiplier;
        EndlessRuntime.SpeedMode   = (EndlessSpeedMode)Mathf.Clamp(
            d.endlessSpeedMode, 0, EndlessRuntime.ModeCount - 1);
        EndlessRuntime.Stage       = d.endlessStageCount;

        // ── 货币 ──
        if (YuanMuManager.Instance != null) YuanMuManager.Instance.SetCurrent(d.yuanmu);

        // ── 门挑战 ──
        var gate = GateChallengeManager.Instance;
        if (gate != null) gate.RestoreFromSave(d.gateFloor, d.gateDifficultyMultiplier);

        // ── 奇遇 ──
        AdventureOptionBase.usedOptionsThisRun.Clear();
        foreach (var s in d.usedAdventures) AdventureOptionBase.usedOptionsThisRun.Add(s);
        AdventurePersonalityDissolve.SetRunCounterForSave(d.personalityDissolveCount);
        AdventureNuwaFailed.SetSelectedCountForSave(d.nuwaSelectedCount);

        // ── 世界Boss ──
        var wb = WorldBossManager.Instance;
        if (wb != null) wb.RestoreDefeatedFactions(d.defeatedFactions);

        ResumePending = false;
        Debug.Log("[EndlessSave] 已恢复存档：" + d.savedAt);
    }

    // ══════════════════════ 技能快照 ══════════════════════

    private static EndlessSkillSnapshot SnapshotSkill(Skillbase sb)
    {
        var snap = new EndlessSkillSnapshot
        {
            skillName = sb.Skillname,
            componentType = sb.GetType().Name,
        };
        var fields = sb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var f in fields)
        {
            if (f.IsStatic) continue;
            if (f.FieldType == typeof(float))
            {
                snap.floatFields.Add(f.Name);
                snap.floatValues.Add((float)f.GetValue(sb));
            }
            else if (f.FieldType == typeof(int))
            {
                snap.intFields.Add(f.Name);
                snap.intValues.Add((int)f.GetValue(sb));
            }
            else if (f.FieldType == typeof(bool))
            {
                snap.boolFields.Add(f.Name);
                snap.boolValues.Add((bool)f.GetValue(sb));
            }
        }
        return snap;
    }

    /// <summary>重建单个技能：优先从升级卡池找同名 prefab，找不到则用反射新建组件。</summary>
    private static void RestoreSkill(Player p, EndlessSkillSnapshot snap)
    {
        GameObject prefab = FindSkillPrefab(snap.skillName);
        GameObject go;
        if (prefab != null)
        {
            go = Object.Instantiate(prefab, p.SkillList);
        }
        else
        {
            // 进化技能（地狱火/风之形/亡者领域等）没有独立学习卡 → 新建组件
            go = new GameObject(snap.skillName);
            go.transform.SetParent(p.SkillList, false);
            System.Type t = System.Type.GetType(snap.componentType)
                            ?? typeof(Skillbase).Assembly.GetType(snap.componentType);
            if (t != null && !t.IsAbstract) go.AddComponent(t);
        }
        if (!go.activeSelf) go.SetActive(true);

        Skillbase sb = go.GetComponent<Skillbase>();
        if (sb == null) { Object.Destroy(go); return; }
        sb.Skillname = snap.skillName;
        sb.player    = p.gameObject;

        var fields = sb.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        var byName = new Dictionary<string, FieldInfo>();
        foreach (var f in fields) byName[f.Name] = f;

        for (int i = 0; i < snap.floatFields.Count; i++)
            if (byName.TryGetValue(snap.floatFields[i], out var f) && f.FieldType == typeof(float))
                f.SetValue(sb, snap.floatValues[i]);
        for (int i = 0; i < snap.intFields.Count; i++)
            if (byName.TryGetValue(snap.intFields[i], out var f) && f.FieldType == typeof(int))
                f.SetValue(sb, snap.intValues[i]);
        for (int i = 0; i < snap.boolFields.Count; i++)
            if (byName.TryGetValue(snap.boolFields[i], out var f) && f.FieldType == typeof(bool))
                f.SetValue(sb, snap.boolValues[i]);
    }

    /// <summary>从 ChoiceUI.skillEntries 的学习卡中按 Skillname 找技能 prefab。</summary>
    private static GameObject FindSkillPrefab(string skillName)
    {
        var choice = ChoiceUI.Instance;
        if (choice == null || choice.skillEntries == null) return null;
        foreach (var entry in choice.skillEntries)
        {
            if (entry == null || entry.learnSkillPrefab == null) continue;
            var learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
            if (learn == null || learn.skill == null) continue;
            if (learn.skill.Skillname == skillName) return learn.skill.gameObject;
        }
        return null;
    }

    // ══════════════════════ 工具 ══════════════════════

    private static Player FindPlayer()
    {
        var layer = GameObject.Find("playerlayer");
        if (layer != null)
        {
            foreach (Transform t in layer.transform)
                if (t != null && t.CompareTag("Player"))
                {
                    var pl = t.GetComponent<Player>();
                    if (pl != null) return pl;
                }
            if (layer.transform.childCount > 0)
            {
                var pl = layer.transform.GetChild(0).GetComponent<Player>();
                if (pl != null) return pl;
            }
        }
        return Object.FindObjectOfType<Player>();
    }
}
