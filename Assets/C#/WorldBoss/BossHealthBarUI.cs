using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Boss 血条系统：屏幕 overlay 上为所有激活的 Boss 显示血条。
///
/// 关键设计变更（v3）：
/// - 不再使用 Image.Type.Filled（精灵边界 + pivot 不可靠导致掉血方向/比例全错）
/// - 改用 Image + 直接控制 fill RectTransform 宽度 = fillAmount * barWidthPixels
/// - 血条使用固定合理宽度（barWidthPixels），不依赖 Screen.width 避免超屏
/// - 多个 Boss 垂直堆叠在时间文字下方
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("布局（固定像素，不依赖屏幕尺寸）")]
    public float barWidthPixels = 500f;   // 血条宽
    public float barHeight      = 24f;    // 血条高
    public float avatarSize     = 44f;    // 头像尺寸
    public float rowSpacing     = 8f;     // 多条血条间距
    public float topOffset      = 60f;    // 距屏幕顶偏移

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
        _container.sizeDelta = new Vector2(avatarSize + 8f + barWidthPixels, 500f);
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
            float w = barWidthPixels * pct;
            e.fillRt.sizeDelta = new Vector2(w, e.fillRt.sizeDelta.y);
            // fill 颜色：低血红 → 中血黄 → 高血绿
            e.fill.color = pct > 0.5f
                ? Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(0.2f, 1f, 0.3f), (pct - 0.5f) * 2f)
                : Color.Lerp(new Color(1f, 0.2f, 0.2f), new Color(1f, 0.85f, 0.1f), pct * 2f);

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

        // 条目根（垂直堆叠用）
        var rootGo = new GameObject("BossBar_" + boss.rolename, typeof(RectTransform));
        rootGo.transform.SetParent(_container, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(avatarSize + 8f + barWidthPixels, rowH);
        rootRt.anchoredPosition = Vector2.zero;

        // 头像背景（深色底片）
        var avatarBgGo = new GameObject("AvatarBg", typeof(RectTransform));
        avatarBgGo.transform.SetParent(rootGo.transform, false);
        var abrt = avatarBgGo.GetComponent<RectTransform>();
        abrt.anchorMin = abrt.anchorMax = new Vector2(0f, 0.5f);
        abrt.pivot = new Vector2(0f, 0.5f);
        abrt.sizeDelta = new Vector2(avatarSize + 4f, avatarSize + 4f);
        abrt.anchoredPosition = Vector2.zero;
        var abImg = avatarBgGo.AddComponent<Image>();
        abImg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
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

        // 血条背景图（精灵 + Sliced）
        float barX = avatarSize + 8f;
        var bgGo = new GameObject("BarBg", typeof(RectTransform));
        bgGo.transform.SetParent(rootGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.sizeDelta = new Vector2(barWidthPixels, barHeight);
        bgRt.anchoredPosition = new Vector2(barX, 0f);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = Resources.Load<Sprite>("UI/BossBarBg");
        bgImg.type = Image.Type.Sliced;
        bgImg.raycastTarget = false;

        // 血量填充 — width 由代码控制（不用 Filled 裁剪）
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(3f, 0f);  // 内边距
        fillRt.sizeDelta = new Vector2(barWidthPixels - 6f, -6f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = Resources.Load<Sprite>("UI/BossBarFill");
        fillImg.type = Image.Type.Sliced;
        fillImg.color = new Color(0.2f, 1f, 0.3f, 1f);
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
