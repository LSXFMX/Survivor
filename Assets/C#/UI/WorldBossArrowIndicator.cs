using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 屏幕边缘箭头指示器：指向世界Boss方向，附带颜色区分头像 + 距离。
/// 挂在任意 GameObject 上即可（建议挂到 battleUI 或独立空物体）。
/// </summary>
public class WorldBossArrowIndicator : MonoBehaviour
{
    [Header("布局")]
    public float edgePadding   = 70f;   // 箭头距屏幕边缘的距离
    public float arrowSize     = 36f;   // 箭头图标尺寸
    public float portraitSize  = 28f;   // 头像圆圈直径
    public float maxShowDist   = 120f;  // 超出此距离才显示箭头（进入屏幕后隐藏）

    private Canvas            _canvas;
    private Camera            _cam;
    private Transform         _player;

    // 活跃指示器：enemy实例 → UI RectTransform
    private Dictionary<enemy, GameObject> _indicators
        = new Dictionary<enemy, GameObject>();

    // ──────── 颜色映射 ────────
    private static readonly Color ColMushroom = new Color(0.35f, 0.90f, 0.35f);
    private static readonly Color ColBat      = new Color(0.72f, 0.35f, 0.95f);
    private static readonly Color ColWolf     = new Color(0.95f, 0.30f, 0.30f);
    private static readonly Color ColSlime    = new Color(0.30f, 0.65f, 0.95f);

    // ──────── 图形生成 ────────
    private static Texture2D s_arrowTex;
    private static Sprite     s_arrowSprite;

    void Awake()
    {
        // 创建全屏透明 Canvas
        var go = new GameObject("WorldBossArrowCanvas");
        go.transform.SetParent(transform);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 200;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
    }

    void Start()
    {
        _cam = Camera.main;
        var bui = FindObjectOfType<battleUI>();
        if (bui != null && bui.player != null)
            _player = bui.player.transform;

        EnsureArrowSprite();
    }

    private static bool s_firstFrame = true;

    void Update()
    {
        // 懒初始化：Camera / Player 可能在 Start 时还未就绪
        if (_cam == null) _cam = Camera.main;
        if (_player == null)
        {
            var bui = FindObjectOfType<battleUI>();
            if (bui != null && bui.player != null)
                _player = bui.player.transform;
        }
        if (_cam == null || _player == null || _canvas == null) return;

        // 搜寻活跃的世界Boss——注意不能用 FindObjectsOfType<WorldBossBase>()
        // 因为 WorldBossSlime 等实体继承链路是 WorldBossSlime→SlimeBoss→enemy，而非 WorldBossBase。
        // 这里改为搜全部 enemy 再过滤 4 个世界Boss子类型，只处理少数实例，性能无压力。
        var all = FindObjectsOfType<enemy>();
        // 每秒一次诊断日志
        if (Time.frameCount % 60 == 0)
        {
            int alive = 0, dead = 0, total = 0;
            foreach (var b in all)
            {
                if (b == null || !IsWorldBoss(b)) continue;
                total++;
                if (!b.gameObject.activeInHierarchy) continue;
                if (b.rolestate == enemy.state.dead || b.health <= 0) { dead++; continue; }
                alive++;
            }
            Debug.Log($"[WorldBossArrow] 世界Boss总 {total} | 激活 {alive} | 死亡 {dead} | _indicators={_indicators.Count}");
        }

        // 移除已死亡/销毁的Boss指示器
        var toRemove = new List<enemy>();
        foreach (var kv in _indicators)
        {
            if (kv.Key == null || !kv.Key.gameObject.activeSelf
                || kv.Key.rolestate == enemy.state.dead || kv.Key.health <= 0)
                toRemove.Add(kv.Key);
        }
        foreach (var dead in toRemove)
        {
            if (_indicators.TryGetValue(dead, out var deadGo))
            {
                Destroy(deadGo);
                _indicators.Remove(dead);
            }
        }

        foreach (var boss in all)
        {
            if (boss == null || !IsWorldBoss(boss) || !boss.gameObject.activeSelf) continue;
            if (boss.rolestate == enemy.state.dead || boss.health <= 0) continue;

            if (!_indicators.ContainsKey(boss))
            {
                var indicatorGo = CreateIndicator(boss);
                _indicators[boss] = indicatorGo;
            }
        }

        // 更新所有指示器位置
        foreach (var kv in _indicators)
        {
            UpdateIndicator(kv.Key, kv.Value);
        }
    }

    private GameObject CreateIndicator(enemy boss)
    {
        var go = new GameObject($"Arrow_{GetBossFaction(boss)}_{boss.GetInstanceID()}");
        go.transform.SetParent(_canvas.transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(arrowSize + portraitSize + 60, arrowSize);

        // ── 箭头（左）──
        var arrowGo = new GameObject("Arrow");
        arrowGo.transform.SetParent(go.transform, false);
        var arrowRt = arrowGo.AddComponent<RectTransform>();
        arrowRt.sizeDelta = new Vector2(arrowSize, arrowSize);
        arrowRt.anchorMin = new Vector2(0, 0.5f);
        arrowRt.anchorMax = new Vector2(0, 0.5f);
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.anchoredPosition = new Vector2(arrowSize * 0.5f, 0);
        var arrowImg = arrowGo.AddComponent<Image>();
        arrowImg.sprite = s_arrowSprite;
        arrowImg.color = GetBossColor(boss);
        arrowImg.raycastTarget = false;

        // ── 头像圆圈（中）──
        var portraitGo = new GameObject("Portrait");
        portraitGo.transform.SetParent(go.transform, false);
        var portraitRt = portraitGo.AddComponent<RectTransform>();
        portraitRt.sizeDelta = new Vector2(portraitSize, portraitSize);
        portraitRt.anchorMin = new Vector2(0, 0.5f);
        portraitRt.anchorMax = new Vector2(0, 0.5f);
        portraitRt.pivot = new Vector2(0.5f, 0.5f);
        portraitRt.anchoredPosition = new Vector2(arrowSize + portraitSize * 0.5f + 8, 0);
        var portraitImg = portraitGo.AddComponent<Image>();
        portraitImg.sprite = GetBossPortrait(boss);
        portraitImg.color = Color.white;
        portraitImg.raycastTarget = false;

        // ── 距离文本（右）──
        var distGo = new GameObject("Distance");
        distGo.transform.SetParent(go.transform, false);
        var distRt = distGo.AddComponent<RectTransform>();
        distRt.sizeDelta = new Vector2(50, 22);
        distRt.anchorMin = new Vector2(0, 0.5f);
        distRt.anchorMax = new Vector2(0, 0.5f);
        distRt.pivot = new Vector2(0, 0.5f);
        distRt.anchoredPosition = new Vector2(arrowSize + portraitSize + 16, 0);
        var distTxt = distGo.AddComponent<Text>();
        distTxt.font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        distTxt.fontSize = 16;
        distTxt.color = Color.white;
        distTxt.alignment = TextAnchor.MiddleLeft;
        distTxt.raycastTarget = false;
        if (distTxt.font == null)
            distTxt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        // 给文字加描边效果：用 Shadow 组件
        var shadow = distGo.AddComponent<Shadow>();
        shadow.effectColor = Color.black;
        shadow.effectDistance = new Vector2(1, -1);

        return go;
    }

    private void UpdateIndicator(enemy boss, GameObject indicatorGo)
    {
        Vector3 bossPos = boss.transform.position;
        Vector3 playerPos = _player.position;

        // 方向向量（玩家 → Boss，XZ 平面）
        Vector3 dirXZ = bossPos - playerPos; dirXZ.y = 0;
        float dist = dirXZ.magnitude;

        // 只显示远处Boss的箭头（近处已在屏幕上可见）
        if (dist < maxShowDist)
        {
            if (indicatorGo.activeSelf) indicatorGo.SetActive(false);
            return;
        }
        if (!indicatorGo.activeSelf) indicatorGo.SetActive(true);

        // 更新距离文本
        var distTxt = indicatorGo.transform.Find("Distance")?.GetComponent<Text>();
        if (distTxt != null) distTxt.text = $"{dist:F0}m";

        // 世界方向 → 屏幕方向（XZ中的X→屏幕右，Z→屏幕上）
        // Canvas 中 0° = 右，90° = 上
        Vector2 screenDir = new Vector2(dirXZ.x, dirXZ.z).normalized;
        float angleDeg = Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;

        // 箭头旋转（箭头贴图默认朝上，-90° 补偿）
        var arrowRt = indicatorGo.transform.Find("Arrow")?.GetComponent<RectTransform>();
        if (arrowRt != null)
            arrowRt.localRotation = Quaternion.Euler(0, 0, angleDeg - 90f);

        // 将Boss的世界坐标转到屏幕像素
        Vector3 screenPoint = _cam.WorldToScreenPoint(bossPos);
        RectTransform canvasRt = _canvas.GetComponent<RectTransform>();
        Vector2 screenSize = canvasRt.sizeDelta;

        // 如果Boss在屏幕后面(z<0)或屏幕外，计算屏幕边缘交点
        bool behind = screenPoint.z < 0;
        Vector2 center = screenSize * 0.5f;
        Vector2 targetPos;
        if (behind || screenPoint.x < -50 || screenPoint.x > screenSize.x + 50
                  || screenPoint.y < -50 || screenPoint.y > screenSize.y + 50)
        {
            // 使用方向向量计算边缘交点（比WorldToViewportPoint更稳定）
            targetPos = ClampToEdgeDir(center, screenDir, screenSize);
        }
        else
        {
            // Boss在屏幕内但还不够近 → 推到最近边缘
            targetPos = new Vector2(screenPoint.x, screenPoint.y);
            targetPos = ClampToEdgeDir(center, (targetPos - center).normalized, screenSize);
        }

        // Canvas anchoredPosition 原点在屏幕中心
        var rt = indicatorGo.GetComponent<RectTransform>();
        rt.anchoredPosition = targetPos - center;

        // 越远箭头越小
        float scale = Mathf.Clamp(1f - (dist - maxShowDist) / 200f, 0.7f, 1f);
        rt.localScale = Vector3.one * scale;
    }

    /// <summary>从center沿dir方向找到屏幕边缘（留padding），返回屏幕像素坐标。</summary>
    private Vector2 ClampToEdgeDir(Vector2 center, Vector2 dir, Vector2 screenSize)
    {
        float halfW = screenSize.x * 0.5f - edgePadding;
        float halfH = screenSize.y * 0.5f - edgePadding;
        float scaleX = Mathf.Abs(dir.x) > 0.001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        float scale  = Mathf.Min(scaleX, scaleY);
        return center + dir * scale;
    }

    // ──────── 辅助方法 ────────

    // ──────── 世界Boss类型判断 ────────
    private static bool IsWorldBoss(enemy e)
    {
        return e is WorldBossSlime || e is WorldBossWolf
            || e is WorldBossBat   || e is WorldBossMushroomMan;
    }

    private static FactionType GetBossFaction(enemy e)
    {
        if (e is WorldBossSlime)       return FactionType.Slime;
        if (e is WorldBossWolf)        return FactionType.Wolf;
        if (e is WorldBossBat)         return FactionType.Bat;
        if (e is WorldBossMushroomMan) return FactionType.Mushroom;
        return FactionType.Slime;
    }

    private Color GetBossColor(enemy boss)
    {
        switch (GetBossFaction(boss))
        {
            case FactionType.Bat:      return ColBat;
            case FactionType.Wolf:     return ColWolf;
            case FactionType.Slime:    return ColSlime;
            default:                   return ColMushroom; // Mushroom
        }
    }

    private Sprite GetBossPortrait(enemy boss)
    {
        // 尝试取 Boss 自身的 SpriteRenderer sprite 作为头像
        var sr = boss.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        // 回退：生成纯色圆形
        return CreateCircleSprite(GetBossColor(boss));
    }

    // ──────── 运行时贴图生成 ────────

    private static void EnsureArrowSprite()
    {
        if (s_arrowSprite != null) return;
        int sz = 64;
        s_arrowTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        Color[] px = new Color[sz * sz];
        Vector2 c = new Vector2(sz * 0.5f, sz * 0.5f);
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                // 画一个向上的三角形（▼方向，Canvas 旋转后指向目标）
                float r = (y - c.y) / (sz * 0.4f);
                float halfW = (sz * 0.45f) * (1f - (y / (float)sz));
                bool inside = y >= sz * 0.1f
                           && y <= sz * 0.85f
                           && Mathf.Abs(x - c.x) <= halfW;
                px[y * sz + x] = inside ? Color.white : Color.clear;
            }
        }
        s_arrowTex.Apply();
        s_arrowSprite = Sprite.Create(s_arrowTex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
    }

    private static Sprite CreateCircleSprite(Color col)
    {
        int sz = 32;
        var tex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(sz * 0.5f, sz * 0.5f);
        float r = sz * 0.45f;
        Color[] px = new Color[sz * sz];
        for (int y = 0; y < sz; y++)
        {
            for (int x = 0; x < sz; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = 1f - Mathf.SmoothStep(r - 2f, r, d);
                px[y * sz + x] = new Color(col.r, col.g, col.b, a);
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, sz, sz), new Vector2(0.5f, 0.5f));
    }
}
