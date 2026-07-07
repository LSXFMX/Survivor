using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI Boss 血条系统：在战斗 UI 中为所有激活的 Boss 显示血条。
/// 
/// 设计：
/// - 替代 WorldBossHealthBar（世界空间 Canvas 小血条，有错位问题）
/// - 在屏幕 overlay 上显示大血条（宽 = 屏幕的 1/2，居中，时间文字下方）
/// - 多个 Boss 垂直排列
/// - 血条左侧有 Boss 头像（从 SpriteRenderer 实时取当前帧）
/// - 世界 Boss 被激活后才出现血条；关底 Boss 生成时立即出现
///
/// 使用：挂在 BattleUI 同 Canvas 下的任意对象上，或由 battleUI 运行时创建。
/// 外部通过静态方法 Register / Unregister 注册 Boss。
/// </summary>
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }

    [Header("布局")]
    [Tooltip("血条宽度占屏幕比例")]
    public float barWidthRatio = 0.5f;
    [Tooltip("单条血条高度（像素）")]
    public float barHeight = 28f;
    [Tooltip("头像尺寸（像素）")]
    public float avatarSize = 44f;
    [Tooltip("条目间距（像素）")]
    public float spacing = 6f;
    [Tooltip("距屏幕顶部偏移（像素）—— 时间文字下方")]
    public float topOffset = 60f;

    private RectTransform _container;
    private readonly List<BossEntry> _entries = new List<BossEntry>();

    private class BossEntry
    {
        public enemy boss;
        public SpriteRenderer bossSR;
        public RectTransform root;
        public Image avatar;
        public Image barBg;
        public Image barFill;
        public TextMeshProUGUI nameText;
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
        // 找到 overlay canvas（battleUI 所在的 canvas）
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var go = new GameObject("BossHealthBarContainer", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        _container = go.GetComponent<RectTransform>();
        // 顶部居中锚定
        _container.anchorMin = new Vector2(0.5f, 1f);
        _container.anchorMax = new Vector2(0.5f, 1f);
        _container.pivot = new Vector2(0.5f, 1f);
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

            // 更新血条
            float pct = (float)e.boss.health / Mathf.Max(1, e.boss.healthmax);
            e.barFill.fillAmount = Mathf.Clamp01(pct);

            // 更新颜色（高血绿 → 低血红）
            e.barFill.color = pct > 0.5f ? Color.Lerp(Color.yellow, Color.green, (pct - 0.5f) * 2f)
                            : Color.Lerp(Color.red, Color.yellow, pct * 2f);

            // 更新头像（实时帧）
            if (e.bossSR != null && e.bossSR.sprite != null)
                e.avatar.sprite = e.bossSR.sprite;

            // Boss 死了 → 标记删除
            if (e.boss.health <= 0 || e.boss.rolestate == enemy.state.dead)
                e.dead = true;
        }

        // 重新排列位置
        float y = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];
            if (e.root == null) continue;
            e.root.anchoredPosition = new Vector2(0f, -y);
            y += barHeight + avatarSize * 0.2f + spacing;
        }
    }

    // ─── 公开 API ───

    /// <summary>注册一个 Boss 让 UI 显示血条。可在任何时候调用（激活后 / 生成时）。</summary>
    public static void Register(enemy boss)
    {
        if (Instance == null) EnsureInstance();
        if (Instance == null) return;
        Instance.DoRegister(boss);
    }

    /// <summary>注销（Boss 销毁前手动注销，或 LateUpdate 自动清理）。</summary>
    public static void Unregister(enemy boss)
    {
        if (Instance == null) return;
        Instance._entries.RemoveAll(e => e.boss == boss);
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        // 找到 battleUI 的 Canvas 并挂上
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
        // 防重复
        if (_entries.Exists(e => e.boss == boss)) return;
        if (_container == null) BuildContainer();
        if (_container == null) return;

        float barW = Screen.width * barWidthRatio;
        float totalW = avatarSize + 6f + barW;

        // 条目根节点
        var rootGo = new GameObject("BossBar_" + boss.rolename, typeof(RectTransform));
        rootGo.transform.SetParent(_container, false);
        var rootRt = rootGo.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 1f);
        rootRt.pivot = new Vector2(0.5f, 1f);
        rootRt.sizeDelta = new Vector2(totalW, Mathf.Max(barHeight, avatarSize));

        // 头像背景（深色底）
        var avatarBgGo = new GameObject("AvatarBg", typeof(RectTransform));
        avatarBgGo.transform.SetParent(rootGo.transform, false);
        var abrt = avatarBgGo.GetComponent<RectTransform>();
        abrt.anchorMin = abrt.anchorMax = new Vector2(0f, 0.5f);
        abrt.pivot = new Vector2(0f, 0.5f);
        abrt.sizeDelta = new Vector2(avatarSize + 4f, avatarSize + 4f);
        abrt.anchoredPosition = new Vector2(0f, 0f);
        var abImg = avatarBgGo.AddComponent<Image>();
        abImg.color = new Color(0f, 0f, 0f, 0.7f);
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

        // 血条背景
        float barX = avatarSize + 6f;
        var bgGo = new GameObject("BarBg", typeof(RectTransform));
        bgGo.transform.SetParent(rootGo.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.sizeDelta = new Vector2(barW, barHeight);
        bgRt.anchoredPosition = new Vector2(barX, 0f);
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        bgImg.raycastTarget = false;

        // 血条填充
        var fillGo = new GameObject("BarFill", typeof(RectTransform));
        fillGo.transform.SetParent(bgGo.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        fillImg.color = Color.green;
        fillImg.raycastTarget = false;

        // Boss 名字（血条内左侧）
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(bgGo.transform, false);
        var nrt = nameGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(6f, 0f);
        nrt.offsetMax = new Vector2(-6f, 0f);
        var nameTmp = nameGo.AddComponent<TextMeshProUGUI>();
        nameTmp.text = boss.rolename ?? "Boss";
        nameTmp.fontSize = 14;
        nameTmp.alignment = TextAlignmentOptions.Left;
        nameTmp.color = Color.white;
        nameTmp.raycastTarget = false;

        // 获取 SpriteRenderer
        var sr = boss.GetComponent<SpriteRenderer>();

        // 设置初始头像
        if (sr != null && sr.sprite != null)
            avatarImg.sprite = sr.sprite;

        // 记录
        _entries.Add(new BossEntry
        {
            boss = boss,
            bossSR = sr,
            root = rootRt,
            avatar = avatarImg,
            barBg = bgImg,
            barFill = fillImg,
            nameText = nameTmp,
            dead = false,
        });
    }
}
