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
    [SerializeField] public AdventureUI adventureUI;

    [Header("触发按钮")]
    [SerializeField] private Button triggerButton;
    [SerializeField] private Color colorReady   = Color.green;  // 源木足够
    [SerializeField] private Color colorNotReady = Color.gray;  // 源木不足

    public int TriggerThreshold => triggerThreshold;
    public static AdventureEventManager Instance { get; private set; }

    private Image _buttonImage;

    /// <summary>SSR_11 气运之子 equipmentSystemId=36，解锁后奇遇从2选变3选</summary>
    private const int SSR_FORTUNE_CHILD_ID = 36;

    private void Awake()
    {
        Instance = this;
        // 新一局开始：清空奇遇已使用集合，使所有 oneShot 奇遇恢复可用
        AdventureOptionBase.usedOptionsThisRun.Clear();
        // 同步清零"人格解离"独立计数（该奇遇用计数式而非 HashSet 控制最多触发 1/2 次）
        AdventurePersonalityDissolve.ResetRunCounter();
        // 清零女娲补天无尽模式多选计数
        AdventureNuwaFailed.ResetRunCounter();

        // 运行时确保奇遇10「源木收集者」存在于奇遇池（无需在场景 Inspector 手动拖入）
        EnsureYuanmuCollectorOption();

        if (triggerButton != null)
        {
            _buttonImage = triggerButton.GetComponent<Image>();
            triggerButton.onClick.AddListener(OnTriggerButtonClick);
        }
    }

    private void Update()
    {
        if (triggerButton == null) return;

        // 奇遇选择面板显示中 → 按钮不可交互，防止重复点击扣源木
        bool adventureShowing = adventureUI != null && adventureUI.IsShowing;
        if (triggerButton.interactable == adventureShowing)
            triggerButton.interactable = !adventureShowing;
        if (adventureShowing) return;

        // 全局设计：没有可选择的奇遇（可用数量 == 0）→ 隐藏奇遇按钮
        bool hasOptions = CountAvailableOptions() > 0;
        if (triggerButton.gameObject.activeSelf != hasOptions)
            triggerButton.gameObject.SetActive(hasOptions);
        if (!hasOptions) return;

        if (_buttonImage == null || YuanMuManager.Instance == null) return;
        bool canAfford = YuanMuManager.Instance.Current >= triggerThreshold;
        _buttonImage.color = canAfford ? colorReady : colorNotReady;
    }

    /// <summary>统计当前可出现的奇遇数量（受无尽排除 / oneShot 去重影响）。</summary>
    private int CountAvailableOptions()
    {
        if (optionPool == null) return 0;
        int c = 0;
        foreach (var opt in optionPool)
            if (opt != null && opt.IsAvailableInCurrentDifficulty()) c++;
        return c;
    }

    /// <summary>确保奇遇10「源木收集者」在池中（运行时创建并加载图标）。幂等。</summary>
    private void EnsureYuanmuCollectorOption()
    {
        if (optionPool == null) optionPool = new List<AdventureOptionBase>();
        foreach (var o in optionPool)
            if (o is AdventureOption10_YuanmuCollector) return;

        var go = new GameObject("AdventureOption10_YuanmuCollector");
        go.transform.SetParent(transform, false);
        var opt = go.AddComponent<AdventureOption10_YuanmuCollector>();
        opt.optionName        = "源木收集者";
        opt.optionDescription = "money is power";
        opt.effectDescription = "每持有100源木，增加一点攻击力和一点防御力（选择后动态实时生效）";
        opt.oneShot           = true;
        opt.icon              = LoadAdventure10Icon();
        optionPool.Add(opt);
    }

    private Sprite LoadAdventure10Icon()
    {
        const string editorPath   = "像素幸存者资源包/奇遇图标/10源木收集者/10.源木收集者.png";
        const string resourcesPath = "像素幸存者资源包/奇遇图标/10源木收集者/10.源木收集者";
        var tex = RuntimeAssetLoader.LoadTexture(null, resourcesPath, editorPath);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>玩家点击按钮时调用</summary>
    public void OnTriggerButtonClick()
    {
        if (YuanMuManager.Instance == null) return;
        if (YuanMuManager.Instance.Current < triggerThreshold) return;

        // 奇遇选择面板已显示，不允许重复触发（避免重复扣源木）
        if (adventureUI != null && adventureUI.IsShowing) return;

        // 三选一升级进行中不允许触发奇遇（连源木也不扣）
        var bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null && bui.choiceUI != null && bui.choiceUI.activeSelf) return;

        // 扣除源木
        YuanMuManager.Instance.Spend(triggerThreshold);
        TryTriggerEvent();
    }

    private void TryTriggerEvent()
    {
        if (optionPool == null || optionPool.Count == 0)
        {
            Debug.LogWarning("[AdventureEventManager] 选项池为空，跳过触发");
            return;
        }
        if (adventureUI != null && adventureUI.IsShowing) return;

        int pickCount = 2;
        if (PlayerPrefs.GetInt("EQ_3_36", 0) == 1) pickCount = 3;

        var options = PickOptions(pickCount);
        if (options == null || options.Count == 0) return;

        // 只剩 1 个可选奇遇时，直接执行无需弹面板
        if (options.Count == 1)
        {
            options[0].Execute();
            return;
        }

        Debug.Log($"[AdventureEventManager] pickCount={pickCount} picked={options.Count}");
        if (pickCount >= 3 && options.Count >= 3)
            adventureUI?.Show(options[0], options[1], options[2], triggerThreshold);
        else
            adventureUI?.Show(options[0], options[1], triggerThreshold);
    }

    private List<AdventureOptionBase> PickOptions(int count)
    {
        List<AdventureOptionBase> available = new List<AdventureOptionBase>();
        foreach (var opt in optionPool)
        {
            if (opt == null) continue;
            if (opt.IsAvailableInCurrentDifficulty()) available.Add(opt);
        }

        if (available.Count == 0) return null;

        count = Mathf.Min(count, available.Count);

        Debug.Log($"[AdventureEventManager] PickOptions count={count} available={available.Count}");

        List<AdventureOptionBase> picked = new List<AdventureOptionBase>();
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(0, available.Count);
            picked.Add(available[idx]);
            available.RemoveAt(idx);
        }
        return picked;
    }
}
