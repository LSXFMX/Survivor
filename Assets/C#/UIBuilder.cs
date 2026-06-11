using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时快速搭建 uGUI 控件的工具类。
/// 用于 SettingsPanelUI / InstructionsPanelUI 在 Awake 时自动构建 UI，
/// 避免在 Inspector 里手搭一堆子节点。
///
/// 设计原则：所有 Create* 方法返回的控件本身已是激活、可交互、外观可见的（用纯色 + 半透明）。
/// 不依赖任何项目自带的 sprite，使用 Image 默认白色矩形即可（Unity 内置）。
/// </summary>
public static class UIBuilder
{
    private static readonly Color ButtonNormal   = new Color(0.20f, 0.20f, 0.22f, 0.95f);
    private static readonly Color ButtonHighlight= new Color(0.30f, 0.30f, 0.33f, 1f);
    private static readonly Color ButtonPressed  = new Color(0.12f, 0.12f, 0.13f, 1f);

    /// <summary>创建文本（标签/标题等）。</summary>
    public static TextMeshProUGUI CreateText(
        RectTransform parent, string name, string content, int fontSize, FontStyles style,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        if (font != null) tmp.font = font;

        return tmp;
    }

    /// <summary>创建一行「Label + Toggle」（label 在左，勾选框在右）。返回 Toggle。</summary>
    public static Toggle CreateToggle(RectTransform parent, string name, string label,
        Vector2 topLeftAnchoredFromTop, Vector2 sizeDelta, TMP_FontAsset font = null)
    {
        // 行容器
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = new Vector2(0f, 1f);
        rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = topLeftAnchoredFromTop;
        rrt.sizeDelta = sizeDelta;

        // Label
        CreateText(rrt, "Label", label, 26, FontStyles.Normal,
            new Vector2(0f, 0f), new Vector2(0.7f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), Vector2.zero, font).alignment = TextAlignmentOptions.MidlineLeft;

        // Toggle GO
        var tg = new GameObject("Toggle", typeof(RectTransform));
        tg.transform.SetParent(rrt, false);
        var tgrt = (RectTransform)tg.transform;
        tgrt.anchorMin = new Vector2(1f, 0.5f);
        tgrt.anchorMax = new Vector2(1f, 0.5f);
        tgrt.pivot = new Vector2(1f, 0.5f);
        tgrt.anchoredPosition = new Vector2(0f, 0f);
        tgrt.sizeDelta = new Vector2(48f, 48f);
        var toggle = tg.AddComponent<Toggle>();

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(tgrt, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.25f);
        toggle.targetGraphic = bgImg;

        // Checkmark
        var ckGo = new GameObject("Checkmark", typeof(RectTransform));
        ckGo.transform.SetParent(bgRt, false);
        var ckRt = (RectTransform)ckGo.transform;
        ckRt.anchorMin = new Vector2(0.15f, 0.15f);
        ckRt.anchorMax = new Vector2(0.85f, 0.85f);
        ckRt.offsetMin = Vector2.zero; ckRt.offsetMax = Vector2.zero;
        var ckImg = ckGo.AddComponent<Image>();
        ckImg.color = new Color(0.35f, 0.85f, 0.4f, 1f);
        toggle.graphic = ckImg;

        toggle.isOn = true;
        return toggle;
    }

    /// <summary>创建一行「Label + Slider」。返回 Slider。</summary>
    public static Slider CreateSlider(RectTransform parent, string name, string label,
        Vector2 topLeftAnchoredFromTop, Vector2 sizeDelta, TMP_FontAsset font = null)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rrt = (RectTransform)row.transform;
        rrt.anchorMin = new Vector2(0f, 1f);
        rrt.anchorMax = new Vector2(0f, 1f);
        rrt.pivot = new Vector2(0f, 1f);
        rrt.anchoredPosition = topLeftAnchoredFromTop;
        rrt.sizeDelta = sizeDelta;

        // Label
        var lbl = CreateText(rrt, "Label", label, 26, FontStyles.Normal,
            new Vector2(0f, 0f), new Vector2(0.4f, 1f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), Vector2.zero, font);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;

        // Slider GO
        var sg = new GameObject("Slider", typeof(RectTransform));
        sg.transform.SetParent(rrt, false);
        var sgrt = (RectTransform)sg.transform;
        sgrt.anchorMin = new Vector2(0.4f, 0.5f);
        sgrt.anchorMax = new Vector2(1f, 0.5f);
        sgrt.pivot = new Vector2(0.5f, 0.5f);
        sgrt.offsetMin = new Vector2(10f, -10f);
        sgrt.offsetMax = new Vector2(0f, 10f);

        var slider = sg.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f; slider.maxValue = 1f;

        // Background
        var bgGo = new GameObject("Background", typeof(RectTransform));
        bgGo.transform.SetParent(sgrt, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = new Vector2(0f, 0.4f);
        bgRt.anchorMax = new Vector2(1f, 0.6f);
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.2f);

        // Fill Area / Fill
        var fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sgrt, false);
        var faRt = (RectTransform)fillArea.transform;
        faRt.anchorMin = new Vector2(0f, 0.4f);
        faRt.anchorMax = new Vector2(1f, 0.6f);
        faRt.offsetMin = new Vector2(5f, 0f);
        faRt.offsetMax = new Vector2(-15f, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(faRt, false);
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero; fillRt.offsetMax = Vector2.zero;
        fillRt.sizeDelta = new Vector2(10f, 0f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.75f, 1f, 1f);
        slider.fillRect = fillRt;

        // Handle Slide Area / Handle
        var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sgrt, false);
        var haRt = (RectTransform)handleArea.transform;
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(10f, 0f);
        haRt.offsetMax = new Vector2(-10f, 0f);

        var hGo = new GameObject("Handle", typeof(RectTransform));
        hGo.transform.SetParent(haRt, false);
        var hRt = (RectTransform)hGo.transform;
        hRt.sizeDelta = new Vector2(20f, 30f);
        var hImg = hGo.AddComponent<Image>();
        hImg.color = Color.white;
        slider.handleRect = hRt;
        slider.targetGraphic = hImg;

        slider.value = 0.6f;
        return slider;
    }

    /// <summary>创建按钮（带文本）。</summary>
    public static Button CreateButton(RectTransform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta, TMP_FontAsset font = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = sizeDelta;

        var img = go.AddComponent<Image>();
        img.color = ButtonNormal;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor = ButtonNormal;
        colors.highlightedColor = ButtonHighlight;
        colors.pressedColor = ButtonPressed;
        colors.selectedColor = ButtonHighlight;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        btn.colors = colors;

        // 文本
        var txt = CreateText(rt, "Text", text, 28, FontStyles.Bold,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, font);
        txt.alignment = TextAlignmentOptions.Center;
        // 让文本填满按钮
        var txtRt = (RectTransform)txt.transform;
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero; txtRt.offsetMax = Vector2.zero;

        return btn;
    }
}
