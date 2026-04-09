using UnityEngine;
using UnityEngine.UI;

public class DeleteArchiveButton : MonoBehaviour
{
    [Header("ÒýÓÃ")]
    public DeleteArchiveConfirm deleteConfirmPanel;
    public Button deleteButton;

    private void Awake()
    {
        if (deleteButton == null)
            deleteButton = GetComponent<Button>();

        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(OnDeleteButtonClick);
        }
    }

    private void OnDeleteButtonClick()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.OpenConfirmPanel();
        else
            Debug.LogError("[DeleteArchiveButton] DeleteArchiveConfirm Î´°ó¶¨");
    }

    public void DeleteArchiveDirectly()
    {
        if (deleteConfirmPanel != null)
            deleteConfirmPanel.OpenConfirmPanel();
    }
}
