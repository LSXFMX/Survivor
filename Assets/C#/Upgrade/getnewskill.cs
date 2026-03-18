using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class getnewskill : Upgradeoptionsbase
{
    public override void chocieupgrade()//¾ªÈËµÄ¼òµ¥
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
        Instantiate(skill.gameObject, player.SkillList);
        battleUI.RefreshSkill();
        closechoice();
    }
}
