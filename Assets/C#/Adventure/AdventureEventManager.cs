using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 奇遇触发管理器。改为手动触发：玩家点击按钮时检查源木是否达到阈值。
/// </summary>
public class AdventureEventManager : MonoBehaviour
{
    [SerializeField] private int triggerThreshold = 200;
    [SerializeField] private List<AdventureOptionBase> optionPool;
    [SerializeField] private AdventureUI adventureUI;

    [Header("触发按钮")]
    [SerializeField] private Button triggerButton;
    [SerializeField] private Color colorReady   = Color.green;  // 源木足够
    [SerializeField] private Color colorNotReady = Color.gray;  // 源木不足

    public int TriggerThreshold => triggerThreshold;
    public static AdventureEventManager Instance { get; private set; }

    private Image _buttonImage;

    /// <summary>SSR_11 气运之子 equipmentSystemId=14，解锁后奇遇从2选变3选</summary>
    private const int SSR_FORTUNE_CHILD_ID = 14;

    private void Awake()
    {
        Instance = this;
        // 新一局开始：清空奇遇已使用集合，使所有 oneShot 奇遇恢复可用
        AdventureOptionBase.usedOptionsThisRun.Clear();
        // 同步清零"人格解离"独立计数（该奇遇用计数式而非 HashSet 控制最多触发 1/2 次）
        AdventurePersonalityDissolve.ResetRunCounter();
        if (triggerButton != null)
        {
            _buttonImage = triggerButton.GetComponent<Image>();
            triggerButton.onClick.AddListener(OnTriggerButtonClick);
        }
    }

    private void Update()
    {
        if (_buttonImage == null || YuanMuManager.Instance == null) return;
        bool canAfford = YuanMuManager.Instance.Current >= triggerThreshold;
        _buttonImage.color = canAfford ? colorReady : colorNotReady;
    }

    /// <summary>玩家点击按钮时调用</summary>
    public void OnTriggerButtonClick()
    {
        if (YuanMuManager.Instance == null) return;
        if (YuanMuManager.Instance.Current < triggerThreshold) return;

        // 扣除源木
        YuanMuManager.Instance.Spend(triggerThreshold);
        TryTriggerEvent();
    }

    private void TryTriggerEvent()
    {
        if (optionPool == null || optionPool.Count < 2)
        {
            Debug.LogWarning("[AdventureEventManager] 选项池少于2个，跳过触发");
            return;
        }
        if (adventureUI != null && adventureUI.IsShowing) return;

        int pickCount = 2;
        if (EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, SSR_FORTUNE_CHILD_ID))
        {
            pickCount = 3; // SSR_11 气运之子：二选一变三选一
        }

        var options = PickOptions(pickCount);
        if (options == null || options.Count < 2) return;
        if (pickCount == 3) adventureUI?.Show(options[0], options[1], options[2], triggerThreshold);
        else                adventureUI?.Show(options[0], options[1], triggerThreshold);
    }

    private List<AdventureOptionBase> PickOptions(int count)
    {
        List<AdventureOptionBase> available = new List<AdventureOptionBase>();
        foreach (var opt in optionPool)
        {
            if (opt == null) continue;
            if (opt.IsAvailableInCurrentDifficulty()) available.Add(opt);
        }

        if (available.Count < 2)
        {
            Debug.LogWarning("[AdventureEventManager] 当前难度可用选项少于2个");
            return null;
        }

        List<AdventureOptionBase> picked = new List<AdventureOptionBase>();
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, available.Count);
            picked.Add(available[idx]);
            available.RemoveAt(idx);
            if (available.Count == 0) break;
        }
        return picked;
    }
}
