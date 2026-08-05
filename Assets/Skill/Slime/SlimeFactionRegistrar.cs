using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 史莱姆社群运行时注册器。
///
/// 沿用狼人社群（EquipmentInitializer.EnsureWolfFactionRegistered）验证过的思路：
/// **全部走代码构建，不依赖任何 .prefab 资源与场景配置**。
/// 好处是打包后不会因为 prefab 引用丢失 / 场景漏配而让整个社群静默失效——
/// 这类问题在本项目历史上出现过多次，排查成本极高。
///
/// 负责：
///   1. 构造「阴史莱姆」「阳史莱姆」两个技能模板（inactive，作为 Instantiate蓝本）；
///   2. 向 ChoiceUI 注册 2 张独立学习卡 + 4 张共享升级卡（写作"阴/阳史莱姆"）；
///   3. 好感度 100 时开局赠予两个技能 + 生成太极图宠物；
///   4. 确保玩家身上挂有 TaijiSlimeWatcher（合体检测 + 共享升级同步）。
///
/// 由 EquipmentInitializer.Start 末尾调用。
/// </summary>
public static class SlimeFactionRegistrar
{
    private const string GROUP_LEARN_YIN  = "slime_yin_learn";
    private const string GROUP_LEARN_YANG = "slime_yang_learn";

    /// <summary>升级卡的组前缀。共享升级 → 阴阳共用同一组上限。</summary>
    private const string GROUP_UP_PREFIX = "slime_yinyang_";

    private static GameObject _yinTemplate;
    private static GameObject _yangTemplate;

    /// <summary>技能模板宿主（挂在 EquipmentInitializer 下，随场景销毁）。</summary>
    private static Transform _host;

    public static void Register(MonoBehaviour host)
    {
        if (host == null) return;
        try
        {
            RegisterCore(host);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SlimeFaction] 注册异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private static void RegisterCore(MonoBehaviour host)
    {
        _host = host.transform;

        // 场景重载后静态模板会变成 fake-null / 指向已销毁对象，必须重建
        if (_yinTemplate == null)_yinTemplate  = BuildSkillTemplate(isYin: true);
        if (_yangTemplate == null) _yangTemplate = BuildSkillTemplate(isYin: false);

        var choiceUI = ChoiceUI.Instance ?? Object.FindObjectOfType<ChoiceUI>(true);
        if (choiceUI == null)
        {
            Debug.LogWarning("[SlimeFaction] ChoiceUI 未找到，本局阴/阳史莱姆升级卡未注册");
            return;
        }

        if (choiceUI.skillEntries == null)
            choiceUI.skillEntries = new List<SkillUpgradeEntry>();

        // 清理上一局残留的同名 entry（场景重载时 ChoiceUI 可能被复用）
        PurgeOldEntries(choiceUI);

        // ── 升级卡：全社群只建**一套** 4 张，阴/阳两个 entry 共用同一批引用 ──
        // 这是"升级卡共享"的关键实现：
        //   • 引用相同 → ChoiceUI 的 `while (c1 == c2)` 引用去重天然生效，
        //     同一屏绝不会出现两张文案一样的"阴/阳史莱姆 · 伤害 +2"；
        //   • SlimeSharedUpgrade 自己遍历 SkillList 应用到实际存在的每一支，
        //     不依赖 Skillname 匹配，因此"只有阳、没有阴"时也能正常升级（不会软锁）。
        var sharedUpgrades = BuildSharedUpgrades();

        RegisterOne(choiceUI, _yinTemplate,  isYin: true,
                    skillName: SlimeFactionAssets.SKILL_YIN,
                    learnGroup: GROUP_LEARN_YIN,
                    favorThreshold: SlimeFactionAssets.FAVOR_YIN,
                    sharedUpgrades: sharedUpgrades);

        RegisterOne(choiceUI, _yangTemplate, isYin: false,
                    skillName: SlimeFactionAssets.SKILL_YANG,
                    learnGroup: GROUP_LEARN_YANG,
                    favorThreshold: SlimeFactionAssets.FAVOR_YANG,
                    sharedUpgrades: sharedUpgrades);

        Debug.Log($"[SlimeFaction] 注册完成。好感度={SlimeFactionAssets.CurrentFavor()}，" +
                  $"本次共享升级卡 {sharedUpgrades.Count}/4 张，" +
                  $"skillEntries 总数={choiceUI.skillEntries.Count}，" +
                  $"HasUsableEntries={HasUsableEntries(choiceUI)}");

        if (sharedUpgrades.Count == 0)
            Debug.LogError("[SlimeFaction] 共享升级卡全部构建失败！玩家将刷不到任何" +
                           "「阴/阳史莱姆」升级卡，请检查上方的构建异常日志。");
    }

    /// <summary>
    /// 自愈入口：若 ChoiceUI 里没有可用的史莱姆 entry，则重新注册一次。
    ///
    /// 存在意义：注册发生在 EquipmentInitializer.Start 这一个时间点，
    /// 只要那一刻出现任何意外（素材加载异常、ChoiceUI 尚未就绪、
    /// 多个 EquipmentInitializer 互相 Purge 等），玩家整局就再也刷不到升级卡，
    /// 且现象极具迷惑性——技能明明在，卡池却空。
    /// 由 TaijiSlimeWatcher 低频轮询调用，把"一次性时序"变成"最终一致"。
    /// </summary>
    public static void EnsureRegistered(MonoBehaviour host)
    {
        var choiceUI = ChoiceUI.Instance ?? Object.FindObjectOfType<ChoiceUI>(true);
        if (choiceUI == null) return;
        if (HasUsableEntries(choiceUI)) return;

        Debug.LogWarning("[SlimeFaction] 检测到 ChoiceUI 中没有可用的阴/阳史莱姆升级卡 entry，" +
                         "正在重新注册（自愈）……");
        Register(host);
    }

    /// <summary>
    /// 构建共享升级卡（规格：升级内容有范围、伤害、冷却、数量）。
    /// 图标用阴阳合体的太极图，呼应"这是一套共享成长"的语义。
    ///
    /// 每张卡单独 try/catch：一张构建失败也不该拖垮其余三张与整个 entry 注册。
    /// </summary>
    private static List<GameObject> BuildSharedUpgrades()
    {
        var list = new List<GameObject>(4);
        TryAdd(list, Upgradeoptionsbase.skillAttribute.damage,       2f,   "伤害 +2");
        TryAdd(list, Upgradeoptionsbase.skillAttribute.CDtime,      -0.3f, "冷却 -0.3s");
        TryAdd(list, Upgradeoptionsbase.skillAttribute.number,1f,   "数量 +1（射弹 / 太极印）");
        TryAdd(list, Upgradeoptionsbase.skillAttribute.attackRadius, 3f,   "范围 +3");
        return list;
    }

    private static void TryAdd(List<GameObject> list,
                               Upgradeoptionsbase.skillAttribute attr, float value, string desc)
    {
        try
        {
            GameObject go = BuildUpgrade(attr, value, desc);
            if (go != null) list.Add(go);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[SlimeFaction] 升级卡 {attr} 构建失败，已跳过：" +
                           $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理上一局/上一次注册残留的**史莱姆** entry。
    ///
    /// 【2026-08 修正】原实现会把所有 <c>learnSkillPrefab == null</c> 的 entry 一律删掉，
    /// 那是越权的—— 场景里其他系统的 entry 若恰好没配 learnSkillPrefab 会被误删。
    /// 现在只删两类：① 整条为null 的空洞；② 确认属于阴/阳史莱姆的旧 entry。
    /// </summary>
    private static void PurgeOldEntries(ChoiceUI choiceUI)
    {
        if (choiceUI.skillEntries == null) return;
        for (int i = choiceUI.skillEntries.Count - 1; i >= 0; i--)
        {
            var e = choiceUI.skillEntries[i];
            if (e == null) { choiceUI.skillEntries.RemoveAt(i); continue; }
            if (e.learnSkillPrefab == null) continue;   // 不是我们的，别动

            var lrn = e.learnSkillPrefab.GetComponent<getnewskill>();
            if (lrn != null && lrn.skill != null &&
                (lrn.skill.Skillname == SlimeFactionAssets.SKILL_YIN ||
                 lrn.skill.Skillname == SlimeFactionAssets.SKILL_YANG))
            {
                choiceUI.skillEntries.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// ChoiceUI 里当前是否已存在**可用的**史莱姆 entry
    /// （至少一条阴/阳 entry，且带有非空的升级卡列表）。
    /// 供 <see cref="TaijiSlimeWatcher"/> 做自愈检测。
    /// </summary>
    public static bool HasUsableEntries(ChoiceUI choiceUI)
    {
        if (choiceUI == null || choiceUI.skillEntries == null) return false;

        foreach (var e in choiceUI.skillEntries)
        {
            if (e == null || e.learnSkillPrefab == null) continue;
            var lrn = e.learnSkillPrefab.GetComponent<getnewskill>();
            if (lrn == null || lrn.skill == null) continue;
            if (lrn.skill.Skillname != SlimeFactionAssets.SKILL_YIN &&
                lrn.skill.Skillname != SlimeFactionAssets.SKILL_YANG) continue;

            if (e.upgradeOptions == null) continue;
            foreach (var up in e.upgradeOptions)
                if (up != null) return true;   // 找到一条有效的就够了
        }
        return false;
    }

    /// <summary>
    /// 构造技能模板。作为 Instantiate 蓝本，保持 inactive
    /// （否则模板自己也会跑 Start / 召唤一条鱼出来）。
    /// </summary>
    private static GameObject BuildSkillTemplate(bool isYin)
    {
        GameObject go = new GameObject(isYin ? "SkillTemplate_YinSlime" : "SkillTemplate_YangSlime");
        if (_host != null) go.transform.SetParent(_host, false);
        go.SetActive(false);

        var s = go.AddComponent<SkillYinYangSlime>();
        s.isYin = isYin;
        s.Skillname = isYin ? SlimeFactionAssets.SKILL_YIN : SlimeFactionAssets.SKILL_YANG;

        // ── 初始数值（规格：冷却 5s，射弹伤害较低但数量很多，初始约 6 发）──
        s.CDtime = 5f;
        s.CDkey = 0f;
        s.damage = 4;          // 低伤害；靠数量与齐射频率堆总输出
        s.number = 6;          // 初始 6 发
        s.speed = 12f;
        s.lifetime = 2.2f;
        s.pass = 0;
        s.size = 1f;
        s.interval = 0f;       // 基类连发间隔不用；逐发节奏由 shotInterval 控制
        s.shotInterval = 0.06f;// 逐发间隔，避免"一帧炸出一圈"的网状乱射
        s.level = 1;
        s.attackRadius = 9f;
        s.icon = SlimeFactionAssets.IconOf(isYin);

        return go;
    }

    private static void RegisterOne(ChoiceUI choiceUI, GameObject template, bool isYin,
                                   string skillName, string learnGroup, int favorThreshold,
                                   List<GameObject> sharedUpgrades)
    {
        if (template == null) return;
        var skillbase = template.GetComponent<SkillYinYangSlime>();
        if (skillbase == null) return;

        // ── 学习卡（阴/阳各自独立，规格明确要求不共享）──
        GameObject learnGo = new GameObject("LearnCard_" + skillName);
        if (_host != null) learnGo.transform.SetParent(_host, false);
        learnGo.SetActive(false);

        var learn = learnGo.AddComponent<getnewskill>();
        learn.Upgradename = skillName;
        // 注意文案里刻意不用"蝌蚪"二字：项目用的 heiti SDF 是**静态图集**，
        // 生僻字「蚪」未被收录 → 运行时渲染成方框□（玩家反馈的"中文乱码"）。
        // 这里统一改用常用字「灵弹」，语义不变且任何静态图集都能显示。
        learn.Upgradedescription = isYin
            ? "召唤太极阴鱼，向周围敌人发射大量黑色能量灵弹"
            : "召唤太极阳鱼，向周围敌人发射大量白色能量灵弹";
        learn.type = Upgradeoptionsbase.Upgradetype.getnewskill;
        learn.skill = skillbase;
        learn.icon = SlimeFactionAssets.IconOf(isYin);
        learn.upgradeGroup = learnGroup;
        learn.maxUpgrades = 1;
        learn.requireFavor = true;
        learn.favorFaction = FactionType.Slime;
        learn.favorThreshold = favorThreshold;
        learnGo.SetActive(true);

        // 升级卡列表用**同一批引用**（不是拷贝对象），见 BuildSharedUpgrades 注释。
        // 注意这里必须 new 一个 List 包装：SkillUpgradeEntry.upgradeOptions 是
        // ChoiceUI 直接持有的引用，若两个 entry 共用同一个 List 实例，
        // 将来任何一方对列表做增删都会意外影响另一方。
        var entry = new SkillUpgradeEntry
        {
            learnSkillPrefab = learnGo,
            upgradeOptions = new List<GameObject>(sharedUpgrades)
        };
        choiceUI.skillEntries.Add(entry);
    }

    private static GameObject BuildUpgrade(Upgradeoptionsbase.skillAttribute attr,
                                          float value, string desc)
    {
        GameObject go = new GameObject($"Upg_SlimeShared_{attr}");
        if (_host != null) go.transform.SetParent(_host, false);
        go.SetActive(false);

        // 用 SlimeSharedUpgrade 而不是 skillupgrade：后者按Skillname 匹配单支，
        // 在"只持有另一支"时会 return 且不关面板，导致三选一卡死（详见该类注释）。
        var up = go.AddComponent<SlimeSharedUpgrade>();
        up.Upgradename = SlimeFactionAssets.SKILL_SHARED_DISPLAY;
        up.Upgradedescription = desc;
        up.type = Upgradeoptionsbase.Upgradetype.upgradeskill;
        // skill 仅用于 ChoiceUI 的过滤判断（如 IsCooldownReductionUseless）与图标展示，
        // 实际生效目标由 SlimeSharedUpgrade 自己遍历 SkillList 决定。
        up.skill = _yinTemplate != null ? _yinTemplate.GetComponent<SkillYinYangSlime>() : null;
        up.skillAtr = attr;
        up.upgradenumber = value;
        up.upgradeGroup = GROUP_UP_PREFIX + attr; // 不含阴/阳 → 共享上限
        up.maxUpgrades = 5;

        // 图标放到**最后**赋值，且不让它影响卡片本身的可用性。
        // 这张图是首次加载（学习卡用的是 IconOf(isYin)，与此不同），
        // 抠图过程较重；即使拿不到也只是"卡片没图标"，绝不能影响卡片进池。
        up.icon = SlimeFactionAssets.IconTaiji;

        go.SetActive(true);
        return go;
    }

    // ══════════════════════════════════════════════════════════════
    //  好感度装备应用
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// 好感度装备 9「阴史莱姆」/ 10「阳史莱姆」：
    /// 达到门槛只解锁**学习资格**（由学习卡的 favorThreshold 把关），不开局赠予。
    /// 与孢子之心 / 血族血统 / 月牙吊坠 完全一致的套路。
    /// 真正的"开局自带"只在好感度 100（装备 11）时发生。
    /// </summary>
    public static void ApplyEquip9And10()
    {
        int favor = SlimeFactionAssets.CurrentFavor();
        if (favor >= SlimeFactionAssets.FAVOR_YIN)
            ToastManager.Show("[好感度装备] 阴史莱姆：已可在升级三选一中学习");
        if (favor >= SlimeFactionAssets.FAVOR_YANG)
            ToastManager.Show("[好感度装备] 阳史莱姆：已可在升级三选一中学习");
    }

    /// <summary>
    /// 好感度装备 11「太极两仪」（好感度 100）：
    ///   1. 开局直接赠予「阴史莱姆」+「阳史莱姆」两个技能
    ///      → 二者同时在场，TaijiSlimeWatcher 会立即合体为太极史莱姆
    ///      （即规格的"初始拥有太极史莱姆（阴史莱姆和阳史莱姆两个学习卡）"）；
    ///   2. 生成太极图宠物。
    /// </summary>
    public static void ApplyEquip11_TaijiLiangYi(Player player, Transform skillList)
    {
        if (player == null || skillList == null) return;
        if (SlimeFactionAssets.CurrentFavor() < SlimeFactionAssets.FAVOR_TAIJI) return;

        GrantSkillIfMissing(skillList, player, isYin: true);
        GrantSkillIfMissing(skillList, player, isYin: false);

        SpawnPet(player);

        ToastManager.Show("<color=#9BE8FF>[装备觉醒] 太极两仪：阴阳双生已在开局降临！</color>");
    }

    /// <summary>赠予技能。已存在同极性技能时跳过，避免重复。</summary>
    public static void GrantSkillIfMissing(Transform skillList, Player player, bool isYin)
    {
        if (skillList == null) return;

        GameObject template = isYin ? _yinTemplate : _yangTemplate;
        if (template == null)
        {
            // 注册器还没跑过（理论上不会，但保险起见就地补一份模板）
            template = BuildSkillTemplate(isYin);
            if (isYin) _yinTemplate = template; else _yangTemplate = template;
        }

        foreach (Transform t in skillList)
        {
            if (t == null) continue;
            var s = t.GetComponent<SkillYinYangSlime>();
            if (s != null && s.isYin == isYin) return; // 已有
        }

        GameObject go = Object.Instantiate(template, skillList);
        go.SetActive(true);
        var sb = go.GetComponent<Skillbase>();
        if (sb != null && player != null) sb.player = player.gameObject;
    }

    /// <summary>生成太极图宠物（全局唯一）。</summary>
    private static void SpawnPet(Player player)
    {
        if (player == null) return;
        if (Object.FindObjectOfType<TaijiTuPet>() != null) return;

        GameObject petObj = new GameObject("TaijiTuPet");
        petObj.transform.position = player.transform.position + new Vector3(1.5f, 0.45f, -0.3f);

        var sprGo = new GameObject("PetSprite");
        sprGo.transform.SetParent(petObj.transform, false);
        var sr = sprGo.AddComponent<SpriteRenderer>();
        sr.sprite = SlimeFactionAssets.PetSprite;
        sr.sortingOrder = 87;

        var pet = petObj.AddComponent<TaijiTuPet>();
        pet.owner = player;
    }

    /// <summary>
    /// 确保玩家身上挂有合体看守者（全局唯一）。
    /// 它同时承担卡池自愈，因此必须尽最大努力挂上—— 传入的 player 为空时
    /// 退化为全场查找，避免因为 Inspector 漏配就彻底失去自愈能力。
    /// </summary>
    public static void EnsureWatcher(Player player)
    {
        if (player == null) player = Object.FindObjectOfType<Player>();
        if (player == null)
        {
            Debug.LogWarning("[SlimeFaction] 找不到 Player，TaijiSlimeWatcher 未挂载" +
                             "（合体检测与卡池自愈将不可用）");
            return;
        }
        if (player.GetComponent<TaijiSlimeWatcher>() != null) return;
        player.gameObject.AddComponent<TaijiSlimeWatcher>();
    }

    /// <summary>场景重载时清空静态模板引用。</summary>
    public static void ResetStatics()
    {
        _yinTemplate = null;
        _yangTemplate = null;
        _host = null;
    }
}
