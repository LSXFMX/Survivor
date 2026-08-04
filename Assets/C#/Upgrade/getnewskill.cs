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

        // === 2026-06 调整：删除孢子领域 / 血族血统的关卡难度限制 ===
        // 原本：
        //   - 孢子领域 (SkillSporeField) 还要 N5+ 难度才进卡池；
        //   - 血族血统 (SkillBloodline) 还要 N7+ 难度才进卡池。
        // 现在统一改为：只要好感度门槛达成（蘑菇 / 蝙蝠社群好感 ≥ 10，由 prefab 上 favorThreshold 控制），
        // 任何难度都能在升级卡池抽到。难度门槛被彻底解除。
        //
        // 如未来需要恢复某一项的难度限制，按以下模板加回即可：
        //   if (skill != null && skill.GetComponent<SkillSporeField>() != null)
        //       return IsDifficultyUnlocked(5);
        //   if (skill != null && skill.GetComponent<SkillBloodline>() != null)
        //       return IsDifficultyUnlocked(7);

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

        GameObject newSkillObj = Instantiate(skill.gameObject, player.SkillList);

        // 【2026-08 修复】若技能模板是"运行时构造的 inactive GameObject"（史莱姆社群的
        //   阴/阳史莱姆就是这样，见 SlimeFactionRegistrar.BuildSkillTemplate），
        //   Instantiate 出来的实例会继承 inactive 状态 → Start() 永不执行 →
        //   技能对象存在但完全不工作（不召唤鱼、不发射、图标也不刷新）。
        //   注意：这与传统 .prefab 资源不同——prefab 资源的 m_IsActive 通常为 true，
        //   所以历史上从未暴露这个问题。这里统一补一次激活，对已有技能无任何行为变化。
        if (!newSkillObj.activeSelf) newSkillObj.SetActive(true);

        // === 夏无专属：学习血族血统时立即应用加成（number→5, lifestealRatio→0.20）===
        // 无需等蝙蝠好感度 100 开局自带——只要夏无在任何时机通过三选一学到血族血统，
        // 就立即享受 UR 角色加成。ApplyXiaWuBloodlineBuff 内部会检查 CurrentSkinIndex。
        SkillBloodline newBloodline = newSkillObj.GetComponent<SkillBloodline>();
        if (newBloodline != null)
        {
            PlayerSkinSkillBuff.ApplyXiaWuBloodlineBuff(newBloodline);
        }

        // SSR9「三清化一」：新技能同步克隆到 SkillListClone
        //   SkillListClone 存在 → 三清化一已被触发过 → 本体后续学新技能，
        //   分身技能列表也自动补上该技能（属性继承按原逻辑：有 SSR6 时 30%，无 SSR6 时 100%）。
        if (player.SkillListClone != null)
        {
            GameObject cloneObj = Instantiate(skill.gameObject, player.SkillListClone);
            if (!cloneObj.activeSelf) cloneObj.SetActive(true); // 同上：inactive 模板需激活
            Skillbase cloneSb = cloneObj.GetComponent<Skillbase>();
            Skillbase ownerSb = newSkillObj.GetComponent<Skillbase>();
            if (cloneSb != null && ownerSb != null)
            {
                cloneSb.player = player.gameObject;
                // 分身技能固定继承 30% 数值（与有无 SSR6 无关）
                // 有 SSR6 时后续通过 SyncUpgradeToCloneSkills 实时同步；无 SSR6 时仅创建瞬间固定。
                cloneSb.CDtime   = ownerSb.CDtime;                        // CD 不缩放
                cloneSb.damage   = Mathf.RoundToInt(ownerSb.damage   * 0.3f);
                cloneSb.lifetime = Mathf.RoundToInt(ownerSb.lifetime * 0.3f);
                cloneSb.pass     = Mathf.RoundToInt(ownerSb.pass     * 0.3f);
                cloneSb.speed    = Mathf.RoundToInt(ownerSb.speed    * 0.3f);
                cloneSb.number   = Mathf.Max(1, Mathf.RoundToInt(ownerSb.number * 0.3f));
                cloneSb.bullet   = ownerSb.bullet;
                cloneSb.size     = ownerSb.size     * 0.9f;
                cloneSb.interval = ownerSb.interval * 0.3f;
                cloneSb.angel    = ownerSb.angel    * 0.3f;
            }
        }

        battleUI.RefreshSkill();
        closechoice();
    }
}
