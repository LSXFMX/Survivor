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
    public Button[] difficultyButtons; // N1~N13

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
        "解锁世界Boss",             // N8
        "新增N9通关装备",           // N9
        "新增N10通关装备",          // N10
        "新增N11通关装备",          // N11
        "新增N12通关装备",          // N12
        "终极难度·新增N13通关装备", // N13
    };

    // OverlayLayer 化的运行时占位
    [System.NonSerialized] private Transform _originalParent;          // 原始父节点（首次 reparent 之前记录，OnDisable 时还原）
    [System.NonSerialized] private int _originalSiblingIndex = -1;      // 原始 sibling 顺序（OnDisable 时还原）
    [System.NonSerialized] private GameObject _runtimeBackdrop;         // 动态加的全屏黑色遮罩

    void OnEnable()
    {
        // ============ 关键：彻底解决"关卡选择被压在主菜单下层 / 看起来歪在屏幕底部"的层级问题 ============
        Transform overlay = UIOverlayLayer.Get();
        if (overlay != null && transform.parent != overlay)
        {
            if (_originalParent == null)
            {
                _originalParent = transform.parent;
                _originalSiblingIndex = transform.GetSiblingIndex();
            }
            transform.SetParent(overlay, false);
            transform.SetAsLastSibling();
        }

        EnsureRuntimeBackdrop(overlay);

        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
            foreach (var graphic in tooltipPanel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.raycastTarget = false;
        }

        if (DifficultyManager.Instance == null) return;

        int totalDifficulties = DifficultyManager.Instance.configs.Length;

        // 关键修复：确保数组大小与配置一致，自动绑定 Inspector 中未拖入的按钮
        EnsureButtonArraySize(totalDifficulties);

        // 用 totalDifficulties 作为循环边界，防止 difficultyButtons 数组长度 > configs.Length 时越界
        for (int i = 0; i < totalDifficulties; i++)
        {
            // 防御性检查：如果 configs 数组长度异常，直接报错并退出
            if (i >= DifficultyManager.Instance.configs.Length)
            {
                Debug.LogError($"[难度选择] 严重：configs 数组长度为 {DifficultyManager.Instance.configs.Length}，但循环到 i={i}！请在 Inspector 中重置 DifficultyManager 组件。");
                break;
            }

            if (difficultyButtons[i] == null) continue;
            int idx = i;
            var btn = difficultyButtons[i];

            // 统一用 IsButtonUnlocked 判断，并在日志里输出诊断信息
            bool unlocked = IsButtonUnlocked(i);
            Debug.Log($"[难度选择] 按钮[{idx}]{DifficultyManager.Instance.configs[idx].label} " +
                $"unlocked={unlocked} " +
                (idx > 0 ? $"(检查 key=ClearCount_{DifficultyManager.Instance.configs[idx - 1].label}, " +
                    $"值={ClearRecordManager.Instance?.GetClearCount(DifficultyManager.Instance.configs[idx - 1].label)})" : ""));

            // 所有按钮必须 interactable=true，否则 EventTrigger 的 PointerEnter 事件被屏蔽
            btn.interactable = true;
            ApplyLockedVisual(btn, unlocked);

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (!IsButtonUnlocked(idx))
                {
                    string prevLabel = DifficultyManager.Instance.configs[idx - 1].label;
                    ToastManager.Show($"请先通关 {prevLabel} 解锁该难度！");
                    return;
                }
                OnSelectDifficulty(idx);
            });

            SetupTooltipTrigger(btn.gameObject, idx);
        }
    }

    /// <summary>
    /// 确保 difficultyButtons 数组大小与 configs 一致。
    /// 遍历所有子对象（含深层、含 Clone 后缀），按名称匹配自动绑定。
    /// </summary>
    private void EnsureButtonArraySize(int total)
    {
        // 1) 扩展数组
        if (difficultyButtons == null || difficultyButtons.Length < total)
        {
            Button[] newArray = new Button[total];
            int copyLen = Mathf.Min(difficultyButtons != null ? difficultyButtons.Length : 0, total);
            for (int i = 0; i < copyLen; i++)
                newArray[i] = difficultyButtons[i];
            difficultyButtons = newArray;
        }

        // 2) 按 configs 顺序，递归查找每个未绑定的按钮
        for (int i = 0; i < total; i++)
        {
            if (difficultyButtons[i] != null) continue;

            string label = DifficultyManager.Instance.configs[i].label;
            // 递归查找：去除 "(Clone)" 后缀后比较名称
            Button found = FindChildButtonByName(transform, label);
            if (found != null)
            {
                difficultyButtons[i] = found;
                Debug.Log($"[难度选择] 自动绑定按钮 [{i}] '{label}'");
            }
            else
            {
                Debug.LogWarning($"[难度选择] 未找到按钮 '{label}'（已递归搜索所有子对象）");
            }
        }
    }

    /// <summary>
    /// 递归在 parent 的所有子对象中查找名称匹配的 Button。
    /// 比较时自动去除 "(Clone)" 后缀。
    /// </summary>
    private Button FindChildButtonByName(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            string childName = child.name.Replace("(Clone)", "").Trim();
            if (childName == name)
            {
                var btn = child.GetComponent<Button>();
                if (btn != null) return btn;
            }
            // 递归查找孙对象
            Button found = FindChildButtonByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>
    /// 判断指定难度是否已解锁（独立方法，供 onClick 回调使用）。
    /// </summary>
    private bool IsButtonUnlocked(int index)
    {
        if (index == 0) return true;
        if (ClearRecordManager.Instance == null) return false;
        int total = DifficultyManager.Instance.configs.Length;
        if (index >= total) return false;
        return ClearRecordManager.Instance.GetClearCount(
            DifficultyManager.Instance.configs[index - 1].label) > 0;
    }

    /// <summary>
    /// 设置按钮视觉状态：未解锁时变灰，解锁时恢复正常颜色。
    /// 遍历按钮及子对象所有 Image 组件统一设置。
    /// </summary>
    private void ApplyLockedVisual(Button btn, bool unlocked)
    {
        Color targetColor = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.5f);

        // 按钮自身的 Image / targetGraphic
        var selfImg = btn.GetComponent<Image>();
        if (selfImg != null) selfImg.color = targetColor;
        if (btn.targetGraphic != null && btn.targetGraphic != selfImg as UnityEngine.UI.Graphic)
            btn.targetGraphic.color = targetColor;

        // 子对象中的 Image（图标等）
        foreach (var img in btn.GetComponentsInChildren<Image>(true))
            if (img != selfImg) img.color = targetColor;
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
            // 关键修复：遮罩只做视觉效果，raycastTarget=false 不阻挡射线，
            // 这样按钮点击事件才能正常穿透到 DifficultyPanel 的按钮上
            img.raycastTarget = false;

            // 不再给遮罩加 Button 组件，避免它拦截所有点击
        }
        _runtimeBackdrop.SetActive(true);
        // backdrop 放最底层（先绘制），面板放最顶层（后绘制）
        _runtimeBackdrop.transform.SetAsFirstSibling();
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

        int total = DifficultyManager.Instance.configs.Length;
        if (index >= total) return;

        var cfg = DifficultyManager.Instance.configs[index];
        int clearCount = ClearRecordManager.Instance != null
            ? ClearRecordManager.Instance.GetClearCount(cfg.label)
            : 0;

        // 统一用 IsButtonUnlocked 判断，避免散落多处逻辑不一致
        bool unlocked = IsButtonUnlocked(index);

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
