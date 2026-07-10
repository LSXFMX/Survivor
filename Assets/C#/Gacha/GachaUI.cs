using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 抽奖页面UI控制器
/// </summary>
public class GachaUI : MonoBehaviour
{
    [Header("主界面")]
    public TextMeshProUGUI yuanText;
    public TextMeshProUGUI totalDrawsText;
    public TextMeshProUGUI poolRemainText;
    public Button          draw1Button;
    public Button          draw10Button;

    [Header("结果面板")]
    public GameObject      resultPanel;
    public Transform       resultContent;
    public GameObject      resultItemPrefab;
    public Button          closeButton;

    [Header("源提示/聚宝盆按钮（可在Scene中调整）")]
    public TMP_FontAsset uiFont;
    public Vector2 infoButtonAnchoredPosition = new Vector2(145f, 0f);
    public Vector2 infoTooltipAnchoredPosition = new Vector2(-40f, -48f);
    public Vector2 infoTooltipSize = new Vector2(760f, 480f);
    public Vector2 treasureButtonAnchoredPosition = new Vector2(0f, -14f);
    public Vector2 treasureButtonSize = new Vector2(112f, 112f);

    [Header("聚宝盆图标 Sprite（Inspector 静态绑定，弃用磁盘加载）")]
    [Tooltip("场景里请把 像素幸存者资源包/存档装备图标/聚宝盆/FirstClearChest.png 拖到这里。" +
             "保持空也能跑（按钮无图），但不会再走运行时 File.ReadAllBytes 动态加载。")]
    public Sprite chestIconSprite;

    private Button infoButton;
    private GameObject infoTooltip;
    private Button treasureButton;
    private GameObject treasurePanel;

    void OnEnable()
    {
        EnsureExtraUI();
        RefreshUI();
        if (draw1Button  != null) draw1Button.onClick.AddListener(OnDraw1);
        if (draw10Button != null) draw10Button.onClick.AddListener(OnDraw10);
        if (closeButton  != null) closeButton.onClick.AddListener(CloseResult);
        if (resultPanel  != null)
        {
            resultPanel.SetActive(false);
            var p = RightClickClosePanel.EnsureOn(resultPanel);
            p.onRightClickClose = new UnityEngine.Events.UnityEvent();
            p.onRightClickClose.AddListener(CloseResult);
        }
    }

    void OnDisable()
    {
        if (draw1Button  != null) draw1Button.onClick.RemoveListener(OnDraw1);
        if (draw10Button != null) draw10Button.onClick.RemoveListener(OnDraw10);
        if (closeButton  != null) closeButton.onClick.RemoveListener(CloseResult);
    }

    private void EnsureExtraUI()
    {
        if (uiFont == null && yuanText != null) uiFont = yuanText.font;
        if (infoButton == null) CreateInfoButton();
        if (treasureButton == null) CreateTreasureButton();
    }

    private void CreateInfoButton()
    {
        Transform parent = yuanText != null ? yuanText.transform : transform;
        GameObject go = new GameObject("YuanSourceInfoButton");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(34, 34);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = infoButtonAnchoredPosition;

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.18f, 0.92f);
        infoButton = go.AddComponent<Button>();

        TextMeshProUGUI t = CreateText(go.transform, "i", 26, TextAlignmentOptions.Center);
        t.raycastTarget = false;

        EventTrigger trigger = go.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerEnter, _ => ShowYuanTooltip(true));
        AddTrigger(trigger, EventTriggerType.PointerExit, _ => ShowYuanTooltip(false));
    }

    private void ShowYuanTooltip(bool show)
    {
        if (show)
        {
            if (infoTooltip == null)
            {
                infoTooltip = CreatePanel("YuanSourceTooltip", infoTooltipSize);
                RectTransform rect = infoTooltip.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = infoTooltipAnchoredPosition;
                TextMeshProUGUI txt = CreateText(infoTooltip.transform, "", 20, TextAlignmentOptions.TopLeft);
                RectTransform tr = txt.GetComponent<RectTransform>();
                tr.offsetMin = new Vector2(20, 20);
                tr.offsetMax = new Vector2(-20, -20);
                txt.enableWordWrapping = true;
                txt.overflowMode = TextOverflowModes.Overflow;
                txt.lineSpacing = -6f;
                txt.name = "Content";
            }
            TextMeshProUGUI content = infoTooltip.transform.Find("Content")?.GetComponent<TextMeshProUGUI>();
            if (content != null && GachaManager.Instance != null)
                content.text = GachaManager.Instance.GetYuanSourceDescription();
            infoTooltip.SetActive(true);
        }
        else if (infoTooltip != null)
        {
            infoTooltip.SetActive(false);
        }
    }

    private void CreateTreasureButton()
    {
        Transform parent = totalDrawsText != null ? totalDrawsText.transform : transform;
        GameObject go = new GameObject("FirstClearTreasureButton");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = treasureButtonSize;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = treasureButtonAnchoredPosition;

        Image img = go.AddComponent<Image>();
        // 优先使用 Inspector 静态绑定；为空时回退到 Resources/UI/FirstClearChest
        // （这是为了避免没绑 sprite 时按钮渲染成一片纯白方块——之前主界面左下角看到的"白方块"就是这个原因）。
        Sprite icon = chestIconSprite;
        if (icon == null)
        {
            icon = Resources.Load<Sprite>("UI/FirstClearChest");
            // 二级兜底：Resources.Load<Sprite> 返回 null 通常意味着该 png 的 TextureType 不是 Sprite，
            // 这里退化用 Texture2D 加载后手动包一个 Sprite，保证至少能显示。
            if (icon == null)
            {
                var tex = Resources.Load<Texture2D>("UI/FirstClearChest");
                if (tex != null)
                {
                    icon = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                }
            }
            if (icon != null) chestIconSprite = icon; // 缓存一次，避免下次进入 OnEnable 重复加载
            else Debug.LogWarning("[抽奖] 聚宝盆按钮 chestIconSprite 未在 Inspector 绑定，且 Resources/UI/FirstClearChest 缺失，将显示占位底色。");
        }
        if (icon != null)
        {
            img.sprite = icon;
            img.color = Color.white;
        }
        else
        {
            // 仍然没有 sprite：给个深底兜底，至少不会显示成纯白方块
            img.color = new Color(0.12f, 0.16f, 0.28f, 0.95f);
        }
        img.preserveAspect = true;
        treasureButton = go.AddComponent<Button>();
        treasureButton.onClick.AddListener(ToggleTreasurePanel);
    }

    private void ToggleTreasurePanel()
    {
        if (treasurePanel != null && treasurePanel.activeSelf)
        {
            treasurePanel.SetActive(false);
            return;
        }
        ShowTreasurePanel();
    }

    private void ShowTreasurePanel()
    {
        if (treasurePanel == null)
        {
            treasurePanel = CreatePanel("FirstClearTreasurePanel", new Vector2(720, 940));
            RectTransform rect = treasurePanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            // 整个界面向下扩展后向上提一些，避免 N13 顶到屏幕边缘
            rect.anchoredPosition = new Vector2(0f, 60f);
            var closePanel = RightClickClosePanel.EnsureOn(treasurePanel);
            closePanel.onRightClickClose = new UnityEngine.Events.UnityEvent();
            closePanel.onRightClickClose.AddListener(() => treasurePanel.SetActive(false));
        }
        treasurePanel.SetActive(true);
        RebuildTreasurePanel();
    }

    private void RebuildTreasurePanel()
    {
        foreach (Transform child in treasurePanel.transform) Destroy(child.gameObject);
        TextMeshProUGUI title = CreateText(treasurePanel.transform, "聚宝盆 · 首次通关宝箱", 30, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -14), new Vector2(-24, 64));

        for (int i = 1; i <= 13; i++)
        {
            string label = "N" + i;
            int clearCount = ClearRecordManager.Instance != null ? ClearRecordManager.Instance.GetClearCount(label) : 0;
            bool claimed = GachaManager.Instance != null && GachaManager.Instance.IsFirstClearChestClaimed(label);
            int reward = GachaManager.Instance != null ? GachaManager.Instance.GetFirstClearChestReward(label) : i * 3;
            float y = -80f - (i - 1) * 58f;

            TextMeshProUGUI rowText = CreateText(treasurePanel.transform,
                $"{label}  已通关：{clearCount}次  宝箱：{reward}源", 22, TextAlignmentOptions.Left);
            SetRect(rowText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(28, y), new Vector2(-190, 42));

            Button btn = CreateTextButton(treasurePanel.transform, claimed ? "已领取" : (clearCount > 0 ? "领取" : "未通关"), new Vector2(140, 42));
            RectTransform br = btn.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(1, 1);
            br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(1, 1);
            br.anchoredPosition = new Vector2(-28, y);
            btn.interactable = clearCount > 0 && !claimed;
            string capturedLabel = label;
            btn.onClick.AddListener(() =>
            {
                GachaManager.Instance?.ClaimFirstClearChest(capturedLabel);
                RefreshUI();
                RebuildTreasurePanel();
            });
        }

        Button close = CreateTextButton(treasurePanel.transform, "关闭", new Vector2(160, 52));
        close.gameObject.AddComponent<RightClickCloseable>();
        RectTransform cr = close.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0.5f, 0);
        cr.anchorMax = new Vector2(0.5f, 0);
        cr.pivot = new Vector2(0.5f, 0);
        cr.anchoredPosition = new Vector2(0, 50);
        close.onClick.AddListener(() => treasurePanel.SetActive(false));
    }

    private void RefreshUI()
    {
        if (GachaManager.Instance == null) return;

        if (yuanText != null)
            yuanText.text = $"源：{GachaManager.Instance.GetYuan()}";

        int totalDraws = GachaManager.Instance.GetTotalDrawCount();
        if (totalDrawsText != null)
            totalDrawsText.text = $"累计抽取：{totalDraws}次";

        if (poolRemainText != null)
        {
            int r = GachaManager.Instance.GetRarityRemain(GachaRarity.R);
            int sr = GachaManager.Instance.GetRarityRemain(GachaRarity.SR);
            int ssr = GachaManager.Instance.GetRarityRemain(GachaRarity.SSR);
            int ur = GachaManager.Instance.GetRarityRemain(GachaRarity.UR);
            var sb = new System.Text.StringBuilder();
            if (totalDrawsText == null) sb.AppendLine($"累计抽取：{totalDraws}次");
            sb.AppendLine($"奖池剩余：{r + sr + ssr + ur} 件");
            if (r > 0) sb.AppendLine($"R：{r}");
            if (sr > 0) sb.AppendLine($"SR：{sr}");
            if (ssr > 0) sb.AppendLine($"SSR：{ssr}");
            if (ur > 0) sb.AppendLine($"UR：{ur}");
            poolRemainText.text = sb.ToString().TrimEnd();
        }

        bool canDraw1 = GachaManager.Instance.GetYuan() >= 1;
        bool canDraw10 = GachaManager.Instance.GetYuan() >= 10;
        if (draw1Button != null) draw1Button.interactable = canDraw1;
        if (draw10Button != null) draw10Button.interactable = canDraw10;
    }

    private void OnDraw1()
    {
        if (GachaManager.Instance == null) return;
        var result = GachaManager.Instance.DrawOne();
        if (result == null) { ShowNoResult(); return; }
        StartCoroutine(ShowResultsRoutine(new List<GachaItemData> { result }));
        RefreshUI();
    }

    private void OnDraw10()
    {
        if (GachaManager.Instance == null) return;
        var results = GachaManager.Instance.DrawTen();
        if (results.Count == 0) { ShowNoResult(); return; }
        StartCoroutine(ShowResultsRoutine(results));
        RefreshUI();
    }

    private System.Collections.IEnumerator ShowResultsRoutine(List<GachaItemData> results)
    {
        if (resultPanel == null || resultContent == null) yield break;
        foreach (Transform t in resultContent) Destroy(t.gameObject);
        resultPanel.SetActive(true);

        foreach (var item in results)
        {
            if (resultItemPrefab == null) break;
            GameObject obj = Instantiate(resultItemPrefab, resultContent);
            var img = obj.GetComponentInChildren<Image>();
            if (img != null && item.icon != null) img.sprite = item.icon;
            var tmp = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                string rarityColor = item.rarity switch
                {
                    GachaRarity.R => "#4D99FF",
                    GachaRarity.SR => "#B24DFF",
                    GachaRarity.SSR => "#FFD700",
                    GachaRarity.UR => "#FF3333",
                    _ => "#FFFFFF"
                };
                tmp.text = $"<color={rarityColor}>[{item.rarity}]</color> {item.itemName}";
            }
            var border = obj.GetComponent<GachaItemBorder>();
            if (border != null) border.SetRarity(item.rarity);
            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    private void ShowNoResult()
    {
        if (GachaManager.Instance.GetYuan() < 1) Debug.Log("[抽奖] 源不足");
        else Debug.Log("[抽奖] 奖池已空");
    }

    private void CloseResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    private GameObject CreatePanel(string name, Vector2 size)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.025f, 0.04f, 0.96f);
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string text, int fontSize, TextAlignmentOptions align)
    {
        GameObject go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = align;
        if (uiFont != null) tmp.font = uiFont;
        return tmp;
    }

    private Button CreateTextButton(Transform parent, string text, Vector2 size)
    {
        GameObject go = new GameObject("Button_" + text);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        // 必须给一张内置 Sprite——否则部分 Unity 版本下 Image 没有 sprite 时会渲染成纯白方块，
        // 同时 color 也无法正确生效（这就是"宝箱面板领取按钮一片白色"的根因）。
        // 使用 UI/Skin 自带的 UISprite 作为兜底，找不到再回退到 Background。
        Sprite uiSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd")
                          ?? Resources.GetBuiltinResource<Sprite>("UI/Skin/Background.psd");
        if (uiSprite != null) img.sprite = uiSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.12f, 0.16f, 0.28f, 0.95f);
        Button btn = go.AddComponent<Button>();
        // 显式配 ColorBlock，避免 Disabled 状态下也整体偏白
        var cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f);
        cb.pressedColor     = new Color(0.75f, 0.78f, 0.9f, 1f);
        cb.selectedColor    = Color.white;
        cb.disabledColor    = new Color(0.55f, 0.58f, 0.62f, 1f);
        btn.colors = cb;
        TextMeshProUGUI tmp = CreateText(go.transform, text, 22, TextAlignmentOptions.Center);
        tmp.raycastTarget = false;
        return btn;
    }

    private void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
    }

    private void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(action);
        trigger.triggers.Add(entry);
    }

}
