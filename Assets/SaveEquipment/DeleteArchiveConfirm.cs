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

        // ─────────────────────────────────────────────────────────────
        // 【关键修复·2026-06】之前只清了 EQ_* 装备解锁键，但里程碑型成就装备
        //   （沙漏 / 钥匙剑 / 扎营大师 / 孢子异变 / 万象天引）实际由
        //   EquipmentInitializer.EnsurePersistentUnlocks() 在每次进战斗时
        //   读取 TotalPlayMinutes / GateChallengeStartedOnce / CampCapturedCount /
        //   MushroomDefeatedCount 等"成就计数器"重新解锁——所以删档后只要再
        //   进一次战斗，这些成就装备又"复活"了。
        //   这里必须把所有底层计数器也一并清除，删档才算彻底。
        ClearAchievementCounters();

        // 同样清除新手指引红点状态（删档语义：玩家再次"第一次"开游戏）
        PlayerPrefs.DeleteKey("InstructionsLastSeenUnlockCount");
        PlayerPrefs.DeleteKey("InstructionsEverViewed");

        // 强制把"当前选中角色"重置为琪露诺（skinId=0），并清掉 UR 角色解锁兜底键
        // 这样删档后回到主菜单，默认外观就是琪露诺——和新玩家首次进入时一致。
        PlayerPrefs.SetInt("SelectedSkin", 0);
        PlayerPrefs.DeleteKey("SkinUnlocked_1");
        PlayerPrefs.DeleteKey("SkinUnlocked_2");
        PlayerPrefs.DeleteKey("SkinUnlocked_3");

        // 局内皮肤覆盖器若存在，也立即同步切回琪露诺（防止"删档时正在战斗里"出现皮肤未刷新）
        var overrider = FindObjectOfType<PlayerSkinOverrider>();
        if (overrider != null) overrider.skinIndex = 0;

        PlayerPrefs.Save();

        if (_archiveManager != null)
        {
            _archiveManager.UpdateAllEquipmentIcons();
            _archiveManager.ClearAllDisplay();
        }

        CloseConfirmPanel();
        ToastManager.Show("存档已删除");
        Debug.Log("[DeleteArchive] 存档删除完成（含成就计数器与角色选择重置）");
    }

    /// <summary>
    /// 清除所有"成就型"持久化计数器。这些键是 EquipmentInitializer.EnsurePersistentUnlocks
    /// 用于"自动重新解锁"成就装备/好感度装备的依据，删档时必须一并清除。
    /// 累计游戏时长（TotalPlayMinutes）也会被清除——它本身也是"沙漏"成就装备的解锁条件。
    /// </summary>
    private static void ClearAchievementCounters()
    {
        // 沙漏（成就装备 4）：累计游玩 30 分钟
        PlayerPrefs.DeleteKey("TotalPlayMinutes");
        // 钥匙剑（成就装备 3）：进行过门挑战
        PlayerPrefs.DeleteKey("GateChallengeStartedOnce");
        // 扎营大师（成就装备 5）：攻占营地 100 次
        PlayerPrefs.DeleteKey("CampCapturedCount");
        // 孢子异变（成就装备 6）：击败 500 个蘑菇敌人
        PlayerPrefs.DeleteKey("MushroomDefeatedCount");
        // 万象天引（成就装备 7）：单局达到 50 级
        PlayerPrefs.DeleteKey("BestSingleRunLevel");
        PlayerPrefs.DeleteKey("ReachedLevel50Once");
        // 孢子异变开关（运行时玩家在"成就装备 6"详情里手动开关的偏好），删档时回到默认
        PlayerPrefs.DeleteKey("SporeMutationEnabled");
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
