using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Upgradeoptionsbase : MonoBehaviour
{
    public string Upgradename;//升级项名称
    public string Upgradedescription;//升级项描述
    public Upgradetype type;
    public float upgradenumber;
    public skillAttribute skillAtr;
    public playerAttribute playerAtr;
    public Player player;
    public Sprite icon;
    public Skillbase skill;
    public battleUI battleUI;
    public enum Upgradetype//升级项类型
    {
        upgradeplayer,
        upgradeskill,
        getnewskill,
    }

    public enum playerAttribute
    {
        healthmax,//血量上限
        atk,//攻击力
        def,//防御力
        speed,//移速 
        CR,//暴击率critical rate
        CD,//暴击伤害critical damage
        EVA,//闪避率
        DR, //掉宝率drop rate
    }

    public enum skillAttribute
    {
        CDtime,//CD时间,固定参数
        damage,//技能伤害
        lifetime,//生命周期
        pass,//穿透
        speed,//子弹速度
        number,//子弹数量
        size,//子弹大小
        interval,//多数量时的发射间隔时间
    }
    void OnEnable()
    {
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
        battleUI=GameObject.Find("BattleUI").GetComponent<battleUI>();  
    }

    public virtual void chocieupgrade()
    {

    }
    public void closechoice()
    {
        Time.timeScale = 1.0f;
        battleUI.choiceUI.SetActive(false);
    }
}
