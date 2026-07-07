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
    public Slider bgmSlider;
    public Slider sfxSlider;
    public Button closeButton;

    [Header("自动构建样式")]
    [Tooltip("自动构建时使用的字体（null 则使用 TMP 默认字体）")]
    public TMP_FontAsset font;
    [Tooltip("自动构建时面板的尺寸（W,H）")]
    public Vector2 autoBuildSize = new Vector2(700f, 720f);

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


    private void OnBgmChanged(float v)        { AudioManager.SetBgmVolume(v); }
    private void OnSfxChanged(float v)        { AudioManager.SetSfxVolume(v); }

    public void Close() { gameObject.SetActive(false); }

    // ─── 自动构建 ───────────────────────────────────────────────────────────

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        // 已经全部拖好了就不构建
        if (attackRangeToggle != null && cloneAttackRangeToggle != null && damageNumberToggle != null && bgmSlider != null && sfxSlider != null && closeButton != null) return;

        var rt = GetComponent<RectTransform>();
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();
        // 撑满父级 + 居中（如果父级是 Canvas）
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = autoBuildSize;

        // 半透明黑色底板（覆盖整面板）
        if (GetComponent<Image>() == null)
        {
            var bg = gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.78f);
            bg.raycastTarget = true; // 拦截点击
        }

        // 标题
        UIBuilder.CreateText(rt, "Title", "设置", 40, FontStyles.Bold,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -50f), new Vector2(autoBuildSize.x - 40, 60), font);

        // 多行控件
        float y0 = -130f;   // 第一行 Y（相对面板顶部）
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
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
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

        // 关闭按钮：右下角（避开所有控件行）
        if (closeButton == null)
        {
            closeButton = UIBuilder.CreateButton(rt, "CloseButton", "关闭",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-30f, 30f), new Vector2(140f, 60f), font);
        }
    }
}
