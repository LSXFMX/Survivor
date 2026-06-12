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
    private int gateChallengeMaxUpgradeBonus = 0; // 门挑战本局加成，随本局重置

    /// <summary>
    /// 本局是否是玩家第一次触发升级三选一（开局第一次升级必然给学习技能卡）。
    /// 初始为 true，**仅当玩家在首轮保底三选一中做出选择（closechoice）后**才置 false。
    /// SSR「启动资金」触发的三选一也不算"第一次"（那是额外赠送的），仅普通升级的第一轮算。
    ///
    /// 2026-06-11 修复：原来在 refresh() 里立即置 false，导致玩家刷新后保底被吃掉——
    /// 因为 click_refresh 重新调用 refresh()，此时标志已经消耗。
    /// 现在改为：refresh() 中只读取不消耗，消耗时机延迟到 ConsumeFirstUpgradeGuarantee()，
    /// 由 closechoice / click_skip 在玩家真正做出选择或跳过时调用。
    /// </summary>
    private bool _isFirstNormalUpgrade = true;

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

    public void DecreaseGroupCount(string group, int amount = 1)
    {
        if (string.IsNullOrEmpty(group) || amount <= 0) return;
        if (!upgradeGroupCount.ContainsKey(group)) return;
        upgradeGroupCount[group] = Mathf.Max(0, upgradeGroupCount[group] - amount);
    }

    /// <summary>当技能被“遗忘/替换”时，回退其学习组计数，允许基础技能重新进入卡池。</summary>
    public void OnSkillForgotten(string skillName)
    {
        if (string.IsNullOrEmpty(skillName) || skillEntries == null) return;
        foreach (var entry in skillEntries)
        {
            if (entry == null || entry.learnSkillPrefab == null) continue;
            getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
            if (learn == null || learn.skill == null) continue;
            if (learn.skill.Skillname != skillName) continue;
            if (!string.IsNullOrEmpty(learn.upgradeGroup))
                DecreaseGroupCount(learn.upgradeGroup, 1);
            return;
        }
    }

    private bool IsGroupMaxed(GameObject upgradeObj)
    {
        Upgradeoptionsbase opt = upgradeObj.GetComponent<Upgradeoptionsbase>();
        if (opt == null || string.IsNullOrEmpty(opt.upgradeGroup) || opt.maxUpgrades <= 0)
            return false;
        return GetGroupCount(opt.upgradeGroup) >= GetEffectiveMaxUpgrades(opt, opt is getnewskill);
    }

    /// <summary>
    /// 减 CD 类升级（type=upgradeskill / skillAttr=CDtime / upgradenumber<0）的额外过滤：
    /// 若目标技能的 CDtime 已经压到阈值附近（再降也无意义，且会让 Skillbase.FixedUpdate 永远出 CDkey>CDtime
    /// 分支，造成"卡满 CDkey 但写不进去再降"），就把这个升级选项从卡池里剔除。
    /// 阈值取 0.05 秒，给浮点和触发逻辑留点余量。
    /// </summary>
    private bool IsCooldownReductionUseless(GameObject upgradeObj)
    {
        if (upgradeObj == null || playerskill == null) return false;
        Upgradeoptionsbase opt = upgradeObj.GetComponent<Upgradeoptionsbase>();
        if (opt == null) return false;
        if (opt.type != Upgradeoptionsbase.Upgradetype.upgradeskill) return false;
        if (opt.skillAtr != Upgradeoptionsbase.skillAttribute.CDtime) return false;
        if (opt.upgradenumber >= 0f) return false; // 只过滤"减少 CD"
        if (opt.skill == null || string.IsNullOrEmpty(opt.skill.Skillname)) return false;

        // 找到玩家身上同名的实际技能实例，用其当前 CDtime 判定（prefab 上的字段不能反映本局升级累积值）。
        const float kCooldownFloor = 0.05f;
        foreach (Transform t in playerskill)
        {
            if (t == null) continue;
            Skillbase s = t.GetComponent<Skillbase>();
            if (s == null) continue;
            if (s.Skillname != opt.skill.Skillname) continue;
            return s.CDtime <= kCooldownFloor;
        }
        // 玩家还没学这个技能，本来就轮不到出现这张减CD升级卡（升级卡只有学了才出现），按"不过滤"处理。
        return false;
    }

    /// <summary>
    /// 学了亡者领域之后，孢子领域作为"基础形态"已被进化吞噬（或被锁定半径=10、紫色配色）：
    /// 其攻击「范围」由亡者领域接管，再去升级孢子领域的 attackRadius 完全无意义
    /// （孢子领域可能已被销毁；即便保留也已 LockToTombDomainPalette 锁死半径），
    /// 因此**仅过滤"提升孢子领域攻击范围"这一类升级卡**。
    ///
    /// 注意：伤害 / 冷却 / 持续时间 等其它属性升级，对仍存活的孢子领域（不忘初心 / 无罪皮肤场景下）
    /// 依然是真实生效的，不应被过滤——否则玩家进化后会失去全部孢子领域成长路径，体验受损。
    /// 故本方法只匹配 skillAtr == attackRadius；其它属性一律放行。
    /// </summary>
    private bool IsSporeFieldUpgradeUselessAfterTombDomain(GameObject upgradeObj)
    {
        if (upgradeObj == null || playerskill == null) return false;
        Upgradeoptionsbase opt = upgradeObj.GetComponent<Upgradeoptionsbase>();
        if (opt == null) return false;
        if (opt.type != Upgradeoptionsbase.Upgradetype.upgradeskill) return false;
        if (opt.skill == null) return false;
        // 仅针对孢子领域升级卡
        if (opt.skill.GetComponent<SkillSporeField>() == null) return false;
        // 仅过滤范围升级，伤害/冷却等保留
        if (opt.skillAtr != Upgradeoptionsbase.skillAttribute.attackRadius) return false;

        // 玩家身上有亡者领域 → 剔除孢子领域的【范围】升级卡
        foreach (Transform t in playerskill)
        {
            if (t == null) continue;
            if (t.GetComponent<SkillTombDomain>() != null) return true;
        }
        return false;
    }

    /// <summary>
    /// 火球术的「大小」和「飞行速度」升级卡已移除（策划决定这两项对火球术无实际意义）。
    /// 直接从卡池中永久排除。
    /// 注：火球术没有专属脚本类，用 Skillbase.Skillname == "火球术" 识别。
    /// </summary>
    private bool IsFireballSizeOrSpeedUpgrade(GameObject upgradeObj)
    {
        if (upgradeObj == null) return false;
        Upgradeoptionsbase opt = upgradeObj.GetComponent<Upgradeoptionsbase>();
        if (opt == null) return false;
        if (opt.type != Upgradeoptionsbase.Upgradetype.upgradeskill) return false;
        if (opt.skill == null || opt.skill.Skillname != "火球术") return false;
        // 排除大小和速度
        return opt.skillAtr == Upgradeoptionsbase.skillAttribute.size ||
               opt.skillAtr == Upgradeoptionsbase.skillAttribute.speed;
    }

    private bool ShouldExcludeUpgrade(GameObject upgradeObj)
    {
        if (IsGroupMaxed(upgradeObj)) return true;
        if (IsCooldownReductionUseless(upgradeObj)) return true;
        if (IsSporeFieldUpgradeUselessAfterTombDomain(upgradeObj)) return true;
        if (IsFireballSizeOrSpeedUpgrade(upgradeObj)) return true;
        return false;
    }

    public int GetEffectiveMaxUpgrades(Upgradeoptionsbase opt)
    {
        return GetEffectiveMaxUpgrades(opt, false);
    }

    public int GetEffectiveMaxUpgrades(Upgradeoptionsbase opt, bool isLearnOption)
    {
        if (opt == null || opt.maxUpgrades <= 0) return opt != null ? opt.maxUpgrades : 0;
        int paleMemoryBonus = 0;
        if (EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 3))
            paleMemoryBonus = 1; // SSR「苍白记忆」：本局所有技能升级组上限 +1

        int observedBonus = 0;
        if (isLearnOption &&
            EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 6))
        {
            // SSR「被观测者」：未学习技能（学习卡）也能吃到额外上限奖励
            observedBonus = 1;
        }
        return opt.maxUpgrades + gateChallengeMaxUpgradeBonus + paleMemoryBonus + observedBonus;
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

    private bool IsKeepOriginalEvolutionUnlocked()
    {
        if (EquipmentSystem.Instance == null) return false;
        // 兼容历史映射：SSR9「不忘初心」可能落在 id=9 或 id=7
        return EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 9) ||
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 7);
    }

    private bool ShouldBlockBaseSkillRelearn(string skillName)
    {
        if (string.IsNullOrEmpty(skillName) || playerskill == null) return false;
        if (IsKeepOriginalEvolutionUnlocked()) return false; // 有SSR9时允许共存，不做拦截

        bool hasFormOfWind = false;
        bool hasHellfire = false;
        bool hasTombDomain = false;
        foreach (Transform t in playerskill)
        {
            if (t == null) continue;
            if (t.GetComponent<SkillFormOfWind>() != null) hasFormOfWind = true;
            if (t.GetComponent<SkillHellfire>() != null) hasHellfire = true;
            if (t.GetComponent<SkillTombDomain>() != null) hasTombDomain = true;
        }

        // 无SSR9时：已进化则基础技能不可重新学习
        if (skillName == "飓风" && hasFormOfWind) return true;
        if (skillName == "火球术" && hasHellfire) return true;
        if (skillName == "孢子领域" && hasTombDomain) return true;
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

        // 诊断日志：本次刷新会逐个 entry 报告"是否进入候选池/为什么没进"，
        // 帮助调试"进化选项不出现"的问题。
        Debug.Log("===== [升级·三选一] 开始构建卡池 =====");

        foreach (var item in upplayer)
        {
            if (!ShouldExcludeUpgrade(item)) list.Add(item);
            if (!ShouldExcludeUpgrade(item)) nonLearnSkillCandidates.Add(item);
        }

        if (skillEntries != null)
        {
            foreach (var entry in skillEntries)
            {
                if (entry.learnSkillPrefab == null) continue;

                getnewskill learnOpt = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learnOpt == null) continue;

                string skillName = learnOpt.skill != null ? learnOpt.skill.Skillname : "";
                string entryTag = $"{entry.learnSkillPrefab.name}({(string.IsNullOrEmpty(skillName) ? "?" : skillName)})";
                bool alreadyHave = !string.IsNullOrEmpty(skillName) && PlayerHasSkill(skillName);

                if (alreadyHave)
                {
                    // 已拥有该技能（含装备/好感度初始赠送）：升级卡不再受难度/好感度门槛限制
                    int upCount = 0;
                    if (entry.upgradeOptions != null)
                        foreach (var upItem in entry.upgradeOptions)
                            if (!ShouldExcludeUpgrade(upItem))
                            {
                                list.Add(upItem);
                                nonLearnSkillCandidates.Add(upItem);
                                upCount++;
                            }
                    Debug.Log($"[升级·条目] {entryTag} → 已拥有，加入 {upCount} 个升级卡");
                }
                else
                {
                    // 仅学习卡需要校验解锁条件 + 特殊难度门槛（如孢子领域需 N5+、血族血统需 N7+）
                    if (!learnOpt.IsAvailableInPool())
                    {
                        Debug.Log($"[升级·条目] {entryTag} → 学习卡 IsAvailableInPool()=false，跳过");
                        continue;
                    }

                    if (!ShouldExcludeUpgrade(entry.learnSkillPrefab))
                    {
                        if (ShouldBlockBaseSkillRelearn(skillName))
                        {
                            Debug.Log($"[升级·条目] {entryTag} → 已进化，基础技能不可重学，跳过");
                            continue;
                        }
                        list.Add(entry.learnSkillPrefab);
                        learnSkillCandidates.Add(entry.learnSkillPrefab);
                        Debug.Log($"[升级·条目] {entryTag} → 学习卡进入候选池");
                    }
                    else
                    {
                        Debug.Log($"[升级·条目] {entryTag} → 学习卡被 ShouldExcludeUpgrade 过滤（组上限/CD 兜底）");
                    }
                }
            }
        }

        Debug.Log($"===== [升级·三选一] 卡池构建完成：总候选 {list.Count} 个（学习卡 {learnSkillCandidates.Count} 个） =====");

        if (list.Count == 0)
        {
            // 兜底：找不到 BattleUI 时也要恢复时间流，避免卡在 timeScale=0
            GameObject battleUIObj = GameObject.Find("BattleUI");
            battleUI bui = battleUIObj != null ? battleUIObj.GetComponent<battleUI>() : null;
            if (bui == null)
            {
                if (gameObject != null) gameObject.SetActive(false);
                Time.timeScale = 1f;
                return;
            }

            if (bui.choiceUI != null) bui.choiceUI.SetActive(false);
            if (bui.PendingGachaStartupChoices > 0)
            {
                bui.AbortGachaStartupChain();
                ToastManager.Show("[抽卡·SSR] 启动资金：当前无可选升级，已结束开局三选一");
            }
            bui.ResumeTime();
            return;
        }

        // === 首轮三选一保底：三个选项全部必出「学习新技能」卡 ===
        // 策划意图：玩家本局的"第一次三选一"中，三个选项全部为「学习新技能」类型卡牌，
        // 确保玩家在开局时能直接选一个新技能进行学习。
        //
        // "第一次三选一"定义：
        //   - 若没有 SSR「启动资金」：第一次普通升级时的三选一。
        //   - 若有 SSR「启动资金」（开局 3 次三选一）：赠送的第 1 轮（PendingGachaStartupChoices==3）。
        //     第 2、第 3 轮不再保底。
        //
        // 条件：
        //   1. _isFirstNormalUpgrade == true
        //   2. 当前不是 SSR 启动资金的第 2/3 轮（第 1 轮也允许触发保底）
        //   3. learnSkillCandidates 非空
        // 如果学习卡池不足 3 张，则有多少用多少（不足的位置从全池随机补）。
        battleUI bui2 = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        int pendingStartup = bui2 != null ? bui2.PendingGachaStartupChoices : 0;
        // 只有启动资金的第 2/3 轮（pending==2 或 1）才视为"不允许保底的启动资金轮"
        bool isGachaStartupLaterRound = pendingStartup > 0 && pendingStartup < 3;
        bool useFirstUpgradeGuarantee = _isFirstNormalUpgrade && !isGachaStartupLaterRound && learnSkillCandidates.Count > 0;
        // 2026-06-11 修复：不再在 refresh() 中消耗 _isFirstNormalUpgrade。
        // 消耗时机改为 ConsumeFirstUpgradeGuarantee()，由 closechoice / click_skip 调用。
        // 这样玩家在首轮保底的三选一屏点「刷新」后，保底仍然生效。
        if (useFirstUpgradeGuarantee)
        {
            // 三个选项全部从学习卡池抽取（不重复）
            List<GameObject> shuffled = new List<GameObject>(learnSkillCandidates);
            // Fisher-Yates 洗牌
            for (int i = shuffled.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                GameObject tmp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = tmp;
            }
            c1 = shuffled[0];
            c2 = shuffled.Count > 1 ? shuffled[1] : getrandom();
            c3 = shuffled.Count > 2 ? shuffled[2] : getrandom();
            Debug.Log($"[升级·三选一] 首轮保底生效（三选项全学习卡）：learnSkillCandidates={learnSkillCandidates.Count}，c1={c1.name}, c2={c2.name}, c3={c3.name}");
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
        // 优先从"非学习卡"池里抽，避免三张全是学习技能。
        // （历史上这里有 firstChoiceMustOfferLearnSkill 限制，已随首轮特殊分支一起移除。）
        if (nonLearnSkillCandidates != null && nonLearnSkillCandidates.Count > 0)
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

    /// <summary>
    /// 消耗首轮保底资格。由 closechoice（选择后） / click_skip（跳过后）调用。
    /// SSR 启动资金的第 2/3 轮不消耗（因为第 1 轮已经用掉了保底资格）。
    /// 启动资金第 1 轮（PendingGachaStartupChoices==3 进入，选完后 TryAdvance 将其减为 2）：
    ///   此时 closechoice 先调用本方法再调用 TryAdvance，所以这里看到 pending 仍为 3 → 允许消耗。
    ///   但实际上 TryAdvance 是在 closechoice 里本方法之后才调用的，这里看到的 pending 值取决于
    ///   调用顺序。为确保正确，我们用"只有 pending==2 或 1 时才跳过消耗"的方式判断。
    /// </summary>
    public void ConsumeFirstUpgradeGuarantee()
    {
        if (!_isFirstNormalUpgrade) return;
        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        int pending = bui != null ? bui.PendingGachaStartupChoices : 0;
        // pending==3 时是启动资金第 1 轮（保底轮），允许消耗
        // pending==2 或 1 时是启动资金第 2/3 轮，不消耗（它们本来就没触发保底）
        // pending==0 时是正常升级轮，允许消耗
        bool isLaterStartupRound = pending > 0 && pending < 3;
        if (!isLaterStartupRound)
        {
            _isFirstNormalUpgrade = false;
            Debug.Log("[升级·三选一] 首轮保底资格已消耗（玩家做出选择或跳过）");
        }
    }

    public void click_refresh()
    {
        if (remainRefresh <= 0) return;
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
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
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
        // 2026-06-11：跳过也消耗首轮保底资格
        ConsumeFirstUpgradeGuarantee();
        battleUI bui = GameObject.Find("BattleUI").GetComponent<battleUI>();
        bui.choiceUI.SetActive(false);
        if (bui.TryAdvanceGachaStartupChain())
            return;
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

    public void click1() { AudioManager.PlaySfx(AudioManager.SfxKey.Click); c1.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click2() { AudioManager.PlaySfx(AudioManager.SfxKey.Click); c2.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
    public void click3() { AudioManager.PlaySfx(AudioManager.SfxKey.Click); c3.GetComponent<Upgradeoptionsbase>().chocieupgrade(); }
}
