using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 抽奖页面UI控制器
/// Inspector 配置：
/// - yuanText        : 显示【源】数量
/// - poolRemainText  : 显示奖池剩余种类数量
/// - draw1Button     : 抽一次按钮
/// - draw10Button    : 抽十次按钮
/// - resultPanel     : 抽奖结果面板
/// - resultContent   : 结果列表父节点
/// - resultItemPrefab: 单条结果 prefab（含 Image + TextMeshProUGUI）
/// - closeButton     : 关闭结果面板按钮
/// </summary>
public class GachaUI : MonoBehaviour
{
    [Header("主界面")]
    public TextMeshProUGUI yuanText;
    public TextMeshProUGUI poolRemainText;
    public Button          draw1Button;
    public Button          draw10Button;

    [Header("结果面板")]
    public GameObject      resultPanel;
    public Transform       resultContent;
    public GameObject      resultItemPrefab;
    public Button          closeButton;

    void OnEnable()
    {
        RefreshUI();
        if (draw1Button  != null) draw1Button.onClick.AddListener(OnDraw1);
        if (draw10Button != null) draw10Button.onClick.AddListener(OnDraw10);
        if (closeButton  != null)
        {
            closeButton.onClick.AddListener(CloseResult);
        }
        if (resultPanel  != null) resultPanel.SetActive(false);
    }

    void OnDisable()
    {
        if (draw1Button  != null) draw1Button.onClick.RemoveListener(OnDraw1);
        if (draw10Button != null) draw10Button.onClick.RemoveListener(OnDraw10);
        if (closeButton  != null)
        {
            closeButton.onClick.RemoveListener(CloseResult);
        }
    }

    private void RefreshUI()
    {
        if (GachaManager.Instance == null) return;

        if (yuanText != null)
            yuanText.text = $"源：{GachaManager.Instance.GetYuan()}";

        if (poolRemainText != null)
        {
            int r   = GachaManager.Instance.GetRarityRemain(GachaRarity.R);
            int sr  = GachaManager.Instance.GetRarityRemain(GachaRarity.SR);
            int ssr = GachaManager.Instance.GetRarityRemain(GachaRarity.SSR);
            int ur  = GachaManager.Instance.GetRarityRemain(GachaRarity.UR);
            var sb  = new System.Text.StringBuilder();
            sb.AppendLine($"奖池剩余：{r + sr + ssr + ur} 件");
            if (r   > 0) sb.AppendLine($"R：{r}");
            if (sr  > 0) sb.AppendLine($"SR：{sr}");
            if (ssr > 0) sb.AppendLine($"SSR：{ssr}");
            if (ur  > 0) sb.AppendLine($"UR：{ur}");
            poolRemainText.text = sb.ToString().TrimEnd();
        }

        bool canDraw1  = GachaManager.Instance.GetYuan() >= 1;
        bool canDraw10 = GachaManager.Instance.GetYuan() >= 10;
        if (draw1Button  != null) draw1Button.interactable  = canDraw1;
        if (draw10Button != null) draw10Button.interactable = canDraw10;
    }

    private void OnDraw1()
    {
        if (GachaManager.Instance == null) return;
        var result = GachaManager.Instance.DrawOne();
        if (result == null) { ShowNoResult(); return; }
        StartCoroutine(ShowResultsRoutine(new System.Collections.Generic.List<GachaItemData> { result }));
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

    private System.Collections.IEnumerator ShowResultsRoutine(System.Collections.Generic.List<GachaItemData> results)
    {
        if (resultPanel == null || resultContent == null) yield break;

        // 清空旧结果
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
                    GachaRarity.R   => "#4D99FF", // 蓝
                    GachaRarity.SR  => "#B24DFF", // 紫
                    GachaRarity.SSR => "#FFD700", // 金
                    GachaRarity.UR  => "#FF3333", // 红
                    _               => "#FFFFFF"
                };
                tmp.text = $"<color={rarityColor}>[{item.rarity}]</color> {item.itemName}";
            }

            // 设置流动边框稀有度颜色
            var border = obj.GetComponent<GachaItemBorder>();
            if (border != null) border.SetRarity(item.rarity);

            yield return new WaitForSecondsRealtime(0.2f);
        }
    }

    private void ShowNoResult()
    {
        if (GachaManager.Instance.GetYuan() < 1)
            Debug.Log("[抽奖] 源不足");
        else
            Debug.Log("[抽奖] 奖池已空");
    }

    private void CloseResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }
}
