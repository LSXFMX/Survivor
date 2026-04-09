using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 全局消息提示系统（自包含）
/// 用法：ToastManager.Show("消息内容");
///
/// Inspector 配置：
///   font            - 拖入 heiti SDF 字体资源
///   iconSprite      - 小图标 Sprite，嵌在每条消息最左侧
///   messageInterval - 每条消息弹出间隔（秒）
/// </summary>
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("字体 & 图标")]
    public TMP_FontAsset font;
    public Sprite iconSprite;

    [Header("设置")]
    public float displayTime = 2.5f;
    public float fadeTime = 0.5f;
    public float messageInterval = 0.3f;
    public int maxMessages = 5;
    public int fontSize = 20;
    public Color textColor = new Color(1f, 1f, 0.6f, 1f);

    private Canvas _canvas;
    private RectTransform _container;
    private readonly List<GameObject> _activeMessages = new List<GameObject>();
    private readonly Queue<string> _pendingMessages = new Queue<string>();
    private bool _isShowingMessage = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUI();
    }

    public static void Show(string message)
    {
        if (Instance == null)
        {
            var go = new GameObject("ToastManager");
            go.AddComponent<ToastManager>();
        }
        Instance.Enqueue(message);
    }

    private void Enqueue(string message)
    {
        _pendingMessages.Enqueue(message);
        if (!_isShowingMessage)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        _isShowingMessage = true;
        while (_pendingMessages.Count > 0)
        {
            AddMessage(_pendingMessages.Dequeue());
            yield return new WaitForSecondsRealtime(messageInterval);
        }
        _isShowingMessage = false;
    }

    // ── UI 构建 ──────────────────────────────────────────

    private void BuildUI()
    {
        var canvasGo = new GameObject("ToastCanvas");
        canvasGo.transform.SetParent(transform);
        DontDestroyOnLoad(canvasGo);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // 消息容器，锚定左下角
        var containerGo = new GameObject("MessageContainer");
        containerGo.transform.SetParent(canvasGo.transform, false);
        _container = containerGo.AddComponent<RectTransform>();
        _container.anchorMin = new Vector2(0f, 0f);
        _container.anchorMax = new Vector2(0.5f, 0.35f);
        _container.offsetMin = new Vector2(20f, 20f);
        _container.offsetMax = new Vector2(-20f, -20f);

        var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerLeft;
        vlg.spacing = 4f;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        containerGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void AddMessage(string message)
    {
        if (_container == null) BuildUI();

        if (_activeMessages.Count >= maxMessages)
        {
            var oldest = _activeMessages[0];
            _activeMessages.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }

        // ── 条目根对象 ──
        var item = new GameObject("ToastItem");
        item.transform.SetParent(_container, false);

        var rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 30f);

        var cg = item.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // 背景
        var bg = item.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        // 水平布局：图标 + 文字
        var hlg = item.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 4f;
        hlg.padding = new RectOffset(4, 8, 2, 2);
        hlg.childControlHeight = true;
        hlg.childControlWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = false;

        // ── 图标（最左侧）──
        if (iconSprite != null)
        {
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(item.transform, false);

            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(24f, 24f);

            var iconImg = iconGo.AddComponent<Image>();
            iconImg.sprite = iconSprite;
            iconImg.preserveAspect = true;

            var iconLayout = iconGo.AddComponent<LayoutElement>();
            iconLayout.minWidth = 24f;
            iconLayout.preferredWidth = 24f;
            iconLayout.flexibleWidth = 0f;
        }

        // ── 文字 ──
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(item.transform, false);

        var textRt = textGo.AddComponent<RectTransform>();
        textRt.sizeDelta = new Vector2(300f, 0f);

        var textLayout = textGo.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = message;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        if (font != null) tmp.font = font;

        _activeMessages.Add(item);
        StartCoroutine(MessageLifecycle(item, cg));
    }

    private IEnumerator MessageLifecycle(GameObject item, CanvasGroup cg)
    {
        // 淡入
        float t = 0f;
        while (t < 0.2f)
        {
            if (item == null) yield break;
            t += Time.unscaledDeltaTime;
            cg.alpha = t / 0.2f;
            yield return null;
        }
        cg.alpha = 1f;

        yield return new WaitForSecondsRealtime(displayTime);

        // 淡出
        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            if (item == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = 1f - elapsed / fadeTime;
            yield return null;
        }

        if (item != null)
        {
            _activeMessages.Remove(item);
            Destroy(item);
        }
    }
}
