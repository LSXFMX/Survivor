using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Boss 血条系统（v4）
///
/// 关键修复：
/// - 弃用精灵图（之前 BossBarBg 内部透明 + Sliced 边界导致整条血条看起来透明）
/// - 改用纯色 Image，背景深灰、边框更暗、fill 按血量变色，绝对不透明
/// - topOffset 加大到 80 像素确保避开计时器
/// - 多 Boss 垂直堆叠
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("布局（固定像素）")]
    public float barWidthPixels = 500f;   // 血条宽
    public float barHeight      = 28f;    // 血条高
    public float avatarSize     = 48f;    // 头像尺寸
    public float rowSpacing     = 10f;    // 多条血条间距
    public float topOffset      = 80f;    // 距屏幕顶偏移（避开计时器）
    public float barBorderWidth = 2f;    // 血条边框宽度

    private RectTransform _container;
    private readonly List<BossEntry> _entries = new List<BossEntry>();

    private class BossEntry
    {
        public enemy boss;
        public SpriteRenderer bossSR;
        public RectTransform root;
        public Image avatar;
        public RectTransform fillRt;
        public Image fill;
        public bool dead;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildContainer();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildContainer()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("BossHealthBarContainer", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        _container = go.GetComponent<RectTransform>();
        _container.anchorMin = new Vector2(0.5f, 1f);
        _container.anchorMax = new Vector2(0.5f, 1f);
        _container.pivot     = new Vector2(0.5f, 1f);
        _container.anchoredPosition = new Vector2(0f, -topOffset);
        _container.sizeDelta = new Vector2(avatarSize + 10f + barWidthPixels, 500f);
    }

    void LateUpdate()
    {
        // 清理已死 Boss
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            if (e.boss == null || e.dead)
            {
                if (e.root != null) Destroy(e.root.gameObject);
                _entries.RemoveAt(i);
                continue;
            }

            float maxHp = Mathf.Max(1, e.boss.healthmax);
            float pct  = Mathf.Clamp01((float)e.boss.health / maxHp);

            // 直接控制 fill 宽度 → 从右往左掉血
            float w = Mathf.Max(0f, barWidthPixels * pct);
            e.fillRt.sizeDelta = new Vector2(w, e.fillRt.sizeDelta.y);
            // fill 颜色：低血红 → 中血黄 → 高血绿（全部 alpha=1，不透明）
            e.fill.color = pct > 0.5f
                ? Color.Lerp(new Color(1f, 0.85f, 0.1f, 1f), new Color(0.2f, 1f, 0.3f, 1f), (pct - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.2f, 0.2f, 1f), new Color(1f, 0.85f, 0.1f, 1f), pct * 2f);

            // 实时头像
            if (e.bossSR != null && e.bossSR.sprite != null)
                e.avatar.sprite = e.bossSR.sprite;

            if (e.boss.health <= 0 || e.boss.rolestate == enemy.state.dead)
                e.dead = true;
        }

        // 垂直堆叠
        float y = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.root == null) continue;
            float rowH = avatarSize + 6f;
            e.root.anchoredPosition = new Vector2(0f, -y);
            y += rowH + rowSpacing;
        }
    }

    // ─── 公开 API ───

    public static void Register(enemy boss)
    {
        if (Instance == null) EnsureInstance();
        if (Instance == null) return;
        Instance.DoRegister(boss);
    }

    public static void Unregister(enemy boss)
    {
        if (Instance == null) return;
        Instance._entries.RemoveAll(e => e.boss == boss);
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var bui = FindObjectOfType<battleUI>();
        if (bui == null) return;
        Canvas canvas = bui.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        var go = new GameObject("[BossHealthBarUI]");
        go.transform.SetParent(canvas.transform, false);
        Instance = go.AddComponent<BossHealthBarUI>();
    }

    private void DoRegister(enemy boss)
    {
        if (_entries.Exists(e => e.boss == boss)) return;
        if (_container == null) BuildContainer();
        if (_container == null) return;

        float rowH = avatarSize + 6f;

        // 条目根
        var rootGo = new GameObject("BossBar_" + boss.rolename, typeof(RectTransform));
        rootGo.transform.SetParent(_container, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(avatarSize + 10f + barWidthPixels, rowH);
        rootRt.anchoredPosition = Vector2.zero;

        // 头像背景（纯色黑底，绝对不透明）
        var avatarBgGo = new GameObject("AvatarBg", typeof(RectTransform));
        avatarBgGo.transform.SetParent(rootGo.transform, false);
        var abrt = avatarBgGo.GetComponent<RectTransform>();
        abrt.anchorMin = abrt.anchorMax = new Vector2(0f, 0.5f);
        abrt.pivot = new Vector2(0f, 0.5f);
        abrt.sizeDelta = new Vector2(avatarSize + 4f, avatarSize + 4f);
        abrt.anchoredPosition = Vector2.zero;
        var abImg = avatarBgGo.AddComponent<Image>();
        abImg.color = new Color(0f, 0f, 0f, 1f);
        abImg.raycastTarget = false;

        // 头像
        var avatarGo = new GameObject("Avatar", typeof(RectTransform));
        avatarGo.transform.SetParent(rootGo.transform, false);
        var art = avatarGo.GetComponent<RectTransform>();
        art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f);
        art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = new Vector2(avatarSize, avatarSize);
        art.anchoredPosition = new Vector2(2f, 0f);
        var avatarImg = avatarGo.AddComponent<Image>();
        avatarImg.preserveAspect = true;
        avatarImg.raycastTarget = false;

        // 血条外框（暗色边框，模拟精灵的边框效果但纯色绝对不透明）
        float barX = avatarSize + 10f;
        var frameGo = new GameObject("BarFrame", typeof(RectTransform));
        frameGo.transform.SetParent(rootGo.transform, false);
        var frameRt = frameGo.GetComponent<RectTransform>();
        frameRt.anchorMin = frameRt.anchorMax = new Vector2(0f, 0.5f);
        frameRt.pivot = new Vector2(0f, 0.5f);
        float frameH = barHeight + barBorderWidth * 2f;
        frameRt.sizeDelta = new Vector2(barWidthPixels + barBorderWidth * 2f, frameH);
        frameRt.anchoredPosition = new Vector2(barX - barBorderWidth, 0f);
        var frameImg = frameGo.AddComponent<Image>();
        frameImg.color = new Color(0.05f, 0.05f, 0.05f, 1f); // 黑色边框
        frameImg.raycastTarget = false;

        // 血条背景（深灰色，绝对不透明）
        var bgGo = new GameObject("BarBg", typeof(RectTransform));
        bgGo.transform.SetParent(frameGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(barWidthPixels, barHeight);
        bgRt.anchoredPosition = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // 深灰背景
        bgImg.raycastTarget = false;

        // 血量填充（纯色，左对齐，宽度由代码控制）
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = Vector2.zero;
        fillRt.sizeDelta = new Vector2(barWidthPixels, 0f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 1f, 0.3f, 1f); // 默认绿色
        fillImg.raycastTarget = false;

        // 初始头像
        var sr = boss.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            avatarImg.sprite = sr.sprite;

        _entries.Add(new BossEntry
        {
            boss    = boss,
            bossSR  = sr,
            root    = rootRt,
            avatar  = avatarImg,
            fill    = fillImg,
            fillRt  = fillRt,
            dead    = false,
        });
    }
}
