using UnityEngine;

/// <summary>
/// 在进入战斗场景时初始化存档装备效果。
/// 挂在 BattleUI 或战斗场景根对象上，在 Start() 时调用。
/// </summary>
public class EquipmentInitializer : MonoBehaviour
{
    [Header("引用")]
    public Player player;
    public Transform playerSkillList;
    public GameObject windArrowSkillPrefab;
    public GameObject sporeFieldSkillPrefab;
    public battleUI battleUI; // 用于装备4显示速度按钮

    private void Start()
    {
        // 成就装备0永久解锁，每次进入战斗都确保已解锁
        EquipmentSystem.Instance?.UnlockEquipment(EquipmentType.AchievementEquipment, 0);
        ApplyAllEquipments();
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

        // N6 通关装备 13：蘑菇之甲 - 闪避率 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 13))
        {
            if (player != null) player.EVA += 1;
            ToastManager.Show("[装备] 蘑菇之甲：闪避率 +1");
        }

        // N6 通关装备 14：孢子之心 - 自然回血 +1
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.ClearEquipment, 14))
        {
            if (player != null) player.regen += 1;
            ToastManager.Show("[装备] 孢子之心：自然回血 +1");
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

        // 成就装备 4：沙漏 - 解锁二倍速按钮
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 4))
            ApplyAchievement4_DoubleSpeed();

        // 好感度装备 0：孢子之心（好感度≥10解锁）
        if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 0))
            ApplyFavorEquipment0_SporeHeart();
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

    /// <summary>成就装备1：增加20%经验石拾取范围</summary>
    private void ApplyAchievement1_PickupRadius()
    {
        if (player == null) return;
        player.PickupRadius *= 1.2f;
        ToastManager.Show($"[装备] 成就装备1：拾取范围 x1.2");
    }

    /// <summary>成就装备2：解锁冲刺技能（移动时按空格冲刺，2秒CD）</summary>
    private void ApplyAchievement2_Dash()
    {
        if (player == null) return;
        player.dashUnlocked = true;
        ToastManager.Show("[装备] 蘑菇滑板：冲刺技能已解锁（移动时按空格）");
    }

    /// <summary>成就装备4：沙漏 - 解锁二倍速按钮</summary>
    private void ApplyAchievement4_DoubleSpeed()
    {
        if (battleUI == null) battleUI = FindObjectOfType<battleUI>();
        if (battleUI == null) return;
        if (battleUI.speedButtonText != null)
            battleUI.speedButtonText.transform.parent.gameObject.SetActive(true);
        ToastManager.Show("[装备] 沙漏：二倍速功能已解锁");
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
    }
}
