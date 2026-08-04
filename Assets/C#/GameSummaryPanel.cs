using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏总结面板：对局结束后显示，含 4 页翻页 + 圆点指示器 + 返回按钮。
/// 完全静态化：所有 UI 元素通过 Inspector 拖入，脚本只负责显示 + 内容刷新 + 翻页。
/// 首次使用请右键组件 → "生成默认面板" 一键在场景中构造完整层级。
/// </summary>
public class GameSummaryPanel : MonoBehaviour
{
    public static GameSummaryPanel Instance { get; private set; }

    [Header("─── UI 引用（在 Inspector 中拖入）───")]
    [Tooltip("面板根节点")]
    public GameObject panelRoot;
    [Tooltip("全屏遮罩（阻止点击穿透）")]
    public GameObject blocker;
    [Tooltip("面板背景 Image（用于绑定 AI 素材）")]
    public Image panelBackgroundImage;

    [Header("─── 4 个页面（顺序：概览/技能伤害/首领/装备）───")]
    public GameObject[] pages = new GameObject[4];
    [Tooltip("每页的内容 TMP，索引对应 pages")]
    public TextMeshProUGUI[] pageContents = new TextMeshProUGUI[4];

    [Header("─── 翻页 & 返回按钮 ───")]
    public Button prevButton;
    public Button nextButton;
    public Button returnButton;

    [Header("─── 圆点指示器 ───")]
    public Image[] dotImages = new Image[4];
    public Sprite dotEmptySprite;
    public Sprite dotFilledSprite;

    [Header("─── AI 素材（Editor 一键生成时自动绑定）───")]
    public Sprite bannerSprite;
    public Sprite buttonSprite;
    public Sprite dotsSpriteSheet; // 圆点合集图（未拆分则用作按钮背景等）

    // ── 运行时状态 ──
    private int _currentPage;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        // 默认全部隐藏
        if (panelRoot != null) panelRoot.SetActive(false);
        if (blocker != null) blocker.SetActive(false);

        // 绑定按钮事件
        if (prevButton != null) prevButton.onClick.AddListener(PrevPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);
        if (returnButton != null) returnButton.onClick.AddListener(OnReturnToMenu);

        // 应用 AI 素材
        ApplyBackgroundSprite();
    }

    private void ApplyBackgroundSprite()
    {
        // 【强制覆盖】无论 Inspector 里挂了什么，一律用新简约背景
        var newBg = Resources.Load<Sprite>("UI/PanelBg_Summary");
        if (newBg == null) return;
        bannerSprite = newBg;

        if (panelRoot == null) return;

        // 1) 根节点 Image 强制覆盖
        var rootImg = panelRoot.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = newBg;
            rootImg.type = Image.Type.Sliced;
            rootImg.color = Color.white;
            panelBackgroundImage = rootImg;
        }

        // 2) 遍历 panelRoot 下所有名字疑似背景/边框的 Image，全部换成新简约背景，
        //    彻底清除旧的紫底金框盾徽装饰层。
        var allImgs = panelRoot.GetComponentsInChildren<Image>(true);
        foreach (var im in allImgs)
        {
            if (im == null) continue;
            var n = im.gameObject.name.ToLower();
            if (n.Contains("bg") || n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("border") || n.Contains("banner") || n.Contains("deco"))
            {
                im.sprite = newBg;
                im.type = Image.Type.Sliced;
                im.color = Color.white;
            }
        }
    }

    // ── 公开方法：显示面板 ──

    public void Show()
    {
        if (panelRoot == null)
        {
            Debug.LogError("[GameSummaryPanel] panelRoot 未绑定！请在 Inspector 中拖入或右键组件 → 生成默认面板。直接返回主菜单");
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 【2026-08 修复】防重复显示。
        //   双 Boss 关卡 / 玩家死亡与 Boss 同帧倒下等情况下，battleUI 可能连续调用两次 Show()，
        //   旧版会重复 SetAsLastSibling + 重复把 timeScale 置 0，并把 _currentPage 强制拉回 0
        //   —— 表现为"玩家刚翻到第 3 页，面板自己跳回第 1 页"。
        if (panelRoot.activeSelf)
        {
            Debug.Log("[GameSummaryPanel] 面板已显示，忽略重复 Show()");
            return;
        }

        // 【2026-08 修复】兜底 Finalize。
        //   若某条结束路径（例如 GateChallenge 异常退出）忘了调 FinalizeSession，
        //   sessionEndTime 会保持 0 → DurationSeconds 返回 0 → 用时显示"0秒"、DPS 为 0。
        //   这里检测到未结算就地补一次，保证面板数据永不为空。
        var tk = GameSessionTracker.Instance;
        if (tk != null && tk.sessionEndTime <= 0f)
        {
            Debug.LogWarning("[GameSummaryPanel] 检测到 session 未 Finalize，就地补结算");
            tk.FinalizeSession(tk.isVictory, string.IsNullOrEmpty(tk.difficultyPlayed)
                ? (DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.label : "??")
                : tk.difficultyPlayed, tk.playerFinalLevel);
        }

        _currentPage = 0;

        // 【关键】显示前先纠正字体，杜绝中文乱码
        FixChineseFontOnAllTMP();

        // 每次显示都重新应用背景（防止 Inspector 挂着旧素材）
        ApplyBackgroundSprite();

        // 【2026-08 修复】必须先激活再做布局。
        //   RelayoutBottomBar / FixPagePadding / AutoScaleFontSizes 都依赖
        //   RectTransform.rect 的真实尺寸，而 Unity 对**未激活**对象不保证 rect 已完成
        //   一次 layout pass —— 旧版在 SetActive(true) 之前调 AutoScaleFontSizes，
        //   首次打开面板时 rect.size 可能读到 (0,0) 导致函数直接 return，
        //   字号/内边距完全没生效（表现为"第一次打开排版错乱，关掉再开就正常了"）。
        //
        // 【顺序至关重要】blocker 必须先置顶，panelRoot 后置顶。
        //   blocker 是一张铺满全屏的半透明黑色 Image（带 raycast），作用是"挡住背后的
        //   战斗 UI，不让玩家点到"。若反过来把 blocker 放在 panelRoot 之后，
        //   它就盖在面板正上方—— 整个结算面板会被蒙上一层灰、且所有按钮点不动。
        //   （这正是"页面无法点击 + 不知名遮罩"的原因。）
        if (blocker != null)
        {
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
        }

        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);

        // 强制立即完成一次布局计算，让下面读到的 rect 一定是最终尺寸
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot.transform as RectTransform);

        // 布局校正顺序：先重排控件 → 再算内边距 → 最后定字号。
        // AutoScaleFontSizes 放最后，因为它依赖前两步确定下来的可用区域。
        RelayoutBottomBar();
        FixPagePadding();
        AutoScaleFontSizes();

        RefreshAllPages();
        ShowPage(0);

        Time.timeScale = 0f;
        Debug.Log("[GameSummaryPanel] 面板已显示");
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (blocker != null) blocker.SetActive(false);
    }

    private void ShowPage(int pageIndex)
    {
        // 【2026-08 修复】pages 可能未绑定或长度为 0（Inspector 漏拖 / 数组被清空），
        //   旧版 `Mathf.Clamp(pageIndex, 0, pages.Length - 1)` 在 Length==0 时会得到
        //   Clamp(x, 0, -1) = -1，随后 dotImages 循环里用 -1 比较虽不崩，
        //   但 prevButton/nextButton 的 interactable 判断会得到荒谬结果（末页永远可点）。
        if (pages == null || pages.Length == 0)
        {
            Debug.LogError("[GameSummaryPanel] pages 未绑定，无法翻页");
            return;
        }

        _currentPage = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == _currentPage);

        // 圆点数量可能与页数不一致（例如页数改成 4 但场景里只有 3 个圆点），
        // 用 Min 取交集，避免 IndexOutOfRange。
        int dotCount = dotImages != null ? dotImages.Length : 0;
        for (int i = 0; i < dotCount; i++)
        {
            if (dotImages[i] == null) continue;

            bool active = (i == _currentPage);
            // 超出页数的多余圆点直接隐藏，而不是显示成"永远的空点"
            dotImages[i].gameObject.SetActive(i < pages.Length);

            dotImages[i].sprite = active ? dotFilledSprite : dotEmptySprite;
            // 若未绑定 sprite，则用颜色区分；即使绑定了也统一刷一次 alpha，
            // 让当前页圆点在两种模式下都明显高亮。
            dotImages[i].color = active
                ? new Color(1f, 0.85f, 0.4f, 1f)
                : new Color(1f, 1f, 1f, 0.35f);
        }

        // 边界按钮：首页禁用上一页，末页禁用下一页
        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pages.Length - 1;
    }

    private void PrevPage() { ShowPage(_currentPage - 1); }
    private void NextPage() { ShowPage(_currentPage + 1); }

    /// <summary>
    /// 【2026-08 新增】键盘/滚轮翻页，提升交互体验。
    /// 面板显示时 timeScale=0，所以必须用 Update + unscaled 输入（Input 不受 timeScale 影响）。
    /// </summary>
    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) PrevPage();
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) NextPage();

        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0.1f) PrevPage();
        else if (scroll < -0.1f) NextPage();
    }

    private void OnReturnToMenu()
    {
        // 【2026-08 修复】防重入。timeScale=0 下按钮仍可被连点，
        //   旧版会连续触发多次 LoadScene，在低端机上表现为"点返回后卡住/黑屏"。
        if (_returning) return;
        _returning = true;

        Debug.Log("[GameSummaryPanel] 返回主菜单");
        Time.timeScale = 1f;

        // 隐藏自身，避免场景重载过渡帧里旧面板闪现
        Hide();

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private bool _returning;

    // ── 刷新各页 ──

    private void RefreshAllPages()
    {
        var tracker = GameSessionTracker.Instance;
        if (tracker == null) return;
        RefreshPage0(tracker);
        RefreshPage1(tracker);
        RefreshPage2(tracker);
        RefreshPage3(tracker);
    }

    private void RefreshPage0(GameSessionTracker t)
    {
        var tmp = GetPageContent(0);
        if (tmp == null) return;

        string s = $"难度：<color=#FFD24A>{Esc(t.difficultyPlayed)}</color>    "
                 + $"结果：<color=#{(t.isVictory ? "80FF80>胜利" : "FF6060>失败")}</color>\n"
                 + $"用时：<color=#C0C0FF>{FormatDuration(t.DurationSeconds)}</color>    "
                 + $"等级：<color=#FFD24A>Lv.{t.playerFinalLevel}</color>\n"
                 + "<color=#404050>────────────────────</color>\n"
                 + $"总伤害：<color=#FF6060>{FormatNumber(t.TotalDamage())}</color>    "
                 + $"DPS：<color=#FF8060>{FormatNumber(t.DPS)}</color>\n"
                 + $"最高单击：<color=#FFA040>{FormatNumber(t.maxSingleHit)}</color>    "
                 + $"击杀数：<color=#FFD24A>{t.totalKills}</color>\n"
                 + $"承受伤害：<color=#FF80A0>{FormatNumber(t.damageTaken)}</color>    "
                 + $"治疗量：<color=#80FF80>{FormatNumber(t.totalHealing)}</color>\n"
                 + "<color=#404050>────────────────────</color>\n"
                 + $"技能数：<color=#80FFC0>{t.skillsAcquired.Count}</color>    "
                 + $"首领：<color=#FFD24A>{t.bossesDefeated.Count}</color>    "
                 + $"新装备：<color=#FF80C0>{t.equipmentUnlockedThisSession.Count}</color>\n"
                 + $"源木：<color=#C0A060>{FormatNumber(t.woodCollected)}</color>";

        // 亡者领域专项：仅在本局真的复活过友军时才显示，避免没学该技能的玩家看到 0 行
        if (t.alliesRevived > 0)
            s += $"    复活友军：<color=#C080FF>{t.alliesRevived}</color>";

        tmp.text = s;
    }

    private void RefreshPage1(GameSessionTracker t)
    {
        var tmp = GetPageContent(1);
        if (tmp == null) return;

        var sorted = t.GetSortedSkillDamage();
        if (sorted.Count == 0) { tmp.text = "<color=#888>本局未造成任何伤害。</color>"; return; }

        float total = t.TotalDamage();

        // 【2026-08 修复】旧版把全部技能一次性拼进单个 TMP，技能多时（一局可到 10+ 个）
        //   会溢出面板底部且没有滚动条 —— 表现为"排名靠后的技能看不见"。
        //   现在限制最多 8 行，剩余合并为"其它 N 项"，保证任意面板尺寸下都能完整显示。
        const int MaxRows = 8;
        var sb = new System.Text.StringBuilder(512);
        int rank = 1;
        float shownSum = 0f;

        for (int i = 0; i < sorted.Count && rank <= MaxRows; i++, rank++)
        {
            var kv = sorted[i];
            float pct = total > 0f ? kv.Value / total * 100f : 0f;
            shownSum += kv.Value;
            sb.Append(BuildDamageRow(rank, kv.Key, kv.Value, pct));
        }

        int rest = sorted.Count - (rank - 1);
        if (rest > 0)
        {
            float restVal = total - shownSum;
            float restPct = total > 0f ? restVal / total * 100f : 0f;
            sb.Append($"<color=#888>其它 {rest} 项  {FormatNumber(restVal)} ({restPct:F1}%)</color>");
        }

        tmp.text = sb.ToString().TrimEnd();
    }

    /// <summary>
    /// 构造一行技能伤害。用 Unicode 方块字符画进度条（比旧版 '|' 更整齐、宽度稳定），
    /// 并把"技能名 / 数值 / 百分比 / 条"压进两行，让一页能容纳更多技能。
    /// </summary>
    private static string BuildDamageRow(int rank, string skillName, float value, float pct)
    {
        // 【修复】pct 可能是 NaN（total 为 0 时的除法已规避，但字典脏数据仍可能导致）。
        // Mathf.RoundToInt(NaN) 在部分平台返回 int.MinValue，会让 new string(c, len) 抛异常
        // → 结算面板整体白屏。这里显式钳制。
        if (float.IsNaN(pct) || float.IsInfinity(pct)) pct = 0f;
        pct = Mathf.Clamp(pct, 0f, 100f);

        const int BarSlots = 20;
        int filled = Mathf.Clamp(Mathf.RoundToInt(pct / 100f * BarSlots), 0, BarSlots);
        string bar = new string('█', filled) + new string('─', BarSlots - filled);

        return $"<color=#FFD24A>{rank}.</color> {Esc(skillName)}  "
             + $"<color=#FF6060>{FormatNumber(value)}</color> <color=#888>({pct:F1}%)</color>\n"
             + $"<color=#00D0A0>{bar}</color>\n";
    }

    private void RefreshPage2(GameSessionTracker t)
    {
        var tmp = GetPageContent(2);
        if (tmp == null) return;

        if (t.bossesDefeated.Count == 0)
        {
            tmp.text = "<color=#888>本局未击败任何首领。</color>";
            return;
        }

        var sb = new System.Text.StringBuilder(256);
        for (int i = 0; i < t.bossesDefeated.Count; i++)
            sb.Append($"<color=#FFD24A>{i + 1}.</color> {Esc(t.bossesDefeated[i])}\n");
        tmp.text = sb.ToString().TrimEnd();
    }

    private void RefreshPage3(GameSessionTracker t)
    {
        var tmp = GetPageContent(3);
        if (tmp == null) return;

        var sb = new System.Text.StringBuilder(512);

        // 【2026-08 改进】旧版第 4 页只列"解锁装备"，本局获得的技能列表完全没有任何一页展示
        //   （概览页只显示一个数字）。这里把「本局技能」并入本页，信息更完整。
        if (t.skillsAcquired.Count > 0)
        {
            sb.Append("<color=#80FFC0>【本局技能】</color>\n");
            for (int i = 0; i < t.skillsAcquired.Count; i++)
            {
                sb.Append(Esc(t.skillsAcquired[i]));
                sb.Append(i < t.skillsAcquired.Count - 1 ? "、" : "\n");
            }
            sb.Append('\n');
        }

        sb.Append("<color=#FF80C0>【新解锁装备】</color>\n");
        if (t.equipmentUnlockedThisSession.Count == 0)
        {
            sb.Append("<color=#888>本局未解锁新装备。</color>");
        }
        else
        {
            for (int i = 0; i < t.equipmentUnlockedThisSession.Count; i++)
                sb.Append($"<color=#FFD24A>{i + 1}.</color> <color=#FF80C0>{Esc(t.equipmentUnlockedThisSession[i])}</color>\n");
        }

        tmp.text = sb.ToString().TrimEnd();
    }

    private TextMeshProUGUI GetPageContent(int pageIndex)
    {
        if (pageContents != null && pageIndex < pageContents.Length && pageContents[pageIndex] != null)
            return pageContents[pageIndex];
        if (pages == null || pageIndex >= pages.Length || pages[pageIndex] == null) return null;
        var tmps = pages[pageIndex].GetComponentsInChildren<TextMeshProUGUI>(true);
        return tmps.Length >= 2 ? tmps[1] : (tmps.Length > 0 ? tmps[0] : null);
    }

    /// <summary>
    /// 转义 TMP 富文本标记。
    /// 【修复】技能名 / 装备名 / Boss 名都来自数据，若其中含有 '&lt;' 会被 TMP 当成标签解析，
    /// 导致该行整体消失或后续文本全部变色 —— 这是"结算面板偶尔少一行"的隐蔽原因。
    /// </summary>
    private static string Esc(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        return raw.Replace("<", "&lt;");
    }

    /// <summary>格式化时长，超过 1 小时显示"时分秒"。</summary>
    private static string FormatDuration(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) seconds = 0f;
        int total = Mathf.RoundToInt(seconds);
        int h = total / 3600, m = (total % 3600) / 60, s = total % 60;
        if (h > 0) return $"{h}时{m}分{s}秒";
        if (m > 0) return $"{m}分{s}秒";
        return $"{s}秒";
    }

    private static string FormatNumber(float n)
    {
        // 【修复】NaN / Infinity 会直接被 ToString 输出为 "NaN"/"∞"，
        // 且 n >= 1_000_000_000 的比较对 NaN 恒为 false，会落到最后一行输出 "NaN"。
        if (float.IsNaN(n) || float.IsInfinity(n)) return "0";
        if (n < 0f) n = 0f;
        if (n >= 1_000_000_000) return $"{n / 1_000_000_000f:F1}B";
        if (n >= 1_000_000) return $"{n / 1_000_000f:F1}M";
        if (n >= 1_000) return $"{n / 1_000f:F1}K";
        return n.ToString("F0");
    }

#if UNITY_EDITOR
    // ── 一键生成默认面板（右键组件 → 生成默认面板）──

    [ContextMenu("生成默认面板")]
    private void BuildDefaultPanelInEditor()
    {
        Canvas canvas = FindCanvasInScene();
        if (canvas == null) { Debug.LogError("[GameSummaryPanel] 场景中没有 Canvas！"); return; }

        // 清理旧对象
        if (panelRoot != null) DestroyImmediate(panelRoot);
        if (blocker != null) DestroyImmediate(blocker);

        // 加载 AI 素材
        TryLoadAssets();

        // ---- 全屏遮罩 ----
        blocker = new GameObject("SummaryBlocker", typeof(RectTransform), typeof(Image));
        blocker.transform.SetParent(canvas.transform, false);
        var blkRt = (RectTransform)blocker.transform;
        blkRt.anchorMin = Vector2.zero; blkRt.anchorMax = Vector2.one;
        blkRt.sizeDelta = Vector2.zero;
        blkRt.anchoredPosition = Vector2.zero;
        var blkImg = blocker.GetComponent<Image>();
        blkImg.color = new Color(0f, 0f, 0f, 0.7f);
        blkImg.raycastTarget = true;
        blocker.SetActive(false);

        // ---- 面板根 ----
        panelRoot = new GameObject("SummaryPanel_Root", typeof(RectTransform), typeof(Image));
        panelRoot.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)panelRoot.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(720f, 560f);
        rt.anchoredPosition = Vector2.zero;

        panelBackgroundImage = panelRoot.GetComponent<Image>();
        panelBackgroundImage.color = new Color(0.07f, 0.06f, 0.16f, 1f);
        panelBackgroundImage.raycastTarget = true;
        if (bannerSprite != null)
        {
            panelBackgroundImage.sprite = bannerSprite;
            panelBackgroundImage.type = Image.Type.Sliced;
        }

        // ---- 标题栏 ----
        var titleGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
        titleGo.transform.SetParent(rt, false);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, TitleBarHeight);
        trt.anchoredPosition = Vector2.zero;

        var titleBg = titleGo.GetComponent<Image>();
        titleBg.color = new Color(0.1f, 0.08f, 0.24f, 1f);
        if (bannerSprite != null) { titleBg.sprite = bannerSprite; titleBg.type = Image.Type.Sliced; }
        titleBg.raycastTarget = false;

        var titleTxtGo = new GameObject("TitleText", typeof(RectTransform));
        titleTxtGo.transform.SetParent(trt, false);
        var titleTxt = titleTxtGo.AddComponent<TextMeshProUGUI>();
        var titleTxtRt = (RectTransform)titleTxtGo.transform;
        titleTxtRt.anchorMin = Vector2.zero; titleTxtRt.anchorMax = Vector2.one;
        titleTxtRt.sizeDelta = Vector2.zero;
        titleTxt.text = "对局总结";
        titleTxt.fontSize = 30;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = new Color(1f, 0.88f, 0.4f);
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.raycastTarget = false;
        // 清除 heiti SDF 自带的外描边（黑边）
        titleTxt.outlineWidth = 0f;
        titleTxt.outlineColor = Color.clear;
        // 阴影也去掉
        titleTxt.fontSharedMaterial = null; // 防止从 material 继承 outline
        TryAssignFont(titleTxt);

        // ---- 页面容器 ----
        var pageContainerGo = new GameObject("PageContainer", typeof(RectTransform));
        pageContainerGo.transform.SetParent(rt, false);
        var pcRt = (RectTransform)pageContainerGo.transform;
        pcRt.anchorMin = new Vector2(0f, BottomBarHeightRatio); pcRt.anchorMax = new Vector2(1f, 1f);
        pcRt.offsetMin = new Vector2(24f, 6f);
        pcRt.offsetMax = new Vector2(-24f, -(TitleBarHeight + 6f));

        // ---- 4 页 ----
        string[] pageTitles = { "对局概览", "技能伤害", "击败首领", "解锁装备" };
        pages = new GameObject[4];
        pageContents = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
            CreatePage(i, pageTitles[i], pcRt);

        // ---- 底栏 ----
        // 高度与 RelayoutBottomBar 的 BottomBarHeightRatio 保持一致（两行布局）
        var bottomGo = new GameObject("BottomBar", typeof(RectTransform));
        bottomGo.transform.SetParent(rt, false);
        var brt = (RectTransform)bottomGo.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, BottomBarHeightRatio);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        // 注意：这里的坐标只是"编辑器里能看"的初值，
        // 运行时 Show() 会调用 RelayoutBottomBar() 按实际面板尺寸重排。
        // 第一行：上一页（左）/ 下一页（右），圆点居中
        prevButton = CreateButton("BtnPrev", brt,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(20f, 18f), new Vector2(110f, 40f), "< 上一页");

        nextButton = CreateButton("BtnNext", brt,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-20f, 18f), new Vector2(110f, 40f), "下一页 >");

        // 第二行：返回主菜单，独立居中（主操作强调）
        returnButton = CreateButton("BtnReturn", brt,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, -20f), new Vector2(180f, 42f), "返回主菜单");
        // 返回按钮特殊配色（绿）
        var retImg = returnButton.GetComponent<Image>();
        if (retImg != null) retImg.color = new Color(0.20f, 0.55f, 0.30f, 1f);

        // 圆点指示器（与翻页按钮同行、居中）
        var dotsGo = new GameObject("Dots", typeof(RectTransform));
        dotsGo.transform.SetParent(brt, false);
        var drt = (RectTransform)dotsGo.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(200f, 20f);
        drt.anchoredPosition = new Vector2(0f, 18f);

        dotImages = new Image[4];
        float dotSize = 16f, dotSpace = 32f;
        float startX = -(4 - 1) * dotSpace / 2f;
        for (int i = 0; i < 4; i++)
        {
            var dGo = new GameObject($"Dot_{i}", typeof(RectTransform), typeof(Image));
            dGo.transform.SetParent(drt, false);
            var dRt = (RectTransform)dGo.transform;
            dRt.anchorMin = dRt.anchorMax = new Vector2(0.5f, 0.5f);
            dRt.pivot = new Vector2(0.5f, 0.5f);
            dRt.sizeDelta = new Vector2(dotSize, dotSize);
            dRt.anchoredPosition = new Vector2(startX + i * dotSpace, 0f);
            var img = dGo.GetComponent<Image>();
            img.raycastTarget = false;
            // 默认无 sprite，用颜色区分（Start 里会应用 dotFilledSprite/dotEmptySprite）
            img.color = (i == 0) ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(1f, 1f, 1f, 0.35f);
            dotImages[i] = img;
        }

        panelRoot.SetActive(false);
        blocker.SetActive(false);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(panelRoot);
        Debug.Log("[GameSummaryPanel] 默认面板已生成，可在 Scene 中调整。若 AI 素材尚未导入为 Sprite，请检查纹理类型。");
    }

    private void CreatePage(int index, string pageTitle, RectTransform container)
    {
        var go = new GameObject($"Page_{index}_{pageTitle}", typeof(RectTransform));
        go.transform.SetParent(container, false);
        var prt = (RectTransform)go.transform;
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;

        // 页标题
        var ptGo = new GameObject("PageTitle", typeof(RectTransform));
        ptGo.transform.SetParent(prt, false);
        var ptRt = (RectTransform)ptGo.transform;
        ptRt.anchorMin = new Vector2(0f, 1f); ptRt.anchorMax = new Vector2(1f, 1f);
        ptRt.pivot = new Vector2(0.5f, 1f);
        ptRt.sizeDelta = new Vector2(0f, 34f);
        ptRt.anchoredPosition = Vector2.zero;
        var ptTmp = ptGo.AddComponent<TextMeshProUGUI>();
        ptTmp.text = pageTitle;
        ptTmp.fontSize = 22;
        ptTmp.alignment = TextAlignmentOptions.Left;
        ptTmp.color = new Color(1f, 0.8f, 0.3f);
        ptTmp.fontStyle = FontStyles.Bold;
        ptTmp.raycastTarget = false;
        TryAssignFont(ptTmp);

        // 内容
        var cGo = new GameObject("Content", typeof(RectTransform));
        cGo.transform.SetParent(prt, false);
        var cRt = (RectTransform)cGo.transform;
        cRt.anchorMin = Vector2.zero; cRt.anchorMax = Vector2.one;
        cRt.offsetMin = new Vector2(6f, 6f);
        cRt.offsetMax = new Vector2(-6f, -40f);
        var cTmp = cGo.AddComponent<TextMeshProUGUI>();
        cTmp.fontSize = 15;
        cTmp.alignment = TextAlignmentOptions.TopLeft;
        cTmp.color = new Color(0.92f, 0.92f, 0.92f);
        cTmp.raycastTarget = false;
        cTmp.lineSpacing = 3f;
        TryAssignFont(cTmp);

        pages[index] = go;
        pageContents[index] = cTmp;
        go.SetActive(index == 0);
    }

    private Button CreateButton(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.28f, 1f);
        img.raycastTarget = true;
        if (buttonSprite != null) { img.sprite = buttonSprite; img.type = Image.Type.Sliced; }

        var lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(rt, false);
        var lRt = (RectTransform)lblGo.transform;
        lRt.anchorMin = Vector2.zero; lRt.anchorMax = Vector2.one;
        lRt.offsetMin = Vector2.zero; lRt.offsetMax = Vector2.zero;
        var lTmp = lblGo.AddComponent<TextMeshProUGUI>();
        lTmp.text = label;
        lTmp.fontSize = 14;
        lTmp.alignment = TextAlignmentOptions.Center;
        lTmp.color = Color.white;
        lTmp.raycastTarget = false;
        TryAssignFont(lTmp);

        return go.GetComponent<Button>();
    }

    private Canvas FindCanvasInScene()
    {
        var bui = FindObjectOfType<battleUI>();
        if (bui != null)
        {
            if (bui.health != null && bui.health.canvas != null) return bui.health.canvas;
            var c = bui.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        return FindObjectOfType<Canvas>();
    }

    private void TryLoadAssets()
    {
        // 【最高优先级】Resources 下的简约背景
        var res = Resources.Load<Sprite>("UI/PanelBg_Summary");
        if (res != null)
        {
            bannerSprite = res;
        }
        else
        {
            // 次优：Simple_clean 系列
            string[] simpleGuids = UnityEditor.AssetDatabase.FindAssets("Simple_clean_pixel_art_UI_pane t:Sprite");
            if (simpleGuids.Length > 0)
            {
                bannerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(simpleGuids[0]));
            }
            else
            {
                // 最后兜底：旧素材
                string[] bannerGuids = UnityEditor.AssetDatabase.FindAssets("pixel_art__dark_fantasy_UI_pan t:Sprite");
                if (bannerGuids.Length > 0)
                    bannerSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(bannerGuids[0]));
            }
        }

        string[] btnGuids = UnityEditor.AssetDatabase.FindAssets("pixel_art_game_UI_button t:Sprite");
        if (btnGuids.Length > 0)
            buttonSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(btnGuids[0]));

        string[] dotGuids = UnityEditor.AssetDatabase.FindAssets("pixel_art__simple_circle_page t:Sprite");
        if (dotGuids.Length > 0)
            dotsSpriteSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(dotGuids[0]));

        Debug.Log($"[GameSummaryPanel] 素材加载: banner={bannerSprite}, button={buttonSprite}, dots={dotsSpriteSheet}");
    }
#endif

    // ── 运行时工具（非 Editor 也需编译）────────────────────────────

    private static TMP_FontAsset _cachedChineseFont;

    private static TMP_FontAsset FindBestFont()
    {
        if (_cachedChineseFont != null) return _cachedChineseFont;

        // 1. 【最高优先级】强制加载 heiti SDF（LiberationSans 不含中文字形，必须避免）
#if UNITY_EDITOR
        var heitiEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/像素幸存者资源包/字体/heiti SDF.asset");
        if (heitiEditor != null) { _cachedChineseFont = heitiEditor; return heitiEditor; }
#endif
        var heitiRes = Resources.Load<TMP_FontAsset>("Fonts/heiti SDF");
        if (heitiRes != null) { _cachedChineseFont = heitiRes; return heitiRes; }

        // 2. 扫描已加载的所有 TMP_FontAsset，按名字匹配中文字体
        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in allFonts)
        {
            if (f == null) continue;
            var n = f.name.ToLower();
            if (n.Contains("heiti") || n.Contains("黑体") || n.Contains("chinese") || n.Contains("cjk"))
            { _cachedChineseFont = f; return f; }
        }

        // 3. 从 battleUI.health 拿
        var bui = FindObjectOfType<battleUI>();
        if (bui != null && bui.health != null && bui.health.font != null)
        {
            _cachedChineseFont = bui.health.font;
            return bui.health.font;
        }

        // 4. 最后兜底：TMP 默认（无中文，会显示口口口，作为极端兜底）
        var def = TMP_Settings.defaultFontAsset;
        if (def != null) return def;
        return null;
    }

    private void TryAssignFont(TextMeshProUGUI tmp)
    {
        var font = FindBestFont();
        if (font != null) tmp.font = font;
    }

    /// <summary>
    /// 遍历面板下所有 TMP 文本，非中文字体一律换成 heiti，杜绝口口口。
    /// 【关键】必须遍历 panelRoot 而非 this.gameObject —— 因为脚本可能挂在 battleUI 上，
    /// 而 panelRoot 是独立的 Canvas 子节点，不在 this 的子树里。
    /// </summary>
    private void FixChineseFontOnAllTMP()
    {
        var cnFont = FindBestFont();
        if (cnFont == null) return;

        // 1. 遍历 panelRoot 下所有 TMP
        if (panelRoot != null)
        {
            var allTmp = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in allTmp)
            {
                if (t == null) continue;
                if (t.font == null || !IsChineseCapableFont(t.font))
                    t.font = cnFont;
            }
        }

        // 2. 遍历 blocker 下（如果有 TMP，虽通常没有）
        if (blocker != null)
        {
            var bTmp = blocker.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in bTmp)
            {
                if (t == null) continue;
                if (t.font == null || !IsChineseCapableFont(t.font)) t.font = cnFont;
            }
        }

        // 3. 显式绑定的引用兜底
        if (pageContents != null)
        {
            foreach (var pc in pageContents)
            {
                if (pc == null) continue;
                if (pc.font == null || !IsChineseCapableFont(pc.font)) pc.font = cnFont;
            }
        }
    }

    /// <summary>
    /// 【2026-08 新增】底栏控件重排，运行时按面板实际宽度自适应。
    ///
    /// 旧布局问题（见 BuildDefaultPanelInEditor 里的硬编码 anchoredPosition）：
    ///   • 「下一页」按钮固定在 x=-180，「返回主菜单」在 x=-20 —— 面板宽度一旦小于
    ///     ~600px，两者会与居中的圆点指示器重叠，圆点被按钮盖住；
    ///   • 三个按钮尺寸各不相同（110/110/150 宽，40/40/44 高），底栏视觉重心偏右；
    ///   • 圆点固定居中，但左右按钮并非对称，观感失衡。
    ///
    /// 新布局（对称 + 分区）：
    ///   ┌──────────────────────────────────────┐
    ///   │ [< 上一页]      ● ○ ○ ○      [下一页 >] │   ← 翻页区：左右对称，圆点严格居中
    ///   │                [ 返回主菜单 ]           │   ← 主操作区：独立一行，居中强调
    ///   └──────────────────────────────────────┘
    /// 把「返回主菜单」从"和翻页按钮挤在一行的右端"改为"独立居中一行"，
    /// 建立清晰的视觉层次：翻页是浏览操作，返回是终结操作，不应同级并列。
    /// </summary>
    private void RelayoutBottomBar()
    {
        if (panelRoot == null) return;
        var rootRt = panelRoot.transform as RectTransform;
        if (rootRt == null) return;

        float w = rootRt.rect.width, h = rootRt.rect.height;
        if (w <= 0f || h <= 0f) return;

        // 底栏高度提升到 22%（旧版 14% 容纳不了两行）
        var bottomT = panelRoot.transform.Find("BottomBar") as RectTransform;
        if (bottomT != null)
        {
            bottomT.anchorMin = new Vector2(0f, 0f);
            bottomT.anchorMax = new Vector2(1f, BottomBarHeightRatio);
            bottomT.offsetMin = Vector2.zero;
            bottomT.offsetMax = Vector2.zero;
        }

        // 按钮尺寸按面板宽度比例，最小/最大做钳制，保证小面板不挤、大面板不虚胖
        float btnW = Mathf.Clamp(w * 0.17f, 92f, 150f);
        float btnH = Mathf.Clamp(h * 0.075f, 34f, 48f);
        float sideMargin = Mathf.Clamp(w * 0.03f, 14f, 32f);

        // ── 第一行（上）：翻页按钮 + 圆点 ──
        // pivot 用 (0,0.5)/(1,0.5)贴边，anchoredPosition.y 取底栏上半区中心
        float rowPageY = (bottomT != null ? bottomT.rect.height : h * BottomBarHeightRatio) * 0.30f;

        SetRectSafe(prevButton, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f), new Vector2(sideMargin, rowPageY), new Vector2(btnW, btnH));

        SetRectSafe(nextButton, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f), new Vector2(-sideMargin, rowPageY), new Vector2(btnW, btnH));

        // 圆点组：与翻页按钮同一行、严格居中
        var dotsT = (bottomT != null ? bottomT.Find("Dots") : null) as RectTransform;
        if (dotsT != null)
        {
            dotsT.anchorMin = dotsT.anchorMax = new Vector2(0.5f, 0.5f);
            dotsT.pivot = new Vector2(0.5f, 0.5f);
            dotsT.anchoredPosition = new Vector2(0f, rowPageY);

            // 圆点间距随面板缩放，且保证整组不会宽过"两个翻页按钮之间的空档"
            int n = (pages != null && pages.Length > 0) ? pages.Length : 4;
            float avail = w - (sideMargin + btnW) * 2f - 24f;
            float dotSize = Mathf.Clamp(h * 0.028f, 10f, 18f);
            float spacing = n > 1 ? Mathf.Clamp(avail / n, dotSize + 6f, 34f) : 0f;

            dotsT.sizeDelta = new Vector2(spacing * n, dotSize + 4f);

            float startX = -(n - 1) * spacing / 2f;
            for (int i = 0; i < (dotImages?.Length ?? 0); i++)
            {
                if (dotImages[i] == null) continue;
                var dRt = dotImages[i].transform as RectTransform;
                if (dRt == null) continue;
                dRt.anchorMin = dRt.anchorMax = new Vector2(0.5f, 0.5f);
                dRt.pivot = new Vector2(0.5f, 0.5f);
                dRt.sizeDelta = new Vector2(dotSize, dotSize);
                dRt.anchoredPosition = new Vector2(startX + i * spacing, 0f);
            }
        }

        // ── 第二行（下）：返回主菜单，居中且略宽，作为主操作强调 ──
        float rowReturnY = -(bottomT != null ? bottomT.rect.height : h * BottomBarHeightRatio) * 0.28f;
        SetRectSafe(returnButton, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, rowReturnY),
            new Vector2(Mathf.Clamp(w * 0.26f, 130f, 220f), btnH));

        // 返回按钮配色强调（琥珀金，呼应面板的金色标题与边框），
        // 翻页按钮保持低饱和深板岩色，形成层次
        ApplyButtonTint(returnButton, new Color(0.62f, 0.46f, 0.16f, 1f));
        ApplyButtonTint(prevButton,   new Color(0.16f, 0.16f, 0.26f, 1f));
        ApplyButtonTint(nextButton,   new Color(0.16f, 0.16f, 0.26f, 1f));

        // 页面容器下沿必须让开加高后的底栏，否则正文会被按钮压住
        var pcT = panelRoot.transform.Find("PageContainer") as RectTransform;
        if (pcT != null)
        {
            pcT.anchorMin = new Vector2(0f, BottomBarHeightRatio);
            pcT.anchorMax = new Vector2(1f, 1f);
            pcT.offsetMin = new Vector2(sideMargin, 6f);
            pcT.offsetMax = new Vector2(-sideMargin, -(TitleBarHeight + 6f));
        }

        // 标题栏高度也按比例，避免大面板上标题显得过扁
        var titleT = panelRoot.transform.Find("TitleBar") as RectTransform;
        if (titleT != null)
        {
            titleT.anchorMin = new Vector2(0f, 1f);
            titleT.anchorMax = new Vector2(1f, 1f);
            titleT.pivot = new Vector2(0.5f, 1f);
            titleT.sizeDelta = new Vector2(0f, TitleBarHeight);
            titleT.anchoredPosition = Vector2.zero;
        }
    }

    /// <summary>底栏占面板高度的比例。两行布局需要比旧版 0.14 更高。</summary>
    private const float BottomBarHeightRatio = 0.22f;
    /// <summary>标题栏高度（像素）。</summary>
    private const float TitleBarHeight = 58f;

    private static void SetRectSafe(Button btn, Vector2 aMin, Vector2 aMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        if (btn == null) return;
        var rt = btn.transform as RectTransform;
        if (rt == null) return;
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.pivot = pivot;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
    }

    /// <summary>
    /// 设置按钮底色。
    ///
    /// 【2026-08 修复】同时清掉按钮的 sprite。原因有两个：
    ///   ① **绿幕背景**：按钮用的是 AI 生成的 UI 素材，四周残留未抠净的纯色底；
    ///      再叠上 Image.color 的着色后，就变成玩家看到的"一块突兀的绿色方块"。
    ///   ② **比例不对**：该sprite 没有 9-slice border，Image 默认 Simple 模式会把它
    ///      直接拉伸到按钮尺寸（180×42），与源图长宽比不符 → 明显变形。
    /// 改为纯色填充：外观干净统一、任意尺寸都不会失真，也不再依赖素材抠图质量。
    /// </summary>
    private static void ApplyButtonTint(Button btn, Color c)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img == null) return;
        img.sprite = null;              // 去掉带绿边/ 会被拉伸的素材
        img.type = Image.Type.Simple;
        img.color = c;
    }

    /// <summary>
    /// 运行时校正每一页 PageTitle / Content 的内边距，避免文字紧贴像素边框。
    /// 内边距按面板尺寸比例计算，通用于任何面板大小。
    /// </summary>
    private void FixPagePadding()
    {
        if (panelRoot == null) return;
        var rootRt = panelRoot.transform as RectTransform;
        if (rootRt == null) return;
        var size = rootRt.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        // 左右内边距（相对每一页 Page 容器）：≈ 面板宽度的 3%（Page 容器本身已有 24px 边距，再叠一层内边距）
        float sidePad = Mathf.Clamp(size.x * 0.03f, 12f, 32f);
        // 页标题高度
        float titleH = Mathf.Clamp(size.y * 0.08f, 30f, 60f);
        // 页标题距顶 padding
        float titleTop = Mathf.Clamp(size.y * 0.02f, 4f, 16f);
        // 页标题与 Content 间距
        float gap = 8f;
        // 底部内边距
        float bottomPad = Mathf.Clamp(size.y * 0.03f, 8f, 24f);

        if (pages == null) return;
        for (int i = 0; i < pages.Length; i++)
        {
            var page = pages[i];
            if (page == null) continue;
            var pageRt = page.transform as RectTransform;
            if (pageRt == null) continue;

            // PageTitle
            var titleT = page.transform.Find("PageTitle") as RectTransform;
            if (titleT != null)
            {
                titleT.anchorMin = new Vector2(0f, 1f);
                titleT.anchorMax = new Vector2(1f, 1f);
                titleT.pivot = new Vector2(0.5f, 1f);
                titleT.sizeDelta = new Vector2(-sidePad * 2f, titleH);
                titleT.anchoredPosition = new Vector2(0f, -titleTop);
            }

            // Content
            var contentT = page.transform.Find("Content") as RectTransform;
            if (contentT != null)
            {
                contentT.anchorMin = Vector2.zero;
                contentT.anchorMax = Vector2.one;
                contentT.offsetMin = new Vector2(sidePad, bottomPad);
                contentT.offsetMax = new Vector2(-sidePad, -(titleTop + titleH + gap));
            }
        }
    }

    /// <summary>
    /// 根据 panelRoot 大小动态调整所有 TMP 字号 + 按钮字号。
    /// 基准尺寸 720×560，字号按短边比例线性缩放。
    ///
    /// 【2026-08 修复】旧版对**所有** TMP 都enableAutoSizing = true，副作用严重：
    ///   ① 各页内容长度不同 → TMP 自动算出的字号也不同 → 玩家翻页时字号忽大忽小；
    ///   ② 技能伤害页的进度条（等宽方块字符）被自动缩放后与上一行文字不对齐；
    ///   ③ autoSizing 每帧都要做二分测量，timeScale=0 下依然消耗 CPU。
    ///   现在只对「按钮标签」保留 autoSizing（按钮宽度固定、文字短，自适应确实有用），
    ///   标题与正文改为固定字号，保证跨页一致 + 进度条严格对齐。
    /// </summary>
    private void AutoScaleFontSizes()
    {
        if (panelRoot == null) return;
        var rt = panelRoot.transform as RectTransform;
        if (rt == null) return;
        var size = rt.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        const float baseW = 720f, baseH = 560f;
        float scale = Mathf.Min(size.x / baseW, size.y / baseH);
        scale = Mathf.Clamp(scale, 0.55f, 2.0f);

        var allTmp = panelRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in allTmp)
        {
            if (t == null) continue;

            string n = t.gameObject.name.ToLower();
            bool isButtonLabel = t.transform.parent != null
                && t.transform.parent.name.ToLower().Contains("btn");

            float baseFs;
            if (isButtonLabel)             baseFs = 18f;   // 按钮标签
            else if (n.Contains("pagetitle")) baseFs = 21f;   // 页内小标题（必须先判，否则被 title 抢走）
            else if (n.Contains("title"))     baseFs = 29f;   // 面板主标题
            else                              baseFs = 17f;   // 正文

            if (isButtonLabel)
            {
                // 按钮：保留自适应，避免"返回主菜单"四字在窄按钮里溢出
                t.enableAutoSizing = true;
                t.fontSizeMin = baseFs * scale * 0.6f;
                t.fontSizeMax = baseFs * scale;
            }
            else
            {
                // 标题与正文：固定字号，跨页一致
                t.enableAutoSizing = false;
                t.fontSize = baseFs * scale;
            }

            // 正文行距略放宽，提升可读性与视觉呼吸感
            if (!isButtonLabel && !n.Contains("title"))
                t.lineSpacing = 6f;
        }
    }

    private static bool IsChineseCapableFont(TMP_FontAsset f)
    {
        if (f == null) return false;
        var n = f.name.ToLower();
        if (n.Contains("liberation") || n.Contains("roboto") || n.Contains("arial")) return false;
        return true;
    }
}
