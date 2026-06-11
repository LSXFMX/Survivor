using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 标题界面音量面板控制器：
/// - 把 BGM/SE 两个 Slider 绑到 AudioManager 的全局音量
/// - 点击音乐符号按钮切换两个滑条组的显示/隐藏
/// - 仅在【主页面】显示音乐符号 + 滑条；进入战斗或打开抽卡/换装/存档/难度等子面板时整体隐藏
///
/// Inspector 配置：
/// - bgmGroup / seGroup : BGM、SE 整组父 GameObject（用于 SetActive 切换）
/// - bgmSlider / sfxSlider : 两个滑条
/// - musicIconButton : 音乐符号上挂的 Button
/// - startVisible : 启动时滑条组是否展开（音乐符号本身的可见性由"是否在主页面"决定）
/// </summary>
public class AudioPanelUI : MonoBehaviour
{
    [Header("分组（用于显示/隐藏）")]
    public GameObject bgmGroup;
    public GameObject seGroup;

    [Header("滑条")]
    public Slider bgmSlider;
    public Slider sfxSlider;

    [Header("切换按钮（音乐符号）")]
    public Button musicIconButton;

    [Header("初始显示")]
    public bool startVisible = true;

    private bool _slidersExpanded;

    // 主页面检测：自动找 title 组件 + 各个会"接管"主页面的子面板，
    // 任意一个子面板激活就视为"离开主页面"，整体隐藏音乐符号。
    private title _titleRef;
    private GameObject[] _hideWhenAnyActive;
    private bool _onMainScreenLast = true; // 上一次主页面状态，仅在切换时打 log/SetActive 减少开销

    private void Awake()
    {
        // ===== 关键：层级自愈 =====
        // 场景里美术把"音乐符号按钮"误挂到了 bgmGroup（或 seGroup）的子节点下，
        // 而 SetVisible 又会 SetActive(bgmGroup, false) —— 一点击按钮，按钮自己也被关掉，
        // 表现就是"点一下整个 BGM/SE 面板连同符号一起消失再也唤不回"。
        // 这里在 Awake 阶段把按钮 reparent 到 bgmGroup/seGroup 的父节点下（与它们平级），
        // 保留视觉位置（worldPositionStays=true），从根本上断开"按钮父节点 = 被控制隐藏的组"。
        FixMusicIconParenting();

        // 同步当前音量到滑条
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f;
            bgmSlider.maxValue = 1f;
            bgmSlider.value = AudioManager.GetBgmVolume();
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.value = AudioManager.GetSfxVolume();
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }
        if (musicIconButton != null)
        {
            musicIconButton.onClick.AddListener(Toggle);
        }

        SetSlidersExpanded(startVisible);

        // 解析"主页面是否激活"的依赖（title + 它的几个子面板）
        ResolveMainScreenRefs();
    }

    /// <summary>
    /// 把 musicIconButton 从 bgmGroup/seGroup 的子树里"剥出来"，挂到它们的父节点下。
    /// - 若按钮父节点不在 bgm/seGroup 子树里，直接跳过；
    /// - reparent 时 worldPositionStays=true，保留按钮原视觉位置（RectTransform 同样会保留 world pos）；
    /// - 完成后按钮在层级上和 bgmGroup/seGroup 是兄弟关系，SetActive 这两个组对它无影响。
    /// </summary>
    private void FixMusicIconParenting()
    {
        if (musicIconButton == null) return;
        Transform btn = musicIconButton.transform;

        Transform offendingGroup = null;
        if (bgmGroup != null && btn.IsChildOf(bgmGroup.transform) && btn != bgmGroup.transform)
            offendingGroup = bgmGroup.transform;
        else if (seGroup != null && btn.IsChildOf(seGroup.transform) && btn != seGroup.transform)
            offendingGroup = seGroup.transform;

        if (offendingGroup == null) return;

        // reparent 到组的父节点（即 AudioPanel 容器），保留世界位置
        Transform newParent = offendingGroup.parent;
        if (newParent == null) newParent = transform; // 兜底挂到本组件所在 GO
        if (btn.parent != newParent)
        {
            btn.SetParent(newParent, true);
            // 让按钮始终绘制在最上面，避免 reparent 后被滑条等遮挡
            btn.SetAsLastSibling();
            Debug.Log($"[AudioPanelUI] 修正层级：musicIconButton 已从 \"{offendingGroup.name}\" reparent 到 \"{newParent.name}\"，避免点击时被自身父节点 SetActive(false) 隐藏。");
        }
    }

    /// <summary>
    /// 自动定位 title 组件 + 它身上的 fightscene/savescene/gachaPanel/skinShowroomPanel/difficultySelectUI。
    /// 任何一个激活就视作"已经离开主页面"，音乐符号要整体隐藏。
    /// 用反射读取 title 字段是因为 title 字段都是 public，可能会改名——这里改成显式按字段名读，
    /// 避免与 title.cs 强耦合，只要字段在就读到，缺失就跳过。
    /// </summary>
    private void ResolveMainScreenRefs()
    {
        _titleRef = FindObjectOfType<title>(true);
        if (_titleRef == null)
        {
            _hideWhenAnyActive = new GameObject[0];
            return;
        }

        // title.fightscene/savescene/gachaPanel/skinShowroomPanel/difficultySelectUI 都是 public 字段，
        // 直接读即可。
        var list = new System.Collections.Generic.List<GameObject>(5);
        if (_titleRef.fightscene != null)            list.Add(_titleRef.fightscene);
        if (_titleRef.savescene != null)             list.Add(_titleRef.savescene);
        if (_titleRef.gachaPanel != null)            list.Add(_titleRef.gachaPanel);
        if (_titleRef.skinShowroomPanel != null)     list.Add(_titleRef.skinShowroomPanel);
        if (_titleRef.difficultySelectUI != null)    list.Add(_titleRef.difficultySelectUI);
        _hideWhenAnyActive = list.ToArray();
    }

    private void Update()
    {
        bool onMain = ComputeOnMainScreen();
        if (onMain == _onMainScreenLast) return;
        _onMainScreenLast = onMain;

        // 主页面：按钮 + 已展开过的滑条恢复显示；非主页面：全部 SetActive(false)
        if (musicIconButton != null) musicIconButton.gameObject.SetActive(onMain);
        if (onMain)
        {
            // 滑条组按用户最近一次展开/收起的状态恢复
            ApplySlidersVisible(_slidersExpanded);
        }
        else
        {
            // 非主页面：把展开的滑条组也强制隐藏（不改 _slidersExpanded，回到主页面再恢复）
            if (bgmGroup != null) bgmGroup.SetActive(false);
            if (seGroup  != null) seGroup.SetActive(false);
        }
    }

    /// <summary>
    /// "主页面"判断：title 组件存在且自身激活，且 fightscene/savescene/gachaPanel/skinShowroomPanel/difficultySelectUI
    /// 任何一个都没激活；title 不存在时（例如打包后场景结构变了）退化为"始终显示"避免误隐藏。
    /// </summary>
    private bool ComputeOnMainScreen()
    {
        if (_titleRef == null)
        {
            // 启动顺序：可能 AudioPanelUI Awake 早于 title 出现；这里再尝试解析一次
            ResolveMainScreenRefs();
            if (_titleRef == null) return true;
        }
        if (!_titleRef.gameObject.activeInHierarchy) return false;
        if (_hideWhenAnyActive != null)
        {
            for (int i = 0; i < _hideWhenAnyActive.Length; i++)
            {
                var go = _hideWhenAnyActive[i];
                if (go != null && go.activeInHierarchy) return false;
            }
        }
        return true;
    }

    private void OnDestroy()
    {
        if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
        if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        if (musicIconButton != null) musicIconButton.onClick.RemoveListener(Toggle);
    }

    private void OnBgmChanged(float v) { AudioManager.SetBgmVolume(v); }
    private void OnSfxChanged(float v) { AudioManager.SetSfxVolume(v); }

    /// <summary>用户点击音乐符号：展开/收起滑条组。</summary>
    public void Toggle() { SetSlidersExpanded(!_slidersExpanded); }

    /// <summary>
    /// 兼容旧绑定（外部脚本/事件可能还在调 SetVisible(bool)）。
    /// 现在含义 = 滑条组展开/收起；按钮本身的可见性由"是否在主页面"自动决定。
    /// </summary>
    public void SetVisible(bool v) { SetSlidersExpanded(v); }

    private void SetSlidersExpanded(bool v)
    {
        _slidersExpanded = v;
        // 如果当前不在主页面，先不显示（Update 会在切回主页面时恢复）
        if (_onMainScreenLast)
            ApplySlidersVisible(v);
    }

    private void ApplySlidersVisible(bool v)
    {
        if (bgmGroup != null) bgmGroup.SetActive(v);
        if (seGroup  != null) seGroup.SetActive(v);
    }
}
