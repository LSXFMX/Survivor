using UnityEngine;

/// <summary>学习「风之形」：需风箭攻击范围≥10、多重≥2、已拥有飓风。
/// 所有角色规则一致——风之形与「亡者领域」UR 进化没有关系，无罪不再特殊处理。
/// </summary>
public class getnewskill_FormOfWind : getnewskill
{
    public const float RequiredWindArrowRadius = 10f;
    public const int RequiredWindArrowMultishot = 2;
    public const string HurricaneSkillName = "飓风";
    // 风之形对应抽卡装备解锁位（GachaEquipment id）
    public const int RequiredUrEquipmentId = 4;
    // SSR「不忘初心」：历史映射可能出现 id=9 或 id=7，二者任一解锁都视为生效
    public const int KeepOriginalOnEvolutionEquipmentId = 9;
    public const int KeepOriginalOnEvolutionEquipmentFallbackId = 7;

    public override bool IsAvailableInPool()
    {
        if (!base.IsAvailableInPool())
        {
            Debug.Log("[风之形·候选] base.IsAvailableInPool() 失败");
            return false;
        }
        if (EquipmentSystem.Instance == null)
        {
            Debug.Log("[风之形·候选] EquipmentSystem.Instance == null");
            return false;
        }
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, RequiredUrEquipmentId))
        {
            Debug.Log($"[风之形·候选] UR 装备 id={RequiredUrEquipmentId}（风之形）未解锁——必须先在抽卡里抽到「风之形」UR 抽卡装备");
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
            Debug.Log("[风之形·候选] 找不到 Player 或 Player.SkillList==null");
            return false;
        }

        bool hasHurricane = false;
        SkillWindArrow wa = null;

        foreach (Transform ski in pl.SkillList)
        {
            if (ski == null) continue;
            var s = ski.GetComponent<Skillbase>();
            if (s != null && s.Skillname == HurricaneSkillName)
                hasHurricane = true;

            var wind = ski.GetComponent<SkillWindArrow>();
            if (wind != null)
                wa = wind;
        }

        if (wa == null)
        {
            Debug.Log("[风之形·候选] 玩家未持有「风箭」——风之形是风箭的进化，必须先学到风箭");
            return false;
        }
        if (!hasHurricane)
        {
            Debug.Log("[风之形·候选] 玩家未持有「飓风」（风之形必需前置）——可在升级里学到飓风后再来");
            return false;
        }
        if (wa.attackRadius < RequiredWindArrowRadius)
        {
            Debug.Log($"[风之形·候选] 风箭 attackRadius={wa.attackRadius} < {RequiredWindArrowRadius}（继续升风箭范围以达成）");
            return false;
        }
        if (wa.number < RequiredWindArrowMultishot)
        {
            Debug.Log($"[风之形·候选] 风箭 number={wa.number} < {RequiredWindArrowMultishot}（继续升风箭多重以达成）");
            return false;
        }
        Debug.Log("[风之形·候选] 全部条件满足，进入卡池！");
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

        Skillbase hurricaneSkill = null;
        foreach (Transform ski in player.SkillList)
        {
            if (ski == null) continue;
            Skillbase sb = ski.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == HurricaneSkillName)
            {
                hurricaneSkill = sb;
                break;
            }
        }

        Instantiate(skill.gameObject, player.SkillList);

        bool keepOriginal = IsKeepOriginalUnlocked();

        // 学习风之形后默认删除基础技能：飓风；若有SSR「不忘初心」则保留
        if (hurricaneSkill != null && !keepOriginal)
            Destroy(hurricaneSkill.gameObject);

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
