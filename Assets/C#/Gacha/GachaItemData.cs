using UnityEngine;

public enum GachaRarity { R, SR, SSR, UR }

/// <summary>
/// 单个抽卡奖品数据
/// rarityId：稀有度内独立编号（R从0开始，SR从0开始，各自独立）
/// equipmentSystemId：SSR/UR 在 EquipmentSystem 中的 GachaEquipment id（R/SR 不用）
/// unlockThreshold：解锁所需累计抽卡次数（0=初始就在奖池）
/// poolRefillEveryDraws / poolRefillAmount：累计抽卡里程碑补池（0=不启用；每满 EveryDraws 抽往该道具奖池加 Amount）
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
    /// <summary>每满多少累计抽，向奖池补一次 poolRefillAmount（与补池量均大于 0 时启用）</summary>
    public int         poolRefillEveryDraws;
    public int         poolRefillAmount;
    public Sprite      icon;

    public string PoolKey  => $"GachaPool_{rarity}_{rarityId}";
    public string CountKey => $"GachaCount_{rarity}_{rarityId}";
    public string PoolMilestoneKey => $"GachaPoolMilestone_{rarity}_{rarityId}";
}
