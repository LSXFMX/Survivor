using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI Boss 血条系统：在屏幕 overlay 上为所有激活的 Boss 显示血条。
/// 替代 WorldBossHealthBar——使用精灵图血条，视觉更好。
/// 血条宽 = 屏幕 1/2，居中，时间文字下方；多 Boss 垂直排列；左侧实时头像。
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("布局")]
    public float barWidthRatio = 0.5f;
    public float barHeight     = 28f;
    public float avatarSize    = 44f;
    public float spacing       = 6f;
    public float topOffset     = 60f;

    private RectTransform _container;
    private readonly List<BossEntry> _entries = new List<BossEntry>();

    private class BossEntry
    {
        public enemy boss;
        public SpriteRenderer bossSR;
        public RectTransform root;
        public Image avatar;
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
        _container.sizeDelta = new Vector2(Screen.width * barWidthRatio + avatarSize + 10f, 400f);
    }

    void LateUpdate()
    {
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
            float pct = Mathf.Clamp01((float)e.boss.health / maxHp);
            e.fill.fillAmount = pct;

            // 颜色：高血绿 → 低血红
            e.fill.color = pct > 0.5f ? Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f)
                         : Color.Lerp(Color.red, Color.yellow, pct * 2f);

            // 实时头像
            if (e.bossSR != null && e.bossSR.sprite != null)
                e.avatar.sprite = e.bossSR.sprite;

            if (e.boss.health <= 0 || e.boss.rolestate == enemy.state.dead)
                e.dead = true;
        }

        float y = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.root == null) continue;
            e.root.anchoredPosition = new Vector2(0f, -y);
            y += barHeight + spacing + avatarSize * 0.15f;
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

        float barW = Screen.width * barWidthRatio;
        float totalW = avatarSize + 6f + barW;

        // 条目根
        var rootGo = new GameObject("BossBar_" + boss.rolename, typeof(RectTransform));
        rootGo.transform.SetParent(_container, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(totalW, Mathf.Max(barHeight, avatarSize));
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

        // 血条背景图（精灵）
        float barX = avatarSize + 6f;
        var bgGo = new GameObject("BarBg", typeof(RectTransform));
        bgGo.transform.SetParent(rootGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.sizeDelta = new Vector2(barW, barHeight);
        bgRt.anchoredPosition = new Vector2(barX, 0f);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.sprite = Resources.Load<Sprite>("UI/BossBarBg");
        bgImg.type = Image.Type.Sliced;
        bgImg.raycastTarget = false;

        // 填充图（精灵）——关键：fillOrigin=Left 确保从右往左掉血
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.sprite = Resources.Load<Sprite>("UI/BossBarFill");
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        fillImg.color = Color.green;
        fillImg.raycastTarget = false;

        // 初始头像
        var sr = boss.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            avatarImg.sprite = sr.sprite;

        _entries.Add(new BossEntry
        {
            boss = boss,
            bossSR = sr,
            root = rootRt,
            avatar = avatarImg,
            fill = fillImg,
            dead = false,
        });
    }
}
