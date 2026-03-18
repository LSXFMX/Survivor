using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;

public class DeleteArchiveConfirm : MonoBehaviour
{
    [Header("UI组件引用")]
    public GameObject confirmPanel;           // 确认面板
    public TextMeshProUGUI titleText;         // 标题文本
    public TextMeshProUGUI messageText;       // 消息文本
    public Button confirmButton;              // 确认按钮
    public Button cancelButton;               // 取消按钮

    [Header("文本设置")]
    [TextArea(1, 2)]
    public string title = "删除存档";
    [TextArea(2, 4)]
    public string message = "确定要删除所有存档装备吗？\n此操作不可撤销！";

    [Header("按钮文本")]
    public string confirmText = "是，删除";
    public string cancelText = "取消";

    [Header("颜色设置")]
    public Color confirmTextColor = Color.red;
    public Color cancelTextColor = Color.white;

    [Header("确认后回调")]
    public UnityEvent onConfirm;              // 确认删除后的回调事件

    // 引用其他管理器
    private EquipmentSystem equipmentSystem;
    private ArchiveManager archiveManager;

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        // 获取其他管理器
        equipmentSystem = EquipmentSystem.Instance;
        archiveManager = FindObjectOfType<ArchiveManager>();

        // 设置确认面板初始状态
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        // 设置文本
        if (titleText != null)
        {
            titleText.text = title;
        }

        if (messageText != null)
        {
            messageText.text = message;
        }

        // 设置确认按钮
        if (confirmButton != null)
        {
            // 设置文本
            TextMeshProUGUI confirmBtnText = confirmButton.GetComponentInChildren<TextMeshProUGUI>();
            if (confirmBtnText != null)
            {
                confirmBtnText.text = confirmText;
                confirmBtnText.color = confirmTextColor;
            }

            // 添加点击事件
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmDelete);
        }

        // 设置取消按钮
        if (cancelButton != null)
        {
            // 设置文本
            TextMeshProUGUI cancelBtnText = cancelButton.GetComponentInChildren<TextMeshProUGUI>();
            if (cancelBtnText != null)
            {
                cancelBtnText.text = cancelText;
                cancelBtnText.color = cancelTextColor;
            }

            // 添加点击事件
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelDelete);
        }
    }

    // 打开确认面板
    public void OpenConfirmPanel()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
            Debug.Log("打开删除存档确认面板");
        }
    }

    // 确认删除
    private void OnConfirmDelete()
    {
        Debug.Log("确认删除所有存档装备");

        // 删除EquipmentSystem中的存档
        if (equipmentSystem != null)
        {
            equipmentSystem.ResetAllEquipments();
            Debug.Log("已清空EquipmentSystem中的所有装备存档");
        }
        else
        {
            Debug.LogWarning("EquipmentSystem未找到");
        }

        // 更新ArchiveManager显示
        if (archiveManager != null)
        {
            archiveManager.UpdateAllEquipmentIcons();
            archiveManager.ClearAllDisplay();
            Debug.Log("已更新ArchiveManager显示");
        }

        // 触发回调事件
        onConfirm?.Invoke();

        // 关闭确认面板
        CloseConfirmPanel();

        // 显示删除成功消息
        ShowDeleteCompleteMessage();
    }

    // 取消删除
    private void OnCancelDelete()
    {
        Debug.Log("取消删除存档");
        CloseConfirmPanel();
    }

    // 关闭确认面板
    private void CloseConfirmPanel()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // 显示删除完成消息
    private void ShowDeleteCompleteMessage()
    {
        // 可以在这里显示一个短暂的消息提示
        Debug.Log("所有存档装备已成功删除！");

        // 如果你有消息提示系统，可以在这里调用
        // MessageSystem.Show("所有存档装备已删除", 2f);
    }

    // 安全删除（带双重确认）
    public void SafeDeleteArchive()
    {
        // 先检查是否有已解锁的装备
        bool hasUnlockedEquipment = CheckIfHasUnlockedEquipment();

        if (!hasUnlockedEquipment)
        {
            // 如果没有解锁的装备，直接提示
            Debug.Log("没有已解锁的装备，无需删除");
            ShowNoUnlockedEquipmentMessage();
            return;
        }

        // 有解锁的装备，打开确认面板
        OpenConfirmPanel();
    }

    // 检查是否有已解锁的装备
    private bool CheckIfHasUnlockedEquipment()
    {
        if (equipmentSystem == null) return false;

        // 获取已解锁的装备列表
        var unlockedEquipments = equipmentSystem.GetUnlockedEquipments();
        return unlockedEquipments != null && unlockedEquipments.Count > 0;
    }

    // 显示没有解锁装备的消息
    private void ShowNoUnlockedEquipmentMessage()
    {
        Debug.Log("没有已解锁的装备可以删除");

        // 如果你有消息提示系统，可以在这里调用
        // MessageSystem.Show("没有已解锁的装备", 2f);
    }

    // 手动设置EquipmentSystem引用
    public void SetEquipmentSystem(EquipmentSystem system)
    {
        equipmentSystem = system;
    }

    // 手动设置ArchiveManager引用
    public void SetArchiveManager(ArchiveManager manager)
    {
        archiveManager = manager;
    }

    // 调试方法
    [ContextMenu("测试打开确认面板")]
    public void TestOpenConfirmPanel()
    {
        OpenConfirmPanel();
    }

    [ContextMenu("测试安全删除")]
    public void TestSafeDelete()
    {
        SafeDeleteArchive();
    }
}