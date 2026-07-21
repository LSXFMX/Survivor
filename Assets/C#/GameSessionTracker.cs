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

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.realtimeSinceStartup;
    }

    void Start()
    {
        // 对局开始：快照当前已解锁装备
        SnapshotEquipment();
        // 订阅装备解锁事件
        if (EquipmentSystem.Instance != null)
            EquipmentSystem.Instance.OnEquipmentUnlocked += OnEquipmentUnlocked;
    }

    void OnDestroy()
    {
        if (EquipmentSystem.Instance != null)
            EquipmentSystem.Instance.OnEquipmentUnlocked -= OnEquipmentUnlocked;
        if (Instance == this) Instance = null;
    }

    // ── 记录接口 ──

    /// <summary>记录某技能造成的一次伤害</summary>
    public void RecordDamage(string skillName, float damage)
    {
        if (skillName == null) return;
        if (skillDamage.ContainsKey(skillName))
            skillDamage[skillName] += damage;
        else
            skillDamage[skillName] = damage;
    }

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

    /// <summary>获取装备显示名称</summary>
    public static string GetEquipmentDisplayName(EquipmentType type, int id)
    {
        string prefix = type switch
        {
            EquipmentType.ClearEquipment       => "通关",
            EquipmentType.AchievementEquipment => "成就",
            EquipmentType.FavorEquipment       => "好感",
            EquipmentType.GachaEquipment       => "抽卡",
            EquipmentType.InheritEquipment     => "继承",
            _                                  => "??"
        };
        return $"{prefix}装备·{id}";
    }

    /// <summary>获取技能伤害占比最高的排序列表</summary>
    public List<KeyValuePair<string, float>> GetSortedSkillDamage()
    {
        var list = new List<KeyValuePair<string, float>>(skillDamage);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }

    /// <summary>计算总伤害</summary>
    public float TotalDamage()
    {
        float total = 0f;
        foreach (var v in skillDamage.Values) total += v;
        return total;
    }

    private static string EqKey(EquipmentType type, int id) => $"{(int)type}_{id}";
}
