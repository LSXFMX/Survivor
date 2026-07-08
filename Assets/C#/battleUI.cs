using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    [Header("Boss")]
    public GameObject bossPrefab;       // 蘑菇人Boss（N2~N5、N9、N13）
    public GameObject batBossPrefab;    // 蝙蝠Boss（N7~N8）
    public GameObject wolfBossPrefab;   // 狼人社群Boss（N10~N11）
    public GameObject slimeBossPrefab;  // 史莱姆社群Boss（N12）
    public Transform bossSpawnPoint;
    public Transform enemylayer;
    private bool bossSpawned = false;
    private BossMushroomMan spawnedBoss = null;
    private BossBat spawnedBatBoss = null;
    private WolfBoss spawnedWolfBoss = null;
    private SlimeBoss spawnedSlimeBoss = null;
    private int _doubleBossRemain = 0;

    [Header("胜利/失败")]
    public GameObject victoryPanel;
    [Tooltip("胜利/失败标题文字（victoryPanel 内的主标题 TMP，若留空则自动取 victoryPanel 上第一个 TMP）")]
    public TextMeshProUGUI victoryTitleText;
    [Tooltip("胜利/失败副标题（提示如目标完成情况、用时等），可留空")]
    public TextMeshProUGUI victorySubtitleText;
    private bool bossPhase = false;
    private float bossTimer = 0f;
    private const float BOSS_TIME_LIMIT = 90f;

    // 龙王战：无时间限制，倒计时文本由 DragonBoss 接管（"呵呵" + 分阶段文字/颜色）
    private bool _dragonBossMode = false;
    /// <summary>进入龙王战模式：关闭 Boss 计时上限，倒计时文本交给 DragonBoss 控制。</summary>
    public void EnterDragonBossMode() { _dragonBossMode = true; }
    /// <summary>由 DragonBoss 设置倒计时区显示的文字与颜色。</summary>
    public void SetBossCountdownText(string text, Color color)
    {
        if (timeui != null) { timeui.text = text; timeui.color = color; }
    }

    [Header("难度限制对象")]
    public GameObject adventureUIRoot;  // 奇遇UI根对象（N1/N2隐藏）
    public GameObject yuanmuUIRoot;     // 源木UI根对象（N1/N2隐藏）

    [Header("通关演出")]
    public float slowMoScale = 0.2f;
    public float slowMoDuration = 1.5f;
    public float victoryDelay = 2f;

    [Header("速度按钮")]
    public TextMeshProUGUI speedButtonText;
    private int speedMode = 1; // 1/2/3 倍速；2倍速开局自带，3倍速由成就装备4解锁
    private Button _speedButton; // 自动从 speedButtonText 父级取，用于暂停时禁用

    [Header("暂停菜单子面板")]
    [Tooltip("「设置」面板根对象（含攻击范围开关/音量滑条）")]
    public GameObject settingsPanel;
    [Tooltip("「操作说明」面板根对象")]
    public GameObject instructionsPanel;

    /// <summary>SSR「启动资金」：开局额外三选一剩余轮次（与升级三选一相同 UI）</summary>
    private int _pendingGachaStartupChoices;

    // ===== 角色头像 + 玩家属性升级总进度 UI（运行时构造，无需场景拖拽）=====
    // 视觉：把头像 + 底片 + 升级进度文本固定在【血条 life 的左上角，即"最左 + 血条上方"】。
    // 显示规则参照技能图标右下角的 n/max（每条技能各自的升级组次数 / 上限）。
    // 这里的"角色升级"指 8 项玩家属性升级（healthmax/atk/def/speed/CR/CD/EVA/DR）的累计次数 / 上限总和。
    private Image _charAvatarImage;
    private Image _charAvatarBackground;
    private TextMeshProUGUI _charUpgradeText;
    private int _charAppliedSkin = -2; // -2 = 从未刷新过；-1 = 头像加载失败
    private int _charCachedMaxTotal = -1; // 玩家属性升级总上限（首次访问 ChoiceUI 后缓存）

    void Start()
    {
        enemy.adventureHpMultiplier = 1.0f;
        enemy.adventureAtkMultiplier = 1.0f;

        DisableUINavigationSubmit();
        choiceUI.SetActive(false);
        RefreshSkill();
        startcount = false;
        if (timeui != null) timeui.text = "--:--";
        ApplyDifficultyRestrictions();
        StartCoroutine(DelayedStartTime());
        ApplyStartupFundEquipmentIfUnlocked();

        // 暂停菜单相关初始化（缓存倍速按钮 + 给"操作说明"按钮挂红点 + 接入右键关闭）
        InitSpeedButtonRef();
        RefreshSpeedButtonState();
        SetupInstructionsBadge();
        SetupRightClickClose();

        // 角色头像 + 玩家升级进度 UI（运行时构造，无需场景拖拽）
        EnsureCharacterPanel();
    }

    /// <summary>从 speedButtonText 父级自动取 Button，避免新增字段 / 修改场景。</summary>
    private void InitSpeedButtonRef()
    {
        if (speedButtonText != null)
            _speedButton = speedButtonText.GetComponentInParent<Button>();
    }

    /// <summary>扫描暂停菜单内调用 Click_instructions 的按钮，自动附加红点提醒组件。</summary>
    private void SetupInstructionsBadge()
    {
        if (menu == null) return;
        var buttons = menu.GetComponentsInChildren<Button>(true);
        foreach (var btn in buttons)
        {
            int n = btn.onClick.GetPersistentEventCount();
            for (int i = 0; i < n; i++)
            {
                if (btn.onClick.GetPersistentMethodName(i) == nameof(Click_instructions))
                {
                    if (btn.GetComponent<InstructionsNewBadge>() == null)
                        btn.gameObject.AddComponent<InstructionsNewBadge>();
                    return; // 只处理第一个匹配的按钮
                }
            }
        }
    }

    /// <summary>
    /// 给可右键关闭的面板自动挂 RightClickClosePanel（幂等）。
    /// 三选一/胜利面板等不应被跳过的，不接入。
    /// </summary>
    private void SetupRightClickClose()
    {
        // 暂停 menu 用 Click_continue（会处理 Time.timeScale 恢复）
        if (menu != null)
        {
            var p = RightClickClosePanel.EnsureOn(menu.gameObject);
            p.onRightClickClose = new UnityEngine.Events.UnityEvent();
            p.onRightClickClose.AddListener(Click_continue);
        }
        // settings/instructions 默认 SetActive(false) 即可
        if (settingsPanel != null)     RightClickClosePanel.EnsureOn(settingsPanel);
        if (instructionsPanel != null) RightClickClosePanel.EnsureOn(instructionsPanel);
    }

    /// <summary>SSR 启动资金：开局等级+3，并连续 3 次升级三选一（与 levelup 相同 ChoiceUI）</summary>
    private void ApplyStartupFundEquipmentIfUnlocked()
    {
        if (EquipmentSystem.Instance == null || player == null) return;
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 2)) return;

        player.level += 3;
        _pendingGachaStartupChoices = 3;
        Time.timeScale = 0f;
        choiceUI.SetActive(true);
        ToastManager.Show("[抽卡·SSR] 启动资金：等级+3，开局3次升级三选一");
    }

    /// <summary>三选一关闭或跳过本屏后：若仍有开局额外轮次则继续弹出。返回 true 表示保持暂停，勿 ResumeTime。</summary>
    public bool TryAdvanceGachaStartupChain()
    {
        if (_pendingGachaStartupChoices <= 0) return false;
        _pendingGachaStartupChoices--;
        if (_pendingGachaStartupChoices > 0)
        {
            Time.timeScale = 0f;
            choiceUI.SetActive(true);
            return true;
        }
        return false;
    }

    public int PendingGachaStartupChoices => _pendingGachaStartupChoices;

    /// <summary>无可选项等异常时清空开局三选一剩余次数</summary>
    public void AbortGachaStartupChain()
    {
        _pendingGachaStartupChoices = 0;
    }

    private void DisableUINavigationSubmit()
    {
        if (EventSystem.current == null) return;
        // 关闭键盘/手柄导航提交，避免空格触发当前选中的UI按钮
        EventSystem.current.sendNavigationEvents = false;
        EventSystem.current.SetSelectedGameObject(null);
    }

    private void ApplyDifficultyRestrictions()
    {
        if (DifficultyManager.Instance == null) return;
        string label = DifficultyManager.Instance.Current.label;
        if (label == "N1" || label == "N2")
        {
            if (adventureUIRoot != null) adventureUIRoot.SetActive(false);
            if (yuanmuUIRoot != null)    yuanmuUIRoot.SetActive(false);
        }
    }

    private IEnumerator DelayedStartTime()
    {
        yield return null;
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
            Skillbase sb = playerskill.GetComponent<Skillbase>();
            if (sb == null || !ShouldCreateCooldownUI(sb)) continue;

            GameObject skill = Instantiate(SkillUI, Skillroom);
            skill.transform.GetChild(0).GetComponent<Image>().sprite = sb.icon;
            skill.transform.GetChild(1).GetComponent<Image>().sprite = sb.icon;
            UpdateSkillCountText(skill, sb);

            // 永续技能（无 CD、CDtime 极大）：附加旋转循环边框，告诉玩家"它一直在生效"
            if (IsEternalSkill(sb))
                AttachEternalBorder(skill);
        }

        // SSR9「三清化一」：也显示合并过来的分身技能图标（避免重复显示已有同名技能）
        if (player != null && player.SkillListClone != null)
        {
            foreach (Transform cloneSkill in player.SkillListClone)
            {
                Skillbase sb = cloneSkill != null ? cloneSkill.GetComponent<Skillbase>() : null;
                if (sb == null || !ShouldCreateCooldownUI(sb)) continue;

                // 跳过已在 Skilllist 中存在的同名技能（避免 UI 重复图标）
                bool duplicate = false;
                foreach (Transform t in Skilllist)
                {
                    Skillbase existing = t != null ? t.GetComponent<Skillbase>() : null;
                    if (existing != null && existing.Skillname == sb.Skillname) { duplicate = true; break; }
                }
                if (duplicate) continue;

                GameObject skill = Instantiate(SkillUI, Skillroom);
                skill.transform.GetChild(0).GetComponent<Image>().sprite = sb.icon;
                skill.transform.GetChild(1).GetComponent<Image>().sprite = sb.icon;
                UpdateSkillCountText(skill, sb);
                if (IsEternalSkill(sb))
                    AttachEternalBorder(skill);
            }
        }
    }

    private bool ShouldCreateCooldownUI(Skillbase sb)
    {
        // 之前把所有 UR 进化技能（风之形/地狱火）都排除掉 CD UI，导致玩家不知道这些技能有没有生效。
        // 现在改为：所有技能都建 UI，永续技能（CDtime ≥ 阈值）会在 RefreshSkill 里额外加一圈
        //   旋转循环边框作为视觉标记；有真实 CD 的技能仍按原逻辑显示 fillAmount。
        return true;
    }

    /// <summary>
    /// 判定一个技能是不是"永续 / 无冷却"：CDtime ≥ 1e8 视为永续。
    /// 三个永续技能：SkillFormOfWind / SkillTombDomain（都在 Awake 把 CDtime 设 1e30），
    /// 以及其它继承自 Skillbase 但 CDtime 极大的"装备/标记"型技能也自动适用。
    /// 地狱火等仍有真实 CD 的进化技能不会被误判。
    /// </summary>
    private static bool IsEternalSkill(Skillbase sb)
    {
        return sb != null && sb.CDtime >= 1e8f;
    }

    /// <summary>
    /// 给永续技能的 SkillUI 实例追加一个"旋转循环标志"。
    /// 视觉方案：在 SkillUI 上方挂一个透明容器，里面均匀放 4 个青绿色小方块沿圆周排布，
    ///   整体绕中心匀速旋转 → 看起来是"4 个亮点沿外圈转圈"的 loading-spinner 风格，
    ///   完全不会出现"一个矩形块在动"的观感（之前用 Image.fillRadial 的方案因为槽位 sprite 是矩形，
    ///   切出一段弧本质还是矩形 → 看起来就是个方块在转）。
    /// 已存在则跳过（防御 RefreshSkill 重复调用）。
    /// </summary>
    private void AttachEternalBorder(GameObject skillUI)
    {
        if (skillUI == null) return;
        // 已存在则不重复添加
        Transform existing = skillUI.transform.Find("EternalBorder");
        if (existing != null) return;

        // 旋转容器：填满 SkillUI 槽位（130×130），稍微向外扩 4px 让小点贴在槽位外圈
        var go = new GameObject("EternalBorder", typeof(RectTransform));
        go.transform.SetParent(skillUI.transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(-4f, -4f);
        rt.offsetMax = new Vector2(4f, 4f);
        rt.localScale = Vector3.one;

        // 容器自己不绘制——只做"旋转锚点"，由 4 个子方块呈现视觉
        // 4 个亮点沿圆周 90° 均匀分布；旋转时整体公转 → 形成"loading dots"循环视觉
        const int DotCount = 4;
        // 半径用容器实际宽度的一半（运行时 SkillUI 是 130 → 半径约 65~70px，刚好沿槽位外圈）
        // 由于 anchor 已经填满父槽位，我们用相对锚点 (0~1) 来放小点 → 自动跟随父槽位大小
        for (int i = 0; i < DotCount; i++)
        {
            float angleRad = (i / (float)DotCount) * Mathf.PI * 2f - Mathf.PI * 0.5f; // 从顶部开始
            // 沿单位圆边缘放置（anchor 0~1 系统里中心是 (0.5,0.5)，外圈是 1.0 半径）
            float ax = 0.5f + 0.5f * Mathf.Cos(angleRad);
            float ay = 0.5f + 0.5f * Mathf.Sin(angleRad);

            var dotGo = new GameObject($"Dot_{i}", typeof(RectTransform));
            dotGo.transform.SetParent(go.transform, false);
            var drt = (RectTransform)dotGo.transform;
            drt.anchorMin = drt.anchorMax = new Vector2(ax, ay);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.sizeDelta = new Vector2(10f, 10f);
            drt.anchoredPosition = Vector2.zero;
            drt.localScale = Vector3.one;

            var dimg = dotGo.AddComponent<Image>();
            dimg.sprite = null; // 默认 UI 白色方块 sprite 也行，但 null 时 Unity 用单色 quad → 同样是小方块
            dimg.color = new Color(0.45f, 1f, 0.85f, 0.9f); // 青绿
            dimg.raycastTarget = false;
        }

        // 旋转 + 颜色脉动
        var spinner = go.AddComponent<EternalBorderSpinner>();
        spinner.degreesPerSecond = 90f;  // 4 秒一圈（4 个点 → 视觉上每秒前进 1 个点位，恰好对应 loading-dots 节奏）
        spinner.applyToChildren = true;  // 让颜色脉动作用到 4 个 Dot 子物体
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
                        max = ChoiceUI.Instance.GetEffectiveMaxUpgrades(first);
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
        minute = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.minutes : 10;
        second = 0;
        timer = 0;
        startcount = true;
        _deathFallbackTimer = 0f;
        _deathFallbackTriggered = false;
    }

    void Update()
    {
        int index = 0;
        if (Skillroom.childCount > 0)
        {
            foreach (Transform skill in Skilllist)
            {
                Skillbase s = skill.GetComponent<Skillbase>();
                if (s == null || !ShouldCreateCooldownUI(s)) continue;
                if (index >= Skillroom.childCount) break;

                // 永续技能（CDtime ≥ 1e8）：CDkey/CDtime ≈ 0 → fillAmount 会算到 1，导致整个图标
                // 被冷却遮罩盖住"看不见"。直接给它一个 0 → 图标完全显示，由旋转边框表达"持续生效"。
                float fillAmount;
                if (IsEternalSkill(s))
                    fillAmount = 0f;
                else
                    fillAmount = s.CDtime > 0f ? (1f - s.CDkey / s.CDtime) : 0f;
                Skillroom.transform.GetChild(index).GetChild(1).GetComponent<Image>().fillAmount = fillAmount;
                index++;
            }
        }

        life.fillAmount = (float)player.health / (float)player.healthmax;
        exp.localScale = new Vector3((float)player.exp / (float)player.expmax, 1, 1);
        level.text = "level:" + player.level;
        health.text = player.health + "/" + player.healthmax;

        // 角色头像 + 玩家升级进度（每帧刷新文字与皮肤切换检测——成本极低）
        RefreshCharacterPanel();

        if (startcount)
        {
            timer += Time.deltaTime;
            if (timer >= 1)
            {
                timer = 0;
                second--;
                if (second < 0) { minute--; second = 59; }
                timeui.text = minute + (second < 10 ? ":0" : ":") + second;
                if (minute <= 0 && second <= 0) timeover();
            }
        }

        if (bossPhase)
        {
            // 龙王战无时间限制：不倒计时、不因超时失败；倒计时文本由 DragonBoss 接管
            if (!_dragonBossMode)
            {
                bossTimer -= Time.deltaTime;
                if (timeui != null)
                    timeui.text = "Boss: " + Mathf.CeilToInt(Mathf.Max(0, bossTimer)).ToString();
                if (bossTimer <= 0f)
                {
                    bossPhase = false;
                    StartCoroutine(ReturnToMain(false));
                }
            }
        }

        if (YuanMuManager.Instance != null && yuanmuText != null)
            yuanmuText.text = ": " + YuanMuManager.Instance.Current;

        // ESC 快捷键开关暂停菜单
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 优先关子面板
            if (settingsPanel != null && settingsPanel.activeSelf)
                settingsPanel.SetActive(false);
            else if (instructionsPanel != null && instructionsPanel.activeSelf)
                instructionsPanel.SetActive(false);
            else if (menu.gameObject.activeSelf)
                Click_continue();
            else
                Click_menu();
        }

        // ── 兜底：检测主体死亡但 ReturnToMain 未启动的异常状态 ──────────────
        // 场景：某些极端竞争条件下，Player.death() 可能被提前 return（误判为分身 /
        //       grace period / 重入 / ReviveManager 异常），导致主体 health<=0 但
        //       ReturnToMain 没有被启动，表现为"倒计时卡住、游戏不暂停、还能操纵分身"。
        // 方案：每帧检测 player.health <= 0 && startcount 仍为 true（说明游戏仍在计时
        //       但死亡结算没启动），等待短暂缓冲后强制触发 ReturnToMain。
        CheckPlayerDeathFallback();
    }

    // ── 死亡兜底逻辑 ──
    private float _deathFallbackTimer;
    private bool  _deathFallbackTriggered;
    private const float DEATH_FALLBACK_DELAY = 0.5f; // 给 ReviveManager / grace period 留余量

    private void CheckPlayerDeathFallback()
    {
        if (_deathFallbackTriggered) return;

        // 前置条件：游戏正在运行中（startcount=true）且主体已经 health<=0
        if (player == null || player.health > 0 || !startcount)
        {
            _deathFallbackTimer = 0f;
            return;
        }

        // ReviveManager 正在弹窗等待玩家选择时不干预（它会暂停 timeScale）
        if (Time.timeScale == 0f) { _deathFallbackTimer = 0f; return; }

        // 累计满缓冲时间后触发
        _deathFallbackTimer += Time.unscaledDeltaTime;
        if (_deathFallbackTimer < DEATH_FALLBACK_DELAY) return;

        // 强制触发死亡结算
        _deathFallbackTriggered = true;
        Debug.LogWarning("[battleUI] 兜底触发：检测到主体 health<=0 但 ReturnToMain 未启动，强制启动死亡结算");

        // 清场所有分身（模拟 Player.death 中的清场逻辑）
        if (player.transform.parent != null)
        {
            foreach (Transform t in player.transform.parent)
            {
                if (t == null || t == player.transform) continue;
                var p = t.GetComponent<Player>();
                if (p != null)
                {
                    t.gameObject.SetActive(false);
                    Destroy(t.gameObject);
                }
            }
        }

        StartCoroutine(ReturnToMain(false));
    }

    public void ResumeTime()
    {
        Time.timeScale = Mathf.Clamp(speedMode, 1, CanUseTripleSpeed() ? 3 : 2);
    }

    public bool CanUseTripleSpeed()
    {
        return EquipmentSystem.Instance != null &&
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 4);
    }

    public void RefreshSpeedButtonState()
    {
        if (speedButtonText == null) return;
        speedButtonText.transform.parent.gameObject.SetActive(true);
        if (!CanUseTripleSpeed() && speedMode > 2) speedMode = 2;
        speedButtonText.text = "X" + speedMode;
    }

    public void openchoice()
    {
        Time.timeScale = 0;
        choiceUI.SetActive(true);
    }

    public void timeover()
    {
        startcount = false;
        // N1：不生成Boss，直接胜利
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.Current.label == "N1")
        {
            StartCoroutine(ReturnToMain(true));
            return;
        }
        if (!bossSpawned) SpawnBoss();
    }

    public void OnBossDefeated()
    {
        if (!bossPhase) return;

        // 双Boss模式：需要两只都死
        if (_doubleBossRemain > 0)
        {
            _doubleBossRemain--;
            if (_doubleBossRemain > 0) return; // 还有Boss存活
        }

        bossPhase = false;
        StartCoroutine(ReturnToMain(true));
    }

    /// <summary>供 Player.death() 调用的公开包装</summary>
    public IEnumerator ReturnToMainPublic(bool victory) => ReturnToMain(victory);

    private IEnumerator ReturnToMain(bool victory)
    {
        startcount = false;
        bossPhase = false;
        SetSpeedButtonInteractable(false);

        if (victory)
        {
            Time.timeScale = slowMoScale;
            yield return new WaitForSecondsRealtime(slowMoDuration);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(victoryDelay);
            ClearRecordManager.Instance?.RecordClear();
        }
        else
        {
            Time.timeScale = slowMoScale;
            yield return new WaitForSecondsRealtime(slowMoDuration);
            Time.timeScale = 0f;
            AudioManager.StopAll();
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            ApplyVictoryPanelTexts(victory);
        }

        ToastManager.Show("3秒后返回主菜单...");
        yield return new WaitForSecondsRealtime(3f);
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// 把胜利/失败面板上的标题/副标题按当前难度文案填好。
    /// 标题：失败一律「Defeated...」；胜利按难度做差异化（N1 = "Survive!"，N2~N5 = "Boss Down!"，
    /// N6 = "Twin Down!"，N7 = "Swarm Crushed!"，N8 = "World Conquered!"）。
    /// 副标题：补充目标说明 + 通关用时（如果可拿）。
    /// </summary>
    private void ApplyVictoryPanelTexts(bool victory)
    {
        // 解析主/副标题 TMP
        TextMeshProUGUI title = victoryTitleText;
        TextMeshProUGUI subtitle = victorySubtitleText;
        if (title == null || subtitle == null)
        {
            var allTmp = victoryPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (title == null && allTmp.Length > 0) title = allTmp[0];
            if (subtitle == null && allTmp.Length > 1) subtitle = allTmp[1];
        }

        string label = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.label : "";
        string titleText, subText;
        if (victory)
        {
            titleText = GetVictoryTitleForDifficulty(label);
            subText   = GetVictorySubtitleForDifficulty(label);
        }
        else
        {
            titleText = "Defeated...";
            subText   = "再接再厉，下次会更好。";
        }

        if (title != null)
        {
            title.text = titleText;
            // 让胜利标题大一点（如果 TMP 有 enableAutoSizing 也无伤）
            title.fontStyle = FontStyles.Bold;
        }
        if (subtitle != null)
        {
            subtitle.text = subText;
            subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subText));
        }
    }

    /// <summary>按难度返回胜利标题（短，醒目）。除第一关外统一为 "Victory!"。</summary>
    public static string GetVictoryTitleForDifficulty(string label)
    {
        switch (label)
        {
            case "N1": return "Survive!";   // 第一关目标：生存
            default:   return "Victory!";   // 其余关卡统一胜利文案
        }
    }

    /// <summary>按难度返回胜利副标题（描述本关目标）。</summary>
    public static string GetVictorySubtitleForDifficulty(string label)
    {
        switch (label)
        {
            case "N1": return "你撑过了开局的洪流。";
            case "N2": return "蘑菇人 Boss 应声倒下。";
            case "N3": return "新的奇遇即将开启。";
            case "N4": return "夜空中的威胁也被你击碎。";
            case "N5": return "门后的挑战等着勇者。";
            case "N6": return "两头巨蘑同时坍塌。";
            case "N7": return "蝙蝠社群随首领一同陨落。";
            case "N8": return "你已是世界的征服者。";
            case "N10": return "狼人社群的首领轰然倒下。";
            case "N11": return "狼人与月光的契约就此瓦解。";
            case "N12": return "史莱姆巨龙塑形失败，王冠坠地。";
            default:   return "再次通关，强度++";
        }
    }

    private void SpawnBoss()
    {
        string label = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.label : "N2";

        // N11/N12 生成史莱姆社群Boss，N9/N10 生成狼人社群Boss，N7/N8 生成吸血鬼Boss，
        // N6 / N12 生成双Boss，其余生成单蘑菇人Boss
        bool isDragonBoss = label == "N13"; // N13 关底 = 最终龙王
        bool isSlimeBoss  = label == "N11" || label == "N12";
        bool isWolfBoss   = label == "N9"  || label == "N10";
        bool isBatBoss    = label == "N7"  || label == "N8";
        bool isDoubleBoss = label == "N6"  || label == "N12";

        bossSpawned = true;
        bossPhase   = true;
        bossTimer   = BOSS_TIME_LIMIT;
        startcount  = false;

        if (isDragonBoss)
        {
            // N13 关底：最终龙王（5 元素形态状态机，纯代码构建，借用蘑菇 bossPrefab 的 atknumber/expstone/material/red）
            Vector3 pos = GetBossSpawnPos(0, 1);
            DragonBoss dragon = DragonBossBuilder.Build(pos, enemylayer, bossPrefab, this);
            if (dragon == null) Debug.LogWarning("[Boss] 最终龙王构建失败");
        }
        else if (isDoubleBoss)
        {
            // N6 → 双蘑菇boss，N12 → 双史莱姆boss
            GameObject prefab = null;
            string prefabPath = null;
            if (label == "N6")  { prefab = bossPrefab; }
            if (label == "N12") { prefab = slimeBossPrefab != null ? slimeBossPrefab
                                            : Resources.Load<GameObject>("WorldBoss/SlimeBoss"); prefabPath = "WorldBoss/SlimeBoss"; }
            if (prefab == null) { Debug.LogWarning("[Boss] 双Boss预制体缺失"); return; }
            _doubleBossRemain = 2;
            for (int i = 0; i < 2; i++)
            {
                Vector3 pos = GetBossSpawnPos(i, 2);
                GameObject obj = Instantiate(prefab, pos, Quaternion.Euler(45, 0, 0),
                    enemylayer != null ? enemylayer : null);
                if (label == "N6")
                {
                    BossMushroomMan b = obj.GetComponent<BossMushroomMan>();
                    if (b != null) { b.battleUI = this; BossHealthBarUI.Register(b); }
                }
                else if (label == "N12")
                {
                    SlimeBoss s = obj.GetComponent<SlimeBoss>();
                    if (s != null) { s.battleUI = this; BossHealthBarUI.Register(s); }
                }
            }
            Debug.Log("[Boss] 双Boss已生成（" + label + "）");
        }
        else if (isSlimeBoss)
        {
            GameObject prefab = slimeBossPrefab != null ? slimeBossPrefab
                              : Resources.Load<GameObject>("WorldBoss/SlimeBoss");
            if (prefab == null) { Debug.LogWarning("[Boss] 史莱姆Boss prefab 缺失（Resources/WorldBoss/SlimeBoss）"); return; }
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(prefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedSlimeBoss = obj.GetComponent<SlimeBoss>();
            if (spawnedSlimeBoss != null) { spawnedSlimeBoss.battleUI = this; BossHealthBarUI.Register(spawnedSlimeBoss); }
            Debug.Log("[Boss] 史莱姆社群Boss已生成");
        }
        else if (isWolfBoss)
        {
            if (wolfBossPrefab == null) return;
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(wolfBossPrefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedWolfBoss = obj.GetComponent<WolfBoss>();
            if (spawnedWolfBoss != null) { spawnedWolfBoss.battleUI = this; BossHealthBarUI.Register(spawnedWolfBoss); }
            Debug.Log("[Boss] 狼人社群Boss已生成");
        }
        else if (isBatBoss)
        {
            if (batBossPrefab == null) return;
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(batBossPrefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedBatBoss = obj.GetComponent<BossBat>();
            if (spawnedBatBoss != null) { spawnedBatBoss.battleUI = this; BossHealthBarUI.Register(spawnedBatBoss); }
            Debug.Log("[Boss] 蝙蝠Boss已生成");
        }
        else
        {
            if (bossPrefab == null) return;
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(bossPrefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedBoss = obj.GetComponent<BossMushroomMan>();
            if (spawnedBoss != null) { spawnedBoss.battleUI = this; BossHealthBarUI.Register(spawnedBoss); }
            Debug.Log("[Boss] 蘑菇人Boss已生成");
        }
    }

    private Vector3 GetBossSpawnPos(int index, int total)
    {
        if (bossSpawnPoint != null) return bossSpawnPoint.position;
        const float MAP_X_MIN = -90f, MAP_X_MAX = 90f;
        const float MAP_Z_MIN = -90f, MAP_Z_MAX = 90f;
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        float angle = (Random.Range(0f, 360f) + index * (360f / Mathf.Max(total, 1))) * Mathf.Deg2Rad;
        float dist  = Random.Range(8f, 20f);
        float x = Mathf.Clamp(playerPos.x + Mathf.Cos(angle) * dist, MAP_X_MIN, MAP_X_MAX);
        float z = Mathf.Clamp(playerPos.z + Mathf.Sin(angle) * dist, MAP_Z_MIN, MAP_Z_MAX);
        return new Vector3(x, playerPos.y, z);
    }

    public void Click_menu()
    {
        // 奇遇选择中不允许打开暂停菜单
        if (AdventureEventManager.Instance != null &&
            AdventureEventManager.Instance.adventureUI != null &&
            AdventureEventManager.Instance.adventureUI.IsShowing) return;

        menu.gameObject.SetActive(true);
        // 进入暂停菜单时收起子面板
        if (settingsPanel != null)     settingsPanel.SetActive(false);
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        Time.timeScale = 0;
        // 暂停期间禁用倍速切换
        SetSpeedButtonInteractable(false);
    }

    public void Click_continue()
    {
        // 子面板优先关闭
        if (settingsPanel != null && settingsPanel.activeSelf)     { settingsPanel.SetActive(false);     return; }
        if (instructionsPanel != null && instructionsPanel.activeSelf) { instructionsPanel.SetActive(false); return; }
        menu.gameObject.SetActive(false);
        ResumeTime();
        // 恢复倍速按钮可点击
        SetSpeedButtonInteractable(true);
    }

    public void Click_toggleSpeed()
    {
        // 暂停中不允许切换（双保险，按钮 interactable 已在暂停时为 false，这里再防御）
        if (menu != null && menu.gameObject.activeSelf) return;
        if (Time.timeScale == 0f) return;
        // 奇遇选择中不允许切换倍速
        if (AdventureEventManager.Instance != null &&
            AdventureEventManager.Instance.adventureUI != null &&
            AdventureEventManager.Instance.adventureUI.IsShowing) return;

        int maxSpeed = CanUseTripleSpeed() ? 3 : 2;
        speedMode++;
        if (speedMode > maxSpeed) speedMode = 1;

        Time.timeScale = speedMode;
        if (speedButtonText != null)
            speedButtonText.text = "X" + speedMode;

        // 三倍速会显著放大单帧负担（粒子/物理/AI 全 ×3），低端机容易卡顿，给一次提醒。
        if (speedMode == 3)
            ToastManager.Show("三倍速会导致游戏卡顿，慎重使用");
    }

    private void SetSpeedButtonInteractable(bool v)
    {
        if (_speedButton == null) InitSpeedButtonRef();
        if (_speedButton != null) _speedButton.interactable = v;
    }

    public void Click_settings()
    {
        if (settingsPanel == null) { ToastManager.Show("设置面板未配置"); return; }
        // 关掉同级的操作说明，避免叠层
        if (instructionsPanel != null) instructionsPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void Click_instructions()
    {
        if (instructionsPanel == null) { ToastManager.Show("操作说明面板未配置"); return; }
        if (settingsPanel != null) settingsPanel.SetActive(false);
        instructionsPanel.SetActive(true);
    }

    /// <summary>
    /// 旧绑定：暂停菜单「退出游戏」按钮回调。
    /// 行为已改为「返回主菜单」——重载当前场景，title 等场景上的主菜单 UI 会重新初始化显示。
    /// 保留方法名以兼容已有的场景 onClick 绑定（无需手动重新拖线）。
    /// </summary>
    public void Click_exitgame()
    {
        ReturnToTitle();
    }

    /// <summary>显式「返回主菜单」入口（推荐新绑定使用）。</summary>
    public void Click_returnToMenu()
    {
        ReturnToTitle();
    }

    private void ReturnToTitle()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    // ============================================================
    // 角色头像 + 玩家升级进度 UI
    // ============================================================
    /// <summary>
    /// 在【血条 life 的左上角（最左侧、血条上方）】构造一个 56×56 头像 + 深色底片 + 头像下方一行 n/max 总进度。
    /// 完全运行时构造，不需要在场景里拖任何字段——挂到 life 父级下（与血条同容器），
    /// pivot=(0,0)（左下），让面板的"右下角"贴住"血条左上角"，从而面板整体在血条上方且最左对齐。
    /// 头像贴图复用 SkinChanger 同款相对路径加载方案，避免引入新资源依赖。
    /// 之前的版本以 health TMP 的 anchoredPosition 为参考，导致头像出现在血条中央偏左。
    /// 这里改用 life.rectTransform 当参考，明确"最左 + 血条上方"。
    /// </summary>
    private void EnsureCharacterPanel()
    {
        // 优先以血条 life 为参考；life 缺失时退回到 health 父级（保留旧行为兜底）
        RectTransform anchorRt = (life != null) ? life.rectTransform : (health != null ? health.rectTransform : null);
        if (anchorRt == null) return;
        Transform parent = anchorRt.parent;
        if (parent == null) return;

        // 已经创建过就跳过（场景重载或重复 Start 都安全）
        Transform existing = parent.Find("CharacterAvatarPanel");
        if (existing != null)
        {
            _charAvatarImage      = existing.Find("Avatar")?.GetComponent<Image>();
            _charAvatarBackground = existing.Find("AvatarBackground")?.GetComponent<Image>();
            _charUpgradeText      = existing.Find("UpgradeText")?.GetComponent<TextMeshProUGUI>();
            return;
        }

        // 容器：与血条同 anchor，pivot=(0,0)（左下角），位置定在血条的"左上角 + 4px 间距"
        // 这样面板整体落在血条上方、且与血条最左侧对齐。
        const float AVATAR_SIZE   = 56f;
        const float TEXT_HEIGHT   = 20f;
        const float GAP_TO_BAR    = 4f;     // 头像底与血条顶之间的留空
        const float PANEL_W       = AVATAR_SIZE;
        const float PANEL_H       = AVATAR_SIZE + TEXT_HEIGHT;

        var panel = new GameObject("CharacterAvatarPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        var prt = (RectTransform)panel.transform;
        // 沿用血条的 anchor（确保跟着血条一起在屏幕角落，不受分辨率变化影响）
        prt.anchorMin = anchorRt.anchorMin;
        prt.anchorMax = anchorRt.anchorMax;
        prt.pivot     = new Vector2(0f, 0f); // 左下：面板的"左下角"对齐到血条的"左上角"
        prt.sizeDelta = new Vector2(PANEL_W, PANEL_H);

        // 计算"血条的左上角"在父容器局部坐标里的 anchoredPosition
        // 血条左上角相对于其自身 pivot 的偏移是 ( -pivot.x*w, (1-pivot.y)*h )，
        // 加上血条 anchoredPosition 即得在父级下的局部坐标。
        Vector2 lifeAnchored = anchorRt.anchoredPosition;
        Vector2 lifeSize     = anchorRt.rect.size;
        Vector2 lifePivot    = anchorRt.pivot;
        Vector2 lifeTopLeft = new Vector2(
            lifeAnchored.x - lifePivot.x * lifeSize.x,
            lifeAnchored.y + (1f - lifePivot.y) * lifeSize.y
        );
        // 面板的 anchoredPosition：让面板左下角放到血条左上角再上抬 GAP
        prt.anchoredPosition = new Vector2(lifeTopLeft.x, lifeTopLeft.y + GAP_TO_BAR);

        // 1) 底片 Image（先添加 → 渲染在最底层）。深色半透明，略大于头像形成"边框"效果。
        var bgGo = new GameObject("AvatarBackground", typeof(RectTransform));
        bgGo.transform.SetParent(panel.transform, false);
        var brt = (RectTransform)bgGo.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.sizeDelta = new Vector2(AVATAR_SIZE + 6f, AVATAR_SIZE + 6f); // 比头像各边大 3px
        brt.anchoredPosition = new Vector2(0f, 3f); // 上移 3px 让头像位于框中央
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f); // 深色半透明
        bg.raycastTarget = false;
        _charAvatarBackground = bg;

        // 2) 头像 Image（后添加 → 在底片上层）
        var avatarGo = new GameObject("Avatar", typeof(RectTransform));
        avatarGo.transform.SetParent(panel.transform, false);
        var art = (RectTransform)avatarGo.transform;
        art.anchorMin = art.anchorMax = new Vector2(0.5f, 1f);
        art.pivot = new Vector2(0.5f, 1f);
        art.sizeDelta = new Vector2(AVATAR_SIZE, AVATAR_SIZE);
        art.anchoredPosition = Vector2.zero;
        var img = avatarGo.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        _charAvatarImage = img;

        // 3) 升级进度文本（头像下方，占满面板底部一行）
        var textGo = new GameObject("UpgradeText", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = new Vector2(0f, 0f);
        trt.anchorMax = new Vector2(1f, 0f);
        trt.pivot = new Vector2(0.5f, 0f);
        trt.sizeDelta = new Vector2(0f, TEXT_HEIGHT);
        trt.anchoredPosition = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(1f, 0.92f, 0.6f, 1f);
        tmp.text = "0/0";
        tmp.raycastTarget = false;
        // 复用 health TMP 的 font，确保中文/像素字体一致
        if (health != null && health.font != null) tmp.font = health.font;
        _charUpgradeText = tmp;

        // 立刻刷新一次，避免第一帧空白
        RefreshCharacterPanel(forceRefreshSprite: true);
    }

    private void RefreshCharacterPanel(bool forceRefreshSprite = false)
    {
        if (_charAvatarImage == null || _charUpgradeText == null) return;

        // 1) 皮肤切换检测——仅在皮肤变化时重新加载贴图
        int skin = PlayerPrefs.GetInt("SelectedSkin", 0);
        if (forceRefreshSprite || skin != _charAppliedSkin)
        {
            Sprite sp = LoadCharacterAvatarSprite(skin);
            _charAvatarImage.sprite = sp;
            _charAvatarImage.enabled = sp != null;
            _charAppliedSkin = sp != null ? skin : -1;
        }

        // 2) 升级进度文本：n = 所有 player 属性升级 group 的累计次数；max = 它们的上限总和（含装备/门挑战加成）
        int curTotal = 0;
        int maxTotal = 0;
        if (ChoiceUI.Instance != null)
        {
            ComputePlayerUpgradeProgress(out curTotal, out maxTotal);
        }
        if (maxTotal > 0)
        {
            _charUpgradeText.text = curTotal + "/" + maxTotal;
        }
        else
        {
            _charUpgradeText.text = "";
        }
    }

    /// <summary>遍历 ChoiceUI.upplayer 列表，求所有玩家属性升级组的当前次数 / 有效上限总和。</summary>
    private void ComputePlayerUpgradeProgress(out int cur, out int max)
    {
        cur = 0;
        max = 0;
        var cui = ChoiceUI.Instance;
        if (cui == null || cui.upplayer == null) return;

        var seenGroups = new HashSet<string>();
        foreach (var go in cui.upplayer)
        {
            if (go == null) continue;
            var opt = go.GetComponent<Upgradeoptionsbase>();
            if (opt == null) continue;
            if (string.IsNullOrEmpty(opt.upgradeGroup) || opt.maxUpgrades <= 0) continue;
            // 同一 group 只计一次（避免有多份 prefab 共享 group 时上限被重复加）
            if (!seenGroups.Add(opt.upgradeGroup)) continue;

            cur += cui.GetGroupCount(opt.upgradeGroup);
            max += cui.GetEffectiveMaxUpgrades(opt);
        }
    }

    /// <summary>
    /// 加载当前皮肤对应的头像 Sprite。
    /// 改造（2026-06）：原本 Application.dataPath + File.IO 在 Build 后必然失败（dataPath 不指向 Assets）。
    /// 现在通过 RuntimeAssetLoader 三层兜底：
    ///   1. 尝试从场景里 SkinChanger 节点上的 Inspector Texture 字段拿引用（Build 中唯一可靠路径）；
    ///   2. 兜底走 Resources.Load（如果资源放到 Resources/）；
    ///   3. 最后才退到旧的 dataPath 文件读取（仅 UNITY_EDITOR 有效）。
    /// 失败返回 null，外层会自动 disable Image。
    /// </summary>
    private static Sprite LoadCharacterAvatarSprite(int skinId)
    {
        const string CirnoIconPath = "像素幸存者资源包/玩家/琪诺露/闲置/1.png";
        const string Ur0IconPath  = "像素幸存者资源包/玩家/ur0_wind_skin.png";
        const string Ur1IconPath  = "像素幸存者资源包/玩家/ur1_fire_skin.png";
        const string Ur2IconPath  = "像素幸存者资源包/玩家/ur2_tomb_skin.png";

        // 之前 skin==3（无罪）会落入 else 走 Ur1 → 在血条左上角显示成"夏无（地狱火）"的头像，
        // 这就是用户反馈"无罪人物存档缩略图有问题"的根因。补上专属分支。
        string rel;
        switch (skinId)
        {
            case 0:  rel = CirnoIconPath; break;
            case 1:  rel = Ur0IconPath;   break;
            case 2:  rel = Ur1IconPath;   break;
            case 3:  rel = Ur2IconPath;   break;
            default: rel = CirnoIconPath; break;
        }

        // 1) 尝试从场景里的 SkinChanger 拿到 Inspector 引用（最可靠）
        Texture2D directRef = null;
        var skinChanger = Object.FindObjectOfType<SkinChanger>();
        if (skinChanger != null)
        {
            switch (skinId)
            {
                case 0: directRef = skinChanger.cirnoIconTexture; break;
                case 1: directRef = skinChanger.ur0IconTexture;   break;
                case 2: directRef = skinChanger.ur1IconTexture;   break;
                case 3: directRef = skinChanger.ur2IconTexture;   break;
            }
        }
        // 2) 也尝试从 PlayerSkinOverrider 拿（场景里通常都挂着，且 UR 三张直接绑了贴图）
        if (directRef == null && skinId != 0)
        {
            var skinOverrider = Object.FindObjectOfType<PlayerSkinOverrider>();
            if (skinOverrider != null)
            {
                switch (skinId)
                {
                    case 1: directRef = skinOverrider.ur0Texture; break;
                    case 2: directRef = skinOverrider.ur1Texture; break;
                    case 3: directRef = skinOverrider.ur2Texture; break;
                }
            }
        }

        var tex = RuntimeAssetLoader.LoadTexture(directRef, null, rel);
        if (tex == null) return null;
        try
        {
            // skinId=0 单图整张；UR 系列（1/2/3）是 4×3 网格，取顶行第 1 帧（idle 站姿）
            if (skinId == 0)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                int cellW = tex.width / 4;
                int cellH = tex.height / 3;
                int y = cellH * 2;
                return Sprite.Create(tex, new Rect(0, y, cellW, cellH),
                                     new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// 永续技能（无 CD、CDtime 极大）SkillUI 的"循环旋转标志"组件。
/// 视觉：挂在一个空容器上，容器子节点是 4 个沿圆周分布的青绿色小方块，
///   组件让容器整体旋转 → 视觉上是"4 个亮点沿外圈做 loading-dots 循环"，
///   明确告诉玩家"这个技能在持续生效"，且不会出现"一个矩形块在动"的怪异观感。
/// 颜色脉动：青绿↔淡紫之间过渡，强化"亡灵"主题，与"满格冷却"完全区分开。
/// 完全独立于游戏逻辑、不读取任何技能状态——只要 GameObject 存在就一直转。
/// </summary>
public class EternalBorderSpinner : MonoBehaviour
{
    [Tooltip("每秒旋转角度（默认 90 = 4 秒一圈，配合 4 个 dot 形成稳健 loading-spinner 节奏）")]
    public float degreesPerSecond = 90f;
    [Tooltip("挂载的单一 Image（兼容旧代码，置 null 时使用 applyToChildren）")]
    public UnityEngine.UI.Image image;
    [Tooltip("将颜色脉动应用到所有子物体上的 Image（4 个 dot 方案）")]
    public bool applyToChildren = false;

    [Header("颜色脉动")]
    [Tooltip("脉动主色（亡灵幽光感的青绿）")]
    public Color colorA = new Color(0.45f, 1f, 0.85f, 0.9f);
    [Tooltip("脉动副色（暗示亡者主题的淡紫）")]
    public Color colorB = new Color(0.75f, 0.55f, 1f, 0.9f);
    [Tooltip("脉动周期（秒）")]
    public float pulsePeriod = 1.6f;

    private UnityEngine.UI.Image[] _childImagesCache;

    void Update()
    {
        // 不受 Time.timeScale 影响（暂停时也转，避免玩家以为技能停了）→ 用 unscaledDeltaTime
        // 战斗中倍速时也保持恒定节奏，避免倍速下视觉过于刺眼。
        float dt = Time.unscaledDeltaTime;
        transform.Rotate(0f, 0f, -degreesPerSecond * dt);

        if (pulsePeriod > 0f)
        {
            // 0~1 三角波（节奏比 sin 更鲜明）
            float t = (Time.unscaledTime % pulsePeriod) / pulsePeriod;
            float k = t < 0.5f ? (t * 2f) : (2f - t * 2f);
            Color c = Color.Lerp(colorA, colorB, k);

            if (image != null) image.color = c;

            if (applyToChildren)
            {
                // 缓存子 Image 列表（不变化 → 只取一次）
                if (_childImagesCache == null)
                    _childImagesCache = GetComponentsInChildren<UnityEngine.UI.Image>(true);
                for (int i = 0; i < _childImagesCache.Length; i++)
                {
                    if (_childImagesCache[i] != null)
                        _childImagesCache[i].color = c;
                }
            }
        }
    }
}
