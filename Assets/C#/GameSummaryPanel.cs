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

        _currentPage = 0;

        // 【关键】显示前先纠正字体，杜绝中文乱码
        FixChineseFontOnAllTMP();

        // 每次显示都重新应用背景（防止 Inspector 挂着旧素材）
        ApplyBackgroundSprite();

        // 根据面板尺寸动态调整字号
        AutoScaleFontSizes();

        // 校正每页文字内边距，防止贴边框
        FixPagePadding();

        RefreshAllPages();

        if (blocker != null)
        {
            blocker.SetActive(true);
            blocker.transform.SetAsLastSibling();
        }

        panelRoot.transform.SetAsLastSibling();
        panelRoot.SetActive(true);
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
        _currentPage = Mathf.Clamp(pageIndex, 0, pages.Length - 1);
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == _currentPage);

        for (int i = 0; i < dotImages.Length; i++)
        {
            if (dotImages[i] == null) continue;
            dotImages[i].sprite = (i == _currentPage) ? dotFilledSprite : dotEmptySprite;
            // 若未绑定 sprite，则用颜色区分
            if (dotFilledSprite == null && dotEmptySprite == null)
                dotImages[i].color = (i == _currentPage)
                    ? new Color(1f, 0.85f, 0.4f, 1f)
                    : new Color(1f, 1f, 1f, 0.35f);
        }

        // 边界按钮：首页禁用上一页，末页禁用下一页
        if (prevButton != null) prevButton.interactable = _currentPage > 0;
        if (nextButton != null) nextButton.interactable = _currentPage < pages.Length - 1;
    }

    private void PrevPage() { ShowPage(_currentPage - 1); }
    private void NextPage() { ShowPage(_currentPage + 1); }

    private void OnReturnToMenu()
    {
        Debug.Log("[GameSummaryPanel] 返回主菜单");
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

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

        int durSec = Mathf.RoundToInt(t.DurationSeconds);
        int min = durSec / 60, sec = durSec % 60;
        string timeStr = min > 0 ? $"{min}分{sec}秒" : $"{sec}秒";

        string s = $"难度：<color=#FFD24A>{t.difficultyPlayed}</color>\n"
                 + $"结果：<color=#{(t.isVictory ? "80FF80>胜利" : "FF6060>失败")}</color>\n"
                 + $"游戏时长：<color=#C0C0FF>{timeStr}</color>\n"
                 + $"最终等级：<color=#FFD24A>Lv.{t.playerFinalLevel}</color>\n"
                 + $"获得技能数：<color=#80FFC0>{t.skillsAcquired.Count}</color>\n"
                 + $"击败首领数：<color=#FFD24A>{t.bossesDefeated.Count}</color>\n"
                 + $"解锁装备数：<color=#FF80C0>{t.equipmentUnlockedThisSession.Count}</color>\n"
                 + $"总伤害输出：<color=#FF4040>{FormatNumber(t.TotalDamage())}</color>\n"
                 + $"技能种类：<color=#C0C0FF>{t.skillDamage.Count}</color>";
        tmp.text = s;
    }

    private void RefreshPage1(GameSessionTracker t)
    {
        var tmp = GetPageContent(1);
        if (tmp == null) return;

        var sorted = t.GetSortedSkillDamage();
        if (sorted.Count == 0) { tmp.text = "<color=#888>本局未造成任何伤害。</color>"; return; }

        float total = t.TotalDamage();
        string s = "";
        int rank = 1;
        foreach (var kv in sorted)
        {
            float pct = total > 0f ? kv.Value / total * 100f : 0f;
            int barLen = Mathf.Clamp(Mathf.RoundToInt(pct / 2f), 1, 50);
            string bar = new string('|', barLen);
            s += $"{rank}. <color=#FFD24A>{kv.Key}</color>  <color=#FF4040>{FormatNumber(kv.Value)}</color> ({pct:F1}%)\n"
               + $"   <color=#555>{bar}</color>\n\n";
            rank++;
        }
        tmp.text = s.TrimEnd();
    }

    private void RefreshPage2(GameSessionTracker t)
    {
        var tmp = GetPageContent(2);
        if (tmp == null) return;

        if (t.bossesDefeated.Count == 0) { tmp.text = "<color=#888>本局未击败任何首领。</color>"; return; }

        string s = "";
        for (int i = 0; i < t.bossesDefeated.Count; i++)
            s += $"{i + 1}. <color=#FFD24A>{t.bossesDefeated[i]}</color>\n";
        tmp.text = s.TrimEnd();
    }

    private void RefreshPage3(GameSessionTracker t)
    {
        var tmp = GetPageContent(3);
        if (tmp == null) return;

        if (t.equipmentUnlockedThisSession.Count == 0) { tmp.text = "<color=#888>本局未解锁新装备。</color>"; return; }

        string s = "";
        for (int i = 0; i < t.equipmentUnlockedThisSession.Count; i++)
            s += $"{i + 1}. <color=#FF80C0>{t.equipmentUnlockedThisSession[i]}</color>\n";
        tmp.text = s.TrimEnd();
    }

    private TextMeshProUGUI GetPageContent(int pageIndex)
    {
        if (pageContents != null && pageIndex < pageContents.Length && pageContents[pageIndex] != null)
            return pageContents[pageIndex];
        if (pages == null || pageIndex >= pages.Length || pages[pageIndex] == null) return null;
        var tmps = pages[pageIndex].GetComponentsInChildren<TextMeshProUGUI>(true);
        return tmps.Length >= 2 ? tmps[1] : (tmps.Length > 0 ? tmps[0] : null);
    }

    private static string FormatNumber(float n)
    {
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
        trt.sizeDelta = new Vector2(0f, 60f);
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
        TryAssignFont(titleTxt);

        // ---- 页面容器 ----
        var pageContainerGo = new GameObject("PageContainer", typeof(RectTransform));
        pageContainerGo.transform.SetParent(rt, false);
        var pcRt = (RectTransform)pageContainerGo.transform;
        pcRt.anchorMin = new Vector2(0f, 0.14f); pcRt.anchorMax = new Vector2(1f, 1f);
        pcRt.offsetMin = new Vector2(24f, 4f);
        pcRt.offsetMax = new Vector2(-24f, -70f);

        // ---- 4 页 ----
        string[] pageTitles = { "对局概览", "技能伤害", "击败首领", "解锁装备" };
        pages = new GameObject[4];
        pageContents = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
            CreatePage(i, pageTitles[i], pcRt);

        // ---- 底栏 ----
        var bottomGo = new GameObject("BottomBar", typeof(RectTransform));
        bottomGo.transform.SetParent(rt, false);
        var brt = (RectTransform)bottomGo.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0.14f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;

        // 上一页按钮（左）
        prevButton = CreateButton("BtnPrev", brt,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f),
            new Vector2(20f, 0f), new Vector2(110f, 40f), "< 上一页");

        // 下一页按钮（右侧靠左，返回按钮的左边）
        nextButton = CreateButton("BtnNext", brt,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0f),
            new Vector2(-180f, 0f), new Vector2(110f, 40f), "下一页 >");

        // 返回主菜单按钮（最右）
        returnButton = CreateButton("BtnReturn", brt,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0f),
            new Vector2(-20f, 0f), new Vector2(150f, 44f), "返回主菜单");
        // 返回按钮特殊配色（绿）
        var retImg = returnButton.GetComponent<Image>();
        if (retImg != null) retImg.color = new Color(0.25f, 0.6f, 0.3f, 1f);

        // 圆点指示器（居中）
        var dotsGo = new GameObject("Dots", typeof(RectTransform));
        dotsGo.transform.SetParent(brt, false);
        var drt = (RectTransform)dotsGo.transform;
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.sizeDelta = new Vector2(200f, 20f);
        drt.anchoredPosition = Vector2.zero;

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
            // 用 gameObject.name 判定类型，避免误伤内容文本
            string n = t.gameObject.name.ToLower();
            float baseFs;
            if (n.Contains("title")) baseFs = 30f;                    // 标题
            else if (t.transform.parent != null && t.transform.parent.name.ToLower().Contains("btn"))
                baseFs = 20f;                                          // 按钮
            else if (n.Contains("pagetitle")) baseFs = 22f;            // 页内小标题
            else baseFs = 18f;                                         // 正文

            // 启用自动尺寸，让 TMP 自适应但设定基础值和上限
            t.enableAutoSizing = true;
            t.fontSizeMin = baseFs * scale * 0.7f;
            t.fontSizeMax = baseFs * scale * 1.2f;
            t.fontSize = baseFs * scale;
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
