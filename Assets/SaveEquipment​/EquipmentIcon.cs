using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class EquipmentIcon : MonoBehaviour
{
    [Header("装备信息")]
    public EquipmentType equipmentType = EquipmentType.ClearEquipment;
    public int    equipmentId   = 0;
    public string equipmentName = "装备名称";
    [TextArea(2, 3)] public string description = "装备描述";
    [TextArea(1, 2)] public string howToGet    = "获得方法";

    [Header("颜色设置")]
    public Color unlockedColor = Color.white;
    public Color lockedColor   = Color.gray;

    [Header("组件引用")]
    [SerializeField] private Image  iconImage;
    [SerializeField] private Button button;

    [Header("叠加数量显示（R/SR抽卡装备用，可选）")]
    public TextMeshProUGUI countText;
    public GachaRarity     gachaRarity = GachaRarity.R; // 设置该图标对应的稀有度

    [Header("调试选项")]
    public bool enableDebugLogs = false;

    public Action<EquipmentType, int, EquipmentIcon> onClickCallback;

    private bool            isInitialized  = false;
    private EquipmentSystem equipmentSystem;

    private void Start()    => Initialize();
    private void OnEnable() => UpdateDisplay();

    private void Initialize()
    {
        if (isInitialized) return;

        if (button == null) button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        if (iconImage == null) iconImage = GetComponentInChildren<Image>();

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnButtonClicked);

        equipmentSystem = EquipmentSystem.Instance;
        UpdateDisplay();
        isInitialized = true;
    }

    private void OnButtonClicked()
    {
        if (enableDebugLogs) Debug.Log($"点击装备图标: {equipmentType}_{equipmentId}");
        onClickCallback?.Invoke(equipmentType, equipmentId, this);
    }

    public void UpdateDisplay()
    {
        if (iconImage == null) return;

        bool isUnlocked = IsUnlocked();
        iconImage.color = isUnlocked ? unlockedColor : lockedColor;

        // R/SR 抽卡装备：按稀有度单独显示叠加数量
        if (countText != null && equipmentType == EquipmentType.GachaEquipment
            && GachaManager.Instance != null
            && (gachaRarity == GachaRarity.R || gachaRarity == GachaRarity.SR))
        {
            int count = GachaManager.Instance.GetItemCount(gachaRarity, equipmentId);
            countText.text = count > 0 ? $"×{count}" : "";
            countText.gameObject.SetActive(count > 0);
        }
    }

    private bool IsUnlocked()
    {
        // R/SR 抽卡装备：有叠加数量就算解锁
        if (equipmentType == EquipmentType.GachaEquipment && GachaManager.Instance != null
            && (gachaRarity == GachaRarity.R || gachaRarity == GachaRarity.SR))
        {
            return GachaManager.Instance.GetItemCount(gachaRarity, equipmentId) > 0;
        }

        if (equipmentSystem == null) equipmentSystem = EquipmentSystem.Instance;
        if (equipmentSystem == null) equipmentSystem = FindObjectOfType<EquipmentSystem>();
        return equipmentSystem != null && equipmentSystem.IsEquipmentUnlocked(equipmentType, equipmentId);
    }

    public void SetEquipmentInfo(EquipmentType type, int id, string eName, string desc, string howToGetText)
    {
        equipmentType = type; equipmentId = id;
        equipmentName = eName; description = desc; howToGet = howToGetText;
    }

    public void SetColors(Color unlocked, Color locked)
    {
        unlockedColor = unlocked; lockedColor = locked;
        UpdateDisplay();
    }

    [ContextMenu("重新初始化")] public void Reinitialize() { isInitialized = false; Initialize(); }
    [ContextMenu("手动更新显示")] public void ManualUpdateDisplay() => UpdateDisplay();

    [ContextMenu("测试解锁装备")]
    public void TestUnlockThisEquipment()
    {
        EquipmentSystem.Instance?.UnlockEquipment(equipmentType, equipmentId);
        UpdateDisplay();
    }
}
