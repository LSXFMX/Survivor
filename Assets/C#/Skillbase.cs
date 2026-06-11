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
        // 发射音效：火球/冰类播放专属音，其他技能默认不播放
        PlayCastSfx();
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

    /// <summary>子弹命中时调用的命中音效；按技能名派发，子类可 override 自定义。</summary>
    public virtual void PlayHitSfx()
    {
        if (string.IsNullOrEmpty(Skillname)) return;
        if (Skillname.Contains("火球") || Skillname.Contains("地狱火"))
            AudioManager.PlaySfx(AudioManager.SfxKey.FireballHit);
        else if (Skillname.Contains("冰"))
            AudioManager.PlaySfx(AudioManager.SfxKey.IceHit);
        // 其他技能让 Bulletbase 已经触发的通用 Hit 覆盖
    }

    /// <summary>发射音效；按技能名派发，子类可 override 自定义。</summary>
    protected virtual void PlayCastSfx()
    {
        if (string.IsNullOrEmpty(Skillname)) return;
        if (Skillname.Contains("火球") || Skillname.Contains("地狱火"))
            AudioManager.PlaySfx(AudioManager.SfxKey.FireballCast);
    }
}
