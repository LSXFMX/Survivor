using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skillupgrade : Upgradeoptionsbase
{
    public override void chocieupgrade()
    {
        switch (skillAtr)
        {
            case skillAttribute.CDtime:
                skill.CDtime += (int)upgradenumber;
                break;
            case skillAttribute.damage:
                skill.damage += (int)upgradenumber;
                break;
            case skillAttribute.lifetime:
                skill.lifetime += upgradenumber;
                break;
            case skillAttribute.pass:
                skill.pass += (int)upgradenumber;
                break;
            case skillAttribute.speed:
                skill.speed += (int)upgradenumber;
                break;
            case skillAttribute.number:
                skill.number += (int)upgradenumber;
                break;
            case skillAttribute.size:
                skill.size += upgradenumber;
                break;
            case skillAttribute.interval:
                skill.interval += (int)upgradenumber;
                break;
        }
        closechoice();
    }
}
