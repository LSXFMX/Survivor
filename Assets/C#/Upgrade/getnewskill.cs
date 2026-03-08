using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class getnewskill : Upgradeoptionsbase
{
    public override void chocieupgrade()//¾ªÈËµÄ¼òµ¥
    {
        Instantiate(skill.gameObject, player.SkillList);
        closechoice();
    }
}
