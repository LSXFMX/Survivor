using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class battleUI : MonoBehaviour
{
    public GameObject SkillUI;
    public Transform Skillroom;
    public Transform Skilllist;
    public Player player;
    public Transform menu;
    public TextMeshProUGUI health;
    public TextMeshProUGUI level;
    public TextMeshProUGUI timeui;
    public Transform exp;
    public Image life;
    public int minute;
    public int second;
    public bool startcount;
    public float timer;
    public GameObject choiceUI;
    public TextMeshProUGUI yuanmuText;
    public AdventureUI adventureUI;

    void Start()
    {
        choiceUI.SetActive(false);
        RefreshSkill();
        starttime();
    }

    public void RefreshSkill()
    {
        if (Skillroom.childCount > 0)
        {
            foreach (Transform s in Skillroom)
                Destroy(s.gameObject);
        }
        foreach (Transform playerskill in Skilllist)
        {
            GameObject skill = Instantiate(SkillUI, Skillroom);
            Skillbase sb = playerskill.GetComponent<Skillbase>();
            skill.transform.GetChild(0).GetComponent<Image>().sprite = sb.icon;
            skill.transform.GetChild(1).GetComponent<Image>().sprite = sb.icon;
            UpdateSkillCountText(skill, sb);
        }
    }

    private void UpdateSkillCountText(GameObject skillUI, Skillbase sb)
    {
        TextMeshProUGUI countText = skillUI.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        if (countText == null || ChoiceUI.Instance == null) return;

        string group = "";
        int max = 0;
        if (ChoiceUI.Instance.skillEntries != null)
        {
            foreach (var entry in ChoiceUI.Instance.skillEntries)
            {
                if (entry.upgradeOptions == null || entry.upgradeOptions.Count == 0) continue;
                var first = entry.upgradeOptions[0].GetComponent<Upgradeoptionsbase>();
                if (first != null && !string.IsNullOrEmpty(first.upgradeGroup))
                {
                    var learn = entry.learnSkillPrefab?.GetComponent<getnewskill>();
                    if (learn != null && learn.skill != null && learn.skill.Skillname == sb.Skillname)
                    {
                        group = first.upgradeGroup;
                        max = first.maxUpgrades;
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(group) && max > 0)
            countText.text = ChoiceUI.Instance.GetGroupCount(group) + "/" + max;
        else
            countText.text = "";
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
        if (Skillroom.childCount > 0)
        {
            foreach (Transform skill in Skilllist)
            {
                Skillbase s = skill.GetComponent<Skillbase>();
                float ratio = s.CDkey / s.CDtime;
                Skillroom.transform.GetChild(index).GetChild(1).GetComponent<Image>().fillAmount = 1 - ratio;
                index++;
                if (index > Skillroom.childCount) index = 0;
            }
        }
        float healthratio = (float)player.health / (float)player.healthmax;
        life.fillAmount = healthratio;
        float expratio = (float)player.exp / (float)player.expmax;
        exp.localScale = new Vector3(expratio, 1, 1);
        level.text = "level:" + player.level;
        health.text = player.health + "/" + player.healthmax;
        if (startcount)
        {
            timer += Time.deltaTime;
            if (timer >= 1)
            {
                timer = 0;
                second--;
                if (second < 0)
                {
                    minute--;
                    second = 59;
                }
                if (minute <= 0 && second <= 0) timeover();
                timeui.text = minute + (second < 10 ? ":0" : ":") + second;
            }
        }
        if (YuanMuManager.Instance != null && yuanmuText != null)
            yuanmuText.text = ": " + YuanMuManager.Instance.Current;
    }

    public void openchoice()
    {
        Time.timeScale = 0;
        choiceUI.SetActive(true);
    }

    public void timeover() { }

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

    public void Click_settings() { }
    public void Click_instructions() { }
    public void Click_exitgame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
