using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 提供一个**全场景级别**的 Overlay UI 层：
/// 自动创建一个 ScreenSpaceOverlay 模式、SortingOrder 极高的根级 Canvas，
/// 任何"必须画在所有 UI 之上"的面板都可以 reparent 到它下面，
/// 从而**完全绕过项目原有 Canvas 嵌套 / SubCanvas 优先级 / SortingOrder 错综复杂**的层级问题。
///
/// 用法（典型）：
/// <code>
/// var layerTr = UIOverlayLayer.Get();         // 取根 Canvas Transform（懒创建）
/// myPanel.transform.SetParent(layerTr, false);// reparent，过去的 RectTransform 配置保留
/// </code>
/// reparent 时第二个参数传 false，保留 anchor / pivot / localPosition；
/// 如果原 panel 是 anchor=(0,0)~(1,1) 全屏拉伸，re-parent 后依然全屏。
///
/// 之所以采用 reparent 而不是给目标 GameObject 加 Canvas overrideSorting：
/// 后者在嵌套子 Canvas 下表现不稳定（root Canvas 与 sub-Canvas 渲染顺序的优先级），
/// reparent 到一个独立的、根级的、SortingOrder=极高的 Canvas，是唯一**确定性盖在最上层**的办法。
/// </summary>
public static class UIOverlayLayer
{
    private const string GO_NAME = "__UIOverlayLayer__";
    private const int SORTING_ORDER = 10000; // 高于项目里所有现存 Canvas

    private static Canvas _cached;

    /// <summary>取得（懒创建）OverlayLayer 的 Canvas Transform。</summary>
    public static Transform Get()
    {
        EnsureCanvas();
        return _cached != null ? _cached.transform : null;
    }

    public static Canvas GetCanvas()
    {
        EnsureCanvas();
        return _cached;
    }

    private static void EnsureCanvas()
    {
        if (_cached != null) return;

        // 防止有人手动建过同名 GO（编辑器残留 / 重复调用）
        var existing = GameObject.Find(GO_NAME);
        if (existing != null)
        {
            _cached = existing.GetComponent<Canvas>();
            if (_cached != null) return;
            // 同名 GO 但没 Canvas — 重命名后另建
            existing.name = GO_NAME + "_legacy";
        }

        var go = new GameObject(GO_NAME);
        // 不挂在任何对象下面 —— 必须是场景根，才能保证 sortingOrder 比所有 sub-Canvas 都高。
        go.transform.SetParent(null, false);

        _cached = go.AddComponent<Canvas>();
        _cached.renderMode = RenderMode.ScreenSpaceOverlay;
        _cached.sortingOrder = SORTING_ORDER;
        // ScreenSpaceOverlay 模式下不需要 worldCamera

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // GraphicRaycaster：让里面的 Image/Button 可以接收 EventSystem 的点击/hover
        go.AddComponent<GraphicRaycaster>();

        // 保险：保证场景里有 EventSystem（某些场景里可能没启用）
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            var esGo = new GameObject("__OverlayEventSystem__");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }
    }
}
