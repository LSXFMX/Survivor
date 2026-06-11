using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 世界 Boss 头顶血条（v8：完全照抄 Camp 方案）。
///
/// === 为什么 v1～v7 都失败，v8 改回"挂在 boss 身上"===
/// 用户明确指定：参照 CampHealthBar 的最稳做法——在 boss 身上做世界空间 UGUI Canvas 子物体。
/// Camp 在 prefab 里手工搭建 Canvas（World Space）→ background Image → red Filled Image，
/// 脚本只更新 fillAmount。这种结构在游戏里实测稳定显示。
///
/// v1～v6 的失败根因不是"挂在 boss transform 下"本身，而是用 SpriteRenderer 做世界血条
/// （sortingLayer/相机层级容易出问题）。v7 屏幕 HUD 又被用户否定。
/// v8 决定：**完全照抄 Camp 的 UGUI World Space Canvas 子物体结构**，差别仅在于
/// 这次没法人工拖 prefab，所以由代码运行时构造完整的 Canvas 子树。
///
/// === 关键参数（参照 Camp.prefab）===
///   • Canvas: World Space, sortingOrder=10（盖过场景里其他 World Canvas）
///   • Canvas 的 localScale: 1/bossLossyScale（让 Canvas 的 lossyScale=1，与 Camp 一致）
///   • localPosition: 通过 SpriteRenderer.bounds 算出 boss 头顶在 boss 局部空间的位置
///   • LateUpdate 修正旋转：Quaternion.Euler(20°,0°,0°) 保持血条始终面向 45° 俯视相机
///
/// === 实现历史 ===
/// v1～v6：SpriteRenderer 路线，各种 sortingLayer/lossyScale 调参都失败。
/// v7：屏幕 HUD（ScreenSpaceOverlay Canvas），用户否定。
/// v8（当前）：World Space Canvas 子物体，完全照抄 Camp。
/// </summary>
public class WorldBossHealthBar : MonoBehaviour
{
    // —— 视觉常量 ——（参考 Camp，但稍大一些以匹配 Boss 体型）
    private const float BAR_WIDTH        = 3f;     // Canvas 内 background 的宽度（UI 单位）
    private const float BAR_HEIGHT       = 0.4f;   // Canvas 内 background 的高度（UI 单位）
    private const float FOOT_OFFSET_Y    = -2.76f; // 血条相对 bounds.min.y 的 Y 偏移量（负值=向上抬）
    private const float FOOT_OFFSET_Z    = -3f;    // 朝相机方向（-Z）拉回血条，修正 45° 俯视视角下的错位偏移
    private const float CANVAS_BASE_SCALE= 1f;     // Canvas 的世界目标 lossyScale（保持 1）
    private static readonly Color BG_COLOR              = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color FILL_COLOR_NORMAL     = new Color(0.95f, 0.18f, 0.18f, 1f); // 普通：红
    private static readonly Color FILL_COLOR_CONTROLLED = new Color(0.65f, 0.20f, 0.95f, 1f); // 被控制：紫

    private enemy           _en;
    private Image           _fillImage;
    private RectTransform   _canvasRT;
    private GameObject      _canvasGO;
    private Renderer        _bossRenderer;
    private static Sprite   _whiteSprite; // 全局共享 1x1 白图

    private void OnEnable()
    {
        _en = GetComponent<enemy>();
        if (_en == null)
        {
            Debug.LogError($"[WorldBossHealthBar] {gameObject.name} 上没有 enemy 组件，血条无法显示！");
            enabled = false;
            return;
        }
        _bossRenderer = GetComponent<Renderer>();
        if (_bossRenderer == null)
            _bossRenderer = GetComponentInChildren<Renderer>();
        BuildCanvas();
        Debug.Log($"[WorldBossHealthBar] 已为 {gameObject.name} 创建血条 Canvas（lossyScale={_canvasRT?.lossyScale}, pos={_canvasRT?.position}）");
    }

    private void OnDestroy()
    {
        if (_canvasGO != null) Destroy(_canvasGO);
    }

    /// <summary>UGUI Image 在 sprite=null 时可正常画纯色矩形（无需白纹理），但为兼容某些 Unity 版本仍准备一个共享白 Sprite。</summary>
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        Texture2D tex = new Texture2D(2, 2);
        Color[] pixels = new Color[4];
        for (int i = 0; i < 4; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        _whiteSprite.name = "BossBar_White";
        return _whiteSprite;
    }

    private void BuildCanvas()
    {
        if (_canvasGO != null) return;

        // === Canvas 容器（World Space，挂在 boss 之下作为子物体）===
        _canvasGO = new GameObject("BossHealthCanvas", typeof(RectTransform));
        _canvasGO.layer = gameObject.layer;
        _canvasRT = _canvasGO.GetComponent<RectTransform>();
        _canvasRT.SetParent(transform, false);

        Canvas cv = _canvasGO.AddComponent<Canvas>();
        cv.renderMode = RenderMode.WorldSpace;
        cv.sortingOrder = 10; // 盖过场景里其他 World 元素

        _canvasGO.AddComponent<CanvasScaler>();
        // GraphicRaycaster 不是必须，但 Camp 有，加上保持一致
        _canvasGO.AddComponent<GraphicRaycaster>();

        // RectTransform：尺寸跟 background 一致，pivot 居中
        _canvasRT.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
        _canvasRT.anchorMin = new Vector2(0.5f, 0.5f);
        _canvasRT.anchorMax = new Vector2(0.5f, 0.5f);
        _canvasRT.pivot     = new Vector2(0.5f, 0.5f);

        // 抵消父级 lossyScale，让 Canvas 在世界中保持稳定大小
        Vector3 ls = transform.lossyScale;
        float invX = (Mathf.Abs(ls.x) > 1e-4f) ? CANVAS_BASE_SCALE / Mathf.Abs(ls.x) : 1f;
        float invY = (Mathf.Abs(ls.y) > 1e-4f) ? CANVAS_BASE_SCALE / Mathf.Abs(ls.y) : 1f;
        float invZ = (Mathf.Abs(ls.z) > 1e-4f) ? CANVAS_BASE_SCALE / Mathf.Abs(ls.z) : 1f;
        _canvasRT.localScale = new Vector3(invX, invY, invZ);

        // 位置：boss 头顶（用 sprite bounds 计算 + 抬升）
        UpdateCanvasLocalPosition();

        // === background（黑色半透）===
        GameObject bgGO = new GameObject("background", typeof(RectTransform));
        bgGO.transform.SetParent(_canvasGO.transform, false);
        bgGO.layer = _canvasGO.layer;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.pivot     = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = Vector2.zero;
        bgRT.sizeDelta = new Vector2(BAR_WIDTH, BAR_HEIGHT);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = BG_COLOR;
        bgImg.sprite = GetWhiteSprite();
        bgImg.raycastTarget = false;

        // === red（红色 Filled Horizontal）===
        GameObject redGO = new GameObject("red", typeof(RectTransform));
        redGO.transform.SetParent(bgGO.transform, false);
        redGO.layer = _canvasGO.layer;
        RectTransform redRT = redGO.GetComponent<RectTransform>();
        redRT.anchorMin = new Vector2(0f, 0f);
        redRT.anchorMax = new Vector2(1f, 1f);
        redRT.offsetMin = Vector2.zero;
        redRT.offsetMax = Vector2.zero;
        _fillImage = redGO.AddComponent<Image>();
        _fillImage.color   = FILL_COLOR_NORMAL;
        _fillImage.sprite  = GetWhiteSprite();
        _fillImage.type    = Image.Type.Filled;
        _fillImage.fillMethod = Image.FillMethod.Horizontal;
        _fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fillImage.fillAmount = 1f;
        _fillImage.raycastTarget = false;
    }

    /// <summary>
    /// 把 Canvas 放到 boss 脚下（局部空间）。
    /// 用 Renderer.bounds（世界空间）取 bounds.min.y 再向下偏移 FOOT_OFFSET_Y。
    /// 没有 Renderer 时退回固定值。
    /// </summary>
    private void UpdateCanvasLocalPosition()
    {
        if (_canvasRT == null) return;
        Vector3 worldFoot;
        if (_bossRenderer != null)
        {
            Bounds b = _bossRenderer.bounds;
            worldFoot = new Vector3(b.center.x, b.min.y - FOOT_OFFSET_Y, b.center.z + FOOT_OFFSET_Z);
        }
        else
        {
            worldFoot = transform.position + new Vector3(0f, -1f, FOOT_OFFSET_Z);
        }
        _canvasRT.position = worldFoot;
    }

    private void LateUpdate()
    {
        if (_en == null || _fillImage == null) return;

        // 血量比例
        float ratio = (_en.healthmax > 0)
            ? Mathf.Clamp01((float)_en.health / _en.healthmax)
            : 0f;
        _fillImage.fillAmount = ratio;

        // 颜色：被亡者领域控制时变紫色，正常时保持红色
        bool controlled = GetComponent<MindControlled>() != null;
        _fillImage.color = controlled ? FILL_COLOR_CONTROLLED : FILL_COLOR_NORMAL;

        // 死亡：销毁血条避免长期残留
        if (_en.healthmax > 0 && _en.health <= 0 && _canvasGO != null)
        {
            Destroy(_canvasGO);
            _canvasGO = null;
            enabled = false;
            return;
        }

        // 跟随 boss 脚下（boss 可能移动/受冲击/缩放变化）
        UpdateCanvasLocalPosition();

        // 旋转矫正：boss 用 Quaternion.Euler(45,0,0) 实例化，所以血条也保持完整 45° 倾斜，
        // 与场景里其他在斜面上摆放的元素（boss 自身、Camp 血条所在面等）观感一致。
        if (_canvasRT != null)
            _canvasRT.rotation = Quaternion.Euler(45f, 0f, 0f);
    }
}
