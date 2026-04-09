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

    void OnEnable()
    {
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

    void OnDisable()
    {
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
    }

    private void OnSelectDifficulty(int index)
    {
        DifficultyManager.Instance?.SetDifficulty(index);
        if (tooltipPanel != null) tooltipPanel.SetActive(false);
        gameObject.SetActive(false);
        titleScript?.click_start();
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
