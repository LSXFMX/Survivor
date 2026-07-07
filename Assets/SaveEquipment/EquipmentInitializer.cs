using System.Collections;
using UnityEngine;

/// <summary>
/// 在进入战斗场景时初始化存档装备效果。
/// 挂在 BattleUI 或战斗场景根对象上，在 Start() 时调用。
/// </summary>
public class EquipmentInitializer : MonoBehaviour
{
    private const string KEY_TOTAL_PLAY_MINUTES = "TotalPlayMinutes";
    private const string KEY_GATE_CHALLENGE_STARTED_ONCE = "GateChallengeStartedOnce";

    [Header("引用")]
    public Player player;
    public Transform playerSkillList;
    public GameObject windArrowSkillPrefab;
    public GameObject sporeFieldSkillPrefab;
    public GameObject mushroomBabyPetPrefab;
    [Tooltip("吸血鬼线：血族血统技能预制体")]
    public GameObject bloodlineSkillPrefab;
    [Tooltip("吸血鬼线：蝙蝠宝宝宠物预制体")]
    public GameObject batBabyPetPrefab;
    public battleUI battleUI; // 用于装备4显示速度按钮

    private void Start()
    {
        // 成就装备0永久解锁，每次进入战斗都确保已解锁
        EquipmentSystem.Instance?.UnlockEquipment(EquipmentType.AchievementEquipment, 0);
        EnsurePersistentUnlocks();
        ApplyAllEquipments();

        // 复活管理器（R_2 读档币）：进入战斗时重置"每局仅一次"状态。
        // ReviveManager 是 DontDestroyOnLoad 单例，若场景里没有就动态创建一个。
        EnsureReviveManager();
        ReviveManager.Instance?.ResetForNewRun();

        // 测试模式（god-mode）：放在所有装备/抽卡加成之后，
        //   把玩家 hp/atk 强制拉到 99999，避免被前面的 += 累加稀释。
        //   开关由主菜单"测试模式"按钮控制，PlayerPrefs 持久化。
        if (TestModeManager.Instance != null && TestModeManager.Instance.Enabled && player != null)
        {
            player.healthmax = 99999;
            player.health    = 99999;
            player.atk       = 800f;
            ToastManager.Show("[测试模式] 玩家 HP 99999 / ATK 800");
            Debug.Log("[TestMode] 已将玩家 healthmax/health/atk 全部覆写为 99999。");

            // 测试模式起手赠送 10000 源木，便于联调奇遇/门挑战触发条件。
            // 用延迟一帧 + 协程确保 YuanMuManager（同场景组件）已 Awake，避免 null。
            StartCoroutine(GrantTestModeYuanMuNextFrame());
        }
    }

    /// <summary>测试模式：延迟一帧给玩家发放 10000 源木。</summary>
    private IEnumerator GrantTestModeYuanMuNextFrame()
    {
        yield return null;
        if (YuanMuManager.Instance != null)
        {
            YuanMuManager.Instance.Add(10000);
            ToastManager.Show("[测试模式] 源木 +10000");
            Debug.Log("[TestMode] 已发放 10000 源木。");
        }
        else
        {
            Debug.LogWarning("[TestMode] YuanMuManager.Instance 未初始化，10000 源木发放失败");
        }
    }

    /// <summary>
    /// 确保场景里存在 ReviveManager 单例。它是 DontDestroyOnLoad 持久对象——
    /// 玩家第一次进入战斗时由本方法挂出来，之后跨场景复用，无需在场景里手工挂载。
    /// 与 EquipmentSystem 的处理风格一致。
    /// </summary>
    private void EnsureReviveManager()
    {
        if (ReviveManager.Instance != null) return;
        var go = new GameObject("ReviveManager (auto)");
        go.AddComponent<ReviveManager>();
    }

    private void EnsurePersistentUnlocks()
    {
        if (EquipmentSystem.Instance == null) return;

        // 沙漏：累计游玩时长达到30分钟后应长期保持解锁
        int totalMinutes = PlayerPrefs.GetInt(KEY_TOTAL_PLAY_MINUTES, 0);
        if (totalMinutes >= 30)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 4);

        // 钥匙剑：进行过门挑战后应长期保持解锁
        if (PlayerPrefs.GetInt(KEY_GATE_CHALLENGE_STARTED_ONCE, 0) == 1)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 3);

        // 扎营大师：攻占营地达到100次
        if (PlayerPrefs.GetInt("CampCapturedCount", 0) >= 100)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 5);

        // 孢子异变：击败500个蘑菇敌人
        if (PlayerPrefs.GetInt("MushroomDefeatedCount", 0) >= 500)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 6);

        // 孢子之心（好感度装备0）：蘑菇好感度达到10后应保持解锁
        int favor = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Mushroom)
            : PlayerPrefs.GetInt("Favor_Mushroom", 0);
        if (favor >= 10)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 0);
        // 孢子喷射器（好感度装备1）：蘑菇好感度≥50
        if (favor >= 50)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 1);
        if (favor >= 100)
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 2);

        // 蝙蝠社群好感度装备 3–5
        int batFavor = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Bat)
            : PlayerPrefs.GetInt("Favor_Bat", 0);
        if (batFavor >= 10)  EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 3);
        if (batFavor >= 50)  EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 4);
        if (batFavor >= 100) EquipmentSystem.Instance.UnlockEquipment(EquipmentType.FavorEquipment, 5);
    }

    public void ApplyAllEquipments()
    {
        if (EquipmentSystem.Instance == null) return;

        ApplyClearEquipments();
        ApplyAchievementEquipments();
        ApplyGachaEquipments();
    }

    // ── 通关装备 ──────────────────────────────────────────
    private void ApplyClearEquipments()
    {
        // N1 通关装备 0：初心者之剑 - 攻击力 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 0))
        {
            if (player != null) player.atk += 1;
            ToastManager.Show("[装备] 初心者之剑：攻击力 +1");
        }

        // N1 通关装备 1：初心者之甲 - 生命值 +10
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 1))
        {
            if (player != null) { player.healthmax += 10; player.health += 10; }
            ToastManager.Show("[装备] 初心者之甲：生命值 +10");
        }

        // N1 通关装备 2：初心者之心 - 经验效率 DR +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 2))
        {
            if (player != null) player.DR += 1;
            ToastManager.Show("[装备] 初心者之心：经验效率 +1");
        }

        // N3 通关装备 3：小木剑 - 攻击力 +2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 3))
        {
            if (player != null) player.atk += 2;
            ToastManager.Show("[装备] 小木剑：攻击力 +2");
        }

        // N3 通关装备 4：小木甲 - 防御力 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 4))
        {
            if (player != null) player.def += 1;
            ToastManager.Show("[装备] 小木甲：防御力 +1");
        }

        // N3 通关装备 5：迷茫之心 - 经验效率 DR +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 5))
        {
            if (player != null) player.DR += 1;
            ToastManager.Show("[装备] 迷茫之心：经验效率 +1");
        }

        // N4 通关装备 6：正常铁剑 - 攻击力 +5
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 6))
        {
            if (player != null) player.atk += 5;
            ToastManager.Show("[装备] 正常铁剑：攻击力 +5");
        }

        // N4 通关装备 7：正常铁甲 - 生命值 +50
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 7))
        {
            if (player != null) { player.healthmax += 50; player.health += 50; }
            ToastManager.Show("[装备] 正常铁甲：生命值 +50");
        }

        // N4 通关装备 8：恐惧之心 - 闪避率 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 8))
        {
            if (player != null) player.EVA += 1;
            ToastManager.Show("[装备] 恐惧之心：闪避率 +1");
        }

        // N5 通关装备 9：源木之剑 - 攻击力 +10
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 9))
        {
            if (player != null) player.atk += 10;
            ToastManager.Show("[装备] 源木之剑：攻击力 +10");
        }

        // N5 通关装备 10：源木轻甲 - 移动速度 +2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 10))
        {
            if (player != null) player.speed += 2;
            ToastManager.Show("[装备] 源木轻甲：移动速度 +2");
        }

        // N5 通关装备 11：好奇之心 - 暴击率 +2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 11))
        {
            if (player != null) player.CR += 2;
            ToastManager.Show("[装备] 好奇之心：暴击率 +2");
        }

        // N6 通关装备 12：蘑菇之剑 - 攻击力 +15
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 12))
        {
            if (player != null) player.atk += 15;
            ToastManager.Show("[装备] 蘑菇之剑：攻击力 +15");
        }

        // N6 通关装备 13：蘑菇之甲 - 生命值+100
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 13))
        {
            if (player != null) { player.healthmax += 100; player.health += 100; }
            ToastManager.Show("[装备] 蘑菇之甲：生命值+100");
        }

        // N6 通关装备 14：孢子之心 - 自然回血 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 14))
        {
            if (player != null) player.regen += 1;
            ToastManager.Show("[装备] 孢子之心：自然回血 +1");
        }

        // N7 通关装备 15：熟练者之剑 - 攻击力＋15
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 15))
        {
            if (player != null) player.atk += 15;
            ToastManager.Show("[装备] 熟练者之剑：攻击力＋15");
        }

        // N7 通关装备 16：熟练者之甲 - 防御力＋1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 16))
        {
            if (player != null) player.def += 1;
            ToastManager.Show("[装备] 熟练者之甲：防御力＋1");
        }

        // N7 通关装备 17：熟练者之心 - 经验效率＋1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 17))
        {
            if (player != null) player.DR += 1;
            ToastManager.Show("[装备] 熟练者之心：经验效率＋1");
        }

        // N8 通关装备 18：和平之剑 - 攻击力＋20
        // 注：N8 是旧终局难度，单件武器加成比 N7（+15）再高一档。
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 18))
        {
            if (player != null) player.atk += 20;
            ToastManager.Show("[装备] 和平之剑：攻击力＋20");
        }

        // N8 通关装备 19：和平之甲 - 闪避率＋1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 19))
        {
            if (player != null) player.EVA += 1;
            ToastManager.Show("[装备] 和平之甲：闪避率＋1");
        }

        // N8 通关装备 20：和平之心 - 经验效率＋3
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 20))
        {
            if (player != null) player.DR += 3;
            ToastManager.Show("[装备] 和平之心：经验效率＋3");
        }

        // ── N9 通关装备（id 21-23）──────────────────────────
        // N9 通关装备 21：利爪之剑 - 攻击力＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 21))
        {
            if (player != null) player.atk += 20;
            ToastManager.Show("[装备] 利爪之剑：攻击力＋20");
        }

        // N9 通关装备 22：皮毛之甲 - 防御力＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 22))
        {
            if (player != null) player.def += 2;
            ToastManager.Show("[装备] 皮毛之甲：防御力＋2");
        }

        // N9 通关装备 23：野兽之心 - 经验效率＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 23))
        {
            if (player != null) player.DR += 2;
            ToastManager.Show("[装备] 野兽之心：经验效率＋2");
        }

        // ── N10 通关装备（id 24-26）─────────────────────────
        // N10 通关装备 24：月牙之剑 - 攻击力＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 24))
        {
            if (player != null) player.atk += 20;
            ToastManager.Show("[装备] 月牙之剑：攻击力＋20");
        }

        // N10 通关装备 25：月圆之甲 - 防御力＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 25))
        {
            if (player != null) player.def += 2;
            ToastManager.Show("[装备] 月圆之甲：防御力＋2");
        }

        // N10 通关装备 26：月球之心 - 自然回血＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 26))
        {
            if (player != null) player.regen += 2;
            ToastManager.Show("[装备] 月球之心：自然回血＋2");
        }

        // ── N11 通关装备（id 27-29）─────────────────────────
        // N11 通关装备 27：粘液之剑 - 攻击力＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 27))
        {
            if (player != null) player.atk += 20;
            ToastManager.Show("[装备] 粘液之剑：攻击力＋20");
        }

        // N11 通关装备 28：粘液之甲 - 生命值＋100
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 28))
        {
            if (player != null) { player.healthmax += 100; player.health += 100; }
            ToastManager.Show("[装备] 粘液之甲：生命值＋100");
        }

        // N11 通关装备 29：粘液之心 - 经验效率＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 29))
        {
            if (player != null) player.DR += 2;
            ToastManager.Show("[装备] 粘液之心：经验效率＋2");
        }

        // ── N12 通关装备（id 30-32）─────────────────────────
        // N12 通关装备 30：暗影之剑 - 攻击力＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 30))
        {
            if (player != null) player.atk += 20;
            ToastManager.Show("[装备] 暗影之剑：攻击力＋20");
        }

        // N12 通关装备 31：暗影之甲 - 防御力＋2
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 31))
        {
            if (player != null) player.def += 2;
            ToastManager.Show("[装备] 暗影之甲：防御力＋2");
        }

        // N12 通关装备 32：暗影之心 - 暴击伤害＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 32))
        {
            if (player != null) player.CD += 20f;
            ToastManager.Show("[装备] 暗影之心：暴击伤害＋20");
        }

        // ── N13 通关装备（id 33-35）─────────────────────────
        // N13 通关装备 33：龙鳞之剑 - 攻击力＋30
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 33))
        {
            if (player != null) player.atk += 30;
            ToastManager.Show("[装备] 龙鳞之剑：攻击力＋30");
        }

        // N13 通关装备 34：龙鳞之甲 - 防御力＋10
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 34))
        {
            if (player != null) player.def += 10;
            ToastManager.Show("[装备] 龙鳞之甲：防御力＋10");
        }

        // N13 通关装备 35：黄金睛 - 暴击伤害＋20
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 35))
        {
            if (player != null) player.CD += 20f;
            ToastManager.Show("[装备] 黄金睛：暴击伤害＋20");
        }
    }

    // ── 成就装备 ──────────────────────────────────────────
    private void ApplyAchievementEquipments()
    {
        // 成就装备 0：初始解锁风箭技能
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 0))
            ApplyAchievement0_WindArrow();

        // 成就装备 1：增加 20% 经验石拾取范围
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 1))
            ApplyAchievement1_PickupRadius();

        // 成就装备 2：解锁冲刺技能（空格键冲刺，2秒CD）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 2))
            ApplyAchievement2_Dash();

        // 成就装备 3：钥匙剑 - 解锁世界Boss奖励（标记，WorldBossManager 里检查）
        // 无需局内效果，解锁状态由 EquipmentSystem 持久化

        // 成就装备 4：沙漏 - 解锁三倍速按钮（二倍速默认开局自带）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 4))
            ApplyAchievement4_TripleSpeed();

        // 成就装备 5：扎营大师
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 5))
            ToastManager.Show("[装备] 扎营大师已装配：开局营地将吸引至身旁！");

        // 成就装备 6：孢子异变
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 6))
            ToastManager.Show("[装备] 孢子异变已装配：五颜六色的蘑菇人正在逼近！");

        // 成就装备 7：万象天引
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 7))
            ApplyAchievement7_WanxiangAttraction();

        // 好感度装备 0：孢子之心（好感度≥10解锁）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 0))
            ApplyFavorEquipment0_SporeHeart();

        // 好感度装备 1：孢子喷射器（好感度≥50解锁）— 蘑菇滑板（冲刺）冷却 -50%
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 1))
            ApplyFavorEquipment1_SporeEjector();

        // 好感度装备 2：蘑菇宝宝（好感度100解锁）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 2))
            ApplyFavorEquipment2_MushroomBaby();

        // 蝙蝠社群好感度 3–5
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 3))
            ApplyFavorEquipment3_BloodlineAwaken();

        // 血族之力：仅在技能存在时重置一次吸血开关（数值由 SkillBloodline 运行时读取存档）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 4))
            ApplyFavorEquipment4_BloodPower();

        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 5))
            ApplyFavorEquipment5_BatBaby();
    }

    /// <summary>成就装备0：初始解锁风箭技能</summary>
    private void ApplyAchievement0_WindArrow()
    {
        if (windArrowSkillPrefab == null || playerSkillList == null) return;

        foreach (Transform t in playerSkillList)
        {
            if (t.GetComponent<SkillWindArrow>() != null) return;
        }

        GameObject skill = Instantiate(windArrowSkillPrefab, playerSkillList);
        skill.GetComponent<Skillbase>().player = player.gameObject;
        ToastManager.Show("[装备] 成就装备0：风箭技能已激活");
    }

    /// <summary>
    /// 成就装备1「大手子」：初始经验石吸取距离 +30%，
    /// 每通关一个更高的难度额外 +5%（通关 N1 后 35%，N2 后 40%，以此类推）。
    /// </summary>
    private void ApplyAchievement1_PickupRadius()
    {
        if (player == null) return;
        // 基础加成 30%
        float bonusPercent = 30f;
        // 每通关一个更高难度 +5%（检查从 N1 开始连续通关了多少难度）
        int clearedLevels = GetHighestClearedDifficultyCount();
        bonusPercent += clearedLevels * 5f;
        float multiplier = 1f + bonusPercent / 100f;
        player.PickupRadius *= multiplier;
        ToastManager.Show($"[装备] 大手子：拾取范围 +{bonusPercent:0}%（通关 {clearedLevels} 个难度）");
    }

    /// <summary>获取玩家已连续通关的最高难度数量（N1 通了=1，N1+N2 通了=2，...）</summary>
    private int GetHighestClearedDifficultyCount()
    {
        if (ClearRecordManager.Instance == null || DifficultyManager.Instance == null) return 0;
        int count = 0;
        var configs = DifficultyManager.Instance.configs;
        for (int i = 0; i < configs.Length; i++)
        {
            if (ClearRecordManager.Instance.GetClearCount(configs[i].label) > 0)
                count++;
            else
                break; // 连续通关中断则停止计算
        }
        return count;
    }

    /// <summary>成就装备2：解锁冲刺技能（移动时按空格冲刺，2秒CD）</summary>
    private void ApplyAchievement2_Dash()
    {
        if (player == null) return;
        player.dashUnlocked = true;
        ToastManager.Show("[装备] 蘑菇滑板：冲刺技能已解锁（移动时按空格）");
    }

    /// <summary>成就装备4：沙漏 - 解锁三倍速按钮（二倍速默认开局自带）</summary>
    private void ApplyAchievement4_TripleSpeed()
    {
        if (battleUI == null) battleUI = FindObjectOfType<battleUI>();
        if (battleUI == null) return;
        battleUI.RefreshSpeedButtonState();
        // 提醒玩家：3 倍速对单帧负担放大较多（特别是蝙蝠潮、孢子大量子弹时），低端设备会明显卡顿。
        ToastManager.Show("[装备] 沙漏：三倍速功能已解锁（注意：三倍速会导致游戏卡顿，慎重使用）");
    }

    /// <summary>成就装备7：万象天引 - 每隔一分钟，将全图经验石吸引到自己周围</summary>
    private bool _wanxiangAttractionStarted;

    private void ApplyAchievement7_WanxiangAttraction()
    {
        if (player == null || _wanxiangAttractionStarted) return;
        _wanxiangAttractionStarted = true;
        StartCoroutine(WanxiangAttractionRoutine());
        ToastManager.Show("[装备] 万象天引：每隔一分钟吸引全图经验石");
    }

    private IEnumerator WanxiangAttractionRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f);
            PullAllExpStonesToPlayer();
        }
    }

    private void PullAllExpStonesToPlayer()
    {
        getexp[] stones = FindObjectsOfType<getexp>();
        foreach (getexp stone in stones)
        {
            if (stone == null) continue;
            if (player != null) stone.player = player;
            stone.flytoplayer = true;
        }
        if (stones.Length > 0)
            ToastManager.Show($"万象天引：吸引了 {stones.Length} 个经验石");
    }

    /// <summary>好感度装备0：孢子之心 - 好感度100时开局直接解锁孢子领域技能</summary>
    private void ApplyFavorEquipment0_SporeHeart()
    {
        if (sporeFieldSkillPrefab == null || playerSkillList == null) return;

        // 必须解锁了好感度装备0才能生效
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 0))
            return;

        // 好感度100：开局直接解锁，无需学习
        int favor = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Mushroom)
            : UnityEngine.PlayerPrefs.GetInt("Favor_Mushroom", 0);

        if (favor < 100) return;

        // 检查是否已有孢子领域
        foreach (Transform t in playerSkillList)
        {
            if (t.GetComponent<SkillSporeField>() != null) return;
        }

        GameObject skill = Instantiate(sporeFieldSkillPrefab, playerSkillList);
        skill.GetComponent<Skillbase>().player = player.gameObject;
        ToastManager.Show("[装备觉醒] 孢子之心：孢子领域技能已自动解锁！");
    }

    /// <summary>好感度装备1：孢子喷射器 — 蘑菇滑板（冲刺）冷却时间减半（-50%）</summary>
    private void ApplyFavorEquipment1_SporeEjector()
    {
        if (player == null) return;
        float b = player.DashCooldownBase > 0f ? player.DashCooldownBase : player.dashCooldown;
        player.dashCooldown = b * 0.5f;
        ToastManager.Show("[好感度装备] 孢子喷射器：蘑菇滑板冷却 -50%");
    }

    /// <summary>好感度装备2：蘑菇宝宝 - 好感度100时解锁，开局召唤跟随宠物</summary>
    private void ApplyFavorEquipment2_MushroomBaby()
    {
        if (player == null || mushroomBabyPetPrefab == null) return;

        int favor = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Mushroom)
            : PlayerPrefs.GetInt("Favor_Mushroom", 0);
        if (favor < 100) return;

        // 避免重复生成
        if (FindObjectOfType<MushroomBabyPet>() != null) return;

        Vector3 spawnPos = player.transform.position + new Vector3(-1.2f, 0f, 0f);
        GameObject petObj = Instantiate(mushroomBabyPetPrefab, spawnPos, Quaternion.identity);
        MushroomBabyPet pet = petObj.GetComponent<MushroomBabyPet>();
        if (pet != null)
        {
            pet.owner = player;
            if (pet.enemyLayer == null)
                pet.enemyLayer = GameObject.Find("enemylayer")?.transform;
        }
        ToastManager.Show("[装备] 蘑菇宝宝：已加入战斗！");
    }

    /// <summary>好感度装备3「吸血鬼大君·血族血统」：蝙蝠好感≥100时开局解锁血族血统技能。</summary>
    private void ApplyFavorEquipment3_BloodlineAwaken()
    {
        if (bloodlineSkillPrefab == null || playerSkillList == null) return;
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 3))
            return;

        int bf = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Bat)
            : UnityEngine.PlayerPrefs.GetInt("Favor_Bat", 0);

        // 未达到100：仅能通过升级三选一习得，不在此注入
        if (bf < 100) return;

        foreach (Transform t in playerSkillList)
        {
            if (t.GetComponent<SkillBloodline>() != null) return;
        }

        GameObject skill = Instantiate(bloodlineSkillPrefab, playerSkillList);
        var sb = skill.GetComponent<Skillbase>();
        if (sb != null) sb.player = player.gameObject;
        SkillBloodline bl = skill.GetComponent<SkillBloodline>();
        // 夏无专属：开局自动获得血族血统时也立即享受 UR 加成（number→5, lifestealRatio→0.20）
        if (bl != null) PlayerSkinSkillBuff.ApplyXiaWuBloodlineBuff(bl);
        // 蝙蝠宝宝装备同时生效时要 +1 初始数量（叠加在夏无加成之上）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 5) && bl != null)
            bl.number += 1;
        ToastManager.Show("[装备觉醒] 血族血统已在开局解锁！");
    }

    private void ApplyFavorEquipment4_BloodPower()
    {
        int bf = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Bat)
            : UnityEngine.PlayerPrefs.GetInt("Favor_Bat", 0);
        if (bf >= 50)
            ToastManager.Show("[好感度装备] 血族之力：血族血统附带吸血效果已激活（需已学会该技能）");
    }

    private void ApplyFavorEquipment5_BatBaby()
    {
        if (player == null || batBabyPetPrefab == null) return;

        int bf = FavorManager.Instance != null
            ? FavorManager.Instance.GetFavor(FactionType.Bat)
            : PlayerPrefs.GetInt("Favor_Bat", 0);
        if (bf < 100) return;

        if (FindObjectOfType<BatBabyPet>() != null) return;

        Vector3 spawnPos = player.transform.position + new Vector3(1.2f, 0f, 0f);
        GameObject petObj = Instantiate(batBabyPetPrefab, spawnPos, Quaternion.identity);
        BatBabyPet pet = petObj.GetComponent<BatBabyPet>();
        if (pet != null)
        {
            pet.owner = player;
            if (pet.enemyLayer == null)
                pet.enemyLayer = GameObject.Find("enemylayer")?.transform;
        }
        ToastManager.Show("[装备] 蝙蝠宝宝：已加入战斗！");
    }

    // ── 抽卡装备 ──────────────────────────────────────────
    private void ApplyGachaEquipments()
    {
        if (GachaManager.Instance == null) return;

        // R_0：remake 的刷新次数由 ChoiceUI 直接读取存档数量并同步，避免初始化时序导致丢失。

        // SR_0：经验灵果 - 每叠加一个，经验效率 DR +0.1
        int fruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 0);
        if (fruitCount > 0 && player != null)
        {
            player.DR += fruitCount * 0.1f;
            ToastManager.Show($"[抽卡] 经验灵果 ×{fruitCount}：经验效率 +{fruitCount * 0.1f:F1}");
        }

        // SR_1：攻击灵果 - 每叠加一个，攻击力 +0.1（共5攻击）
        int atkFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 1);
        if (atkFruitCount > 0 && player != null)
        {
            player.atk += atkFruitCount * 0.1f;
            ToastManager.Show($"[抽卡] 攻击灵果 ×{atkFruitCount}：攻击力 +{atkFruitCount * 0.1f:F1}");
        }

        // SR_2：防御灵果 - 每叠加一个，防御力 +0.1（共2防御）
        int defFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 2);
        if (defFruitCount > 0 && player != null)
        {
            player.def += defFruitCount * 0.1f;
            ToastManager.Show($"[抽卡] 防御灵果 ×{defFruitCount}：防御力 +{defFruitCount * 0.1f:F1}");
        }

        // SR_3：生命灵果 - 每叠加一个，生命值 +1（共100生命）
        int hpFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 3);
        if (hpFruitCount > 0 && player != null)
        {
            player.healthmax += hpFruitCount;
            player.health    += hpFruitCount;
            ToastManager.Show($"[抽卡] 生命灵果 ×{hpFruitCount}：生命值 +{hpFruitCount}");
        }

        // SR_4：暴击灵果 - 每叠加一个，暴击率 +0.1（共10暴击）
        int crFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 4);
        if (crFruitCount > 0 && player != null)
        {
            player.CR += crFruitCount * 0.1f;
            ToastManager.Show($"[抽卡] 暴击灵果 ×{crFruitCount}：暴击率 +{crFruitCount * 0.1f:F1}");
        }

        // SR_5：暴伤灵果 - 每叠加一个，暴击伤害 +0.1（共30暴伤）
        int cdFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 5);
        if (cdFruitCount > 0 && player != null)
        {
            player.CD += cdFruitCount * 0.1f;
            ToastManager.Show($"[抽卡] 暴伤灵果 ×{cdFruitCount}：暴击伤害 +{cdFruitCount * 0.1f:F1}");
        }

        // SR_6：速度灵果 - 每叠加一个，移动速度 +0.05（满池 100 件 = +5 移速）
        //   注意：Attribute.speed 是 int，无法直接 += 0.05f；
        //   采用「累积法」：count × 0.05 = 加成数值，向下取整后整体一次性加到 player.speed。
        //   例：count=20 → +1；count=100 → +5；count=15 → +0（不足 20 累计不取整）。
        //   这样数值含义与 SR_3 生命灵果（每件 +1）类似但分母更细，符合策划"100 个池"的设计。
        int spdFruitCount = GachaManager.Instance.GetItemCount(GachaRarity.SR, 6);
        if (spdFruitCount > 0 && player != null)
        {
            int spdBonus = Mathf.FloorToInt(spdFruitCount * 0.05f);
            if (spdBonus > 0) player.speed += spdBonus;
            ToastManager.Show($"[抽卡] 速度灵果 ×{spdFruitCount}：移动速度 +{spdBonus}（每20件+1）");
        }

        // R_2：读档币 - 每张可在死亡时原地复活一次，每局仅可使用一次。
        //   实际消耗 / 复活流程由 ReviveManager 在 Player.death() 中拦截处理；
        //   这里只做一次 Toast 让玩家知晓自己持有几张。
        int reviveCount = GachaManager.Instance.GetItemCount(GachaRarity.R, 2);
        if (reviveCount > 0)
        {
            ToastManager.Show($"[抽卡] 读档币 ×{reviveCount}：死亡时可消耗 1 张原地复活（本局上限 1 次）");
        }

        // ── SSR（抽卡解锁型，equipmentSystemId 与 GachaManager.ssrItems 一致）──
        // SSR_4 被观测者：未学习技能也可吃上限奖励（在 ChoiceUI.GetEffectiveMaxUpgrades 中处理）

        // SSR_5 虚空斗篷：蘑菇滑板冲刺过程无敌
        if (IsGachaEquipmentUnlockedAny(7, 5))
            ApplyGachaSSR5_VoidCloak();

        // SSR_6 影分身之术：与人格解离奇遇联动（在 AdventurePersonalityDissolve 中处理）
        // UR（风之形/地狱火）：仅解锁学习资格，不开局自动注入技能

        // SSR_8「我与我与我」(equipmentSystemId=11)：人格解离奇遇可触发第二次，场上最多 2 个分身。
        // 实际逻辑在 AdventurePersonalityDissolve.IsAvailableInCurrentDifficulty / Execute 中，
        // 这里只做 toast 提示。
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 11))
            ToastManager.Show("[抽卡·SSR] 我与我与我：人格解离可触发第二次（场上最多 2 个分身）");

        // SSR_9「三清化一」(equipmentSystemId=12)：分身技能直接合并到本体，分身被销毁。
        // 实际逻辑在 AdventurePersonalityDissolve.Execute 中把分身 SkillList 搬到本体 SkillListClone，
        // 然后 Destroy 分身，彻底避免物理碰撞问题。这里只做 toast 提示。
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 12))
            ToastManager.Show("[抽卡·SSR] 三清化一：分身技能将融入本体，效果翻倍");

        // SSR_10「饮血剑」(equipmentSystemId=13)：全能吸血 +1（所有伤害来源造成伤害的 1% 转为回血）。
        // 数值在 Bulletbase / BulletSporeField / BulletBloodlineBat 等结算点调用 TryAllSourceLifesteal 触发。
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 13))
            ToastManager.Show("[抽卡·SSR] 饮血剑：所有来源伤害的 1% 将转为生命值");

        // SSR_11「气运之子」(equipmentSystemId=14)：奇遇从二选一变三选一
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 14))
            ToastManager.Show("[抽卡·SSR] 气运之子：奇遇可选数量+1（二选一变三选一）");

        // SSR_2 启动资金：等级+3 + 开局3次三选一 — 在 battleUI.Start 末尾处理（避免与 choiceUI 初始化顺序冲突）
        // SSR_0 便携营地：营地数量在 title.click_start 中处理
        // SSR_1 白色杀手：门挑战增伤在 Bulletbase 中处理
        // SSR_3 苍白记忆：技能升级上限在 ChoiceUI.GetEffectiveMaxUpgrades 中处理
    }

    private void ApplyGachaSSR5_VoidCloak()
    {
        if (player == null) return;
        player.dashInvincibleUnlocked = true;
        player.dashPhaseUnlocked = true;
        ToastManager.Show("[抽卡·SSR] 虚空斗篷：冲刺期间无敌且可穿过敌人");
    }

    private bool IsGachaEquipmentUnlockedAny(params int[] ids)
    {
        if (EquipmentSystem.Instance == null || ids == null) return false;
        foreach (int id in ids)
        {
            if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, id))
                return true;
        }
        return false;
    }

    // ── SSR_10 饮血剑：全局吸血 ──────────────────────────────
    /// <summary>
    /// SSR_10「饮血剑」(equipmentSystemId=13) 是否已装备。
    /// 抽卡装备一旦解锁就视为永久生效，无需局内额外配置。
    /// </summary>
    public static bool HasAllSourceLifesteal()
    {
        return EquipmentSystem.Instance != null
            && EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 13);
    }

    /// <summary>每点饮血剑等级对应的吸血比例（当前 1 级 = 1%）。</summary>
    private const float ALL_SOURCE_LIFESTEAL_RATIO = 0.01f;

    /// <summary>
    /// 全局吸血触发：任何技能/子弹对敌人造成最终伤害后，调用此方法把 dealtDamage × 1% 转为玩家回血。
    /// 不依赖具体技能/子弹种类——只要装备解锁就生效（与「血族吸血」叠加生效，不互斥）。
    ///
    /// 调用点：
    ///   - Bulletbase.OnTriggerEnter （风箭/火球/暗齿轮/飓风等所有通用子弹）
    ///   - BulletSporeField.AOE 结算 （孢子领域 / 亡者领域共用）
    ///   - BulletBloodlineBat.HitEnemy （血族蝙蝠，与原 SkillBloodline 吸血叠加）
    ///   - 未来新增的伤害源也应在此方法登记
    /// </summary>
    /// <param name="dealtDamage">本次造成的最终伤害（已扣防御/暴击）</param>
    /// <param name="floatingTextPrefab">用于飘字的 prefab（可空）</param>
    /// <param name="floatingPosition">飘字位置（一般为玩家位置）</param>
    public static void TryAllSourceLifesteal(int dealtDamage, GameObject floatingTextPrefab, Vector3 floatingPosition)
    {
        if (!HasAllSourceLifesteal()) return;
        if (dealtDamage <= 0) return;

        Player pl = FindLocalPlayer();
        if (pl == null) return;

        int heal = Mathf.Max(1, Mathf.RoundToInt(dealtDamage * ALL_SOURCE_LIFESTEAL_RATIO));
        if (pl.health >= pl.healthmax) return; // 满血不弹绿字
        pl.health = Mathf.Min(pl.health + heal, pl.healthmax);

        if (floatingTextPrefab != null && DamageNumberSettings.Visible)
        {
            GameObject num = UnityEngine.Object.Instantiate(floatingTextPrefab, floatingPosition, Quaternion.identity);
            var txt = num.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = "+" + heal;
                // 「饮血剑」用偏暗的血红色与血族（亮绿）区分；策划想统一为绿色可去掉这行。
                txt.color = new Color32(220, 60, 60, 255);
            }
        }
    }

    private static Player _cachedPlayer;
    private static Player FindLocalPlayer()
    {
        if (_cachedPlayer != null) return _cachedPlayer;
        _cachedPlayer = UnityEngine.Object.FindObjectOfType<Player>();
        return _cachedPlayer;
    }
}
