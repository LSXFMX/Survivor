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

    [Header("选项 C — 拖任意子控件即可，缺的自动从 rootC 补齐")]
    public GameObject rootC;
    public TextMeshProUGUI nameC;
    public TextMeshProUGUI descC;
    public TextMeshProUGUI effectC;
    public Image iconC;
    public Button buttonC;

    public bool IsShowing => gameObject.activeSelf;

    private AdventureOptionBase _optionA;
    private AdventureOptionBase _optionB;
    private AdventureOptionBase _optionC;
    private int _cost;

    private void Awake()
    {
        if (buttonA != null) buttonA.onClick.AddListener(OnClickA);
        if (buttonB != null) buttonB.onClick.AddListener(OnClickB);
        ResolveOptionC();
        if (rootC != null) rootC.SetActive(false);
    }

    private void ResolveOptionC()
    {
        if (rootC == null) return;

        // 收集 rootC 下所有 TextMeshProUGUI，按层级/顺序补缺
        var tmps = rootC.GetComponentsInChildren<TextMeshProUGUI>(true);
        // 优先取直接子节点，不够再取孙子
        var direct = new System.Collections.Generic.List<TextMeshProUGUI>();
        foreach (var t in tmps) if (t.transform.parent == rootC.transform) direct.Add(t);
        if (direct.Count == 0) direct.AddRange(tmps);

        if (nameC   == null && direct.Count > 0) nameC   = direct[0];
        if (descC   == null && direct.Count > 1) descC   = direct[1];
        if (effectC == null && direct.Count > 2) effectC = direct[2];

        if (iconC   == null) iconC   = rootC.GetComponentInChildren<Image>(true);
        if (buttonC == null) buttonC = rootC.GetComponent<Button>() ?? rootC.GetComponentInChildren<Button>(true);
        if (buttonC != null) buttonC.onClick.AddListener(OnClickC);
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
            FillOption(nameC, descC, effectC, iconC, rootC, optC);
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
