using UnityEngine;

/// <summary>
/// 在进入战斗场景时初始化存档装备效果。
/// 挂在 BattleUI 或战斗场景根对象上，在 Start() 时调用。
/// </summary>
public class EquipmentInitializer : MonoBehaviour
{
    [Header("引用")]
    public Player player;
    public Transform playerSkillList;       // 玩家技能列表 Transform
    public GameObject windArrowSkillPrefab; // 风箭技能 prefab

    private void Start()
    {
        EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 0);
        ApplyAllEquipments();
    }

    public void ApplyAllEquipments()
    {
        if (EquipmentSystem.Instance == null) return;

        ApplyAchievementEquipments();
        // 后续其他类型装备在此扩展
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
    }

    /// <summary>成就装备0：初始解锁风箭技能</summary>
    private void ApplyAchievement0_WindArrow()
    {
        if (windArrowSkillPrefab == null || playerSkillList == null) return;

        // 检查是否已有风箭，避免重复添加
        foreach (Transform t in playerSkillList)
        {
            if (t.GetComponent<SkillWindArrow>() != null) return;
        }

        GameObject skill = Instantiate(windArrowSkillPrefab, playerSkillList);
        skill.GetComponent<Skillbase>().player = player.gameObject;
        Debug.Log("[装备] 成就装备0：风箭技能已解锁");
    }

    /// <summary>成就装备1：增加20%经验石拾取范围</summary>
    private void ApplyAchievement1_PickupRadius()
    {
        if (player == null) return;
        player.PickupRadius *= 1.2f;
        Debug.Log($"[装备] 成就装备1：拾取范围 -> {player.PickupRadius}");
    }
}
