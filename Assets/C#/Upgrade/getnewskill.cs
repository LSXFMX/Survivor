using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class getnewskill : Upgradeoptionsbase
{
    [Header("???????????????")]
    public bool   requireFavor      = false;
    public FactionType favorFaction = FactionType.Mushroom;
    public int    favorThreshold    = 10;

    /// <summary>???????????</summary>
    public bool IsFavorUnlocked()
    {
        if (!requireFavor) return true;
        if (FavorManager.Instance == null)
        {
            // FavorManager ???????? PlayerPrefs
            int val = UnityEngine.PlayerPrefs.GetInt("Favor_" + favorFaction.ToString(), 0);
            return val >= favorThreshold;
        }
        return FavorManager.Instance.GetFavor(favorFaction) >= favorThreshold;
    }

    public bool IsDifficultyUnlocked(int minN)
    {
        if (minN <= 1) return true;
        if (DifficultyManager.Instance == null) return false;

        string label = DifficultyManager.Instance.Current.label;
        if (!label.StartsWith("N")) return false;
        if (!int.TryParse(label.Substring(1), out int n)) return false;
        return n >= minN;
    }

    /// <summary>是否可加入升级卡池（同时满足解锁与难度条件）</summary>
    public bool IsAvailableInPool()
    {
        if (!IsFavorUnlocked()) return false;

        // 孢子领域：除了装备/好感度解锁外，还要求 N5 及以上难度。
        if (skill != null && skill.GetComponent<SkillSporeField>() != null)
            return IsDifficultyUnlocked(5);

        return true;
    }

    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
        Instantiate(skill.gameObject, player.SkillList);
        battleUI.RefreshSkill();
        closechoice();
    }
}
