using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全局右键返回管理器。
/// 右键时自动找到场景中所有激活的 RightClickCloseable 按钮，
/// 触发其中层级最深（最上层）的那个。
///
/// 使用方式：
/// 1. 把此脚本挂在场景常驻对象上
/// 2. 在每个面板的关闭按钮上挂 RightClickCloseable 组件
/// 无需任何注册/注销代码。
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

        // 找所有激活的 RightClickCloseable
        var all = FindObjectsOfType<RightClickCloseable>();
        if (all == null || all.Length == 0) return;

        // 找层级最深的（GetSiblingIndex 最大，或者用 Canvas sortingOrder）
        RightClickCloseable best = null;
        int bestDepth = -1;

        foreach (var item in all)
        {
            if (!item.gameObject.activeInHierarchy) continue;
            var btn = item.GetComponent<Button>();
            if (btn == null || !btn.interactable) continue;

            int depth = GetHierarchyDepth(item.transform);
            if (depth > bestDepth)
            {
                bestDepth = depth;
                best = item;
            }
        }

        if (best != null)
            best.GetComponent<Button>().onClick.Invoke();
    }

    private int GetHierarchyDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null) { depth++; t = t.parent; }
        return depth;
    }
}
