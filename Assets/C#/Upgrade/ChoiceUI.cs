using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class SkillUpgradeEntry
{
    public GameObject learnSkillPrefab;
    public List<GameObject> upgradeOptions;
}

public class ChoiceUI : MonoBehaviour
{
    public List<GameObject> list;
    public List<GameObject> upplayer;

    [Tooltip("将每个可学习技能与其升级选项绑定")]
    public List<SkillUpgradeEntry> skillEntries;

    public Transform choice1;
    public Transform choice2;
    public Transform choice3;
    public GameObject c1;
    public GameObject c2;
    public GameObject c3;
    public Transform playerskill;

    public static ChoiceUI Instance { get; private set; }

    [Header("刷新设置")]
    public int maxRefreshCount = 1;
    private int remainRefresh;
    public Button refreshButton;
    public TextMeshProUGUI refreshButtonText; // 拖入刷新按钮上的 TextMeshProUGUI

    private Dictionary<string, int> upgradeGroupCount = new Dictionary<string, int>();

    public void RecordUpgrade(string group)
    {
        if (!upgradeGroupCount.ContainsKey(group))
            upgradeGroupCount[group] = 0;
        upgradeGroupCount[group]++;
    }

    public int GetGroupCount(string group)
    {
        return upgradeGroupCount.ContainsKey(group) ? upgradeGroupCount[group] : 0;
    }

    private bool IsGroupMaxed(GameObject upgradeObj)
    {
        Upgradeoptionsbase opt = upgradeObj.GetComponent<Upgradeoptionsbase>();
        if (opt == null || string.IsNullOrEmpty(opt.upgradeGroup) || opt.maxUpgrades <= 0)
            return false;
        return GetGroupCount(opt.upgradeGroup) >= opt.maxUpgrades;
    }

    private bool PlayerHasSkill(string skillName)
    {
        foreach (Transform t in playerskill)
        {
            Skillbase s = t.GetComponent<Skillbase>();
            if (s != null && s.Skillname == skillName)
                return true;
        }
        return false;
    }

    private void UpdateRefreshButton()
    {
        if (refreshButton != null) refreshButton.interactable = remainRefresh > 0;
        if (refreshButtonText != null) refreshButtonText.text = "刷新（" + remainRefresh + "）";
    }

    void Awake()
    {
        Instance = this;
        remainRefresh = maxRefreshCount; // 全局初始化一次，不随界面开关重置
        UpdateRefreshButton();
    }

    void OnEnable()
    {
        UpdateRefreshButton();

        refresh();

        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null) bui.RefreshSkill();
    }

    public void refresh()
    {
        list = new List<GameObject>();

        foreach (var item in upplayer)
            if (!IsGroupMaxed(item)) list.Add(item);

        if (skillEntries != null)
        {
            foreach (var entry in skillEntries)
            {
                if (entry.learnSkillPrefab == null) continue;

                getnewskill learnOpt = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learnOpt == null) continue;

                string skillName = learnOpt.skill != null ? learnOpt.skill.Skillname : "";
                bool alreadyHave = !string.IsNullOrEmpty(skillName) && PlayerHasSkill(skillName);

                if (alreadyHave)
                {
                    if (entry.upgradeOptions != null)
                        foreach (var upItem in entry.upgradeOptions)
                            if (!IsGroupMaxed(upItem)) list.Add(upItem);
                }
                else
                {
                    if (!IsGroupMaxed(entry.learnSkillPrefab))
                        list.Add(entry.learnSkillPrefab);
                }
            }
        }

        if (list.Count == 0)
        {
            Time.timeScale = 1.0f;
            battleUI bui = GameObject.Find("BattleUI").GetComponent<battleUI>();
            bui.choiceUI.SetActive(false);
            return;
        }

        c1 = getrandom();
        c2 = getrandom();
        int safetyCount = 0;
        while (c1 == c2 && list.Count > 1)
        {
            c2 = getrandom();
            if (++safetyCount > 100) break;
        }
        c3 = getrandom();
        safetyCount = 0;
        while ((c1 == c3 || c2 == c3) && list.Count > 2)
        {
            c3 = getrandom();
            if (++safetyCount > 100) break;
        }

        refreshsignalchoice(choice1, c1);
        refreshsignalchoice(choice2, c2);
        refreshsignalchoice(choice3, c3);
    }

    public void click_refresh()
    {
        if (remainRefresh <= 0) return;
        remainRefresh--;
        UpdateRefreshButton();
        refresh();
    }

    public void click_skip()
    {
        Time.timeScale = 1.0f;
        battleUI bui = GameObject.Find("BattleUI").GetComponent<battleUI>();
        bui.choiceUI.SetActive(false);
    }

    public GameObject getrandom()
    {
        return list[Random.Range(0, list.Count)];
    }

    public void refreshsignalchoice(Transform choice, GameObject c)
    {
        Upgradeoptionsbase upchoice = c.GetComponent<Upgradeoptionsbase>();
        choice.GetChild(0).GetComponent<TextMeshProUGUI>().text = upchoice.Upgradename;
        if (upchoice.icon)
        {
            choice.GetChild(2).gameObject.SetActive(true);
            choice.GetChild(2).GetComponent<Image>().sprite = upchoice.icon;
        }
        else
        {
            choice.GetChild(2).gameObject.SetActive(false);
        }
        choice.GetChild(1).GetComponent<TextMeshProUGUI>().text = upchoice.Upgradedescription;
    }

    public void click1() { c1.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click2() { c2.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click3() { c3.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
}
