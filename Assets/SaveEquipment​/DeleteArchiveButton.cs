using UnityEngine;
using UnityEngine.UI;

public class DeleteArchiveButton : MonoBehaviour
{
    [Header("引用")]
    public DeleteArchiveConfirm deleteConfirmPanel;  // 确认面板脚本
    public Button deleteButton;                      // 删除按钮

    [Header("按钮设置")]
    public string buttonText = "删除存档";
    public Color buttonTextColor = Color.red;

    private void Start()
    {
        InitializeButton();
    }

    private void InitializeButton()
    {
        if (deleteButton == null)
        {
            deleteButton = GetComponent<Button>();
        }

        if (deleteButton != null)
        {
            // 设置按钮文本
            Text buttonTextComponent = deleteButton.GetComponentInChildren<Text>();
            if (buttonTextComponent != null)
            {
                buttonTextComponent.text = buttonText;
                buttonTextComponent.color = buttonTextColor;
            }

            // 添加点击事件
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClick);
        }
    }

    private void OnDeleteButtonClick()
    {
        if (deleteConfirmPanel != null)
        {
            // 打开确认面板
            deleteConfirmPanel.OpenConfirmPanel();
        }
        else
        {
            Debug.LogError("DeleteArchiveConfirm未设置");
        }
    }

    // 如果不想用确认面板，可以直接调用安全删除
    public void DeleteArchiveDirectly()
    {
        if (deleteConfirmPanel != null)
        {
            deleteConfirmPanel.SafeDeleteArchive();
        }
    }
}