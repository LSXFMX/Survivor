using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdventureUI : MonoBehaviour
{
    [Header("选项 A")]
    public TextMeshProUGUI nameA;
    public TextMeshProUGUI descA;
    public TextMeshProUGUI effectA;
    public Image iconA;
    public Button buttonA;
    public GameObject rootA;

    [Header("选项 B")]
    public TextMeshProUGUI nameB;
    public TextMeshProUGUI descB;
    public TextMeshProUGUI effectB;
    public Image iconB;
    public Button buttonB;
    public GameObject rootB;

    [Header("选项 C（只需拖 rootC，子控件运行时自动查找）")]
    public GameObject rootC;

    // C 子控件从 rootC 运行时自动提取
    private TextMeshProUGUI _nameC;
    private TextMeshProUGUI _descC;
    private TextMeshProUGUI _effectC;
    private Image _iconC;
    private Button _buttonC;
    private bool _cResolved;

    public bool IsShowing => gameObject.activeSelf;

    private AdventureOptionBase _optionA;
    private AdventureOptionBase _optionB;
    private AdventureOptionBase _optionC;
    private int _cost;

    private void Awake()
    {
        if (buttonA != null) buttonA.onClick.AddListener(OnClickA);
        if (buttonB != null) buttonB.onClick.AddListener(OnClickB);
        if (rootC != null) rootC.SetActive(false);
    }

    private void ResolveOptionC()
    {
        if (_cResolved || rootC == null) return;
        _cResolved = true;

        // 从 rootC 递归查找所有 TextMeshProUGUI 并按其名字匹配
        foreach (var t in rootC.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (t.name.Contains("Name"))   _nameC   = t;
            if (t.name.Contains("Desc"))   _descC   = t;
            if (t.name.Contains("Effect")) _effectC = t;
        }
        _iconC   = rootC.GetComponentInChildren<Image>(true);
        _buttonC = rootC.GetComponent<Button>() ?? rootC.GetComponentInChildren<Button>(true);
        if (_buttonC != null) _buttonC.onClick.AddListener(OnClickC);
    }

    public void Show(AdventureOptionBase optA, AdventureOptionBase optB, int cost)
    {
        Show(optA, optB, null, cost);
    }

    public void Show(AdventureOptionBase optA, AdventureOptionBase optB, AdventureOptionBase optC, int cost)
    {
        _optionA = optA;
        _optionB = optB;
        _optionC = optC;
        _cost = cost;

        FillOption(nameA, descA, effectA, iconA, rootA, optA);
        FillOption(nameB, descB, effectB, iconB, rootB, optB);

        if (optC != null && rootC != null)
        {
            ResolveOptionC();
            FillOption(_nameC, _descC, _effectC, _iconC, rootC, optC);
            rootC.SetActive(true);
        }
        else if (rootC != null)
        {
            rootC.SetActive(false);
        }

        Time.timeScale = 0;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
        if (bui != null) bui.ResumeTime();
        else Time.timeScale = 1;
        gameObject.SetActive(false);
    }

    public void OnClickA() => TryExecute(_optionA);
    public void OnClickB() => TryExecute(_optionB);
    public void OnClickC() => TryExecute(_optionC);

    private void TryExecute(AdventureOptionBase option)
    {
        if (option == null) return;
        option.Execute();
        GateChallengeManager.Instance?.Unlock();
        Hide();
    }

    private void FillOption(TextMeshProUGUI nameText, TextMeshProUGUI descText,
                             TextMeshProUGUI effectText, Image iconImg,
                             GameObject root, AdventureOptionBase opt)
    {
        if (nameText != null) nameText.text = opt.optionName;
        if (descText != null) descText.text = opt.optionDescription;
        if (effectText != null) effectText.text = opt.effectDescription;
        if (iconImg != null)
        {
            iconImg.sprite = opt.icon;
            iconImg.gameObject.SetActive(opt.icon != null);
        }
        if (root != null) root.SetActive(true);
    }
}
