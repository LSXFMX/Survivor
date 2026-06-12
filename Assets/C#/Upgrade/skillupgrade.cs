using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skillupgrade : Upgradeoptionsbase
{
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
        if (player == null) return;
        Skillbase choiceskill=null;
        foreach(Transform ski in player.SkillList)
        {
            Skillbase s = ski.GetComponent<Skillbase>();
            if (skill.Skillname==s.Skillname)
            {
                choiceskill = s;
            }
        }
        if (choiceskill == null) return;
        switch (skillAtr)
        {
            case skillAttribute.CDtime:
                choiceskill.CDtime += upgradenumber; // 减少CD填负数，增加CD填正数
                break;
            case skillAttribute.damage:
                choiceskill.damage += (int)upgradenumber;
                break;
            case skillAttribute.lifetime:
                choiceskill.lifetime += upgradenumber;
                break;
            case skillAttribute.pass:
                choiceskill.pass += (int)upgradenumber;
                break;
            case skillAttribute.speed:
                choiceskill.speed += upgradenumber;
                break;
            case skillAttribute.number:
                choiceskill.number += (int)upgradenumber;
                break;
            case skillAttribute.size:
                choiceskill.size += upgradenumber;
                break;
            case skillAttribute.interval:
                choiceskill.interval += upgradenumber;
                break;
            case skillAttribute.attackRadius:
                SkillWindArrow wa = choiceskill as SkillWindArrow;
                // 2026-06 改动：亡者领域不再锁定风箭范围，风箭始终可被升级加范围
                if (wa != null) wa.attackRadius += upgradenumber;
                SkillSporeField sf = choiceskill as SkillSporeField;
                // 亡者领域锁定后：孢子领域半径固定为 10，不再被升级改动
                if (sf != null && !sf.IsLockedByTombDomain) sf.attackRadius += upgradenumber;
                SkillBloodline bl = choiceskill as SkillBloodline;
                if (bl != null) bl.attackRadius += upgradenumber;
                break;
        }

        // SSR9「三清化一」+ SSR6「影分身之术」联动：
        // 本体技能升级后，同步升级 SkillListClone 中同名技能（维持 SSR6 "实时同步" 语义）
        SyncUpgradeToCloneSkills(player, skill.Skillname, skillAtr, upgradenumber);

        closechoice();
        battleUI.RefreshSkill();
    }

    /// <summary>
    /// SSR9「三清化一」联动：把本体技能升级同步到 SkillListClone 中同名技能。
    /// 仅当 SSR6 解锁时生效（SSR6 语义 = 分身技能继承本体 30% 数值）。
    /// 无 SSR6 时分身技能保持创建时的固定数值，不跟随本体升级。
    /// 有 SSR6 时：升级增量按 30% 缩放后同步到 SkillListClone。
    /// </summary>
    private void SyncUpgradeToCloneSkills(Player p, string skillName, skillAttribute attr, float value)
    {
        if (p == null || p.SkillListClone == null || p.SkillListClone.childCount == 0) return;

        // 检查 SSR6 是否解锁
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 8))
            return;

        // SSR6：升级增量按 30% 同步
        float scaledValue = value * 0.3f;

        foreach (Transform t in p.SkillListClone)
        {
            if (t == null) continue;
            Skillbase s = t.GetComponent<Skillbase>();
            if (s == null || s.Skillname != skillName) continue;

            switch (attr)
            {
                case skillAttribute.CDtime:    s.CDtime    += scaledValue; break;
                case skillAttribute.damage:    s.damage    += Mathf.RoundToInt(scaledValue); break;
                case skillAttribute.lifetime:  s.lifetime  += scaledValue; break;
                case skillAttribute.pass:      s.pass      += Mathf.RoundToInt(scaledValue); break;
                case skillAttribute.speed:     s.speed     += scaledValue; break;
                case skillAttribute.number:    s.number    = Mathf.Max(1, s.number + Mathf.RoundToInt(scaledValue)); break;
                case skillAttribute.size:      s.size      += scaledValue; break;
                case skillAttribute.interval:  s.interval  += scaledValue; break;
                case skillAttribute.attackRadius:
                    SkillWindArrow cwa = s as SkillWindArrow;
                    if (cwa != null) cwa.attackRadius += scaledValue;
                    SkillSporeField csf = s as SkillSporeField;
                    if (csf != null && !csf.IsLockedByTombDomain) csf.attackRadius += scaledValue;
                    SkillBloodline cbl = s as SkillBloodline;
                    if (cbl != null) cbl.attackRadius += scaledValue;
                    break;
            }
        }
    }
}
