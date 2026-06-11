using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗暂停菜单中的「操作说明」面板（PPT 翻页式）。
///
/// 设计：
/// - 每一页是一张完整的说明插画（图片自带标题与文字），无需 TMP 渲染说明内容。
/// - 用户用 ◀ ▶ 按钮翻页，下方显示 "当前页 / 总页数"。
/// - 插画位于 Assets/Resources/InstructionsSlides/ 下，由脚本运行时 Resources.Load 加载。
///
/// 「初次/新解锁红点」：
/// - 静态方法 <see cref="HasNewUnlockToShow"/> 仍可返回是否有新解锁，供暂停菜单按钮显示红点。
/// - 打开本面板时（OnEnable）自动对齐 PlayerPrefs，红点消失。
/// </summary>
public class InstructionsPanelUI : MonoBehaviour
{
    [Header("控件（可留空，会自动生成）")]
    public Button closeButton;
    public Button prevButton;
    public Button nextButton;
    public Image slideImage;
    public TextMeshProUGUI pageIndicator;

    [Header("自动构建样式")]
    public TMP_FontAsset font;
    public Vector2 autoBuildSize = new Vector2(1100f, 760f);

    [Header("幻灯片资源（Resources/InstructionsSlides 下，不含扩展名）")]
    [Tooltip("按顺序排列。运行时使用 Resources.Load<Sprite> 加载。")]
    public string[] slidePaths = new string[]
    {
        "InstructionsSlides/slide_01_move",
        "InstructionsSlides/slide_02_levelup",
        "InstructionsSlides/slide_03_gacha",
        "InstructionsSlides/slide_04_difficulty",
        "InstructionsSlides/slide_05_affinity",
        "InstructionsSlides/slide_06_events",
    };

    // 兼容旧场景节点（已废弃，保留以免反序列化丢失引用）
    [HideInInspector] public TextMeshProUGUI contentText;
    [HideInInspector] public string[] difficultyGoals;
    [HideInInspector] public string[] difficultyUnlocks;

    // ─── 红点持久化 key ───────────────────────────────────────────────────
    private const string PREF_LAST_SEEN_UNLOCK = "InstructionsLastSeenUnlockCount";
    private const string PREF_EVER_VIEWED      = "InstructionsEverViewed";

    private Sprite[] _loadedSlides;
    private int _pageIndex = 0;
    private bool _built = false;

    void Awake() { EnsureBuilt(); }

    void OnEnable()
    {
        EnsureBuilt();
        LoadSlidesIfNeeded();

        // 隐藏废弃的旧文字节点（如果场景里还有）
        if (contentText != null && contentText.gameObject != null)
            contentText.gameObject.SetActive(false);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(PrevPage);
            prevButton.onClick.AddListener(PrevPage);
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextPage);
            nextButton.onClick.AddListener(NextPage);
        }

        _pageIndex = 0;
        Refresh();

        // 写入"已查看到当前解锁数"，红点消失
        PlayerPrefs.SetInt(PREF_LAST_SEEN_UNLOCK, GetCurrentUnlockedCount());
        PlayerPrefs.SetInt(PREF_EVER_VIEWED, 1);
        PlayerPrefs.Save();
    }

    public void Close() { gameObject.SetActive(false); }

    public void PrevPage()
    {
        if (_loadedSlides == null || _loadedSlides.Length == 0) return;
        _pageIndex = (_pageIndex - 1 + _loadedSlides.Length) % _loadedSlides.Length;
        Refresh();
    }

    public void NextPage()
    {
        if (_loadedSlides == null || _loadedSlides.Length == 0) return;
        _pageIndex = (_pageIndex + 1) % _loadedSlides.Length;
        Refresh();
    }

    private void Refresh()
    {
        int total = (_loadedSlides != null) ? _loadedSlides.Length : 0;
        if (slideImage != null)
        {
            if (total > 0 && _pageIndex < total)
            {
                slideImage.sprite = _loadedSlides[_pageIndex];
                slideImage.enabled = _loadedSlides[_pageIndex] != null;
                slideImage.preserveAspect = true;
            }
            else
            {
                slideImage.sprite = null;
                slideImage.enabled = false;
            }
        }
        if (pageIndicator != null)
            pageIndicator.text = total > 0 ? ($"{_pageIndex + 1} / {total}") : "0 / 0";
    }

    private void LoadSlidesIfNeeded()
    {
        if (_loadedSlides != null && _loadedSlides.Length == (slidePaths != null ? slidePaths.Length : 0))
            return;
        if (slidePaths == null) { _loadedSlides = new Sprite[0]; return; }

        _loadedSlides = new Sprite[slidePaths.Length];
        for (int i = 0; i < slidePaths.Length; i++)
        {
            if (string.IsNullOrEmpty(slidePaths[i])) continue;
            _loadedSlides[i] = Resources.Load<Sprite>(slidePaths[i]);
            if (_loadedSlides[i] == null)
                Debug.LogWarning($"[InstructionsPanelUI] 找不到幻灯片资源：Resources/{slidePaths[i]}");
        }
    }

    /// <summary>
    /// 是否需要在"操作说明"按钮上显示红点提醒。
    /// 条件（满足其一即可）：
    /// 1. 从未查看过本面板（首次提醒）
    /// 2. 当前已解锁的难度数 > 上次查看时已解锁的难度数（有新解锁）
    /// </summary>
    public static bool HasNewUnlockToShow()
    {
        if (PlayerPrefs.GetInt(PREF_EVER_VIEWED, 0) == 0) return true;
        int lastSeen = PlayerPrefs.GetInt(PREF_LAST_SEEN_UNLOCK, 0);
        int current  = GetCurrentUnlockedCount();
        return current > lastSeen;
    }

    /// <summary>当前已解锁的难度数量（N1 必解锁；后续依赖前一难度的 ClearRecord）</summary>
    public static int GetCurrentUnlockedCount()
    {
        if (DifficultyManager.Instance == null || DifficultyManager.Instance.configs == null) return 1;
        int count = 1; // N1 始终解锁
        var cfgs = DifficultyManager.Instance.configs;
        for (int i = 1; i < cfgs.Length; i++)
        {
            bool unlocked = ClearRecordManager.Instance != null
                            && ClearRecordManager.Instance.GetClearCount(cfgs[i - 1].label) > 0;
            if (!unlocked) break;
            count++;
        }
        return count;
    }

    // ─── 自动构建 ───────────────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = autoBuildSize;

        if (GetComponent<Image>() == null)
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.88f);
            bg.raycastTarget = true;
        }

        // 关闭按钮（右上角）
        if (closeButton == null)
        {
            closeButton = UIBuilder.CreateButton(rt, "CloseButton", "X",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-30f, -30f), new Vector2(60f, 60f), font);
        }

        // 幻灯片图（居中，留出上下各 70 像素给按钮和页码）
        if (slideImage == null)
        {
            var go = new GameObject("Slide", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var srt = (RectTransform)go.transform;
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, 20f);
            // 留出 ~120 边距，保持 3:2 比例 (图本身 1536x1024 = 3:2)
            float w = autoBuildSize.x - 200f;
            float h = w * 2f / 3f;
            if (h > autoBuildSize.y - 180f) { h = autoBuildSize.y - 180f; w = h * 3f / 2f; }
            srt.sizeDelta = new Vector2(w, h);

            slideImage = go.AddComponent<Image>();
            slideImage.preserveAspect = true;
            slideImage.raycastTarget = false;
        }

        // 左翻页按钮（图片左侧）
        if (prevButton == null)
        {
            prevButton = UIBuilder.CreateButton(rt, "PrevButton", "◀",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(20f, 20f), new Vector2(70f, 70f), font);
        }
        // 右翻页按钮（图片右侧）
        if (nextButton == null)
        {
            nextButton = UIBuilder.CreateButton(rt, "NextButton", "▶",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, 20f), new Vector2(70f, 70f), font);
        }

        // 页码指示（底部居中）
        if (pageIndicator == null)
        {
            pageIndicator = UIBuilder.CreateText(rt, "PageIndicator", "1 / 1", 26, FontStyles.Bold,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(200f, 40f), font);
            pageIndicator.alignment = TextAlignmentOptions.Center;
            pageIndicator.color = Color.white;
        }
    }
}
