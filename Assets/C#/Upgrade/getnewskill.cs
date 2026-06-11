using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class getnewskill : Upgradeoptionsbase
{
    [Header("好感度解锁条件")]
    public bool   requireFavor      = false;
    public FactionType favorFaction = FactionType.Mushroom;
    public int    favorThreshold    = 10;

    /// <summary>是否满足好感度解锁条件</summary>
    public bool IsFavorUnlocked()
    {
        if (!requireFavor) return true;
        if (FavorManager.Instance == null)
        {
            // FavorManager 不存在时回退读取 PlayerPrefs
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
    public virtual bool IsAvailableInPool()
    {
        if (!IsFavorUnlocked()) return false;

        // 孢子领域：除了装备/好感度解锁外，还要求 N5 及以上难度。
        if (skill != null && skill.GetComponent<SkillSporeField>() != null)
            return IsDifficultyUnlocked(5);

        // 血族血统（蝙蝠好感）：要求 N7+（与世界蝙蝠 Boss 脉络一致）。
        if (skill != null && skill.GetComponent<SkillBloodline>() != null)
            return IsDifficultyUnlocked(7);

        return true;
    }

    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        player = null;
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer != null)
        {
            foreach (Transform t in playerLayer)
            {
                if (t != null && t.CompareTag("Player"))
                {
                    player = t.GetComponent<Player>();
                    break;
                }
            }
            if (player == null && playerLayer.childCount > 0)
                player = playerLayer.GetChild(0).GetComponent<Player>();
        }
        if (player == null) return;

        // 防重保险：如果玩家已持有同名技能（比如 UR 角色开局自带，或来自其它途径），
        // 不再重复 Instantiate，避免出现两个同名技能。直接关闭面板，让玩家以为"学到了"，
        // 视觉/流程上不打断。理想情况下 ChoiceUI.refresh() 已经把这种学习卡过滤掉了，
        // 这里只是兜底。
        if (skill != null && !string.IsNullOrEmpty(skill.Skillname) && player.SkillList != null)
        {
            foreach (Transform t in player.SkillList)
            {
                Skillbase existing = t != null ? t.GetComponent<Skillbase>() : null;
                if (existing != null && existing.Skillname == skill.Skillname)
                {
                    Debug.LogWarning($"[getnewskill] 玩家已持有「{skill.Skillname}」，本次学习被忽略以防重复。");
                    closechoice();
                    return;
                }
            }
        }

        Instantiate(skill.gameObject, player.SkillList);
        battleUI.RefreshSkill();
        closechoice();
    }
}
