using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「操作说明」按钮上的红点提醒。
/// - 由 battleUI 在 Awake 时自动扫描暂停菜单按钮并附加，无需场景配置。
/// - 运行时动态创建：右上角红点 + 外圈光晕（发光涟漪）。
/// - 红点呼吸缩放 + 亮度脉动；光晕外扩淡出循环。
/// - 显隐基于 <see cref="InstructionsPanelUI.HasNewUnlockToShow"/>。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class InstructionsNewBadge : MonoBehaviour
{
    private RectTransform _root;   // 红点 + 光晕共同容器
    private Image _dot;            // 实心红点
    private Image _glow;           // 外圈光晕
    private float _checkTimer;
    private float _animTime;

    // 视觉参数
    private const float DotSize       = 20f;
    private const float GlowMaxScale  = 2.4f;   // 光晕最大放大倍数
    private const float PulsePeriod   = 1.0f;   // 呼吸周期(秒)
    private const float GlowPeriod    = 1.2f;   // 光晕扩散周期(秒)

    void Awake()
    {
        EnsureDot();
        Refresh();
    }

    void OnEnable() { Refresh(); _animTime = 0f; }

    void Update()
    {
        // 每 0.5s 检查一次显隐，避免每帧读 PlayerPrefs
        _checkTimer += Time.unscaledDeltaTime;
        if (_checkTimer >= 0.5f)
        {
            _checkTimer = 0f;
            Refresh();
        }

        if (_root == null || !_root.gameObject.activeSelf) return;

        _animTime += Time.unscaledDeltaTime;

        // —— 红点呼吸：缩放 0.85 ~ 1.15，颜色亮度 0.7 ~ 1.0 ——
        float p = (Mathf.Sin(_animTime * Mathf.PI * 2f / PulsePeriod) + 1f) * 0.5f; // 0~1
        float scale = Mathf.Lerp(0.85f, 1.15f, p);
        _dot.rectTransform.localScale = new Vector3(scale, scale, 1f);

        float bright = Mathf.Lerp(0.75f, 1f, p);
        _dot.color = new Color(1f, 0.25f * bright, 0.25f * bright, 1f);

        // —— 外圈光晕：从 1.0 扩到 GlowMaxScale，alpha 由 0.55 衰减到 0 ——
        float g = Mathf.Repeat(_animTime / GlowPeriod, 1f); // 0~1
        float gScale = Mathf.Lerp(1f, GlowMaxScale, g);
        _glow.rectTransform.localScale = new Vector3(gScale, gScale, 1f);
        var gc = _glow.color;
        gc.a = Mathf.Lerp(0.55f, 0f, g);
        _glow.color = gc;
    }

    public void Refresh()
    {
        if (_root == null) EnsureDot();
        if (_root != null) _root.gameObject.SetActive(InstructionsPanelUI.HasNewUnlockToShow());
    }

    private void EnsureDot()
    {
        if (_root != null) return;

        // 容器（不缩放，仅定位到右上角）
        var rootGo = new GameObject("NewBadge", typeof(RectTransform));
        rootGo.transform.SetParent(transform, false);
        _root = (RectTransform)rootGo.transform;
        _root.anchorMin = new Vector2(1f, 1f);
        _root.anchorMax = new Vector2(1f, 1f);
        _root.pivot     = new Vector2(1f, 1f);
        _root.anchoredPosition = new Vector2(-4f, -4f);
        _root.sizeDelta = new Vector2(DotSize, DotSize);

        var sprite = BuildCircleSprite();

        // 光晕（更大，半透明，置于红点下层；先创建 = 渲染在下）
        var glowGo = new GameObject("Glow", typeof(RectTransform));
        glowGo.transform.SetParent(_root, false);
        var glowRt = (RectTransform)glowGo.transform;
        glowRt.anchorMin = glowRt.anchorMax = new Vector2(0.5f, 0.5f);
        glowRt.pivot     = new Vector2(0.5f, 0.5f);
        glowRt.anchoredPosition = Vector2.zero;
        glowRt.sizeDelta = new Vector2(DotSize, DotSize);
        _glow = glowGo.AddComponent<Image>();
        _glow.sprite = sprite;
        _glow.color = new Color(1f, 0.3f, 0.3f, 0.55f);
        _glow.raycastTarget = false;

        // 红点本体
        var dotGo = new GameObject("Dot", typeof(RectTransform));
        dotGo.transform.SetParent(_root, false);
        var dotRt = (RectTransform)dotGo.transform;
        dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
        dotRt.pivot     = new Vector2(0.5f, 0.5f);
        dotRt.anchoredPosition = Vector2.zero;
        dotRt.sizeDelta = new Vector2(DotSize, DotSize);
        _dot = dotGo.AddComponent<Image>();
        _dot.sprite = sprite;
        _dot.color = new Color(1f, 0.25f, 0.25f, 1f);
        _dot.raycastTarget = false;
    }

    /// <summary>
    /// 程序化生成一张带柔边的圆形 Sprite（中心实心，边缘平滑过渡到透明），
    /// 让红点和光晕都自带柔光观感，无需图集资源。
    /// </summary>
    private static Sprite _cachedCircle;
    private static Sprite BuildCircleSprite()
    {
        if (_cachedCircle != null) return _cachedCircle;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float r = size * 0.5f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r; // 0(中心) ~ 1(边)
                // 中心 70% 全实心，外侧 30% 平滑过渡
                float a = d <= 0.7f ? 1f : Mathf.SmoothStep(1f, 0f, (d - 0.7f) / 0.3f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _cachedCircle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        _cachedCircle.name = "BadgeCircle";
        return _cachedCircle;
    }
}
