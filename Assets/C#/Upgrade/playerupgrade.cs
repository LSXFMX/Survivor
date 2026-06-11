using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class playerupgrade : Upgradeoptionsbase
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
        switch (playerAtr)
        {
            case playerAttribute.healthmax:
                player.healthmax += (int)upgradenumber;
                player.health += (int)upgradenumber;
                break;
            case playerAttribute.atk:
                player.atk += (int)upgradenumber;
                break;
            case playerAttribute.def:
                player.def += (int)upgradenumber;
                break;
            case playerAttribute.speed:
                player.speed += (int)upgradenumber;
                break;
            case playerAttribute.CR:
                player.CR += (int)upgradenumber;
                if (player.CR > 100)
                {
                    player.CR = 100;
                }
                break;
            case playerAttribute.CD:
                player.CD += (int)upgradenumber;
                break;
            case playerAttribute.EVA:
                player.EVA += (int)upgradenumber;
                break;
            case playerAttribute.DR:
                player.DR += (int)upgradenumber;
                break;
        }
        closechoice();
    }
}
