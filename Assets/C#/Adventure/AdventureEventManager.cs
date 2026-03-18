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

    private void Awake()
    {
        Instance = this;
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

        var (optA, optB) = PickTwoOptions();
        adventureUI?.Show(optA, optB, triggerThreshold);
    }

    private (AdventureOptionBase, AdventureOptionBase) PickTwoOptions()
    {
        int indexA = Random.Range(0, optionPool.Count);
        int indexB = indexA;
        int safety = 0;
        while (indexB == indexA && optionPool.Count > 1)
        {
            indexB = Random.Range(0, optionPool.Count);
            if (++safety > 100) break;
        }
        return (optionPool[indexA], optionPool[indexB]);
    }
}
