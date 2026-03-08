using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skillbase : MonoBehaviour
{
    public string Skillname;//技能名
    public float CDtime;//CD时间,固定参数
    public float CDkey;//CD间隔，冷却键，每秒恢复的动态量，二者概念相同
    public int damage;//技能伤害
    public int level;//技能等级
    public float lifetime;//生命周期
    public int pass;//穿透
    public float speed;//子弹速度
    public int number;//子弹数量
    public GameObject bullet;//子弹物体
    public float size;//子弹大小
    public float interval;//间隔时间
    public GameObject player;
    public float angel;//旋转角度
    public bool isfaceenemy;//是否朝向最近敌人
    public Sprite icon; 
    void FixedUpdate()
    {
        CDkey += Time.fixedDeltaTime;
        if (CDkey > CDtime )
        {
            CDkey = CDtime;
        }
    }

    public virtual IEnumerator Useskill()//使用技能
    {
        CDkey = 0;
        for ( int i = 0; i < number; i++ )
        { 
            GameObject newbullet = Instantiate( bullet ,player.transform.position,Quaternion.Euler(new Vector3(0,0,angel)));//创建子弹
            Bulletbase n =newbullet.GetComponent<Bulletbase>();
            n.fatherskill = this;
            n.GetFather();
            n.getrole();
            n.cango = true;
            yield return new WaitForSeconds(interval);
        }
    }
}
