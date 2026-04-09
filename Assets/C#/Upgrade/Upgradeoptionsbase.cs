using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Upgradeoptionsbase : MonoBehaviour
{
    public string Upgradename;//??????????
    public string Upgradedescription;//??????????
    public Upgradetype type;
    public float upgradenumber;
    public skillAttribute skillAtr;
    public playerAttribute playerAtr;
    public Player player;
    public Sprite icon;
    public Skillbase skill;
    public battleUI battleUI;

    [Header("升级上限设置")]
    public string upgradeGroup = "";//升级组标识，同组共享上限（如 "fireball"、"hurricane"）
    public int maxUpgrades = 0;//该组最大可选次数，0表示无限制
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
        CDtime,     // CD时间，固定攻击速度
        damage,     // 技能伤害
        lifetime,   // 子弹生命周期
        pass,       // 穿透
        speed,      // 子弹速度
        number,     // 子弹数量/多重数量
        size,       // 子弹大小
        interval,   // 连发时的发射间隔
        attackRadius, // 攻击范围（风箭等范围技能）
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
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();

        // 记录该升级组的选择次数，直接用单例避免层级查找失败
        if (!string.IsNullOrEmpty(upgradeGroup) && maxUpgrades > 0)
        {
            if (ChoiceUI.Instance != null)
                ChoiceUI.Instance.RecordUpgrade(upgradeGroup);
        }

        battleUI.choiceUI.SetActive(false);
        battleUI.ResumeTime();
    }
}
