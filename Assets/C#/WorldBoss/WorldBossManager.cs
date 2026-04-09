using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 世界Boss管理器
/// - N7+ 才生成世界Boss，生成在 enemylayer 下
/// - 击败后：显示社群对象、传送玩家、弹出好感度和已解锁加成
/// </summary>
public class WorldBossManager : MonoBehaviour
{
    public static WorldBossManager Instance { get; private set; }

    [System.Serializable]
    public class WorldBossEntry
    {
        public FactionType faction;
        public GameObject  bossPrefab;
        public Transform   spawnPoint;     // 世界Boss固定生成位置
        public GameObject  factionObject;  // 社群对象（初始隐藏）
        public Transform   teleportTarget; // 击败后传送玩家到此
    }

    [Header("击败演出")]
    public float slowMoScale    = 0.2f;
    public float slowMoDuration = 1.5f;
    public float revealDelay    = 2f;
    public List<WorldBossEntry> worldBossEntries = new List<WorldBossEntry>();

    [Header("引用")]
    public Player    player;
    public Transform enemylayer; // 世界Boss生成在此节点下

    private HashSet<FactionType> _unlockedFactions = new HashSet<FactionType>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        // 所有社群对象初始隐藏
        foreach (var entry in worldBossEntries)
            if (entry.factionObject != null)
                entry.factionObject.SetActive(false);

        if (!ShouldSpawnWorldBoss()) return;
        SpawnAllWorldBosses();
    }

    private bool ShouldSpawnWorldBoss()
    {
        if (DifficultyManager.Instance == null) return false;
        string label = DifficultyManager.Instance.Current.label;
        if (label.StartsWith("N") && int.TryParse(label.Substring(1), out int n))
            return n >= 7;
        return false;
    }

    private void SpawnAllWorldBosses()
    {
        // 如果 enemylayer 未绑定，自动查找
        if (enemylayer == null)
            enemylayer = GameObject.Find("enemylayer")?.transform;

        foreach (var entry in worldBossEntries)
        {
            if (entry.bossPrefab == null || entry.spawnPoint == null) continue;

            GameObject obj = Instantiate(entry.bossPrefab,
                entry.spawnPoint.position,
                Quaternion.Euler(45, 0, 0),
                enemylayer); // 生成在 enemylayer 下

            // 注入引用
            var wbMushroom = obj.GetComponent<WorldBossMushroomMan>();
            if (wbMushroom != null)
            {
                wbMushroom.faction          = entry.faction;
                wbMushroom.worldBossManager = this;
            }

            var wbBat = obj.GetComponent<WorldBossBat>();
            if (wbBat != null)
            {
                wbBat.faction          = entry.faction;
                wbBat.worldBossManager = this;
            }

            Debug.Log($"[WorldBoss] {entry.faction} 世界Boss已生成");
        }
    }

    /// <summary>世界Boss被击败时由子类调用</summary>
    public void OnWorldBossDefeated(FactionType faction)
    {
        if (_unlockedFactions.Contains(faction)) return;

        // 成就装备3「钥匙剑」：解锁世界Boss奖励，未解锁则跳过加成
        if (EquipmentSystem.Instance != null &&
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 3))
        {
            ToastManager.Show("需要「钥匙剑」才能获得世界Boss奖励！");
            return;
        }

        _unlockedFactions.Add(faction);

        WorldBossEntry entry = worldBossEntries.Find(e => e.faction == faction);
        StartCoroutine(DefeatSequence(faction, entry));
    }

    private IEnumerator DefeatSequence(FactionType faction, WorldBossEntry entry)
    {
        // 慢动作
        Time.timeScale = slowMoScale;
        yield return new WaitForSecondsRealtime(slowMoDuration);
        Time.timeScale = 1f;

        yield return new WaitForSecondsRealtime(revealDelay);

        // 显示社群对象
        if (entry?.factionObject != null)
            entry.factionObject.SetActive(true);

        // 传送玩家
        if (entry?.teleportTarget != null && player != null)
            player.transform.position = entry.teleportTarget.position;

        // 弹出提示和加成
        StartCoroutine(ShowFactionUnlockToasts(faction));
    }

    private IEnumerator ShowFactionUnlockToasts(FactionType faction)
    {
        yield return new WaitForSeconds(0.3f);

        int favor = FavorManager.Instance != null ? FavorManager.Instance.GetFavor(faction) : 0;
        ToastManager.Show($"{GetFactionName(faction)}社群已解锁！当前好感度：{favor}");

        yield return new WaitForSeconds(0.5f);

        // 依次弹出已解锁的加成
        if (faction == FactionType.Mushroom)
        {
            if (favor >= 10) { ToastManager.Show("经验效率 +1"); yield return new WaitForSeconds(0.4f); }
            if (favor >= 20) { ToastManager.Show("攻击力 +1");   yield return new WaitForSeconds(0.4f); }
            if (favor >= 30) { ToastManager.Show("移动速度 +1"); yield return new WaitForSeconds(0.4f); }
            // 40：孢子领域伤害（未制作，留空）
            if (favor >= 50) { ToastManager.Show("防御力 +1");   yield return new WaitForSeconds(0.4f); }
            // 60：孢子领域冷却（未制作，留空）
            if (favor >= 70) { ToastManager.Show("闪避率 +1");   yield return new WaitForSeconds(0.4f); }
            // 80：孢子领域范围（未制作，留空）
            if (favor >= 90) { ToastManager.Show("自然回血 +1"); yield return new WaitForSeconds(0.4f); }
        }

        // 应用实际加成
        ApplyFactionBonus(faction);
    }

    private void ApplyFactionBonus(FactionType faction)
    {
        if (player == null) return;
        if (FavorManager.Instance == null) return;

        int favor = FavorManager.Instance.GetFavor(faction);

        if (faction == FactionType.Mushroom)
        {
            if (favor >= 10)
            {
                player.DR += 1;
                EquipmentSystem.Instance?.UnlockEquipment(EquipmentType.FavorEquipment, 0);
            }
            if (favor >= 20) player.atk   += 1;
            if (favor >= 30) player.speed += 1;
            if (favor >= 40)
            {
                // 孢子领域伤害 +1：找到孢子领域技能并增加 damage
                ApplySporeFieldBonus(player, damage: 1);
            }
            if (favor >= 50) player.def   += 1;
            if (favor >= 60)
            {
                // 孢子领域冷却 -1
                ApplySporeFieldBonus(player, cdReduction: 1f);
            }
            if (favor >= 70) player.EVA   += 1;
            if (favor >= 80)
            {
                // 孢子领域范围 +5
                ApplySporeFieldBonus(player, radiusBonus: 5f);
            }
            if (favor >= 90) player.regen += 1;
        }
        else if (faction == FactionType.Bat)
        {
            // 蝙蝠社群加成待后续制作
        }
    }

    /// <summary>对玩家技能列表中的孢子领域技能应用加成</summary>
    private void ApplySporeFieldBonus(Player p, int damage = 0, float cdReduction = 0f, float radiusBonus = 0f)
    {
        if (p == null) return;
        foreach (Transform t in p.SkillList)
        {
            SkillSporeField sf = t.GetComponent<SkillSporeField>();
            if (sf == null) continue;
            if (damage > 0)      sf.damage       += damage;
            if (cdReduction > 0) sf.CDtime        = Mathf.Max(0.5f, sf.CDtime - cdReduction);
            if (radiusBonus > 0) sf.attackRadius += radiusBonus;
        }
    }

    private string GetFactionName(FactionType faction)
    {
        return faction switch
        {
            FactionType.Mushroom => "蘑菇",
            FactionType.Bat      => "蝙蝠",
            _                    => faction.ToString()
        };
    }

    public bool IsFactionUnlocked(FactionType faction) => _unlockedFactions.Contains(faction);
}
