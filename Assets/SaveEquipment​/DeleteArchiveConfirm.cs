using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 删除存档确认面板
/// Inspector 配置：confirmPanel、titleText、messageText、confirmButton、cancelButton
/// </summary>
public class DeleteArchiveConfirm : MonoBehaviour
{
    [Header("UI 引用")]
    public GameObject confirmPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button confirmButton;
    public Button cancelButton;

    [Header("文本内容")]
    public string panelTitle = "删除存档";
    [TextArea(2, 4)]
    public string panelMessage = "确定要删除所有存档装备、通关记录和积分吗？\n此操作不可撤销。";
    public string confirmLabel = "确认删除";
    public string cancelLabel = "取消";

    private ArchiveManager _archiveManager;

    private void Awake()
    {
        // 确保面板初始隐藏
        if (confirmPanel != null) confirmPanel.SetActive(false);

        // 在 Awake 注册监听，避免 savescene 首次激活时 Start 还未执行导致第一次点击无效
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmDelete);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(CloseConfirmPanel);
        }
    }

    public void SetArchiveManager(ArchiveManager manager)
    {
        _archiveManager = manager;
    }

    public void OpenConfirmPanel()
    {
        if (confirmPanel == null) return;

        if (titleText != null)   titleText.text   = panelTitle;
        if (messageText != null) messageText.text = panelMessage;

        SetButtonLabel(confirmButton, confirmLabel);
        SetButtonLabel(cancelButton,  cancelLabel);

        confirmPanel.SetActive(true);
    }

    private void CloseConfirmPanel()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    private void OnConfirmDelete()
    {
        EquipmentSystem.Instance?.ResetAllEquipments();
        ClearRecordManager.Instance?.DeleteAllRecords();
        FavorManager.Instance?.DeleteAllFavor();
        // 抽卡数据需要强制清空，不能依赖 GachaManager 单例是否已加载
        GachaManager.ClearAllSavedData();
        // 若单例已存在，额外重置内存奖池状态，避免本场景残留运行时数据
        GachaManager.Instance?.ResetAll();

        if (_archiveManager != null)
        {
            _archiveManager.UpdateAllEquipmentIcons();
            _archiveManager.ClearAllDisplay();
        }

        CloseConfirmPanel();
        ToastManager.Show("存档已删除");
        Debug.Log("[DeleteArchive] 存档删除完成");
    }

    private void SetButtonLabel(Button btn, string label)
    {
        if (btn == null) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = label;
    }

    [ContextMenu("测试：打开确认面板")]
    public void TestOpen() => OpenConfirmPanel();
}
