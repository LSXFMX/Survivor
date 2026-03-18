using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skillhurricane : Skillbase
{
    public override IEnumerator Useskill()//使用技能
    {
        CDkey = 0;
        float perangle = 360 / number;//加角度
        float nowangle = 0;
        for (int i = 0; i < number; i++)
        {
            Vector3 spawnPosition = player.transform.position + new Vector3(0, size, 0);
            GameObject newbullet = Instantiate(bullet, spawnPosition, Quaternion.Euler(new Vector3(0, 0, angel)));//创建子弹
            Bulletbase n = newbullet.GetComponent<Bulletbase>();
            n.fatherskill = this;
            n.GetFather();
            n.getrole();
            n.GetComponent<Bullethurricane>().orientation = nowangle;
            nowangle += perangle;
            n.cango = true;
            yield return new WaitForSeconds(interval);
        }
    }
}
