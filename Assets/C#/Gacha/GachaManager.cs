using System.Collections.Generic;
using UnityEngine;

public class GachaManager : MonoBehaviour
{
    public static GachaManager Instance { get; private set; }

    private const string KEY_YUAN      = "GachaYuan";
    private const string KEY_DRAWCOUNT = "GachaTotalDraws"; // 累计抽卡次数

    [Header("R 装备（可叠加）")]
    public List<GachaItemData> rItems = new List<GachaItemData>();

    [Header("SR 装备（可叠加）")]
    public List<GachaItemData> srItems = new List<GachaItemData>();

    [Header("SSR 装备（解锁型）")]
    public List<GachaItemData> ssrItems = new List<GachaItemData>();

    [Header("UR 装备（解锁型）")]
    public List<GachaItemData> urItems = new List<GachaItemData>();

    // 合并所有当前可用奖池（满足解锁条件的）
    private List<GachaItemData> AllItems
    {
        get
        {
            int draws = PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0);
            var all = new List<GachaItemData>();
            foreach (var item in rItems)
                if (item.unlockThreshold <= 0 || draws >= item.unlockThreshold) all.Add(item);
            foreach (var item in srItems)
                if (item.unlockThreshold <= 0 || draws >= item.unlockThreshold) all.Add(item);
            foreach (var item in ssrItems)
                if (item.unlockThreshold <= 0 || draws >= item.unlockThreshold) all.Add(item);
            foreach (var item in urItems)
                if (item.unlockThreshold <= 0 || draws >= item.unlockThreshold) all.Add(item);
            return all;
        }
    }

    // 所有装备（含未解锁的，用于 ResetAll）
    private List<GachaItemData> AllItemsIncludingLocked
    {
        get
        {
            var all = new List<GachaItemData>();
            all.AddRange(rItems); all.AddRange(srItems);
            all.AddRange(ssrItems); all.AddRange(urItems);
            return all;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 默认数据（Inspector 未配置时）
        if (rItems.Count == 0)
            rItems.Add(new GachaItemData { itemName = "remake", rarity = GachaRarity.R, rarityId = 0, poolCount = 100 });
        if (srItems.Count == 0)
        {
            // SR_0 经验灵果：初始加入
            srItems.Add(new GachaItemData { itemName = "经验灵果", rarity = GachaRarity.SR, rarityId = 0, poolCount = 20 });
            // SR_1 攻击灵果：初始加入50个
            srItems.Add(new GachaItemData { itemName = "攻击灵果", rarity = GachaRarity.SR, rarityId = 1, poolCount = 50 });
        }

        InitPool();
        UnlockSRByDrawCount(); // 根据累计抽卡次数解锁新SR
    }

    private void InitPool()
    {
        foreach (var item in AllItems)
            if (!PlayerPrefs.HasKey(item.PoolKey))
            {
                PlayerPrefs.SetInt(item.PoolKey, item.poolCount);
                PlayerPrefs.Save();
            }
    }

    // ── 【源】 ────────────────────────────────────────────
    public int GetYuan() => PlayerPrefs.GetInt(KEY_YUAN, 0);

    public void AddYuan(int amount)
    {
        PlayerPrefs.SetInt(KEY_YUAN, GetYuan() + amount);
        PlayerPrefs.Save();
    }

    private bool SpendYuan(int amount)
    {
        int v = GetYuan();
        if (v < amount) return false;
        PlayerPrefs.SetInt(KEY_YUAN, v - amount);
        PlayerPrefs.Save();
        return true;
    }

    // ── 奖池 ──────────────────────────────────────────────
    public int GetPoolRemain(GachaItemData item) => PlayerPrefs.GetInt(item.PoolKey, 0);

    public int GetTotalPoolRemain()
    {
        int total = 0;
        foreach (var item in AllItems) total += GetPoolRemain(item);
        return total;
    }

    public int GetRarityRemain(GachaRarity rarity)
    {
        int total = 0;
        foreach (var item in AllItems)
            if (item.rarity == rarity) total += GetPoolRemain(item);
        return total;
    }

    // ── 抽奖 ──────────────────────────────────────────────
    public GachaItemData DrawOne()
    {
        if (!SpendYuan(1)) return null;

        var available = new List<GachaItemData>();
        foreach (var item in AllItems)
            if (GetPoolRemain(item) > 0) available.Add(item);

        if (available.Count == 0) { AddYuan(1); return null; }

        var result = available[Random.Range(0, available.Count)];
        GrantItem(result);
        return result;
    }

    public List<GachaItemData> DrawTen()
    {
        var results = new List<GachaItemData>();
        for (int i = 0; i < 10; i++)
        {
            var item = DrawOne();
            if (item != null) results.Add(item);
            else break;
        }
        return results;
    }

    private void GrantItem(GachaItemData item)
    {
        if (item.rarity == GachaRarity.R || item.rarity == GachaRarity.SR)
        {
            int count = PlayerPrefs.GetInt(item.CountKey, 0) + 1;
            PlayerPrefs.SetInt(item.CountKey, count);
        }
        else
        {
            // SSR/UR：用 equipmentSystemId 写入 EquipmentSystem
            EquipmentSystem.Instance?.UnlockEquipment(EquipmentType.GachaEquipment, item.equipmentSystemId);
        }

        int remain = Mathf.Max(0, PlayerPrefs.GetInt(item.PoolKey, 0) - 1);
        PlayerPrefs.SetInt(item.PoolKey, remain);

        // 累计抽卡次数
        int draws = PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0) + 1;
        PlayerPrefs.SetInt(KEY_DRAWCOUNT, draws);
        PlayerPrefs.Save();

        UnlockSRByDrawCount();
    }

    /// <summary>根据累计抽卡次数解锁新SR进入奖池</summary>
    private void UnlockSRByDrawCount()
    {
        int draws = PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0);

        // SR_2 防御灵果：抽卡>50次后加入20个
        TryAddSR(draws, 50, new GachaItemData
            { itemName = "防御灵果", rarity = GachaRarity.SR, rarityId = 2, poolCount = 20 });

        // SR_3 生命灵果：抽卡>50次后加入100个
        TryAddSR(draws, 50, new GachaItemData
            { itemName = "生命灵果", rarity = GachaRarity.SR, rarityId = 3, poolCount = 100 });

        // SR_4 暴击灵果：抽卡>100次后加入100个
        TryAddSR(draws, 100, new GachaItemData
            { itemName = "暴击灵果", rarity = GachaRarity.SR, rarityId = 4, poolCount = 100 });

        // SR_5 暴伤灵果：抽卡>100次后加入300个
        TryAddSR(draws, 100, new GachaItemData
            { itemName = "暴伤灵果", rarity = GachaRarity.SR, rarityId = 5, poolCount = 300 });
    }

    private void TryAddSR(int draws, int threshold, GachaItemData template)
    {
        if (draws <= threshold) return;
        // 检查是否已在列表里
        foreach (var item in srItems)
            if (item.rarityId == template.rarityId) return;
        // 加入列表并初始化奖池（只在首次加入时写入 PlayerPrefs）
        srItems.Add(template);
        if (!PlayerPrefs.HasKey(template.PoolKey))
        {
            PlayerPrefs.SetInt(template.PoolKey, template.poolCount);
            PlayerPrefs.Save();
        }
        Debug.Log($"[抽奖] 新SR解锁：{template.itemName}（累计{draws}抽）");
    }

    public int GetTotalDrawCount() => PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0);

    public int GetItemCount(GachaRarity rarity, int rarityId)
        => PlayerPrefs.GetInt($"GachaCount_{rarity}_{rarityId}", 0);

    public void GrantYuanFromClear(string difficultyLabel)
    {
        if (difficultyLabel.StartsWith("N") && int.TryParse(difficultyLabel.Substring(1), out int n))
        {
            AddYuan(n);
            Debug.Log($"[抽奖] 通关 {difficultyLabel} 获得 {n} 源");
        }
    }

    public void ResetAll()
    {
        ClearAllSavedData();
        InitPool();
    }

    /// <summary>
    /// 清除所有抽卡相关存档（不依赖 GachaManager 实例，供删档场景直接调用）。
    /// </summary>
    public static void ClearAllSavedData()
    {
        PlayerPrefs.DeleteKey(KEY_YUAN);
        PlayerPrefs.DeleteKey(KEY_DRAWCOUNT);

        foreach (GachaRarity rarity in System.Enum.GetValues(typeof(GachaRarity)))
        {
            // 预留较大范围，确保所有 rarityId 的奖池和持有计数都被清空
            for (int rarityId = 0; rarityId < 64; rarityId++)
            {
                PlayerPrefs.DeleteKey($"GachaPool_{rarity}_{rarityId}");
                PlayerPrefs.DeleteKey($"GachaCount_{rarity}_{rarityId}");
            }
        }

        PlayerPrefs.Save();
        Debug.Log("[抽卡] 所有抽卡存档已清除");
    }

    [ContextMenu("测试：添加10源")] void Test_Add10() => AddYuan(10);
    [ContextMenu("测试：源数量+100")] void Test_Add100() => AddYuan(100);
    [ContextMenu("测试：奖池全部重置")] void Test_ResetPool() => ResetAll();
    [ContextMenu("测试：打印源数量")] void Test_Print() => Debug.Log($"源：{GetYuan()}，奖池：{GetTotalPoolRemain()}");
}
