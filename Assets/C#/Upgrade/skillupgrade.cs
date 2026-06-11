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
                // 亡者领域锁定后：风箭半径固定为 10，不再被升级改动
                if (wa != null && !wa.IsLockedByTombDomain) wa.attackRadius += upgradenumber;
                SkillSporeField sf = choiceskill as SkillSporeField;
                // 亡者领域锁定后：孢子领域半径固定为 10，不再被升级改动
                if (sf != null && !sf.IsLockedByTombDomain) sf.attackRadius += upgradenumber;
                SkillBloodline bl = choiceskill as SkillBloodline;
                if (bl != null) bl.attackRadius += upgradenumber;
                break;
        }
        closechoice();
        battleUI.RefreshSkill();
    }
}
