using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏会话追踪器（单例，DontDestroyOnLoad）
/// 追踪本局：技能伤害、击败Boss、解锁装备、获得技能、游戏时长。
/// 供 PlayerStatsPanel 和 GameSummaryPanel 读取。
/// </summary>
public class GameSessionTracker : MonoBehaviour
{
    public static GameSessionTracker Instance { get; private set; }

    // ── 对局基础信息 ──
    public float sessionStartTime;
    public float sessionEndTime;
    public bool  isVictory;
    public string difficultyPlayed = "";
    public int   playerFinalLevel;

    /// <summary>对局持续时长（秒）</summary>
    public float DurationSeconds => sessionEndTime > 0f ? sessionEndTime - sessionStartTime : 0f;

    // ── 技能伤害统计：skillName → totalDamage ──
    public Dictionary<string, float> skillDamage = new Dictionary<string, float>();

    // ── 击败的 Boss 列表 ──
    public List<string> bossesDefeated = new List<string>();

    // ── 本局获得的技能列表 ──
    public List<string> skillsAcquired = new List<string>();

    // ── 本局解锁的装备：装备名称列表 ──
    public List<string> equipmentUnlockedThisSession = new List<string>();
    private HashSet<string> _eqSnapshot; // 对局开始时已解锁的装备 key 集合

    // ============================================================
    //  2026-08 结算面板数据补全
    //  ----------------------------------------------------------
    //  背景：旧版只统计 skillDamage 一项，且多个伤害源漏埋点，
    //        导致「总伤害输出」严重偏低（亡者领域友军的输出完全为 0）。
    //  本次新增以下维度，全部在同一处 BeginSession 归零，避免跨局残留。
    // ============================================================

    /// <summary>本局击杀敌人总数（含小怪 / Boss / 营地占领不计）。</summary>
    public int totalKills;

    /// <summary>本局玩家累计承受伤害。</summary>
    public int damageTaken;

    /// <summary>本局单次最高伤害（用于结算页"最高单击"）。</summary>
    public int maxSingleHit;

    /// <summary>本局累计治疗量（回血 / 吸血 / 亡者领域治疗友军）。</summary>
    public int totalHealing;

    /// <summary>本局拾取源木总量。</summary>
    public int woodCollected;

    /// <summary>亡者领域本局复活的友军数量。</summary>
    public int alliesRevived;

    /// <summary>「亡者领域」友军伤害在结算里统一归到这个名字下。</summary>
    public const string TombDomainAllyDamageKey = "亡者领域·友军";

    /// <summary>每秒伤害（DPS）。时长为 0 时返回 0，避免除零得到 Infinity 打进 UI。</summary>
    public float DPS
    {
        get
        {
            float d = DurationSeconds;
            return d > 0.01f ? TotalDamage() / d : 0f;
        }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.realtimeSinceStartup;
    }

    void Start()
    {
        // 对局开始：清空本局追踪数据 + 快照当前已解锁装备
        // 修复：之前 bossesDefeated / skillsAcquired / skillDamage / equipmentUnlockedThisSession
        //   是 DontDestroyOnLoad 单例的字段，跨对局累积 → N12 通关时显示 N7 的"蝙蝠公爵"和
        //   N9 的"狼人首领"，且解锁装备页多列 N11/N12 等所有历史解锁。
        BeginSession();
    }

    /// <summary>对局开始：清空本局追踪数据 + 快照当前装备解锁状态</summary>
    public void BeginSession()
    {
        bossesDefeated.Clear();
        skillsAcquired.Clear();
        skillDamage.Clear();
        equipmentUnlockedThisSession.Clear();
        sessionStartTime = Time.realtimeSinceStartup;
        sessionEndTime   = 0f;

        // 2026-08 新增维度一并归零（漏掉任何一项都会造成跨局数据污染）
        totalKills= 0;
        damageTaken   = 0;
        maxSingleHit  = 0;
        totalHealing  = 0;
        woodCollected = 0;
        alliesRevived = 0;
        isVictory        = false;
        difficultyPlayed = "";
        playerFinalLevel = 0;

        // 装备名缓存跨场景失效（EquipmentIcon 实例在场景重载后全部换新），必须一并清掉，
        // 否则缓存里持有的是已 Destroy 对象采集的旧名字，且新增装备无法被解析。
        _eqNameCache = null;

        // 快照当前已解锁装备
        SnapshotEquipment();
        // 订阅装备解锁事件
        if (EquipmentSystem.Instance != null)
        {
            EquipmentSystem.Instance.OnEquipmentUnlocked -= OnEquipmentUnlocked; // 防重复
            EquipmentSystem.Instance.OnEquipmentUnlocked += OnEquipmentUnlocked;
        }
    }

    void OnDestroy()
    {
        if (EquipmentSystem.Instance != null)
            EquipmentSystem.Instance.OnEquipmentUnlocked -= OnEquipmentUnlocked;
        if (Instance == this) Instance = null;
    }

    // ── 记录接口 ──

    /// <summary>
    /// 记录某技能造成的一次伤害。
    ///
    /// 【2026-08 加固】旧版直接累加，存在三个隐患：
    ///   ① damage 可能是 NaN / Infinity（暴击倍率写错、除零等），一旦污染字典，
    ///      结算页所有百分比全变 NaN%，进度条长度 Mathf.RoundToInt(NaN) 抛异常 → 面板卡死；
    ///   ② damage 可能为负（防御大于伤害时某些技能没有 Max(1,...) 钳制），会让总伤害倒扣；
    ///   ③ skillName 为空串时会在结算列表里出现一行空技能名。
    /// 现统一在入口做校验，保证字典里永远是有限的非负数。
    /// </summary>
    public void RecordDamage(string skillName, float damage)
    {
        if (string.IsNullOrEmpty(skillName)) return;
        if (float.IsNaN(damage) || float.IsInfinity(damage)) return;
        if (damage <= 0f) return;

        if (skillDamage.ContainsKey(skillName))
            skillDamage[skillName] += damage;
        else
            skillDamage[skillName] = damage;

        int hit = Mathf.RoundToInt(damage);
        if (hit > maxSingleHit) maxSingleHit = hit;
    }

    /// <summary>记录一次击杀。</summary>
    public void RecordKill() => totalKills++;

    /// <summary>记录玩家承受伤害。</summary>
    public void RecordDamageTaken(int amount)
    {
        if (amount > 0) damageTaken += amount;
    }

    /// <summary>记录治疗量（玩家回血 / 吸血 / 友军治疗）。</summary>
    public void RecordHealing(int amount)
    {
        if (amount > 0) totalHealing += amount;
    }

    /// <summary>记录源木拾取。</summary>
    public void RecordWood(int amount)
    {
        if (amount > 0) woodCollected += amount;
    }

    /// <summary>记录亡者领域复活一名友军。</summary>
    public void RecordAllyRevived() => alliesRevived++;

    /// <summary>记录击败一个 Boss</summary>
    public void RecordBossDefeated(string bossName)
    {
        if (!string.IsNullOrEmpty(bossName) && !bossesDefeated.Contains(bossName))
            bossesDefeated.Add(bossName);
    }

    /// <summary>记录获得一个技能</summary>
    public void RecordSkillAcquired(string skillName)
    {
        if (!string.IsNullOrEmpty(skillName) && !skillsAcquired.Contains(skillName))
            skillsAcquired.Add(skillName);
    }

    /// <summary>快照当前已解锁装备（对局开始时调用）</summary>
    private void SnapshotEquipment()
    {
        _eqSnapshot = new HashSet<string>();
        if (EquipmentSystem.Instance != null)
        {
            var list = EquipmentSystem.Instance.GetUnlockedEquipments();
            foreach (var (type, id) in list)
                _eqSnapshot.Add(EqKey(type, id));
        }
    }

    /// <summary>装备解锁回调：新装备不在快照中则记录</summary>
    private void OnEquipmentUnlocked(EquipmentType type, int id)
    {
        string key = EqKey(type, id);
        if (_eqSnapshot.Contains(key)) return; // 对局开始前已解锁
        _eqSnapshot.Add(key);

        string name = GetEquipmentDisplayName(type, id);
        if (!string.IsNullOrEmpty(name))
            equipmentUnlockedThisSession.Add(name);
    }

    /// <summary>对局结束，结算数据</summary>
    public void FinalizeSession(bool victory, string difficulty, int finalLevel)
    {
        isVictory = victory;
        difficultyPlayed = difficulty;
        playerFinalLevel = finalLevel;
        sessionEndTime = Time.realtimeSinceStartup;
    }

    /// <summary>
    /// 获取装备显示名称。
    ///
    /// 【2026-08 改进】旧版只返回"抽卡装备·13"这类"类型+数字 ID"，
    ///   玩家根本看不出解锁了什么 —— 结算面板第 4 页信息量近乎为零。
    ///   现在优先从场景里的 EquipmentIcon 组件反查真实装备名（如"饮血剑"），
    ///   查不到才退回旧格式，保证任何情况下都有可读文本。
    /// </summary>
    public static string GetEquipmentDisplayName(EquipmentType type, int id)
    {
        string realName = TryResolveRealEquipmentName(type, id);

        string prefix = type switch
        {
            EquipmentType.ClearEquipment       => "通关",
            EquipmentType.AchievementEquipment => "成就",
            EquipmentType.FavorEquipment       => "好感",
            EquipmentType.GachaEquipment       => "抽卡",
            EquipmentType.InheritEquipment     => "继承",
            _                                  => "??"
        };

        return string.IsNullOrEmpty(realName)
            ? $"{prefix}装备·{id}"
            : $"[{prefix}] {realName}";
    }

    /// <summary>
    /// 从场景中的 EquipmentIcon 组件反查装备真名。
    ///
    /// EquipmentIcon 在 Start/Initialize 时会把 equipmentName 填好（含大量硬编码兜底），
    /// 是全项目唯一持有"id → 中文名"映射的地方。这里用一次性缓存避免每次解锁都全场景扫描
    /// （装备解锁是低频事件，但 FindObjectsOfType 在大场景里仍有可观开销）。
    /// 找不到返回 null（例如该装备的图标节点尚未创建）。
    /// </summary>
    private static Dictionary<string, string> _eqNameCache;

    private static string TryResolveRealEquipmentName(EquipmentType type, int id)
    {
        if (_eqNameCache == null)
        {
            _eqNameCache = new Dictionary<string, string>();
            // includeInactive: true —— 存档界面通常是隐藏的，必须连未激活对象一起找
            var icons = Resources.FindObjectsOfTypeAll<EquipmentIcon>();
            foreach (var ic in icons)
            {
                if (ic == null || string.IsNullOrEmpty(ic.equipmentName)) continue;
                string k = EqKey(ic.equipmentType, ic.equipmentId);
                if (!_eqNameCache.ContainsKey(k))
                    _eqNameCache[k] = ic.equipmentName;
            }
        }

        return _eqNameCache.TryGetValue(EqKey(type, id), out string name) ? name : null;
    }

    /// <summary>获取技能伤害占比最高的排序列表</summary>
    public List<KeyValuePair<string, float>> GetSortedSkillDamage()
    {
        var list = new List<KeyValuePair<string, float>>(skillDamage);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    /// <summary>
    /// 计算总伤害。
    /// 【加固】跳过 NaN / Infinity。虽然 RecordDamage 已在入口拦截，
    /// 但字典是 public 的，外部仍可能直接写入脏值，这里再兜一层。
    /// </summary>
    public float TotalDamage()
    {
        float total = 0f;
        foreach (var v in skillDamage.Values)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) continue;
            total += v;
        }
        return total;
    }

    private static string EqKey(EquipmentType type, int id) => $"{(int)type}_{id}";
}
