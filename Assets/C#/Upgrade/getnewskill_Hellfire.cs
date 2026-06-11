using UnityEngine;

/// <summary>
/// 学习「地狱火」：
/// 需已学习火球术，且风箭多重数量 >= 3。
/// （历史：曾要求 >= 5，与"夏无开局即默认风箭多重=3"配合后改为 3，让 UR 进化条件在 UR 角色身上一进局就近达成。）
/// </summary>
public class getnewskill_Hellfire : getnewskill
{
    public const string FireballSkillName = "火球术";
    public const int RequiredWindArrowMultishot = 3;
    // 按策划约定：UR 编号1 对应 GachaEquipment id=5（风之形为 id=4）
    public const int RequiredUrEquipmentId = 5;
    // SSR「不忘初心」：历史映射可能出现 id=9 或 id=7，二者任一解锁都视为生效
    public const int KeepOriginalOnEvolutionEquipmentId = 9;
    public const int KeepOriginalOnEvolutionEquipmentFallbackId = 7;

    public override bool IsAvailableInPool()
    {
        if (!base.IsAvailableInPool())
        {
            Debug.Log("[地狱火·候选] base.IsAvailableInPool() 失败");
            return false;
        }
        if (EquipmentSystem.Instance == null)
        {
            Debug.Log("[地狱火·候选] EquipmentSystem.Instance == null");
            return false;
        }
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, RequiredUrEquipmentId))
        {
            Debug.Log($"[地狱火·候选] UR 装备 id={RequiredUrEquipmentId}（地狱火）未解锁——必须先在抽卡里抽到「地狱火」UR 抽卡装备");
            return false;
        }

        var playerLayer = GameObject.Find("playerlayer")?.transform;
        Player pl = null;
        if (playerLayer != null)
        {
            foreach (Transform t in playerLayer)
            {
                if (t != null && t.CompareTag("Player"))
                {
                    pl = t.GetComponent<Player>();
                    break;
                }
            }
            if (pl == null && playerLayer.childCount > 0)
                pl = playerLayer.GetChild(0).GetComponent<Player>();
        }
        if (pl == null || pl.SkillList == null)
        {
            Debug.Log("[地狱火·候选] 找不到 Player 或 Player.SkillList==null");
            return false;
        }

        bool hasFireball = false;
        SkillWindArrow wa = null;

        foreach (Transform ski in pl.SkillList)
        {
            if (ski == null) continue;
            var s = ski.GetComponent<Skillbase>();
            if (s != null && s.Skillname == FireballSkillName)
                hasFireball = true;

            var wind = ski.GetComponent<SkillWindArrow>();
            if (wind != null)
                wa = wind;
        }

        if (!hasFireball)
        {
            Debug.Log("[地狱火·候选] 玩家未持有「火球术」——地狱火是火球术的进化，必须先学到火球术");
            return false;
        }
        if (wa == null)
        {
            Debug.Log("[地狱火·候选] 玩家未持有「风箭」——地狱火进化需要风箭多重数量作为门槛");
            return false;
        }
        if (wa.number < RequiredWindArrowMultishot)
        {
            Debug.Log($"[地狱火·候选] 风箭 number={wa.number} < {RequiredWindArrowMultishot}（继续升风箭多重以达成）");
            return false;
        }
        Debug.Log("[地狱火·候选] 全部条件满足，进入卡池！");
        return true;
    }

    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = null;
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer != null)
        {
            foreach (Transform t in playerLayer)
            {
                if (t != null && t.CompareTag("Player"))
                {
                    player = t.GetComponent<Player>();
                    break;
                }
            }
            if (player == null && playerLayer.childCount > 0)
                player = playerLayer.GetChild(0).GetComponent<Player>();
        }
        if (player == null || player.SkillList == null) return;

        Skillbase fireballSkill = null;
        SkillWindArrow windArrowSkill = null;
        foreach (Transform ski in player.SkillList)
        {
            if (ski == null) continue;
            Skillbase sb = ski.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == FireballSkillName)
                fireballSkill = sb;

            var wa = ski.GetComponent<SkillWindArrow>();
            if (wa != null)
                windArrowSkill = wa;
        }

        // 创建进化技能
        GameObject hellfireObj = Instantiate(skill.gameObject, player.SkillList);
        SkillHellfire hellfire = hellfireObj.GetComponent<SkillHellfire>();
        if (hellfire != null)
            hellfire.ApplyInheritanceSnapshot(fireballSkill, windArrowSkill);

        bool keepOriginal = IsKeepOriginalUnlocked();

        // 进化后默认删除基础技能（风箭相关不删；此处删除火球术）；若有SSR「不忘初心」则保留
        if (fireballSkill != null && !keepOriginal)
            Destroy(fireballSkill.gameObject);

        battleUI.RefreshSkill();
        closechoice();
    }

    private bool IsKeepOriginalUnlocked()
    {
        if (EquipmentSystem.Instance == null) return false;
        return EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentId) ||
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentFallbackId);
    }
}
