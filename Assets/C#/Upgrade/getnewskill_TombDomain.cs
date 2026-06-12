using UnityEngine;

/// <summary>
/// 学习「亡者领域」（孢子领域 UR 进化）：
/// 需已学习风箭 + 已学习孢子领域，且二者 attackRadius>=15。
/// （与「风之形」无关——风之形不再是亡者领域的前置技能）
/// 抽卡装备解锁位 GachaEquipment id=10（与 GachaManager.urItems[2].equipmentSystemId 保持一致）。
/// 抽卡池硬门槛：必须先通关 N6（见 GachaManager.IsUrItemUnlocked）。
/// 无罪（SKIN_TOMB）开局自带风箭+孢子领域、attackRadius=20，故开局即满足全部进化条件。
/// </summary>
public class getnewskill_TombDomain : getnewskill
{
    public const string SporeFieldSkillName = "孢子领域";
    public const float RequiredSporeRadius = 15f;
    public const float RequiredWindArrowRadius = 15f;
    // UR_2 对应 GachaEquipment id=10（避开 SSR 已占用的 0~9 命名空间，与 GachaManager.cs 中
    // urItems[2].equipmentSystemId=10 保持一致）。
    public const int RequiredUrEquipmentId = 10;
    public const int KeepOriginalOnEvolutionEquipmentId = 9;
    public const int KeepOriginalOnEvolutionEquipmentFallbackId = 7;

    public override bool IsAvailableInPool()
    {
        if (!base.IsAvailableInPool())
        {
            Debug.Log("[亡者领域·候选] base.IsAvailableInPool() 失败");
            return false;
        }
        if (EquipmentSystem.Instance == null)
        {
            Debug.Log("[亡者领域·候选] EquipmentSystem.Instance == null");
            return false;
        }
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, RequiredUrEquipmentId))
        {
            Debug.Log($"[亡者领域·候选] UR 装备 id={RequiredUrEquipmentId} 未解锁（抽到亡者领域 UR 抽卡装备 后才会解锁）");
            return false;
        }

        var pl = ResolvePlayer();
        if (pl == null || pl.SkillList == null)
        {
            Debug.Log("[亡者领域·候选] 找不到 Player 或 Player.SkillList==null");
            return false;
        }

        SkillSporeField sf = null;
        SkillWindArrow wa = null;
        foreach (Transform t in pl.SkillList)
        {
            if (t == null) continue;
            if (sf == null) sf = t.GetComponent<SkillSporeField>();
            if (wa == null) wa = t.GetComponent<SkillWindArrow>();
        }

        // 进化前置：已学风箭 + 已学孢子领域。与「风之形」无关。
        if (sf == null || wa == null)
        {
            Debug.Log($"[亡者领域·候选] 技能不全：SkillSporeField={(sf!=null)}, SkillWindArrow={(wa!=null)}");
            return false;
        }
        if (sf.attackRadius < RequiredSporeRadius)
        {
            Debug.Log($"[亡者领域·候选] 孢子领域 attackRadius={sf.attackRadius} < {RequiredSporeRadius}");
            return false;
        }
        if (wa.attackRadius < RequiredWindArrowRadius)
        {
            Debug.Log($"[亡者领域·候选] 风箭 attackRadius={wa.attackRadius} < {RequiredWindArrowRadius}");
            return false;
        }
        Debug.Log("[亡者领域·候选] 全部条件满足，进入卡池！");
        return true;
    }

    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = ResolvePlayer();
        if (player == null || player.SkillList == null) return;

        SkillSporeField sporeFieldSkill = null;
        SkillFormOfWind formOfWindSkill = null;
        SkillWindArrow  windArrowSkill  = null;
        foreach (Transform ski in player.SkillList)
        {
            if (ski == null) continue;
            if (sporeFieldSkill == null) sporeFieldSkill = ski.GetComponent<SkillSporeField>();
            if (formOfWindSkill == null) formOfWindSkill = ski.GetComponent<SkillFormOfWind>();
            if (windArrowSkill  == null) windArrowSkill  = ski.GetComponent<SkillWindArrow>();
        }

        GameObject obj = Instantiate(skill.gameObject, player.SkillList);
        SkillTombDomain td = obj.GetComponent<SkillTombDomain>();
        // 风之形不是必须前置：玩家若恰好同时持有，则继承其快照；否则传 null（亡者领域内部需要兼容 null）
        if (td != null) td.ApplyInheritanceSnapshot(sporeFieldSkill, formOfWindSkill);

        bool keepOriginal = IsKeepOriginalUnlocked();
        // 无罪皮肤（SKIN_TOMB）：亡者领域作为本命技能，必须保留原孢子领域——
        //   1) 紫色范围圈是亡者领域唯一的可视范围反馈，靠 SporeField.LockToTombDomainPalette 绘制；
        //      若被销毁，亡者领域玩家会看不到范围圈。
        //   2) 无罪专属"每分钟+1 范围（上限 20）"成长机制（见 SkillTombDomain.FixedUpdate）需要
        //      孢子领域作为载体写入 attackRadius，没有它就没办法应用成长。
        //   实现上等同于给无罪附赠了一份永久"不忘初心"待遇——但仅对无罪生效，不影响其他角色。
        bool isTombSkin = PlayerSkinSkillBuff.CurrentSkinIndex == PlayerSkinSkillBuff.SKIN_TOMB;
        // 学习亡者领域后，默认删除原孢子领域（除非「不忘初心」生效，或当前是无罪皮肤）
        if (sporeFieldSkill != null && !keepOriginal && !isTombSkin)
            Destroy(sporeFieldSkill.gameObject);
        // 若「不忘初心」/无罪皮肤 保留了原孢子领域，则立即锁定其半径=10 + 紫色范围圈，
        // 与亡者领域的"幽冥紫"主题一致；并阻止后续升级再加范围。
        else if (sporeFieldSkill != null && (keepOriginal || isTombSkin))
            sporeFieldSkill.LockToTombDomainPalette();

        // 2026-06 改动：进化亡者领域后不再锁定风箭的攻击范围和范围圈颜色。
        // 风箭保持玩家当前已升级的 attackRadius 和原有圈色，后续仍可通过升级继续加范围。
        // （旧逻辑曾在此处调用 windArrowSkill.LockToTombDomainPalette() 将风箭锁到半径10+紫色，已移除）

        battleUI.RefreshSkill();
        closechoice();
    }

    private Player ResolvePlayer()
    {
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer == null) return null;
        foreach (Transform t in playerLayer)
        {
            if (t != null && t.CompareTag("Player"))
                return t.GetComponent<Player>();
        }
        return playerLayer.childCount > 0 ? playerLayer.GetChild(0).GetComponent<Player>() : null;
    }

    private bool IsKeepOriginalUnlocked()
    {
        if (EquipmentSystem.Instance == null) return false;
        return EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentId) ||
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentFallbackId);
    }
}
