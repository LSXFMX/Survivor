using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 游戏「控制台」输出的全局开关 + 统一入口。
///
/// - Debug.Log/LogColored：仅控制台开关启用时才输出到 Unity Console。
/// - ShowOnScreen：创建全屏居中紫色大字幕（渐入渐出），玩家在游戏中直接可见，
///   同样受 Console.Enabled 开关控制，关闭后不会出现屏上字幕。
///   使用 Unity 原生 Text（非 TMP）以天然支持中文字符回退。
/// </summary>
public static class GameConsole
{
    private const string KEY_ENABLED = "Console.Enabled";
    private static bool _enabled;
    private static bool _loaded;

    // 缓存的共享字体（Unity 原生 Font，系统回退天然支持中文）
    private static Font _sharedFont;
    private static Font GetFont()
    {
        if (_sharedFont == null)
            _sharedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _sharedFont;
    }

    /// <summary>控制台是否启用（默认开）。写入即持久化。</summary>
    public static bool Enabled
    {
        get { Load(); return _enabled; }
        set
        {
            _enabled = value; _loaded = true;
            PlayerPrefs.SetInt(KEY_ENABLED, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    private static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _enabled = PlayerPrefs.GetInt(KEY_ENABLED, 1) != 0;
    }

    /// <summary>仅在控制台启用时输出普通日志。</summary>
    public static void Log(string msg)
    {
        if (Enabled) Debug.Log(msg);
    }

    /// <summary>仅在控制台启用时输出带颜色的富文本日志。</summary>
    public static void LogColored(string msg, string hexColor)
    {
        if (Enabled) Debug.Log($"<color=#{hexColor}>{msg}</color>");
    }

    /// <summary>
    /// 在游戏屏幕上显示大字幕（玩家可见）。受 Console.Enabled 开关控制。
    /// 居中紫色大字、渐入 0.3s → 保持 duration → 渐出 0.5s → 自毁。
    /// </summary>
    public static void ShowOnScreen(string msg, float duration, float fontSize = 42f)
    {
        if (!Enabled) return;

        var go = new GameObject("GameConsole_OnScreen");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        go.AddComponent<GraphicRaycaster>().enabled = false;

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.65f);
        rt.anchorMax = new Vector2(0.5f, 0.65f);
        rt.sizeDelta = new Vector2(Screen.width * 0.85f, fontSize * 1.8f);
        rt.anchoredPosition = Vector2.zero;

        var txt = textGo.AddComponent<Text>();
        txt.text = msg;
        txt.font = GetFont();
        txt.fontSize = Mathf.RoundToInt(fontSize);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = new Color(0.65f, 0.30f, 0.95f, 0f);
        txt.fontStyle = FontStyle.Bold;
        // 黑色阴影增强可读性
        var shadow = textGo.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(2f, -2f);

        var fx = go.AddComponent<ConsoleOnScreenFx>();
        fx.Init(txt, duration);
    }

    /// <summary>挂在大字幕上的渐入渐出驱动器。</summary>
    private class ConsoleOnScreenFx : MonoBehaviour
    {
        private Text _txt;
        private float _life, _max;
        private const float FADE_IN = 0.3f, FADE_OUT = 0.5f;

        public void Init(Text txt, float duration)
        {
            _txt = txt;
            _max = Mathf.Max(0.01f, FADE_IN + duration + FADE_OUT);
            _life = _max;
        }

        private void Update()
        {
            if (_txt == null) { Destroy(gameObject); return; }
            _life -= Time.unscaledDeltaTime;
            float alpha;
            if (_life > _max - FADE_IN)
                alpha = 1f - (_life - (_max - FADE_IN)) / FADE_IN;
            else if (_life < FADE_OUT)
                alpha = _life / FADE_OUT;
            else
                alpha = 1f;
            _txt.color = new Color(0.65f, 0.30f, 0.95f, alpha);
            if (_life <= 0f) Destroy(gameObject);
        }
    }
}
