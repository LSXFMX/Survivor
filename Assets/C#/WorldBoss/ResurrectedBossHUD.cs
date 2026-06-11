using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「无罪」角色（SKIN_TOMB）专属 UI：在屏幕右侧从上到下动态显示
/// 「已被亡者领域复活的世界 Boss」头像 + 血条。
///
/// 接入方式
/// ========
/// - 单例 Canvas（ScreenSpaceOverlay），由 PlayerSkinSkillBuff 在 SKIN_TOMB 分支
///   调用 ResurrectedBossHUD.EnsureExist() 自动建立；其它皮肤不调用即不存在。
/// - 数据源：MindControlled.All 里 isWorldBoss=true && IsAlive 的成员。
/// - 头像：直接复用对应 Boss 自身 SpriteRenderer 当前帧的 sprite —— 不需要额外生成
///   PNG 素材；当 Boss 的 sprite 切换（行走/idle）时，HUD 头像随之更新。
/// - 血条：背景黑底 + 紫色填充（呼应"亡者领域"被复活的友军主题色）。
///
/// 性能
/// ====
/// - 每帧 O(N)，N = 当前活跃的被复活世界 Boss 数（一般 ≤3）；不挂 LateUpdate 也行，
///   但用 LateUpdate 可以与 boss 自身 sprite 切换在同一帧同步。
/// - 条目用对象池：复活新 Boss → 复用旧 entry；条目不会频繁创建销毁。
///
/// 视觉 / 布局
/// ===========
/// - 右对齐：距屏幕右边 20px；顶部 160px 起（在原 80px 基础上再向下 80px，远离顶部数值条/计时区）。
/// - 自动纵向排列：父物体 Root 挂 VerticalLayoutGroup + ContentSizeFitter，
///   未来 Boss 数量增加（无论 1 个还是 10 个）都会从上到下自动排开，
///   每条间距 ENTRY_GAP，无需手算每条 y 坐标。
/// - 头像 64×64；血条宽 200px、高 14px；二者间距 8px。
/// </summary>
public class ResurrectedBossHUD : MonoBehaviour
{
    // —— 布局常量 —— 
    // 2026-06：右侧布局 + 顶部偏移再下移
    private const float SIDE_PADDING_RIGHT = 20f;  // 距屏幕右边
    private const float TOP_OFFSET         = 160f; // 顶部偏移：从 80 → 160（再向下 80px）
    private const float ENTRY_GAP          = 12f;
    private const float PORTRAIT_SIZE      = 64f;
    private const float HP_BAR_WIDTH       = 200f;
    private const float HP_BAR_HEIGHT      = 14f;
    private const float PORTRAIT_HP_GAP    = 8f;
    private const float ENTRY_HEIGHT       = PORTRAIT_SIZE; // 整条以头像高度为准

    private static readonly Color BAR_BG_COLOR   = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color BAR_FILL_COLOR = new Color(0.72f, 0.32f, 0.98f, 1f); // 亡者紫
    private static readonly Color PORTRAIT_BG    = new Color(0.10f, 0.08f, 0.18f, 0.85f);
    private static readonly Color PORTRAIT_FRAME = new Color(0.85f, 0.45f, 1.00f, 1f);

    // —— 单例 —— 
    private static ResurrectedBossHUD _instance;
    public static ResurrectedBossHUD Instance => _instance;

    /// <summary>由 PlayerSkinSkillBuff 在 SKIN_TOMB 分支调用：单例不存在则创建。</summary>
    public static void EnsureExist()
    {
        if (_instance != null) return;
        var go = new GameObject("ResurrectedBossHUD");
        Object.DontDestroyOnLoad(go);
        _instance = go.AddComponent<ResurrectedBossHUD>();
    }

    private Canvas _canvas;
    private RectTransform _root;
    private readonly List<Entry> _entries = new List<Entry>();
    private readonly List<Entry> _pool    = new List<Entry>();

    /// <summary>共享白 sprite，避免每条 entry 各自 new Texture。</summary>
    private static Sprite _whiteSprite;

    private class Entry
    {
        public RectTransform root;
        public Image portraitBg;
        public Image portraitFrame;
        public Image portraitImage;
        public Image hpBg;
        public Image hpFill;
        public MindControlled bound;          // 绑定的源
        public SpriteRenderer boundRenderer;  // 用于持续取 sprite
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        BuildCanvas();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void BuildCanvas()
    {
        // Canvas
        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 500; // 显示在战斗 UI 之上
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Root（右上锚定的容器；挂 VerticalLayoutGroup 让所有 BossEntry 自动从上到下纵向排列）
        // 这样未来无论复活多少个 Boss，每条都会被布局组件自动按 ENTRY_GAP 间距堆叠，
        // BindEntry 里就不再需要手算 y 坐标。
        var rootGO = new GameObject("Root", typeof(RectTransform));
        rootGO.transform.SetParent(transform, false);
        _root = rootGO.GetComponent<RectTransform>();
        // 右上角锚定：anchor=(1,1), pivot=(1,1)
        _root.anchorMin = new Vector2(1f, 1f);
        _root.anchorMax = new Vector2(1f, 1f);
        _root.pivot     = new Vector2(1f, 1f);
        // 距右边 SIDE_PADDING_RIGHT、向下 TOP_OFFSET
        _root.anchoredPosition = new Vector2(-SIDE_PADDING_RIGHT, -TOP_OFFSET);
        _root.sizeDelta = new Vector2(PORTRAIT_SIZE + PORTRAIT_HP_GAP + HP_BAR_WIDTH, 0f);

        // VerticalLayoutGroup：每条 Entry 自动从上往下排列
        var vlg = rootGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperRight;   // 子元素靠右上对齐
        vlg.spacing = ENTRY_GAP;
        vlg.padding = new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth  = false; // Entry 自带 sizeDelta，不让 LayoutGroup 改宽
        vlg.childControlHeight = false; // 同上
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;

        // ContentSizeFitter：Root 高度随条目数量自适应，未来 Boss 越多容器越高
        var fitter = rootGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
    }

    void LateUpdate()
    {
        // 仅 SKIN_TOMB（无罪）启用——其它皮肤万一误挂上也直接 hide
        if (PlayerSkinSkillBuff.CurrentSkinIndex != PlayerSkinSkillBuff.SKIN_TOMB)
        {
            HideAll();
            return;
        }

        // 收集当前活跃的世界 Boss 友军
        var list = MindControlled.All;
        int used = 0;
        if (list != null)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var mc = list[i];
                if (mc == null || !mc.isWorldBoss || !mc.IsAlive) continue;
                Entry e = GetOrCreateEntry(used);
                BindEntry(e, mc, used);
                used++;
            }
        }

        // 多余条目入池隐藏
        for (int i = used; i < _entries.Count; i++)
        {
            _entries[i].root.gameObject.SetActive(false);
            _entries[i].bound = null;
            _entries[i].boundRenderer = null;
        }
    }

    private void HideAll()
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            _entries[i].root.gameObject.SetActive(false);
            _entries[i].bound = null;
            _entries[i].boundRenderer = null;
        }
    }

    private Entry GetOrCreateEntry(int index)
    {
        while (_entries.Count <= index)
        {
            _entries.Add(BuildEntry());
        }
        var e = _entries[index];
        e.root.gameObject.SetActive(true);
        return e;
    }

    private Entry BuildEntry()
    {
        var e = new Entry();

        var rowGO = new GameObject("BossEntry", typeof(RectTransform));
        rowGO.transform.SetParent(_root, false);
        e.root = rowGO.GetComponent<RectTransform>();
        // 右上对齐：让条目内部仍可用"左=头像、右=血条"的相对坐标
        e.root.anchorMin = new Vector2(1f, 1f);
        e.root.anchorMax = new Vector2(1f, 1f);
        e.root.pivot     = new Vector2(1f, 1f);
        float entryWidth = PORTRAIT_SIZE + PORTRAIT_HP_GAP + HP_BAR_WIDTH;
        e.root.sizeDelta = new Vector2(entryWidth, ENTRY_HEIGHT);

        // 让 VerticalLayoutGroup 严格按 ENTRY_HEIGHT × entryWidth 给条目预留空间
        var le = rowGO.AddComponent<LayoutElement>();
        le.preferredWidth  = entryWidth;
        le.preferredHeight = ENTRY_HEIGHT;
        le.minWidth  = entryWidth;
        le.minHeight = ENTRY_HEIGHT;

        // 头像背景（深紫底色）
        var bgGO = new GameObject("PortraitBg", typeof(RectTransform));
        bgGO.transform.SetParent(e.root, false);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.5f);
        bgRT.anchorMax = new Vector2(0f, 0.5f);
        bgRT.pivot     = new Vector2(0f, 0.5f);
        bgRT.anchoredPosition = Vector2.zero;
        bgRT.sizeDelta = new Vector2(PORTRAIT_SIZE, PORTRAIT_SIZE);
        e.portraitBg = bgGO.AddComponent<Image>();
        e.portraitBg.sprite = GetWhiteSprite();
        e.portraitBg.color = PORTRAIT_BG;
        e.portraitBg.raycastTarget = false;

        // 头像主图（boss sprite）
        var imgGO = new GameObject("PortraitImage", typeof(RectTransform));
        imgGO.transform.SetParent(bgGO.transform, false);
        var imgRT = imgGO.GetComponent<RectTransform>();
        imgRT.anchorMin = new Vector2(0.5f, 0.5f);
        imgRT.anchorMax = new Vector2(0.5f, 0.5f);
        imgRT.pivot     = new Vector2(0.5f, 0.5f);
        imgRT.anchoredPosition = Vector2.zero;
        imgRT.sizeDelta = new Vector2(PORTRAIT_SIZE - 8f, PORTRAIT_SIZE - 8f);
        e.portraitImage = imgGO.AddComponent<Image>();
        e.portraitImage.preserveAspect = true;
        e.portraitImage.raycastTarget = false;
        e.portraitImage.color = Color.white;

        // 头像紫色边框（叠在上层、四条等宽边）
        var frameGO = new GameObject("PortraitFrame", typeof(RectTransform));
        frameGO.transform.SetParent(bgGO.transform, false);
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = new Vector2(0f, 0f);
        frameRT.anchorMax = new Vector2(1f, 1f);
        frameRT.offsetMin = Vector2.zero;
        frameRT.offsetMax = Vector2.zero;
        e.portraitFrame = frameGO.AddComponent<Image>();
        e.portraitFrame.sprite = GetWhiteSprite();
        e.portraitFrame.color = new Color(PORTRAIT_FRAME.r, PORTRAIT_FRAME.g, PORTRAIT_FRAME.b, 0f); // 由专门四条边代替
        e.portraitFrame.raycastTarget = false;
        // 简化：直接用一张 Image 上下左右各 2px 描边——通过四个子矩形画
        BuildFrameBorders(frameGO.transform);

        // 血条底
        float hpX = PORTRAIT_SIZE + PORTRAIT_HP_GAP;
        var hpBgGO = new GameObject("HpBg", typeof(RectTransform));
        hpBgGO.transform.SetParent(e.root, false);
        var hpBgRT = hpBgGO.GetComponent<RectTransform>();
        hpBgRT.anchorMin = new Vector2(0f, 0.5f);
        hpBgRT.anchorMax = new Vector2(0f, 0.5f);
        hpBgRT.pivot     = new Vector2(0f, 0.5f);
        hpBgRT.anchoredPosition = new Vector2(hpX, 0f);
        hpBgRT.sizeDelta = new Vector2(HP_BAR_WIDTH, HP_BAR_HEIGHT);
        e.hpBg = hpBgGO.AddComponent<Image>();
        e.hpBg.sprite = GetWhiteSprite();
        e.hpBg.color = BAR_BG_COLOR;
        e.hpBg.raycastTarget = false;

        // 血条填充
        var hpFillGO = new GameObject("HpFill", typeof(RectTransform));
        hpFillGO.transform.SetParent(hpBgGO.transform, false);
        var hpFillRT = hpFillGO.GetComponent<RectTransform>();
        hpFillRT.anchorMin = new Vector2(0f, 0f);
        hpFillRT.anchorMax = new Vector2(1f, 1f);
        hpFillRT.offsetMin = Vector2.zero;
        hpFillRT.offsetMax = Vector2.zero;
        e.hpFill = hpFillGO.AddComponent<Image>();
        e.hpFill.sprite = GetWhiteSprite();
        e.hpFill.color = BAR_FILL_COLOR;
        e.hpFill.type = Image.Type.Filled;
        e.hpFill.fillMethod = Image.FillMethod.Horizontal;
        e.hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        e.hpFill.fillAmount = 1f;
        e.hpFill.raycastTarget = false;

        return e;
    }

    /// <summary>给头像加 4 条紫色描边（顶/底/左/右各一根 2px 矩形）。</summary>
    private void BuildFrameBorders(Transform parent)
    {
        const float THICK = 2f;
        AddBorder(parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -THICK), THICK, true);  // top
        AddBorder(parent, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, THICK), THICK, true);   // bottom
        AddBorder(parent, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(THICK, 0f), THICK, false);  // left
        AddBorder(parent, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-THICK, 0f), THICK, false); // right
    }

    private void AddBorder(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMax, float thick, bool isHorizontal)
    {
        var go = new GameObject("Border", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        if (isHorizontal)
        {
            // 水平条
            rt.sizeDelta = new Vector2(0f, thick);
        }
        else
        {
            rt.sizeDelta = new Vector2(thick, 0f);
        }
        var img = go.AddComponent<Image>();
        img.sprite = GetWhiteSprite();
        img.color = PORTRAIT_FRAME;
        img.raycastTarget = false;
    }

    private void BindEntry(Entry e, MindControlled mc, int index)
    {
        // 位置：交给父物体上的 VerticalLayoutGroup 自动排列，这里无需再手算 anchoredPosition。
        // （未来 Boss 增多也能无脑追加，全部由布局组件按 ENTRY_GAP 间距堆叠。）

        // 重新绑定时更新缓存
        if (e.bound != mc)
        {
            e.bound = mc;
            e.boundRenderer = (mc.Enemy != null) ? mc.Enemy.GetComponent<SpriteRenderer>() : null;
            if (e.boundRenderer == null && mc.Enemy != null)
                e.boundRenderer = mc.Enemy.GetComponentInChildren<SpriteRenderer>();
        }

        // 头像 sprite：实时同步 boss 当前帧
        if (e.boundRenderer != null && e.boundRenderer.sprite != null)
        {
            e.portraitImage.sprite = e.boundRenderer.sprite;
            e.portraitImage.enabled = true;
            // 翻转用 RectTransform.scale 实现，避免 SpriteRenderer.flipX 在 UI Image 上无效
            float fx = e.boundRenderer.flipX ? -1f : 1f;
            var imgRT = e.portraitImage.rectTransform;
            var s = imgRT.localScale;
            s.x = Mathf.Abs(s.x) * fx;
            imgRT.localScale = s;
        }
        else
        {
            e.portraitImage.enabled = false;
        }

        // 血条
        var en = mc.Enemy;
        if (en != null && en.healthmax > 0)
        {
            float ratio = Mathf.Clamp01((float)en.health / en.healthmax);
            e.hpFill.fillAmount = ratio;
        }
        else
        {
            e.hpFill.fillAmount = 0f;
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var pixels = new Color32[4];
        for (int i = 0; i < 4; i++) pixels[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(pixels);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.name = "ResurrectedBossHUD_White";
        return _whiteSprite;
    }
}
