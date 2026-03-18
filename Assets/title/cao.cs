using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class cao : MonoBehaviour
{
    [SerializeField] private string url = "https://space.bilibili.com/123963561";

    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OpenURL);
        }
    }

    public void OpenURL()
    {
        EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 1);
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
    }

    // 如果需要公开方法给其他脚本调用
    public void SetURL(string newURL)
    {
        url = newURL;
    }
}

