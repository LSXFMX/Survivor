using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局右键返回管理器。
/// 右键时自动找到场景中所有激活的"可右键关闭"标记，
/// 触发其中层级最深（最上层）的那一个。
///
/// 支持两类标记，任选其一/或同时使用：
/// 1. <see cref="RightClickCloseable"/> —— 挂在关闭按钮上（旧用法），点击触发按钮 onClick
/// 2. <see cref="RightClickClosePanel"/> —— 挂在面板根上（推荐），触发自定义回调或默认 SetActive(false)
///
/// 使用方式：
/// 1. 把此脚本挂在场景常驻对象上（Awake 自动单例 + DontDestroyOnLoad）
/// 2. 在每个面板的关闭按钮上挂 RightClickCloseable，或在面板根上挂 RightClickClosePanel
/// 3. 也可以在脚本里调用 RightClickClosePanel.EnsureOn(go) 自动添加（零场景改动）
///
/// 选择哪个面板的依据（按优先级降序）：
/// 1. 所属 Canvas 的 sortingOrder 最大（即 UI 渲染最上层）
/// 2. Hierarchy 深度最大
/// </summary>
public class RightClickBackManager : MonoBehaviour
{
    public static RightClickBackManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(1)) return;

        // 收集所有候选
        var candidates = new List<Candidate>();

        // 1. RightClickCloseable（按钮上）
        var closeables = FindObjectsOfType<RightClickCloseable>();
        if (closeables != null)
        {
            foreach (var item in closeables)
            {
                if (!item.gameObject.activeInHierarchy) continue;
                var btn = item.GetComponent<Button>();
                if (btn == null || !btn.interactable) continue;
                candidates.Add(new Candidate
                {
                    sortingOrder = GetSortingOrder(item.transform),
                    depth        = GetHierarchyDepth(item.transform),
                    invoke       = () => btn.onClick.Invoke()
                });
            }
        }

        // 2. RightClickClosePanel（面板根上）
        var panels = FindObjectsOfType<RightClickClosePanel>();
        if (panels != null)
        {
            foreach (var item in panels)
            {
                if (!item.gameObject.activeInHierarchy) continue;
                var captured = item;
                candidates.Add(new Candidate
                {
                    sortingOrder = GetSortingOrder(item.transform),
                    depth        = GetHierarchyDepth(item.transform),
                    invoke       = () => captured.TriggerClose()
                });
            }
        }

        if (candidates.Count == 0) return;

        // 选最上层：先比 sortingOrder，再比 depth
        Candidate best = candidates[0];
        for (int i = 1; i < candidates.Count; i++)
        {
            var c = candidates[i];
            if (c.sortingOrder > best.sortingOrder ||
                (c.sortingOrder == best.sortingOrder && c.depth > best.depth))
                best = c;
        }

        best.invoke?.Invoke();
    }

    private struct Candidate
    {
        public int sortingOrder;
        public int depth;
        public System.Action invoke;
    }

    private static int GetHierarchyDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null) { depth++; t = t.parent; }
        return depth;
    }

    private static int GetSortingOrder(Transform t)
    {
        var canvas = t.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.sortingOrder : 0;
    }
}
