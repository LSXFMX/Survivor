using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skillupgrade : Upgradeoptionsbase
{
    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
        Skillbase choiceskill=null;
        foreach(Transform ski in player.SkillList)
        {
            Skillbase s = ski.GetComponent<Skillbase>();
            if (skill.Skillname==s.Skillname)
            {
                choiceskill = s;
            }
        }
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
                if (wa != null) wa.attackRadius += upgradenumber;
                SkillSporeField sf = choiceskill as SkillSporeField;
                if (sf != null) sf.attackRadius += upgradenumber;
                break;
        }
        closechoice();
        battleUI.RefreshSkill();
    }
}
