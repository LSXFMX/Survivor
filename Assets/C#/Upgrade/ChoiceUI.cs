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

    public List<SkillUpgradeEntry> skillEntries;

    public Transform choice1;
    public Transform choice2;
    public Transform choice3;
    public GameObject c1;
    public GameObject c2;
    public GameObject c3;
    public Transform playerskill;

    public static ChoiceUI Instance { get; private set; }

    [Header("刷新次数")]
    public int maxRefreshCount = 0; // 默认0，由 remake 抽卡装备提供
    private int remainRefresh;
    public Button refreshButton;
    public TextMeshProUGUI refreshButtonText;
    private int baseRefreshCount;
    private bool refreshCountInited;
    private bool firstChoiceMustOfferLearnSkill = true;
    private int gateChallengeMaxUpgradeBonus = 0; // 门挑战本局加成，随本局重置

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
        return GetGroupCount(opt.upgradeGroup) >= GetEffectiveMaxUpgrades(opt);
    }

    public int GetEffectiveMaxUpgrades(Upgradeoptionsbase opt)
    {
        if (opt == null || opt.maxUpgrades <= 0) return opt != null ? opt.maxUpgrades : 0;
        return opt.maxUpgrades + gateChallengeMaxUpgradeBonus;
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
        if (refreshButtonText != null) refreshButtonText.text = "刷新：" + remainRefresh + "次";
    }

    void Awake()
    {
        Instance = this;
        baseRefreshCount = maxRefreshCount;
        SyncRefreshCountWithREquipment(resetRemain: true);
    }

    void OnEnable()
    {
        SyncRefreshCountWithREquipment(resetRemain: false);
        UpdateRefreshButton();
        refresh();
        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null) bui.RefreshSkill();
    }

    private void SyncRefreshCountWithREquipment(bool resetRemain)
    {
        int remakeCount = GachaManager.Instance != null ? GachaManager.Instance.GetItemCount(GachaRarity.R, 0) : 0;
        int targetMax = baseRefreshCount + Mathf.Max(0, remakeCount);
        int delta = targetMax - maxRefreshCount;
        maxRefreshCount = targetMax;

        if (resetRemain || !refreshCountInited)
        {
            remainRefresh = maxRefreshCount;
            refreshCountInited = true;
        }
        else if (delta > 0)
        {
            // 若运行中新增了可用次数（例如热更新存档），把增量补到剩余次数
            remainRefresh += delta;
        }

        if (remainRefresh > maxRefreshCount) remainRefresh = maxRefreshCount;
        if (remainRefresh < 0) remainRefresh = 0;
    }

    public void refresh()
    {
        list = new List<GameObject>();
        List<GameObject> learnSkillCandidates = new List<GameObject>();
        List<GameObject> nonLearnSkillCandidates = new List<GameObject>();

        foreach (var item in upplayer)
        {
            if (!IsGroupMaxed(item)) list.Add(item);
            if (!IsGroupMaxed(item)) nonLearnSkillCandidates.Add(item);
        }

        if (skillEntries != null)
        {
            foreach (var entry in skillEntries)
            {
                if (entry.learnSkillPrefab == null) continue;

                getnewskill learnOpt = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learnOpt == null) continue;

                // 加入卡池前检查：解锁条件 + 特殊难度门槛（如孢子领域需 N5+）
                if (!learnOpt.IsAvailableInPool()) continue;

                string skillName = learnOpt.skill != null ? learnOpt.skill.Skillname : "";
                bool alreadyHave = !string.IsNullOrEmpty(skillName) && PlayerHasSkill(skillName);

                if (alreadyHave)
                {
                    if (entry.upgradeOptions != null)
                        foreach (var upItem in entry.upgradeOptions)
                            if (!IsGroupMaxed(upItem))
                            {
                                list.Add(upItem);
                                nonLearnSkillCandidates.Add(upItem);
                            }
                }
                else
                {
                    if (!IsGroupMaxed(entry.learnSkillPrefab))
                    {
                        list.Add(entry.learnSkillPrefab);
                        learnSkillCandidates.Add(entry.learnSkillPrefab);
                    }
                }
            }
        }

        if (list.Count == 0)
        {
            battleUI bui = GameObject.Find("BattleUI").GetComponent<battleUI>();
            bui.choiceUI.SetActive(false);
            bui.ResumeTime();
            return;
        }

        if (firstChoiceMustOfferLearnSkill && learnSkillCandidates.Count > 0)
        {
            // 首次三选一：三个选项都来自“未学习新技能”池
            c1 = GetRandomFrom(learnSkillCandidates);
            c2 = GetRandomExcluding(learnSkillCandidates, c1);
            c3 = GetRandomExcluding(learnSkillCandidates, c1, c2);
            firstChoiceMustOfferLearnSkill = false;
        }
        else
        {
            c1 = getrandom();
            c2 = GetSecondOrThirdChoice(c1, nonLearnSkillCandidates);
            int safetyCount = 0;
            while (c1 == c2 && list.Count > 1)
            {
                c2 = GetSecondOrThirdChoice(c1, nonLearnSkillCandidates);
                if (++safetyCount > 100) break;
            }
            c3 = GetSecondOrThirdChoice(c2, nonLearnSkillCandidates);
            safetyCount = 0;
            while ((c1 == c3 || c2 == c3) && list.Count > 2)
            {
                c3 = GetSecondOrThirdChoice(c2, nonLearnSkillCandidates);
                if (++safetyCount > 100) break;
            }
        }

        refreshsignalchoice(choice1, c1);
        refreshsignalchoice(choice2, c2);
        refreshsignalchoice(choice3, c3);
    }

    private GameObject GetSecondOrThirdChoice(GameObject fallbackExclude, List<GameObject> nonLearnSkillCandidates)
    {
        // 首次三选一已保底学习技能后，其它位置优先给非学习技能，避免三张全是学习技能。
        if (!firstChoiceMustOfferLearnSkill &&
            nonLearnSkillCandidates != null &&
            nonLearnSkillCandidates.Count > 0)
        {
            return nonLearnSkillCandidates[Random.Range(0, nonLearnSkillCandidates.Count)];
        }
        return getrandom();
    }

    private GameObject GetRandomFrom(List<GameObject> source)
    {
        if (source == null || source.Count == 0) return getrandom();
        return source[Random.Range(0, source.Count)];
    }

    private GameObject GetRandomExcluding(List<GameObject> source, GameObject excludeA, GameObject excludeB = null)
    {
        if (source == null || source.Count == 0) return getrandom();

        List<GameObject> pool = new List<GameObject>();
        foreach (var obj in source)
        {
            if (obj == null) continue;
            if (obj == excludeA || obj == excludeB) continue;
            pool.Add(obj);
        }

        if (pool.Count > 0) return pool[Random.Range(0, pool.Count)];
        return source[Random.Range(0, source.Count)];
    }

    public void click_refresh()
    {
        if (remainRefresh <= 0) return;
        remainRefresh--;
        UpdateRefreshButton();

        // 消耗一个 remake（R_0）的叠加数量
        if (GachaManager.Instance != null)
        {
            string countKey = $"GachaCount_{GachaRarity.R}_0";
            int cur = PlayerPrefs.GetInt(countKey, 0);
            if (cur > 0)
            {
                PlayerPrefs.SetInt(countKey, cur - 1);
                PlayerPrefs.Save();
            }
        }

        refresh();
    }

    public void click_skip()
    {
        battleUI bui = GameObject.Find("BattleUI").GetComponent<battleUI>();
        bui.choiceUI.SetActive(false);
        bui.ResumeTime();
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

    /// <summary>门挑战奖励：本局所有升级组上限 +1（仅本局内存有效）</summary>
    public void IncreaseAllMaxUpgrades()
    {
        // 仅增加本局运行时加成，避免改动 prefab/资源导致“下把叠加”。
        gateChallengeMaxUpgradeBonus++;
    }

    public void click1() { c1.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click2() { c2.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click3() { c3.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
}
