using UnityEngine;

/// <summary>
/// 阴/阳史莱姆的**共享**升级卡。
///
/// 为什么必须单独做一个类，而不是直接复用 <see cref="skillupgrade"/>：
///
///基类 skillupgrade.chocieupgrade() 是靠 <c>skill.Skillname</c> 在玩家 SkillList 里
///   找目标的。而共享升级卡只能绑定其中一支（Skillname 只能是"阴史莱姆"或"阳史莱姆"），
///   于是会出现两个严重问题：
///
///① **软锁**：如果卡绑的是"阴史莱姆"，而玩家手上只有"阳史莱姆"，
///      基类会走到 <c>if (choiceskill == null) return;</c> —— 注意这一行**没有调用
///      closechoice()**，三选一面板不会关闭、Time.timeScale 仍是 0，
///      玩家直接卡死在升级界面，只能重开游戏。
///
///   ② **重复卡**：若给阴、阳各注册一套4 张升级卡，两套卡的显示文案完全相同
///      （都是"阴/阳史莱姆 · 伤害 +2"），而 ChoiceUI 的去重是按 GameObject 引用做的，
///      两个不同对象会被判定为"不重复"，于是同一屏里可能出现两张一模一样的卡。
///
/// 本类的解决方式：
///   • 只注册**一套** 4 张卡，被阴、阳两个 entry 共同引用
///     → 引用相同 → ChoiceUI 的 <c>while (c1 == c2)</c> 去重天然生效，不会出现重复卡；
///   • chocieupgrade() 自己遍历 SkillList，把升级**同时应用到实际存在的每一支**
///     （只有一支时就只升那一支）→ 不依赖 Skillname 匹配，彻底消除软锁；
///   • 无论如何都会调用 closechoice()，即使玩家一支都没有（理论上不会发生，
///     因为卡只在"已拥有"分支才进池）。
/// </summary>
public class SlimeSharedUpgrade : Upgradeoptionsbase
{
    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI")?.GetComponent<battleUI>();

        Player pl = ResolvePlayer();
        if (pl == null || pl.SkillList == null)
        {
            // 找不到玩家也必须关面板，否则 timeScale=0 卡死
            SafeClose();
            return;
        }

        // 收集玩家实际持有的阴/阳史莱姆（0、1 或 2 支）
        SkillYinYangSlime yin = null, yang = null;
        foreach (Transform t in pl.SkillList)
        {
            if (t == null) continue;
            var s = t.GetComponent<SkillYinYangSlime>();
            if (s == null) continue;
            if (s.isYin) { if (yin == null) yin = s; }
            else { if (yang == null) yang = s; }
        }

        ApplyTo(yin);
        ApplyTo(yang);

        // 两支都在→ 拉平数值，保证"共享"名副其实
        if (yin != null && yang != null) Level(yin, yang);

        // 分身（SSR9 三清化一）同步：按 30% 缩放，与skillupgrade 的语义一致
        SyncToClone(pl);

        SafeClose();
        battleUI?.RefreshSkill();
    }

    private void ApplyTo(SkillYinYangSlime s)
    {
        if (s == null) return;

        switch (skillAtr)
        {
            case skillAttribute.CDtime:
                // 冷却下限 1.5s：低于此值合体/分解演出会来不及播完
                // （TaijiSlimeController 的 AnimScale 已按 CD 压缩，但仍需一个硬下限）
                s.CDtime = Mathf.Max(1.5f, s.CDtime + upgradenumber);
                break;
            case skillAttribute.damage:
                s.damage += (int)upgradenumber;
                break;
            case skillAttribute.number:
                s.number = Mathf.Max(1, s.number + (int)upgradenumber);
                break;
            case skillAttribute.attackRadius:
                s.attackRadius += upgradenumber;
                break;
            case skillAttribute.lifetime:
                s.lifetime += upgradenumber;
                break;
            case skillAttribute.pass:
                s.pass += (int)upgradenumber;
                break;
            case skillAttribute.speed:
                s.speed += upgradenumber;
                break;
            case skillAttribute.size:
                s.size += upgradenumber;
                break;
            case skillAttribute.interval:
                s.interval += upgradenumber;
                break;
        }
    }

    /// <summary>把两支拉平（CDtime 取 Min，其余取 Max）。</summary>
    private static void Level(SkillYinYangSlime a, SkillYinYangSlime b)
    {
        a.damage = b.damage = Mathf.Max(a.damage, b.damage);
        a.number = b.number = Mathf.Max(a.number, b.number);
        a.pass = b.pass = Mathf.Max(a.pass, b.pass);
        a.speed = b.speed = Mathf.Max(a.speed, b.speed);
        a.lifetime = b.lifetime = Mathf.Max(a.lifetime, b.lifetime);
        a.attackRadius = b.attackRadius = Mathf.Max(a.attackRadius, b.attackRadius);

        float ca = a.CDtime > 0.01f ? a.CDtime : float.MaxValue;
        float cb = b.CDtime > 0.01f ? b.CDtime : float.MaxValue;
        float cd = Mathf.Min(ca, cb);
        if (cd < float.MaxValue) a.CDtime = b.CDtime = cd;
    }

    /// <summary>SSR9「三清化一」+ SSR6：把升级按 30% 同步到分身技能列表。</summary>
    private void SyncToClone(Player p)
    {
        if (p == null || p.SkillListClone == null || p.SkillListClone.childCount == 0) return;
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 8))
            return;

        float scaled = upgradenumber * 0.3f;
        foreach (Transform t in p.SkillListClone)
        {
            if (t == null) continue;
            var s = t.GetComponent<SkillYinYangSlime>();
            if (s == null) continue;

            switch (skillAtr)
            {
                case skillAttribute.CDtime: s.CDtime = Mathf.Max(1.5f, s.CDtime + upgradenumber); break;
                case skillAttribute.damage: s.damage += Mathf.RoundToInt(scaled); break;
                case skillAttribute.number: s.number = Mathf.Max(1, s.number + Mathf.RoundToInt(scaled)); break;
                case skillAttribute.attackRadius: s.attackRadius += scaled; break;
                case skillAttribute.lifetime: s.lifetime += scaled; break;
                case skillAttribute.pass: s.pass += Mathf.RoundToInt(scaled); break;
                case skillAttribute.speed: s.speed += scaled; break;
                case skillAttribute.size: s.size += scaled; break;
                case skillAttribute.interval: s.interval += scaled; break;
            }
        }
    }

    private static Player ResolvePlayer()
    {
        var layer = GameObject.Find("playerlayer")?.transform;
        if (layer == null) return Object.FindObjectOfType<Player>();
        foreach (Transform t in layer)
        {
            if (t != null && t.CompareTag("Player"))
            {
                var p = t.GetComponent<Player>();
                if (p != null) return p;
            }
        }
        return layer.childCount > 0 ? layer.GetChild(0).GetComponent<Player>() : null;
    }

    /// <summary>
    /// 关闭三选一。closechoice() 内部会做 GameObject.Find("playerlayer") 并直接
    /// GetChild(0)（无判空），极端情况下可能抛异常并把面板留在打开状态；
    /// 这里 try 兜底，异常时手动收尾，保证绝不卡死玩家。
    /// </summary>
    private void SafeClose()
    {
        try
        {
            closechoice();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SlimeFaction] closechoice 异常，走兜底收尾: {ex.Message}");
            if (battleUI != null && battleUI.choiceUI != null)
                battleUI.choiceUI.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}
