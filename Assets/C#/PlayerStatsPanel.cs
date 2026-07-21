using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色属性面板：按 Tab 显示/隐藏，展示玩家各项属性（百分比 + DPS）。
/// 完全静态化：所有 UI 元素通过 Inspector 拖入，脚本只负责显示/隐藏 + 刷新内容。
/// 首次使用请右键组件 → "生成默认面板" 一键在场景中构造完整层级，之后可在 Scene 视图中随意调整。
/// </summary>
public class PlayerStatsPanel : MonoBehaviour
{
    public static PlayerStatsPanel Instance { get; private set; }

    [Header("─── UI 引用（在 Inspector 中拖入）───")]
    [Tooltip("面板根节点（GameObject 会被 SetActive 显示/隐藏）")]
    public GameObject panelRoot;
    [Tooltip("内容 TMP 文本（显示所有属性）")]
    public TextMeshProUGUI contentText;
    [Tooltip("可选：标题 TMP")]
    public TextMeshProUGUI titleText;

    [Header("─── AI 素材（可选：面板背景 / 装饰）───")]
    public Sprite panelBackgroundSprite;
    public Image panelBackgroundImage; // 若设置，则运行时把 sprite 应用上

    [Header("─── 玩家引用（自动查找，也可手动绑定）───")]
    public Player player;
    public battleUI battleUI;

    [Header("─── 交互 ───")]
    [Tooltip("切换显示的按键（默认 Tab）")]
    public KeyCode toggleKey = KeyCode.Tab;
    [Tooltip("对局未开始（starttime 前）是否也允许打开")]
    public bool allowBeforeGameStart = true;

    private bool _visible;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        // 默认隐藏
        if (panelRoot != null) panelRoot.SetActive(false);
        _visible = false;

        // 【强制覆盖】无论 Inspector 里挂了什么，运行时一律用 Resources 里的新简约背景
        var newBg = Resources.Load<Sprite>("UI/PanelBg_Stats");
        if (newBg != null)
        {
            panelBackgroundSprite = newBg;

            // 定位背景 Image：panelRoot 根节点的 Image 优先，否则找子节点第一个
            if (panelRoot != null)
            {
                var rootImg = panelRoot.GetComponent<Image>();
                if (rootImg != null) panelBackgroundImage = rootImg;
                else
                {
                    var imgs = panelRoot.GetComponentsInChildren<Image>(true);
                    if (imgs.Length > 0) panelBackgroundImage = imgs[0];
                }
            }

            // 强制覆盖 sprite
            if (panelBackgroundImage != null)
            {
                panelBackgroundImage.sprite = newBg;
                panelBackgroundImage.type = Image.Type.Sliced;
                panelBackgroundImage.color = Color.white;
            }

            // 把 panelRoot 下所有名字含 bg/background/panel/frame 的 Image 也全部覆盖，清除旧盾徽
            if (panelRoot != null)
            {
                var allImgs = panelRoot.GetComponentsInChildren<Image>(true);
                foreach (var im in allImgs)
                {
                    if (im == null) continue;
                    var n = im.gameObject.name.ToLower();
                    if (n.Contains("bg") || n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("border"))
                    {
                        im.sprite = newBg;
                        im.type = Image.Type.Sliced;
                        im.color = Color.white;
                    }
                }
            }
        }

        // 自动查找玩家和 battleUI
        if (player == null) FindPlayer();
        if (battleUI == null) battleUI = FindObjectOfType<battleUI>();

        // 【关键】运行时自动纠正字体：把面板下所有 TMP 的 LiberationSans 换成 heiti，防止中文乱码
        FixChineseFontOnAllTMP();
        // 根据面板尺寸动态调整字号
        AutoScaleFontSizes();
        // 根据面板尺寸校正 title/content 的内边距，防止文字紧贴边框
        FixTextPadding();
    }

    /// <summary>
    /// 运行时校正 title/content 的 RectTransform 内边距，避免文字卡在像素边框上。
    /// 边框视觉宽度 ≈ 面板短边的 6%，标题额外向下偏移一点。
    /// </summary>
    private void FixTextPadding()
    {
        if (panelRoot == null) return;
        var rootRt = panelRoot.transform as RectTransform;
        if (rootRt == null) return;
        var size = rootRt.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        // 内边距按短边百分比计算，最小 14 最大 40
        float padSide = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.07f, 14f, 40f);
        float padTop = Mathf.Clamp(size.y * 0.06f, 12f, 32f);
        // 标题高度 + 标题与内容间距
        float titleH = Mathf.Clamp(size.y * 0.08f, 30f, 56f);
        float titleGap = 6f;

        // 标题
        if (titleText != null)
        {
            var trt = titleText.rectTransform;
            // 顶部条：横向铺满，顶部向下 padTop
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.sizeDelta = new Vector2(-padSide * 2f, titleH);
            trt.anchoredPosition = new Vector2(0f, -padTop);
        }

        // 内容
        if (contentText != null)
        {
            var crt = contentText.rectTransform;
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            // 左右 padSide，底部 padSide，顶部为 padTop + titleH + titleGap
            crt.offsetMin = new Vector2(padSide, padSide);
            crt.offsetMax = new Vector2(-padSide, -(padTop + titleH + titleGap));
        }
    }

    /// <summary>
    /// 遍历 panelRoot 下所有 TMP 文本，非中文字体一律换成 heiti，杜绝口口口。
    /// 【关键】必须遍历 panelRoot 而非 this.gameObject —— 因为脚本可能挂在 battleUI 上，
    /// 而 panelRoot 是独立的 Canvas 子节点。
    /// </summary>
    private void FixChineseFontOnAllTMP()
    {
        var cnFont = FindBestFont();
        if (cnFont == null) return;

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

        // 显式引用兜底
        if (titleText != null && (titleText.font == null || !IsChineseCapableFont(titleText.font)))
            titleText.font = cnFont;
        if (contentText != null && (contentText.font == null || !IsChineseCapableFont(contentText.font)))
            contentText.font = cnFont;
    }

    /// <summary>
    /// 根据 panelRoot 大小动态调整字号。基准 420×520。
    /// </summary>
    private void AutoScaleFontSizes()
    {
        if (panelRoot == null) return;
        var rt = panelRoot.transform as RectTransform;
        if (rt == null) return;
        var size = rt.rect.size;
        if (size.x <= 0 || size.y <= 0) return;

        const float baseW = 420f, baseH = 520f;
        float scale = Mathf.Min(size.x / baseW, size.y / baseH);
        scale = Mathf.Clamp(scale, 0.6f, 2.0f);

        // 标题
        if (titleText != null)
        {
            const float titleBase = 26f;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = titleBase * scale * 0.7f;
            titleText.fontSizeMax = titleBase * scale * 1.2f;
            titleText.fontSize = titleBase * scale;
        }
        // 内容
        if (contentText != null)
        {
            const float contentBase = 18f;
            contentText.enableAutoSizing = true;
            contentText.fontSizeMin = contentBase * scale * 0.7f;
            contentText.fontSizeMax = contentBase * scale * 1.2f;
            contentText.fontSize = contentBase * scale;
        }
    }

    private static bool IsChineseCapableFont(TMP_FontAsset f)
    {
        if (f == null) return false;
        var n = f.name.ToLower();
        // LiberationSans / Roboto / Arial 等英文字体一律视为不含中文
        if (n.Contains("liberation") || n.Contains("roboto") || n.Contains("arial")) return false;
        return true;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        if (_visible && contentText != null)
        {
            if (player == null) FindPlayer();
            RefreshContent();
        }
    }

    private void Toggle()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("[PlayerStatsPanel] panelRoot 未绑定！请在 Inspector 中拖入或右键组件 → 生成默认面板");
            return;
        }

        // 暂停菜单打开时不响应
        if (battleUI != null && battleUI.menu != null && battleUI.menu.gameObject.activeSelf) return;

        // 对局未开始时按配置决定
        if (!allowBeforeGameStart && battleUI != null && !battleUI.startcount) return;

        _visible = !_visible;
        panelRoot.SetActive(_visible);
        if (_visible)
        {
            // 每次显示都再纠正一次字体、字号、背景、内边距
            FixChineseFontOnAllTMP();
            AutoScaleFontSizes();
            ApplyBackgroundAtRuntime();
            FixTextPadding();
            RefreshContent();
        }
    }

    /// <summary>
    /// 运行时兜底：从 Resources 加载简约背景并应用到 panelBackgroundImage。
    /// </summary>
    private void ApplyBackgroundAtRuntime()
    {
        var newBg = Resources.Load<Sprite>("UI/PanelBg_Stats");
        if (newBg == null) return;
        panelBackgroundSprite = newBg;

        if (panelRoot == null) return;
        var allImgs = panelRoot.GetComponentsInChildren<Image>(true);
        foreach (var im in allImgs)
        {
            if (im == null) continue;
            var n = im.gameObject.name.ToLower();
            // 覆盖所有明显是背景/边框的 Image
            if (n.Contains("bg") || n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("border") || im.gameObject == panelRoot)
            {
                im.sprite = newBg;
                im.type = Image.Type.Sliced;
                im.color = Color.white;
            }
        }
        // 根节点 Image 强制覆盖
        var rootImg = panelRoot.GetComponent<Image>();
        if (rootImg != null)
        {
            rootImg.sprite = newBg;
            rootImg.type = Image.Type.Sliced;
            rootImg.color = Color.white;
            panelBackgroundImage = rootImg;
        }
    }

    private void FindPlayer()
    {
        var pgo = GameObject.Find("playerlayer");
        if (pgo != null && pgo.transform.childCount > 0)
            player = pgo.transform.GetChild(0).GetComponent<Player>();
    }

    // ── 刷新内容 ──

    private void RefreshContent()
    {
        if (player == null || contentText == null) return;

        string s = $"攻击力  <color=#FFA040>{player.atk:F1}</color>\n"
                 + $"防御力  <color=#80C0FF>{player.def:F1}</color>\n"
                 + $"暴击率  <color=#FFD24A>{player.CR:F1}%</color>\n"
                 + $"暴伤    <color=#FF6050>{player.CD:F1}%</color>\n"
                 + $"闪避率  <color=#80FFC0>{player.EVA}%</color>\n"
                 + $"经验    <color=#C0C0FF>×{player.DR:F1}</color>\n"
                 + $"移速    <color=#FFFFFF>{player.speed}</color>\n"
                 + $"回血    <color=#FF80C0>{player.regen}/s</color>\n"
                 + $"\n生命  <color=#FF4040>{player.health}/{player.healthmax}</color>\n"
                 + $"等级  <color=#FFD24A>{player.level}</color>  exp {player.exp}/{player.expmax}\n"
                 + $"\n估算 DPS  <color=#FF4040>{CalcDPS():F1}</color>";

        contentText.text = s;
    }

    private float CalcDPS()
    {
        if (player == null || player.SkillList == null) return 0f;
        float total = 0f;
        CalcSkillListDPS(player.SkillList, ref total);
        if (player.SkillListClone != null)
            CalcSkillListDPS(player.SkillListClone, ref total);
        return total;
    }

    private void CalcSkillListDPS(Transform list, ref float total)
    {
        foreach (Transform t in list)
        {
            if (t == null) continue;
            var sb = t.GetComponent<Skillbase>();
            if (sb == null) continue;

            float cd = sb.CDtime > 0.001f ? sb.CDtime : 1f;
            float baseDmg = sb.damage * (1f + player.atk * 0.1f);
            float critM = 1f + (player.CR / 100f) * (player.CD / 100f - 1f);
            float freq = Mathf.Lerp(1f, 1.8f, Mathf.Clamp01(player.DR / 10f));
            total += baseDmg * critM * sb.number * freq / cd;
        }
    }

#if UNITY_EDITOR
    // ── 一键生成默认面板（右键组件 → 生成默认面板）──

    [ContextMenu("生成默认面板")]
    private void BuildDefaultPanelInEditor()
    {
        Canvas canvas = FindCanvasInScene();
        if (canvas == null)
        {
            Debug.LogError("[PlayerStatsPanel] 场景中没有 Canvas！");
            return;
        }

        // 如果已有面板根，先删除
        if (panelRoot != null)
        {
            DestroyImmediate(panelRoot);
            panelRoot = null;
        }

        // 面板根
        var rootGo = new GameObject("PlayerStatsPanel_Root", typeof(RectTransform), typeof(Image));
        rootGo.transform.SetParent(canvas.transform, false);
        var rt = (RectTransform)rootGo.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 440f);
        rt.anchoredPosition = new Vector2(20f, 0f);

        var bgImg = rootGo.GetComponent<Image>();
        bgImg.color = new Color(0.06f, 0.05f, 0.14f, 0.94f);
        bgImg.raycastTarget = false;
        panelBackgroundImage = bgImg;
        panelRoot = rootGo;

        // 尝试加载 AI 生成的横幅背景
        TryLoadAssets();
        if (panelBackgroundSprite != null)
        {
            bgImg.sprite = panelBackgroundSprite;
            bgImg.type = Image.Type.Sliced;
        }

        // 标题
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(rt, false);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, 36f);
        trt.anchoredPosition = new Vector2(0f, -4f);

        titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.text = "角色属性  [Tab]";
        titleText.fontSize = 18;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(1f, 0.86f, 0.4f);
        titleText.fontStyle = FontStyles.Bold;
        titleText.raycastTarget = false;
        TryAssignFont(titleText);

        // 内容
        var contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(rt, false);
        var crt = (RectTransform)contentGo.transform;
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = new Vector2(14f, 10f);
        crt.offsetMax = new Vector2(-14f, -44f);

        contentText = contentGo.AddComponent<TextMeshProUGUI>();
        contentText.fontSize = 13;
        contentText.alignment = TextAlignmentOptions.TopLeft;
        contentText.color = new Color(0.92f, 0.92f, 0.92f);
        contentText.raycastTarget = false;
        contentText.lineSpacing = 2f;
        TryAssignFont(contentText);

        rootGo.SetActive(false);

        UnityEditor.EditorUtility.SetDirty(this);
        UnityEditor.EditorUtility.SetDirty(rootGo);
        Debug.Log("[PlayerStatsPanel] 默认面板已生成，可在 Scene 视图中调整位置/大小/字体");
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
        // 【最高优先级】Resources 下的简约背景（薄边框、干净可读）
        var res = Resources.Load<Sprite>("UI/PanelBg_Stats");
        if (res != null) { panelBackgroundSprite = res; return; }

        // 次优：项目里的 Simple_clean 系列
        string[] simpleGuids = UnityEditor.AssetDatabase.FindAssets("Simple_clean_pixel_art_UI_pane t:Sprite");
        if (simpleGuids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(simpleGuids[simpleGuids.Length - 1]);
            panelBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            return;
        }

        // 兜底：旧素材
        string[] guids = UnityEditor.AssetDatabase.FindAssets("pixel_art__dark_fantasy_UI_pan t:Sprite");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            panelBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
#endif

    // ── 运行时工具（非 Editor 也需编译）────────────────────────────

    private static TMP_FontAsset _cachedChineseFont;

    private static TMP_FontAsset FindBestFont()
    {
        if (_cachedChineseFont != null) return _cachedChineseFont;

        // 1. 【最高优先级】强制加载中文字体 heiti SDF（LiberationSans 不含中文字形，必须避免）
#if UNITY_EDITOR
        // 编辑器：直接按路径加载
        var heitiEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/像素幸存者资源包/字体/heiti SDF.asset");
        if (heitiEditor != null) { _cachedChineseFont = heitiEditor; return heitiEditor; }
#endif
        // 运行时：从 Resources 加载
        var heitiRes = Resources.Load<TMP_FontAsset>("Fonts/heiti SDF");
        if (heitiRes != null) { _cachedChineseFont = heitiRes; return heitiRes; }

        // 2. 扫描当前场景中已加载的所有 TMP_FontAsset，找名字含 "heiti" 的
        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in allFonts)
        {
            if (f == null) continue;
            var n = f.name.ToLower();
            if (n.Contains("heiti") || n.Contains("黑体") || n.Contains("chinese") || n.Contains("cjk"))
            { _cachedChineseFont = f; return f; }
        }

        // 3. 从 battleUI.health 拿（可能是中文字体）
        var bui = FindObjectOfType<battleUI>();
        if (bui != null && bui.health != null && bui.health.font != null)
        {
            _cachedChineseFont = bui.health.font;
            return bui.health.font;
        }

        // 4. 最后兜底：TMP 默认（LiberationSans，无中文）
        var def = TMP_Settings.defaultFontAsset;
        if (def != null) return def;

        return null;
    }

    private void TryAssignFont(TextMeshProUGUI tmp)
    {
        var font = FindBestFont();
        if (font != null) tmp.font = font;
    }
}
