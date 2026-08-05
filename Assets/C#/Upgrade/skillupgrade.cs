using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class skillupgrade : Upgradeoptionsbase
{
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
        Skillbase choiceskill=null;
        foreach(Transform ski in player.SkillList)
        {
            Skillbase s = ski.GetComponent<Skillbase>();
            if (skill.Skillname==s.Skillname)
            {
                choiceskill = s;
            }
        }
        if (choiceskill == null) return;
        switch (skillAtr)
        {
            case skillAttribute.CDtime:
                choiceskill.CDtime += upgradenumber; // 减少CD填负数，增加CD填正数
                // 火球术 CD 下限 2.0s
                if (choiceskill.Skillname == "火球术")
                    choiceskill.CDtime = Mathf.Max(2f, choiceskill.CDtime);
                // 地狱火 CD 下限与火球术同步 2.0s
                if (choiceskill.Skillname == "地狱火")
                    choiceskill.CDtime = Mathf.Max(2f, choiceskill.CDtime);
                // 飓风 CD 下限 2.0s
                if (choiceskill.Skillname == "飓风")
                    choiceskill.CDtime = Mathf.Max(2f, choiceskill.CDtime);
                // 命途:寄生 CD 下限 2.5s（基础 3.0s，CD 升级卡 -0.1s×5 = -0.5s = 2.5s）
                if (choiceskill.Skillname == "命途:寄生")
                    choiceskill.CDtime = Mathf.Max(2.5f, choiceskill.CDtime);
                break;
            case skillAttribute.damage:
                choiceskill.damage += (int)upgradenumber;
                break;
            case skillAttribute.lifetime:
                choiceskill.lifetime += upgradenumber;
                break;
            case skillAttribute.pass:
                choiceskill.pass += (int)upgradenumber;
                break;
            case skillAttribute.speed:
                choiceskill.speed += upgradenumber;
                break;
            case skillAttribute.number:
                choiceskill.number += (int)upgradenumber;
                break;
            case skillAttribute.size:
                choiceskill.size += upgradenumber;
                break;
            case skillAttribute.interval:
                choiceskill.interval += upgradenumber;
                break;
            case skillAttribute.attackRadius:
                SkillWindArrow wa = choiceskill as SkillWindArrow;
                if (wa != null) wa.attackRadius += upgradenumber;
                SkillSporeField sf = choiceskill as SkillSporeField;
                if (sf != null)
                {
                    if (sf.IsLockedByTombDomain)
                    {
                        // 亡者领域锁定后：范围升级转换为等值伤害升级
                        choiceskill.damage += (int)upgradenumber;
                        Debug.Log($"[亡者领域] 孢子领域范围升级 → 伤害 +{(int)upgradenumber}");
                    }
                    else
                    {
                        sf.attackRadius += upgradenumber;
                    }
                }
                SkillBloodline bl = choiceskill as SkillBloodline;
                if (bl != null) bl.attackRadius += upgradenumber;
                SkillParasite sp = choiceskill as SkillParasite;
                if (sp != null) sp.attackRadius += upgradenumber;
                // 阴/阳史莱姆：attackRadius 定义在 SkillYinYangSlime 上，
                // 若不在这里加分支，「范围 +3」升级卡会被静默吞掉（选了没反应）。
                SkillYinYangSlime ys = choiceskill as SkillYinYangSlime;
                if (ys != null) ys.attackRadius += upgradenumber;
                break;
        }

        // 阴/阳史莱姆「升级卡共享」兜底。
        // 正常路径下史莱姆的升级卡走 SlimeSharedUpgrade（它自己就会同时升两支），
        // 不会进到这里。保留本调用是为了覆盖两种边缘情况：
        //   1. 将来有人在场景里手工配了一张普通 skillupgrade 卡指向阴或阳；
        //   2. 其它系统（奇遇/ 门挑战）复用 skillupgrade 改到了其中一支。
        // 两支都不存在或只有一支时本方法直接返回，无副作用。
        SyncYinYangSlimePair(player);

        // SSR9「三清化一」+ SSR6「影分身之术」联动：
        // 本体技能升级后，同步升级 SkillListClone 中同名技能（维持 SSR6 "实时同步" 语义）
        SyncUpgradeToCloneSkills(player, skill.Skillname, skillAtr, upgradenumber);

        closechoice();
        battleUI.RefreshSkill();
    }

    /// <summary>
    /// 把玩家身上的阴/阳史莱姆两支数值拉平（共享升级）。
    /// 逻辑与 TaijiSlimeWatcher.SyncSharedStats 一致：CDtime 取 Min，其余取 Max。
    /// 只有两支都存在时才需要同步；只有一支时什么都不做。
    /// </summary>
    private static void SyncYinYangSlimePair(Player p)
    {
        if (p == null || p.SkillList == null) return;

        SkillYinYangSlime yin = null, yang = null;
        foreach (Transform t in p.SkillList)
        {
            if (t == null) continue;
            var s = t.GetComponent<SkillYinYangSlime>();
            if (s == null) continue;
            if (s.isYin) { if (yin == null) yin = s; }
            else { if (yang == null) yang = s; }
        }
        if (yin == null || yang == null) return;

        yin.damage = yang.damage = Mathf.Max(yin.damage, yang.damage);
        yin.number = yang.number = Mathf.Max(yin.number, yang.number);
        yin.pass = yang.pass = Mathf.Max(yin.pass, yang.pass);
        yin.speed = yang.speed = Mathf.Max(yin.speed, yang.speed);
        yin.lifetime = yang.lifetime = Mathf.Max(yin.lifetime, yang.lifetime);
        yin.attackRadius = yang.attackRadius = Mathf.Max(yin.attackRadius, yang.attackRadius);

        float cdA = yin.CDtime > 0.01f ? yin.CDtime : float.MaxValue;
        float cdB = yang.CDtime > 0.01f ? yang.CDtime : float.MaxValue;
        float cd = Mathf.Min(cdA, cdB);
        if (cd < float.MaxValue) yin.CDtime = yang.CDtime = SlimeFactionAssets.ClampCD(cd);
    }

    /// <summary>
    /// SSR9「三清化一」联动：把本体技能升级同步到 SkillListClone 中同名技能。</summary>
    private void SyncUpgradeToCloneSkills(Player p, string skillName, skillAttribute attr, float value)
    {
        if (p == null || p.SkillListClone == null || p.SkillListClone.childCount == 0) return;

        // 检查 SSR6 是否解锁
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 8))
            return;

        // SSR6：升级增量按 30% 同步
        float scaledValue = value * 0.3f;

        foreach (Transform t in p.SkillListClone)
        {
            if (t == null) continue;
            Skillbase s = t.GetComponent<Skillbase>();
            if (s == null || s.Skillname != skillName) continue;

            switch (attr)
            {
                case skillAttribute.CDtime:    s.CDtime    += value; break; // CD与本体相同，不缩放
                case skillAttribute.damage:    s.damage    += Mathf.RoundToInt(scaledValue); break;
                case skillAttribute.lifetime:  s.lifetime  += scaledValue; break;
                case skillAttribute.pass:      s.pass      += Mathf.RoundToInt(scaledValue); break;
                case skillAttribute.speed:     s.speed     += scaledValue; break;
                case skillAttribute.number:    s.number    = Mathf.Max(1, s.number + Mathf.RoundToInt(scaledValue)); break;
                case skillAttribute.size:      s.size      += scaledValue; break;
                case skillAttribute.interval:  s.interval  += scaledValue; break;
                case skillAttribute.attackRadius:
                    SkillWindArrow cwa = s as SkillWindArrow;
                    if (cwa != null) cwa.attackRadius += scaledValue;
                    SkillSporeField csf = s as SkillSporeField;
                    if (csf != null)
                    {
                        if (csf.IsLockedByTombDomain)
                            s.damage += Mathf.RoundToInt(scaledValue);
                        else
                            csf.attackRadius += scaledValue;
                    }
                    SkillBloodline cbl = s as SkillBloodline;
                    if (cbl != null) cbl.attackRadius += scaledValue;
                    SkillParasite csp = s as SkillParasite;
                    if (csp != null) csp.attackRadius += scaledValue;
                    SkillYinYangSlime cys = s as SkillYinYangSlime;
                    if (cys != null) cys.attackRadius += scaledValue;
                    break;
            }
        }
    }
}
