using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 难度选择面板。点击难度按钮直接进入游戏，鼠标悬停显示难度详情和开放功能。
/// Inspector 配置：
/// - titleScript：场景中挂有 title 脚本的对象
/// - difficultyButtons：N1~N8 八个按钮（顺序对应 DifficultyManager.configs）
/// - tooltipPanel：悬停提示面板
/// - tooltipText：提示面板内的 TextMeshProUGUI
/// </summary>
public class DifficultySelectUI : MonoBehaviour
{
    [Header("引用")]
    public title titleScript;
    public Button[] difficultyButtons; // N1~N8

    [Header("悬停提示")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI tooltipText;

    // 每个难度对应的开放功能描述（与 DifficultyManager.configs 顺序一致）
    private static readonly string[] FeatureDescriptions = new string[]
    {
        "基础三选一玩法",           // N1
        "蘑菇人Boss&开始解锁通关装备",  // N2
        "开放奇遇功能",             // N3
        "加入蝙蝠敌人",             // N4
        "开放门挑战",               // N5
        "蘑菇Boss × 2",            // N6
        "蝙蝠社群Boss登场",         // N7
        "解锁世界Boss", // N8
    };

    // OverlayLayer 化的运行时占位
    [System.NonSerialized] private Transform _originalParent;          // 原始父节点（首次 reparent 之前记录，OnDisable 时还原）
    [System.NonSerialized] private int _originalSiblingIndex = -1;      // 原始 sibling 顺序（OnDisable 时还原）
    [System.NonSerialized] private GameObject _runtimeBackdrop;         // 动态加的全屏黑色遮罩

    void OnEnable()
    {
        // ============ 关键：彻底解决"关卡选择被压在主菜单下层 / 看起来歪在屏幕底部"的层级问题 ============
        // 项目里有多个嵌套 Canvas（外层"Canvas" → 子 Canvas"标题UI"），sub-Canvas 在同 SortingOrder 下
        // 默认会画在父 Canvas 内的兄弟 Image 之上 —— DifficultyPanel 作为父 Canvas 的兄弟 Image，
        // 怎么改 sibling 顺序都会被"标题UI"sub-Canvas 整个盖住，表现为"红色横幅 + 主菜单按钮在最上面"。
        //
        // 解决：把整个 DifficultySelectUI 这个 GameObject **reparent** 到一个**场景根级**、
        // SortingOrder=10000 的 OverlayCanvas 下（UIOverlayLayer 自动创建），完全脱离原 Canvas 层级，
        // 这是唯一能确定性"画在所有 UI 之上"的方案。
        Transform overlay = UIOverlayLayer.Get();
        if (overlay != null && transform.parent != overlay)
        {
            // 首次 enable：记录原父节点，OnDisable 时还原（避免对场景结构产生持久副作用）
            if (_originalParent == null)
            {
                _originalParent = transform.parent;
                _originalSiblingIndex = transform.GetSiblingIndex();
            }
            transform.SetParent(overlay, false);
            transform.SetAsLastSibling();
        }

        // 额外：原 DifficultyPanel 自带的白色 alpha=0.392 半透明背景**根本盖不住主菜单**，
        // 所以这里**额外**动态创建一个全屏黑色 0.6 alpha 的 backdrop 作为遮罩，
        // 挂在 OverlayLayer 下、且位于本 panel 之前（先绘制 → 在底）。
        EnsureRuntimeBackdrop(overlay);

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
            foreach (var graphic in tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
        }

        if (DifficultyManager.Instance == null) return;

        for (int i = 0; i < difficultyButtons.Length; i++)
        {
            if (difficultyButtons[i] == null) continue;
            int idx = i;
            var btn = difficultyButtons[i];

            bool unlocked = i == 0 || (ClearRecordManager.Instance != null &&
                i < DifficultyManager.Instance.configs.Length &&
                ClearRecordManager.Instance.GetClearCount(
                    DifficultyManager.Instance.configs[i - 1].label) > 0);
            btn.interactable = unlocked;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSelectDifficulty(idx));

            SetupTooltipTrigger(btn.gameObject, idx);
        }
    }

    /// <summary>
    /// 创建/复用一个全屏黑色 0.6 alpha 的 backdrop，挂在 OverlayLayer 下、放在 DifficultyPanel 之前。
    /// 用来遮住主菜单（红色横幅 + 开始游戏/退出游戏按钮）。
    /// </summary>
    private void EnsureRuntimeBackdrop(Transform overlay)
    {
        if (overlay == null) return;
        if (_runtimeBackdrop == null)
        {
            _runtimeBackdrop = new GameObject("DifficultyPanelBackdrop", typeof(RectTransform));
            _runtimeBackdrop.transform.SetParent(overlay, false);
            var rt = (RectTransform)_runtimeBackdrop.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = _runtimeBackdrop.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
            img.raycastTarget = true; // 吞掉点击，防止穿透到下层主菜单按钮

            // Button 双保险吞事件
            var btn = _runtimeBackdrop.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
        }
        _runtimeBackdrop.SetActive(true);
        // backdrop 必须画在本 panel 之前（更早绘制 → 在下层）→ 先把 backdrop 设到末位再把自己设到末位
        _runtimeBackdrop.transform.SetAsLastSibling();
        transform.SetAsLastSibling();
    }

    private void OnSelectDifficulty(int index)
    {
        DifficultyManager.Instance?.SetDifficulty(index);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        gameObject.SetActive(false);
        titleScript?.click_start();
    }

    void OnDisable()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        // 关掉动态加的 backdrop，避免它在面板隐藏后仍然挡着屏幕
        if (_runtimeBackdrop != null) _runtimeBackdrop.SetActive(false);
    }

    private void SetupTooltipTrigger(GameObject target, int index)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger != null) Destroy(trigger);
        trigger = target.AddComponent<EventTrigger>();

        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) => ShowTooltip(index));
        trigger.triggers.Add(enterEntry);

        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((_) => HideTooltip());
        trigger.triggers.Add(exitEntry);
    }

    private void ShowTooltip(int index)
    {
        if (tooltipPanel == null || DifficultyManager.Instance == null) return;
        if (index >= DifficultyManager.Instance.configs.Length) return;

        var cfg = DifficultyManager.Instance.configs[index];
        int clearCount = ClearRecordManager.Instance != null
            ? ClearRecordManager.Instance.GetClearCount(cfg.label)
            : 0;

        bool unlocked = index == 0 || (ClearRecordManager.Instance != null &&
            ClearRecordManager.Instance.GetClearCount(
                DifficultyManager.Instance.configs[index - 1].label) > 0);

        string feature = index < FeatureDescriptions.Length ? FeatureDescriptions[index] : "";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{cfg.label}</b>");
        sb.AppendLine($"敌人血量：×{cfg.hpMultiplier:F1}");
        sb.AppendLine($"敌人攻击：×{cfg.atkMultiplier:F1}");
        sb.AppendLine($"对局时长：{cfg.minutes} 分钟");

        if (!string.IsNullOrEmpty(feature))
            sb.AppendLine($"<color=#FFD700>开放功能：{feature}</color>");

        if (!unlocked)
            sb.AppendLine($"<color=grey>通关 {DifficultyManager.Instance.configs[index - 1].label} 后解锁</color>");
        else
            sb.AppendLine($"通关次数：{clearCount}");

        tooltipText.text = sb.ToString().TrimEnd();
        tooltipPanel.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
            Click_Back();
    }

    public void Click_Back()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        gameObject.SetActive(false);
    }
}
