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

    // 选项 C 由代码自动从 A/B 克隆，无需场景拖拽
    private TextMeshProUGUI nameC;
    private TextMeshProUGUI descC;
    private TextMeshProUGUI effectC;
    private Image iconC;
    private Button buttonC;
    private GameObject rootC;
    private bool _cBuilt = false;

    public bool IsShowing => gameObject.activeSelf;

    private AdventureOptionBase _optionA;
    private AdventureOptionBase _optionB;
    private AdventureOptionBase _optionC;
    private int _cost;

    private void Awake()
    {
        if (buttonA != null) buttonA.onClick.AddListener(OnClickA);
        if (buttonB != null) buttonB.onClick.AddListener(OnClickB);
    }

    /// <summary>从 A 或 B 克隆出一个选项 C（仅首次、幂等）</summary>
    private void EnsureOptionC()
    {
        if (_cBuilt || rootC != null) { _cBuilt = true; return; }
        _cBuilt = true;

        // 优先克隆 rootB（如果有），其次 rootA
        GameObject template = rootB != null ? rootB : rootA;
        if (template == null) return;

        rootC = Instantiate(template, template.transform.parent);
        rootC.name = "OptionC (auto)";
        // 下移一个位置：取 B 的 anchoredPosition 再下移
        RectTransform ta = rootA != null ? rootA.GetComponent<RectTransform>() : null;
        RectTransform tb = rootB != null ? rootB.GetComponent<RectTransform>() : null;
        RectTransform tc = rootC.GetComponent<RectTransform>();
        if (tb != null && ta != null)
        {
            float dy = tb.anchoredPosition.y - ta.anchoredPosition.y;
            tc.anchoredPosition = new Vector2(tb.anchoredPosition.x, tb.anchoredPosition.y + dy);
        }

        // 提取子控件
        nameC   = FindTMPChild(rootC, "Name");
        descC   = FindTMPChild(rootC, "Desc");
        effectC = FindTMPChild(rootC, "Effect");
        iconC   = rootC.GetComponentInChildren<Image>();
        buttonC = rootC.GetComponent<Button>();
        if (buttonC == null) buttonC = rootC.GetComponentInChildren<Button>();
        if (buttonC != null)
        {
            buttonC.onClick.RemoveAllListeners();
            buttonC.onClick.AddListener(OnClickC);
        }
        rootC.SetActive(false);
    }

    private static TextMeshProUGUI FindTMPChild(GameObject parent, string contains)
    {
        foreach (var t in parent.GetComponentsInChildren<TextMeshProUGUI>(true))
            if (t.name.Contains(contains)) return t;
        return null;
    }

    /// <summary>兼容旧调用：二选一</summary>
    public void Show(AdventureOptionBase optA, AdventureOptionBase optB, int cost)
    {
        Show(optA, optB, null, cost);
    }

    /// <summary>SSR_11 解锁后调用：三选一</summary>
    public void Show(AdventureOptionBase optA, AdventureOptionBase optB, AdventureOptionBase optC, int cost)
    {
        _optionA = optA;
        _optionB = optB;
        _optionC = optC;
        _cost = cost;

        FillOption(nameA, descA, effectA, iconA, rootA, optA);
        FillOption(nameB, descB, effectB, iconB, rootB, optB);

        if (optC != null)
        {
            EnsureOptionC();
            if (rootC != null)
            {
                FillOption(nameC, descC, effectC, iconC, rootC, optC);
                rootC.SetActive(true);
            }
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
