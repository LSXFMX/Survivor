using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 标记一个 UI 面板可以被右键关闭。
/// 挂在面板根 GameObject 上（与 RightClickCloseable 标记按钮的方式互补）。
/// 默认行为：SetActive(false)。也可在 onRightClickClose 中自定义关闭逻辑。
///
/// 工作原理：
/// - RightClickBackManager 在右键时扫描所有激活的 RightClickClosePanel + RightClickCloseable，
///   按层级深度/sortingOrder 找到最上层那一个并触发。
/// - 本组件可由代码运行时 AddComponent 自动添加，零场景改动。
/// </summary>
public class RightClickClosePanel : MonoBehaviour
{
    [Tooltip("自定义关闭回调；若为空则默认 gameObject.SetActive(false)。")]
    public UnityEvent onRightClickClose;

    /// <summary>给指定 GameObject 添加一个右键关闭标记（幂等）。</summary>
    public static RightClickClosePanel EnsureOn(GameObject go)
    {
        if (go == null) return null;
        var c = go.GetComponent<RightClickClosePanel>();
        if (c == null) c = go.AddComponent<RightClickClosePanel>();
        return c;
    }

    /// <summary>触发关闭（由 RightClickBackManager 调用）。</summary>
    public void TriggerClose()
    {
        if (onRightClickClose != null)
        {
            onRightClickClose.Invoke();

            // UnityEvent 的运行时 AddListener 不计入 PersistentEventCount。
            // 若没有持久化事件且回调没有关闭本物体，则回退到默认关闭，避免空事件导致右键无效。
            if (onRightClickClose.GetPersistentEventCount() == 0 && gameObject.activeInHierarchy)
                gameObject.SetActive(false);
            return;
        }

        // 默认行为
        gameObject.SetActive(false);
    }
}
