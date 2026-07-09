using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗暂停菜单中的「设置」面板。
///
/// 功能：
/// - 攻击范围显示开关（Toggle）→ AttackRangeIndicatorManager.Visible
/// - 分身攻击范围显示开关（Toggle）→ AttackRangeIndicatorManager.CloneVisible
/// - BGM 音量滑条 → AudioManager.SetBgmVolume
/// - SFX 音量滑条 → AudioManager.SetSfxVolume
/// - 窗口化 / 全屏切换（Toggle）
/// - 分辨率切换按钮（循环切换常用分辨率）
/// - 关闭按钮 → 自身 SetActive(false)
///
/// 用法：
/// 把本脚本挂到一个空 GameObject 上即可——OnEnable 第一次激活时，若 Inspector 没拖控件，会自动用代码生成
/// 半透明黑底 + 标题 + 多组「Label + 控件」 + 关闭按钮 的完整可用 UI。也可以手动拖控件覆盖。
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    [Header("控件（可全部留空，会自动生成）")]
    public Toggle attackRangeToggle;
    public Toggle cloneAttackRangeToggle;
    public Toggle damageNumberToggle;
    public Button  damageSizeButton;      // 伤害数字大小切换（大/中/小）
    public Toggle fullscreenToggle;        // 窗口化/全屏
    public Button  resolutionButton;       // 分辨率切换
    public Toggle consoleToggle;           // 控制台输出开关
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Button closeButton;

    [Header("自动构建样式")]
    [Tooltip("自动构建时使用的字体（null 则使用 TMP 默认字体）")]
    public TMP_FontAsset font;
    [Tooltip("自动构建时面板的尺寸（W,H）")]
    public Vector2 autoBuildSize = new Vector2(840f, 926.4153f);

    // 常用分辨率预设
    private static readonly (int w, int h, string label)[] ResolutionPresets =
    {
        (2560, 1440, "2K 2560×1440"),
        (1920, 1080, "1920×1080"),
        (1680, 1050, "1680×1050"),
        (1600, 900,  "1600×900"),
        (1366, 768,  "1366×768"),
        (1280, 720,  "1280×720"),
    };
    private int _currentResIndex = 0;

    private bool _built = false;

    void Awake()
    {
        EnsureBuilt();
    }

    void OnEnable()
    {
        EnsureBuilt();
        Bind();
    }

    private void Bind()
    {
        if (attackRangeToggle != null)
        {
            attackRangeToggle.SetIsOnWithoutNotify(AttackRangeIndicatorManager.Visible);
            attackRangeToggle.onValueChanged.RemoveListener(OnAttackRangeChanged);
            attackRangeToggle.onValueChanged.AddListener(OnAttackRangeChanged);
        }
        if (cloneAttackRangeToggle != null)
        {
            cloneAttackRangeToggle.SetIsOnWithoutNotify(AttackRangeIndicatorManager.CloneVisible);
            cloneAttackRangeToggle.onValueChanged.RemoveListener(OnCloneAttackRangeChanged);
            cloneAttackRangeToggle.onValueChanged.AddListener(OnCloneAttackRangeChanged);
        }
        if (damageNumberToggle != null)
        {
            damageNumberToggle.SetIsOnWithoutNotify(DamageNumberSettings.Visible);
            damageNumberToggle.onValueChanged.RemoveListener(OnDamageNumberChanged);
            damageNumberToggle.onValueChanged.AddListener(OnDamageNumberChanged);
        }
        if (damageSizeButton != null)
        {
            UpdateDamageSizeButtonText();
            damageSizeButton.onClick.RemoveListener(OnDamageSizeChanged);
            damageSizeButton.onClick.AddListener(OnDamageSizeChanged);
        }
        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            fullscreenToggle.onValueChanged.RemoveListener(OnFullscreenChanged);
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        }
        if (resolutionButton != null)
        {
            UpdateResolutionButtonText();
            resolutionButton.onClick.RemoveListener(OnResolutionChanged);
            resolutionButton.onClick.AddListener(OnResolutionChanged);
            // 全屏时分辨率锁定原生，按钮灰掉；窗口化时可切换
            RefreshResolutionInteractable();
        }
        if (consoleToggle != null)
        {
            consoleToggle.SetIsOnWithoutNotify(GameConsole.Enabled);
            consoleToggle.onValueChanged.RemoveListener(OnConsoleChanged);
            consoleToggle.onValueChanged.AddListener(OnConsoleChanged);
        }
        if (bgmSlider != null)
        {
            bgmSlider.minValue = 0f; bgmSlider.maxValue = 1f;
            bgmSlider.SetValueWithoutNotify(AudioManager.GetBgmVolume());
            bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            bgmSlider.onValueChanged.AddListener(OnBgmChanged);
        }
        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f; sfxSlider.maxValue = 1f;
            sfxSlider.SetValueWithoutNotify(AudioManager.GetSfxVolume());
            sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxChanged);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnAttackRangeChanged(bool v) { AttackRangeIndicatorManager.Visible = v; }
    private void OnCloneAttackRangeChanged(bool v) { AttackRangeIndicatorManager.CloneVisible = v; }
    private void OnDamageNumberChanged(bool v)  { DamageNumberSettings.Visible = v; }
    private void OnDamageSizeChanged()
    {
        DamageNumberSettings.Size = (DamageNumberSettings.Size + 1) % 3;
        UpdateDamageSizeButtonText();
    }
    private void UpdateDamageSizeButtonText()
    {
        if (damageSizeButton == null) return;
        var txt = damageSizeButton.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.text = "伤害数字大小: " + DamageNumberSettings.SizeLabel;
    }

    private void OnConsoleChanged(bool v) { GameConsole.Enabled = v; }

    private void OnFullscreenChanged(bool v)
    {
        Screen.fullScreen = v;
        // 全屏→窗口化时：设为原生分辨率；更新按钮状态
        if (!v) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, false);
        UpdateResolutionButtonText();
        RefreshResolutionInteractable();
    }
    private void OnResolutionChanged()
    {
        if (Screen.fullScreen) return; // 全屏不可切换分辨率
        _currentResIndex = (_currentResIndex + 1) % ResolutionPresets.Length;
        var r = ResolutionPresets[_currentResIndex];
        Screen.SetResolution(r.w, r.h, false);
        UpdateResolutionButtonText();
    }
    private void RefreshResolutionInteractable()
    {
        if (resolutionButton != null)
            resolutionButton.interactable = !Screen.fullScreen;
    }
    private void UpdateResolutionButtonText()
    {
        if (resolutionButton == null) return;
        var txt = resolutionButton.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            if (Screen.fullScreen)
                txt.text = "分辨率: " + Screen.currentResolution.width + "×" + Screen.currentResolution.height + " (原生)";
            else
            {
                var r = ResolutionPresets[_currentResIndex];
                txt.text = "分辨率: " + r.label;
            }
        }
    }

    private void OnBgmChanged(float v) { AudioManager.SetBgmVolume(v); }
    private void OnSfxChanged(float v) { AudioManager.SetSfxVolume(v); }

    public void Close() { gameObject.SetActive(false); }

    // ─── 自动构建 ───────────────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        // 永远同步 sizeDelta 到最新 autoBuildSize（即使控件已拖好，背景框也要扩展）
        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        // 顶部居中锚定：面板顶部与屏幕顶部对齐，向下 30px
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -30f);
        // 用户硬性要求：高度固定 926.4153，宽度沿用 autoBuildSize.x。
        // 之前直接用 autoBuildSize 被场景序列化的 (700, 560) 覆盖 → 高度改不动。
        // 现在强制写死高度，autoBuildSize 字段的 .x 用于宽度。
        rt.sizeDelta = new Vector2(autoBuildSize.x, 926.4153f);

        // 关键：把面板自己的 Image 背景设为 stretch-to-parent（锚定四角 + offset 清零），
        // 这样它永远紧贴面板矩形、自动跟随 sizeDelta 变化铺满整个面板。
        // 之前错误地"stretch 所有子节点 Image"导致暂停菜单按钮（继续游戏/设置/关闭/操作说明/返回主菜单）
        // 的红色背景被强行放大到 840x1000，把面板覆盖了、UI 全乱。
        //   ★ 只动面板自己的 Image，不动任何子节点 Image（按钮/标题/装饰条各自有独立锚定，不应被外层强制改）。
        var selfImg = GetComponent<Image>();
        if (selfImg != null)
        {
            selfImg.rectTransform.anchorMin = new Vector2(0f, 0f);
            selfImg.rectTransform.anchorMax = new Vector2(1f, 1f);
            selfImg.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
            selfImg.rectTransform.offsetMin = Vector2.zero;
            selfImg.rectTransform.offsetMax = Vector2.zero;
        }

        // 已经全部拖好了就只更新背景框大小，不再创建重复控件
        if (attackRangeToggle != null && cloneAttackRangeToggle != null && damageNumberToggle != null
            && fullscreenToggle != null && resolutionButton != null && consoleToggle != null
            && bgmSlider != null && sfxSlider != null && closeButton != null) return;

        if (GetComponent<Image>() == null)
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.78f);
            bg.raycastTarget = true;
        }

        // 标题
        UIBuilder.CreateText(rt, "Title", "设置", 40, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -50f), new Vector2(autoBuildSize.x - 40, 60), font);

        // 多行控件
        float y0 = -130f;
        float rowH = 90f;

        // 行 1：攻击范围显示
        if (attackRangeToggle == null)
        {
            attackRangeToggle = UIBuilder.CreateToggle(rt, "AttackRangeToggle", "显示攻击范围",
                new Vector2(60f, y0 - 0 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 2：分身攻击范围显示
        if (cloneAttackRangeToggle == null)
        {
            cloneAttackRangeToggle = UIBuilder.CreateToggle(rt, "CloneAttackRangeToggle", "显示分身攻击范围",
                new Vector2(60f, y0 - 1 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 3：伤害数字
        if (damageNumberToggle == null)
        {
            damageNumberToggle = UIBuilder.CreateToggle(rt, "DamageNumberToggle", "显示伤害数字",
                new Vector2(60f, y0 - 2 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 3.5：伤害数字大小
        if (damageSizeButton == null)
        {
            damageSizeButton = UIBuilder.CreateButton(rt, "DamageSizeBtn", "伤害数字大小: " + DamageNumberSettings.SizeLabel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, y0 - 2 * rowH - 72f), new Vector2(autoBuildSize.x - 120f, 52f), font);
        }

        // 行 4：BGM
        if (bgmSlider == null)
        {
            bgmSlider = UIBuilder.CreateSlider(rt, "BgmSlider", "BGM 音量",
                new Vector2(60f, y0 - 3 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 5：SFX
        if (sfxSlider == null)
        {
            sfxSlider = UIBuilder.CreateSlider(rt, "SfxSlider", "音效音量",
                new Vector2(60f, y0 - 4 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 6：窗口化切换
        if (fullscreenToggle == null)
        {
            fullscreenToggle = UIBuilder.CreateToggle(rt, "FullscreenToggle", "全屏",
                new Vector2(60f, y0 - 5 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 7：分辨率切换
        if (resolutionButton == null)
        {
            var r = ResolutionPresets[_currentResIndex];
            resolutionButton = UIBuilder.CreateButton(rt, "ResolutionBtn", "分辨率: " + r.label,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(60f, y0 - 6 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 行 8：控制台输出开关
        if (consoleToggle == null)
        {
            consoleToggle = UIBuilder.CreateToggle(rt, "ConsoleToggle", "控制台",
                new Vector2(60f, y0 - 7 * rowH), new Vector2(autoBuildSize.x - 120f, 60f), font);
        }

        // 关闭按钮：面板内部底部居中，控件行之下
        if (closeButton == null)
        {
            closeButton = UIBuilder.CreateButton(rt, "CloseButton", "关闭",
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(200f, 64f), font);
        }
    }
}
