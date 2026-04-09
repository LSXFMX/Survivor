using UnityEngine;
using UnityEngine.UI;

public class cao : MonoBehaviour
{
    [SerializeField] private string url = "https://space.bilibili.com/123963561";
    private const string KEY_GRASS_FIRST_REWARD = "TitleGrassRewarded";

    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OpenURL);
    }

    public void OpenURL()
    {
        // 主页面草：首次点击赠送100源（抽奖代币）
        if (PlayerPrefs.GetInt(KEY_GRASS_FIRST_REWARD, 0) == 0)
        {
            GachaManager.Instance?.AddYuan(100);
            PlayerPrefs.SetInt(KEY_GRASS_FIRST_REWARD, 1);
            PlayerPrefs.Save();
            ToastManager.Show("首次发现彩蛋：获得100源！");
        }

        if (EquipmentSystem.Instance != null)
        {
            bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 1);
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 1);
            if (!alreadyUnlocked)
                ToastManager.Show("成就装备1：已解锁！");
        }

        if (!string.IsNullOrEmpty(url))
            Application.OpenURL(url);
    }

    public void SetURL(string newURL)
    {
        url = newURL;
    }
}
