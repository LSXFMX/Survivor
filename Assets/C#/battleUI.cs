using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class battleUI : MonoBehaviour
{
    public GameObject SkillUI;
    public Transform Skillroom;//技能容器
    public Transform Skilllist;
    public Player player;
    public Transform menu;
    public TextMeshProUGUI health;//数字
    public TextMeshProUGUI level;
    public TextMeshProUGUI timeui;
    public Transform exp;
    public Image life;//血条
    public int minute;
    public int second;
    public bool startcount;//允许计时
    public float timer;
    public GameObject choiceUI;
    void Start()
    {
        RefreshSkill();
        starttime();
    }
    public void RefreshSkill()
    {
        if(Skillroom.childCount>0)
        {
            foreach(Transform s in Skillroom)
            {
                Destroy(s.gameObject);
            }
        }
        foreach (Transform playerskill in Skilllist)
        {
            GameObject skill = Instantiate(SkillUI, Skillroom);

            skill.transform.GetChild(0).GetComponent<Image>().sprite = playerskill.GetComponent<Skillbase>().icon;
            skill.transform.GetChild(1).GetComponent<Image>().sprite = playerskill.GetComponent<Skillbase>().icon;
        }
    }
    public void starttime()
    {
        minute = 10;
        second = 0;
        timer = 0;
        startcount = true;
    }
    void Update()
    {
        int index = 0;
        if(Skillroom.childCount>0 )
        {
            foreach (Transform skill in Skilllist)
            {
                Skillbase s = skill.GetComponent<Skillbase>();
                float ratio = s.CDkey / s.CDtime;//当前冷却进度比例
                Skillroom.transform.GetChild(index).GetChild(1).GetComponent<Image>().fillAmount = 1-ratio;//调整UI填充条
                index++;
                if(index>Skillroom.childCount)
                {
                    index = 0;
                }
            }
        }
        float healthratio = (float)player.health / (float)player.healthmax; 
        life.fillAmount = healthratio;//生命值同步血条
        float expratio=(float)player.exp / (float)player.expmax;
        exp.localScale = new Vector3(expratio,1,1);
        level.text="level:"+player.level;
        health.text = player.health + "/" + player.healthmax;
        if(startcount)
        {
            timer += Time.fixedDeltaTime;
            if(timer >= 1 ) 
            {
                timer = 0;
                second--;
                if(second < 0)
                {
                    minute--;
                    second = 59;
                }

                if(minute <= 0 && second <= 0)//倒计时完毕
                {
                    timeover();
                }    
                if(second<10)
                {
                    timeui.text = minute + ":0" + second;
                }
                else
                {
                    timeui.text = minute + ":" + second;
                }
            }
        }
    }

    public void openchoice()//创建三选一ui
    {
        Time.timeScale = 0;//暂停
        choiceUI.SetActive(true);
    }
    public void timeover()
    {

    }
    public void Click_menu()
    {
        menu.gameObject.SetActive(true);
        Time.timeScale = 0;
    }
    public void Click_continue()
    {
        menu.gameObject.SetActive(false);
        Time.timeScale = 1;
    }
    public void Click_settings()
    {

    }
    public void Click_instructions()
    {

    }
    public void Click_exitgame()
    {

    }
}
