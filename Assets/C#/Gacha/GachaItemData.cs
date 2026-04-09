using UnityEngine;

public enum GachaRarity { R, SR, SSR, UR }

/// <summary>
/// 单个抽卡奖品数据
/// rarityId：稀有度内独立编号（R从0开始，SR从0开始，各自独立）
/// equipmentSystemId：SSR/UR 在 EquipmentSystem 中的 GachaEquipment id（R/SR 不用）
/// unlockThreshold：解锁所需累计抽卡次数（0=初始就在奖池）
/// </summary>
[System.Serializable]
public class GachaItemData
{
    public string      itemName;
    public GachaRarity rarity;
    public int         rarityId;
    public int         equipmentSystemId;
    public int         poolCount;
    public int         unlockThreshold; // 0=初始加入，>0=需要达到该抽卡次数才加入
    public Sprite      icon;

    public string PoolKey  => $"GachaPool_{rarity}_{rarityId}";
    public string CountKey => $"GachaCount_{rarity}_{rarityId}";
}
