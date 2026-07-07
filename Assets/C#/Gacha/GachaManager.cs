using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抽奖管理器 —— 全静态配置版。
///
/// ── 静态化说明 ────────────────────────────────────────────────────────────────
/// 历史上本脚本里写了大量「Inspector 列表为空时 new GachaItemData() 并 Add」的
/// 兜底代码、DefaultSsrTemplates 静态模板、EnsureDefaultSsrItems / EnsureUrItem
/// 自愈补齐、UnlockSRByDrawCount 运行时把新 SR 注入 srItems。这套逻辑在策划表
/// 频繁变更的早期阶段提供了「场景没及时拖最新数据也能跑」的容错，但代价是：
///   - 同一份策划数据在「场景 Inspector」「代码默认值」「Ensure 自愈」三处各存一份；
///   - 字段错乱（如 ssrItems 的 equipmentSystemId 与代码模板长期不一致）很难排查；
///   - 抽卡概率/奖池/UI 显示完全依赖运行时分支，无法在编辑器里所见即所得。
///
/// 现已把所有装备数据完整迁入场景 GachaManager 节点（rItems / srItems / ssrItems /
/// urItems 共 21 条），并对齐了 equipmentSystemId 与 unlockThreshold。本类不再
/// 写入任何 List；所有运行时动态生成已删除，仅保留：
///   - 抽奖随机（必要的运行时行为）
///   - PlayerPrefs 奖池存档读写
///   - 软保底状态机
///   - 通关源/首通宝箱
/// </summary>
public class GachaManager : MonoBehaviour
{
    public static GachaManager Instance { get; private set; }

    private const string KEY_YUAN      = "GachaYuan";
    private const string KEY_DRAWCOUNT = "GachaTotalDraws"; // 累计抽卡次数
    private const string KEY_FIRST_CLEAR_CHEST_PREFIX = "FirstClearChestClaimed_";

    // 「软保底」计数器：自上次抽到 SSR/UR 以来的累计未中次数。
    // 抽到 SSR 时只重置 SSR 计数器，抽到 UR 时只重置 UR 计数器（两者独立累积，互不污染）。
    private const string KEY_PITY_SSR  = "GachaPity_SSR";
    private const string KEY_PITY_UR   = "GachaPity_UR";

    // ── 稀有度抽卡概率（基础 + 每次未中线性增长，抽到后清零）─────────────
    // 历史 bug：DrawOne 旧实现是「在所有可抽道具实例上均匀随机」，由于初始只有 1 个 SSR + 2 个 UR 进池，
    // UR 期望命中率竟高达 2/7 ≈ 28.6%（与策划期望的「UR 1% 起步」完全不符）。
    // 修复：改为两段式 — 先按稀有度概率决定档位、再在档位内按各道具的剩余池量加权随机。
    // 同步引入软保底：未抽到时概率逐抽提高，抽到后清零。

    private const float BASE_R_RATE    = 0.70f;  // R 基础概率
    private const float BASE_SR_RATE   = 0.24f;  // SR 基础概率
    private const float BASE_SSR_RATE  = 0.05f;  // SSR 基础概率
    private const float BASE_UR_RATE   = 0.01f;  // UR 基础概率（合计 1.00）

    // 每次未抽到时，对应稀有度概率的线性增量（百分点）
    private const float SSR_PITY_STEP  = 0.005f; // 未中 SSR：每抽 +0.5%
    private const float UR_PITY_STEP   = 0.003f; // 未中 UR：每抽 +0.3%

    // 软保底上限（避免概率溢出 100%）
    private const float SSR_RATE_CAP   = 0.50f;  // SSR 概率上限 50%
    private const float UR_RATE_CAP    = 0.30f;  // UR 概率上限 30%

    // ── 抽卡奖池（全部由场景 Inspector 静态配置；运行时不再修改这些列表）──
    // 场景中已配置的内容（如有新增/调整，请直接改场景，不要在代码里加 Add）：
    //   rItems   : 3 条 — Remake(0) / 量子源木(1) / 读档币(2; >=200 抽进池, 每 20 抽补 1)
    //   srItems  : 7 条 — 经验灵果 / 攻击灵果 / 防御灵果 / 生命灵果 / 暴击灵果 / 暴伤灵果 / 速度灵果(6)
    //                    后续条用 unlockThreshold (50/50/100/100/300) 控制累计抽卡门槛进池
    //   ssrItems : 10 条 — 便携营地~不忘初心(0~7) + 我与我与我(8) + 三清化一(9)
    //                    equipmentSystemId 与 EquipmentSystem 中真实 id 严格对齐：
    //                    0,1,2,3,6,7,8,9,11,12（4/5 已被 UR 风之形/地狱火占用，10 被 UR 亡者领域占用）
    //   urItems  : 3 条 — 风之形(eqId=4) / 地狱火(eqId=5) / 亡者领域(eqId=10)
    // 注：R_2 读档币 / SR_6 速度灵果若场景里未配置，会在 Awake → EnsureNewGachaItemsExist 时
    // 由代码兜底补出来；一旦场景 Inspector 把它们手动配上，本兜底因 RarityIdExists 命中会跳过。
    [Header("R 装备（可叠加） — 仅 Inspector 配置，运行时只读")]
    public List<GachaItemData> rItems = new List<GachaItemData>();

    [Header("SR 装备（可叠加） — 仅 Inspector 配置，运行时只读")]
    public List<GachaItemData> srItems = new List<GachaItemData>();

    [Header("SSR 装备（解锁型） — 仅 Inspector 配置，运行时只读")]
    public List<GachaItemData> ssrItems = new List<GachaItemData>();

    [Header("UR 装备（解锁型） — 仅 Inspector 配置，运行时只读")]
    public List<GachaItemData> urItems = new List<GachaItemData>();

    // ── 新增 R/SR 抽卡装备的静态图标（直接在 GachaManager 节点 Inspector 里拖 PNG）──
    // 历史问题：之前 EquipmentIcon.ApplyForcedGachaRSrOverrides 通过
    //          File.ReadAllBytes + Texture2D.LoadImage 在运行时从 Application.dataPath
    //          读 R/002.png / SR/6.png 来覆盖图标。这种"绕过 Unity 资源管线"的做法在打包后
    //          dataPath 不再指向项目 Assets，且不同图片导入参数无法保证一致，导致图标
    //          错误（截图显示读档币位置出现的是金色翅膀）。
    //
    // 现在改为标准的"Inspector 引用 Sprite"挂载：把 R/002.png 拖到 reviveCoinIcon、
    // SR/6.png 拖到 speedFruitIcon 即可。EquipmentIcon 会优先从这里取 Sprite，
    // 找不到再 fallback 到旧的文件读取路径，向后兼容。
    [Header("R_2 读档币 / SR_6 速度灵果 静态图标（请把 PNG 拖到这里）")]
    [Tooltip("R_2 读档币图标。建议拖 Assets/像素幸存者资源包/存档装备图标/抽卡装备/R/002.png")]
    public Sprite reviveCoinIcon;
    [Tooltip("SR_6 速度灵果图标。建议拖 Assets/像素幸存者资源包/存档装备图标/抽卡装备/SR/6.png")]
    public Sprite speedFruitIcon;

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
            {
                if (!IsUrItemUnlocked(item, draws)) continue;
                all.Add(item);
            }
            return all;
        }
    }

    /// <summary>
    /// UR 装备的解锁判定：除了抽卡次数门槛，对特定 UR（亡者领域 equipmentSystemId=10）还要求通关 N6。
    /// </summary>
    private bool IsUrItemUnlocked(GachaItemData item, int draws)
    {
        if (item == null) return false;
        // 亡者领域：必须通关 N6 后才加入卡池
        if (item.equipmentSystemId == 10)
        {
            if (ClearRecordManager.Instance == null) return false;
            if (ClearRecordManager.Instance.GetClearCount("N6") <= 0) return false;
        }
        return item.unlockThreshold <= 0 || draws >= item.unlockThreshold;
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

        // 静态化：完全不再在代码里塞默认数据；只校验 Inspector 是否漏配。
        ValidateInspectorConfig();

        // ── 例外：少量"新增抽卡装备"的代码补缺 ──────────────────────────
        // 整体仍然遵循"全静态化、配置在场景"原则；但当新增装备是首发上线、
        // 还来不及让所有协作者把场景同步过来时，允许在代码里"按 (rarity, rarityId) 不存在则补一条"。
        // 这与之前 EquipmentIcon.ApplyForcedGachaSsrOverrides / ArchiveManager.EnsureGachaSsrIconsExist
        // 是同一思路：场景文件里没拖也能跑通，新拉项目/老存档都不爆。
        // 一旦后续把数据落到场景 Inspector，本方法因为「Find 命中=true」会自动跳过，零副作用。
        EnsureNewGachaItemsExist();

        // 必须先 InitPool：须包含「未解锁」的 SSR/UR（unlockThreshold>0），否则解锁后 PoolKey 从未写入，
        // GetPoolRemain 缺省为 0，该道具永远不会进入可抽列表。
        InitPool();
        ApplyPoolRefillsByMilestone(PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0)); // 读档时同步里程碑补池
    }

    /// <summary>
    /// 按需补出新增的抽卡装备数据。
    /// 完全幂等：已存在则直接返回。
    ///
    /// R_2 读档币：unlockThreshold=200, poolCount=100, 每 20 抽补 1 张。
    /// SR_6 速度灵果：unlockThreshold=300, poolCount=100, 不再里程碑补池。
    /// SSR_10 饮血剑(eqId=13)：unlockThreshold=500, poolCount=100。
    /// </summary>
    private void EnsureNewGachaItemsExist()
    {
        if (rItems != null && !RarityIdExists(rItems, 2))
        {
            rItems.Add(new GachaItemData
            {
                itemName = "读档币",
                rarity = GachaRarity.R,
                rarityId = 2,
                equipmentSystemId = 0, // R 不使用 equipmentSystemId
                poolCount = 100,
                unlockThreshold = 200,
                poolRefillEveryDraws = 20, // 之后每累计 20 抽追加 1 张
                poolRefillAmount = 1,
                icon = null,               // 图标由 EquipmentIcon.ApplyForcedGachaRSrOverrides 注入
            });
            Debug.Log("[GachaManager] 已补出 R_2 读档币（场景未配，代码兜底）");
        }

        if (srItems != null && !RarityIdExists(srItems, 6))
        {
            srItems.Add(new GachaItemData
            {
                itemName = "速度灵果",
                rarity = GachaRarity.SR,
                rarityId = 6,
                equipmentSystemId = 0,
                poolCount = 100,
                unlockThreshold = 300,
                poolRefillEveryDraws = 0,
                poolRefillAmount = 0,
                icon = null,
            });
            Debug.Log("[GachaManager] 已补出 SR_6 速度灵果（场景未配，代码兜底）");
        }

        // SSR_10 饮血剑 (equipmentSystemId=13)：累计抽卡 500 次后加入卡池
        if (ssrItems != null && !RarityIdExists(ssrItems, 10))
        {
            ssrItems.Add(new GachaItemData
            {
                itemName = "饮血剑",
                rarity = GachaRarity.SSR,
                rarityId = 10,
                equipmentSystemId = 13,
                poolCount = 100,
                unlockThreshold = 500,
                poolRefillEveryDraws = 0,
                poolRefillAmount = 0,
                icon = null, // 由 EnsureSsrItemIcons 运行时注入
            });
            Debug.Log("[GachaManager] 已补出 SSR_10 饮血剑（场景未配，代码兜底）");
        }

        // SSR_11 气运之子 (equipmentSystemId=14)：累计抽卡 550 次后加入卡池
        if (ssrItems != null && !RarityIdExists(ssrItems, 11))
        {
            ssrItems.Add(new GachaItemData
            {
                itemName = "气运之子",
                rarity = GachaRarity.SSR,
                rarityId = 11,
                equipmentSystemId = 14,
                poolCount = 100,
                unlockThreshold = 550,
                poolRefillEveryDraws = 0,
                poolRefillAmount = 0,
                icon = null, // 由 EnsureSsrItemIcons 运行时注入
            });
            Debug.Log("[GachaManager] 已补出 SSR_11 气运之子（场景未配，代码兜底）");
        }

        // 确保所有 SSR/UR 条目的 icon 字段不为空（场景 Inspector 未拖入时运行时补全）
        EnsureSsrItemIcons();
    }

    /// <summary>
    /// 运行时检查 ssrItems 中 icon 为 null 的条目，按 equipmentSystemId 动态加载图标。
    /// 打包后走 Inspector 引用最可靠（本方法仅在编辑器和场景未完整配置时兜底）。
    /// </summary>
    private void EnsureSsrItemIcons()
    {
        if (ssrItems == null) return;
        foreach (var item in ssrItems)
        {
            if (item == null || item.icon != null) continue;
            string path = GetSsrIconPath(item.equipmentSystemId);
            if (string.IsNullOrEmpty(path)) continue;
            var tex = RuntimeAssetLoader.LoadTexture(null, null, path);
            if (tex != null)
                item.icon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    /// <summary>根据 equipmentSystemId 返回 SSR 图标的 Assets 相对路径。</summary>
    private static string GetSsrIconPath(int eqId)
    {
        // equipmentSystemId → 文件名映射（与 EquipmentIcon.ApplyForcedGachaSsrOverrides 保持一致）
        switch (eqId)
        {
            case 11: return "像素幸存者资源包/存档装备图标/抽卡装备/SSR/008.png";
            case 12: return "像素幸存者资源包/存档装备图标/抽卡装备/SSR/009.png";
            case 13: return "像素幸存者资源包/存档装备图标/抽卡装备/SSR/010.png";
            case 14: return "像素幸存者资源包/存档装备图标/抽卡装备/SSR/011.png";
            default: return null; // 其它 SSR 由场景 Inspector 已拖好
        }
    }

    private static bool RarityIdExists(List<GachaItemData> list, int rarityId)
    {
        if (list == null) return false;
        foreach (var it in list)
        {
            if (it != null && it.rarityId == rarityId) return true;
        }
        return false;
    }

    /// <summary>
    /// 校验 Inspector 列表配置完整性。仅打印警告，不主动补齐 —— 提示开发者及时维护场景。
    /// </summary>
    private void ValidateInspectorConfig()
    {
        if (rItems   == null || rItems.Count   == 0) Debug.LogWarning("[GachaManager] rItems 列表为空，请在场景 Inspector 中配置 R 装备数据");
        if (srItems  == null || srItems.Count  == 0) Debug.LogWarning("[GachaManager] srItems 列表为空，请在场景 Inspector 中配置 SR 装备数据");
        if (ssrItems == null || ssrItems.Count == 0) Debug.LogWarning("[GachaManager] ssrItems 列表为空，请在场景 Inspector 中配置 SSR 装备数据");
        if (urItems  == null || urItems.Count  == 0) Debug.LogWarning("[GachaManager] urItems 列表为空，请在场景 Inspector 中配置 UR 装备数据");
    }

    private void InitPool()
    {
        foreach (var item in AllItemsIncludingLocked)
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
    /// <summary>
    /// 抽一次。两段式抽卡：
    ///   1) 按稀有度概率（基础 + 软保底累计增长）roll 出本次档位；
    ///   2) 在档位内按各道具「剩余池量」加权随机一个具体奖品。
    ///   3) 若 roll 出的档位当前无可抽道具，向下沉到次低档位（UR→SSR→SR→R），
    ///      避免「池空但仍消耗源」的问题。
    /// </summary>
    public GachaItemData DrawOne()
    {
        if (!SpendYuan(1)) return null;

        // 先收集每个档位的可抽道具及加权（按池量）
        var available = AllItems;
        if (available.Count == 0) { AddYuan(1); return null; }

        var byRarity = new Dictionary<GachaRarity, List<GachaItemData>>
        {
            { GachaRarity.R,   new List<GachaItemData>() },
            { GachaRarity.SR,  new List<GachaItemData>() },
            { GachaRarity.SSR, new List<GachaItemData>() },
            { GachaRarity.UR,  new List<GachaItemData>() },
        };
        bool anyRemain = false;
        foreach (var item in available)
        {
            if (GetPoolRemain(item) > 0)
            {
                byRarity[item.rarity].Add(item);
                anyRemain = true;
            }
        }
        if (!anyRemain) { AddYuan(1); return null; }

        // 当前各档实际概率（基础 + 软保底）
        float pSSR = Mathf.Min(SSR_RATE_CAP, BASE_SSR_RATE + PlayerPrefs.GetInt(KEY_PITY_SSR, 0) * SSR_PITY_STEP);
        float pUR  = Mathf.Min(UR_RATE_CAP,  BASE_UR_RATE  + PlayerPrefs.GetInt(KEY_PITY_UR,  0) * UR_PITY_STEP);
        // R / SR 按基础概率比例填充剩余（保证四档之和=1）
        float remainTop = Mathf.Max(0f, 1f - pSSR - pUR);
        float baseRSum  = BASE_R_RATE + BASE_SR_RATE;
        float pR  = remainTop * (BASE_R_RATE  / baseRSum);
        float pSR = remainTop * (BASE_SR_RATE / baseRSum);

        // Roll 出档位
        float roll = Random.value;
        GachaRarity tier;
        if      (roll < pUR)             tier = GachaRarity.UR;
        else if (roll < pUR + pSSR)      tier = GachaRarity.SSR;
        else if (roll < pUR + pSSR + pSR) tier = GachaRarity.SR;
        else                              tier = GachaRarity.R;

        // 档位下沉：所选档位无可抽道具时，依次向下兜底
        GachaRarity[] fallbackOrder = tier switch
        {
            GachaRarity.UR  => new[] { GachaRarity.UR,  GachaRarity.SSR, GachaRarity.SR, GachaRarity.R },
            GachaRarity.SSR => new[] { GachaRarity.SSR, GachaRarity.SR,  GachaRarity.R,  GachaRarity.UR },
            GachaRarity.SR  => new[] { GachaRarity.SR,  GachaRarity.R,   GachaRarity.SSR,GachaRarity.UR },
            _               => new[] { GachaRarity.R,   GachaRarity.SR,  GachaRarity.SSR,GachaRarity.UR },
        };

        List<GachaItemData> tierItems = null;
        foreach (var r in fallbackOrder)
        {
            if (byRarity[r].Count > 0) { tierItems = byRarity[r]; tier = r; break; }
        }
        if (tierItems == null) { AddYuan(1); return null; }

        // 在档位内按剩余池量加权随机
        int totalWeight = 0;
        foreach (var it in tierItems) totalWeight += GetPoolRemain(it);
        int pick = Random.Range(0, totalWeight);
        GachaItemData result = tierItems[tierItems.Count - 1];
        int acc = 0;
        foreach (var it in tierItems)
        {
            acc += GetPoolRemain(it);
            if (pick < acc) { result = it; break; }
        }

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

        // 每抽卡一次赠送 1 点存档装备积分
        int pts = PlayerPrefs.GetInt("ClearEquipmentPoints", 0) + 1;
        PlayerPrefs.SetInt("ClearEquipmentPoints", pts);

        // 软保底计数器更新：抽到 SSR 重置 SSR 计数，抽到 UR 重置 UR 计数；其它情况两条计数都 +1。
        // 这样 SSR/UR 是独立累积的——长期不出 UR 时，UR 概率会逐步逼近上限，但 SSR 不受影响（反之亦然）。
        if (item.rarity == GachaRarity.SSR)
        {
            PlayerPrefs.SetInt(KEY_PITY_SSR, 0);
            PlayerPrefs.SetInt(KEY_PITY_UR,  PlayerPrefs.GetInt(KEY_PITY_UR, 0) + 1);
        }
        else if (item.rarity == GachaRarity.UR)
        {
            PlayerPrefs.SetInt(KEY_PITY_UR,  0);
            PlayerPrefs.SetInt(KEY_PITY_SSR, PlayerPrefs.GetInt(KEY_PITY_SSR, 0) + 1);
        }
        else
        {
            PlayerPrefs.SetInt(KEY_PITY_SSR, PlayerPrefs.GetInt(KEY_PITY_SSR, 0) + 1);
            PlayerPrefs.SetInt(KEY_PITY_UR,  PlayerPrefs.GetInt(KEY_PITY_UR,  0) + 1);
        }
        PlayerPrefs.Save();

        ApplyPoolRefillsByMilestone(draws);
    }

    /// <summary>R 档等：按「每满 N 抽往奖池加 M」里程碑追加（与策划表一致）</summary>
    private void ApplyPoolRefillsByMilestone(int totalDraws)
    {
        void ApplyForList(List<GachaItemData> list)
        {
            foreach (var item in list)
            {
                if (item.poolRefillEveryDraws <= 0 || item.poolRefillAmount <= 0) continue;
                int tier = totalDraws / item.poolRefillEveryDraws;
                int applied = PlayerPrefs.GetInt(item.PoolMilestoneKey, 0);
                if (tier <= applied) continue;
                int add = (tier - applied) * item.poolRefillAmount;
                int cur = PlayerPrefs.GetInt(item.PoolKey, 0);
                PlayerPrefs.SetInt(item.PoolKey, cur + add);
                PlayerPrefs.SetInt(item.PoolMilestoneKey, tier);
                PlayerPrefs.Save();
                Debug.Log($"[抽奖] 里程碑补池：{item.itemName} +{add}（累计{totalDraws}抽，每{item.poolRefillEveryDraws}抽+{item.poolRefillAmount}）");
            }
        }

        ApplyForList(rItems);
    }

    public int GetTotalDrawCount() => PlayerPrefs.GetInt(KEY_DRAWCOUNT, 0);

    public int GetItemCount(GachaRarity rarity, int rarityId)
        => PlayerPrefs.GetInt($"GachaCount_{rarity}_{rarityId}", 0);

    /// <summary>
    /// 按 (rarity, equipmentSystemId) 查找抽卡奖品（仅 SSR/UR 用 equipmentSystemId 索引；R/SR 不用）。
    /// 主要给 ArchiveManager 显示"编号"用——把场景里 EquipmentIcon.equipmentId（=equipmentSystemId）
    /// 转换回该稀有度内的 rarityId 序号，让 UI 显示该 UR 在 UR 列表中的真正序号。
    /// </summary>
    public GachaItemData FindItemByEquipmentSystemId(GachaRarity rarity, int equipmentSystemId)
    {
        var list = rarity == GachaRarity.SSR ? ssrItems
                 : rarity == GachaRarity.UR  ? urItems
                 : null;
        if (list == null) return null;
        foreach (var item in list)
        {
            if (item.equipmentSystemId == equipmentSystemId) return item;
        }
        return null;
    }

    public void GrantYuanFromClear(string difficultyLabel)
        => GrantYuanFromClear(difficultyLabel, 1);

    public void GrantYuanFromClear(string difficultyLabel, int multiplier)
    {
        if (TryGetDifficultyNumber(difficultyLabel, out int n))
        {
            int validMultiplier = Mathf.Max(1, multiplier);
            int amount = n * validMultiplier;
            AddYuan(amount);
            Debug.Log($"[抽奖] 通关 {difficultyLabel} 获得 {amount} 源（{n} x {validMultiplier}）");
        }
    }

    public bool TryGetDifficultyNumber(string difficultyLabel, out int n)
    {
        n = 0;
        return !string.IsNullOrEmpty(difficultyLabel)
            && difficultyLabel.StartsWith("N")
            && int.TryParse(difficultyLabel.Substring(1), out n);
    }

    public int GetFirstClearChestReward(string difficultyLabel)
    {
        // 首通宝箱奖励翻倍：从 n*3 提升到 n*6（活动版本/抽卡爆率提升期）
        return TryGetDifficultyNumber(difficultyLabel, out int n) ? n * 6 : 0;
    }

    public bool IsFirstClearChestClaimed(string difficultyLabel)
    {
        return PlayerPrefs.GetInt(KEY_FIRST_CLEAR_CHEST_PREFIX + difficultyLabel, 0) == 1;
    }

    public bool CanClaimFirstClearChest(string difficultyLabel)
    {
        if (ClearRecordManager.Instance == null) return false;
        return ClearRecordManager.Instance.GetClearCount(difficultyLabel) > 0 && !IsFirstClearChestClaimed(difficultyLabel);
    }

    public int ClaimFirstClearChest(string difficultyLabel)
    {
        if (!CanClaimFirstClearChest(difficultyLabel)) return 0;
        int amount = GetFirstClearChestReward(difficultyLabel);
        if (amount <= 0) return 0;
        AddYuan(amount);
        PlayerPrefs.SetInt(KEY_FIRST_CLEAR_CHEST_PREFIX + difficultyLabel, 1);
        PlayerPrefs.Save();
        ToastManager.Show($"首通宝箱 {difficultyLabel}：获得 {amount} 源！");
        return amount;
    }

    public string GetYuanSourceDescription()
    {
        return "[源]获取方式：\n"
            + "1. 通关难度 N1~N8：分别获得 1~8 个源。\n"
            + "2. 击败世界Boss会提高通关源奖励倍率：\n"
            + "   奖励 = 难度数字 × (1 + 本局击败Boss数量)。\n"
            + "3. 首次通关某难度后，可在聚宝盆领取首通宝箱：\n"
            + "   奖励 = 难度数字 × 6。\n"
            + "4. 首次点击主页面的草：一次性获得 100 源。\n"
            + "5. 抽奖时若奖池为空，会退还本次消耗的源。\n"
            + "\n[读档币]累计抽卡 200 次后加入卡池（初始 100 个），之后每累计 20 抽补充 1 个。\n"
            + "\n[存档装备积分]每抽卡 1 次获得 1 点积分，可在存档装备界面兑换解锁装备。";
    }

    public void ResetAll()
    {
        ClearAllSavedData();
        InitPool();

        // ── 测试便利：奖池重置时，连带把"首通宝箱"领取标记清掉，
        //    并按当前已通关的难度，把通关本应获得的"源"补发一次（含首通宝箱），
        //    这样测试时不必反复通关就能拿到对应抽数。
        ResetFirstClearChestsAndGrantDueYuan();
    }

    // 首页"草"首次点击奖励 100 源的存档 key，与 Assets/title/cao.cs 中
    // KEY_GRASS_FIRST_REWARD 保持一致——重置奖池时一并清掉它，让测试者可以再次触发那 100 源。
    private const string KEY_TITLE_GRASS_REWARDED = "TitleGrassRewarded";

    /// <summary>
    /// 重置全部难度的首通宝箱领取记录、首页草的 100 源领取记录，并按当前已通关难度补发"通关源"。
    /// 规则（仅供测试/重置奖池时使用）：
    ///   - 清除 N1..N8 的 FirstClearChestClaimed 记录（玩家可重新领取首通宝箱本身，但本方法不再代领其奖励）。
    ///   - 清除首页"草"的 100 源领取标记（玩家可再次点击草触发那 100 源）。
    ///   - 对所有 GetClearCount(NX) &gt; 0 的难度，按 GrantYuanFromClear 的基础口径
    ///     （multiplier=1，无法回溯历史 BossKill 数）补发一次"通关源"。
    ///     不再补发首通宝箱源——首通宝箱由玩家自己去聚宝盆领。
    /// </summary>
    private void ResetFirstClearChestsAndGrantDueYuan()
    {
        // 1) 重置首页草的 100 源领取记录
        PlayerPrefs.DeleteKey(KEY_TITLE_GRASS_REWARDED);

        if (ClearRecordManager.Instance == null)
        {
            // 即便没有 ClearRecordManager，也至少把首通宝箱标记清掉（按 N1..N8 固定清）
            string[] fallback = { "N1", "N2", "N3", "N4", "N5", "N6", "N7", "N8" };
            foreach (var l in fallback) PlayerPrefs.DeleteKey(KEY_FIRST_CLEAR_CHEST_PREFIX + l);
            PlayerPrefs.Save();
            Debug.LogWarning("[抽奖] ClearRecordManager 实例缺失，已清首通宝箱与草标记，但无法补发通关源。");
            return;
        }

        // 难度列表与 ClearRecordManager.DeleteAllRecords 保持一致，避免遗漏新增难度
        string[] labels = { "N1", "N2", "N3", "N4", "N5", "N6", "N7", "N8" };

        int totalGranted = 0;
        foreach (var label in labels)
        {
            // 2) 清掉首通宝箱已领取标记 —— 让玩家可以重新去聚宝盆领一次首通宝箱（奖励由领取流程自身发放，本方法不代领）
            PlayerPrefs.DeleteKey(KEY_FIRST_CLEAR_CHEST_PREFIX + label);

            // 3) 若该难度曾通关过，补发对应"通关源"（不含首通宝箱）
            int clears = ClearRecordManager.Instance.GetClearCount(label);
            if (clears <= 0) continue;
            if (!TryGetDifficultyNumber(label, out int n) || n <= 0) continue;

            // 通关源：按 multiplier=1 补发（无法回溯历史 BossKill，给基础值即可）
            int clearYuan = n;
            AddYuan(clearYuan);
            totalGranted += clearYuan;
            Debug.Log($"[抽奖·重置补发] {label}：补发通关源 {clearYuan}");
        }
        PlayerPrefs.Save();

        if (totalGranted > 0)
            ToastManager.Show($"奖池已重置：补发已通关难度通关源共 {totalGranted}，首通宝箱与草奖励可再次领取");
        else
            ToastManager.Show("奖池已重置：首通宝箱与草奖励可再次领取");
    }

    /// <summary>
    /// 清除所有抽卡相关存档（不依赖 GachaManager 实例，供删档场景直接调用）。
    /// </summary>
    public static void ClearAllSavedData()
    {
        PlayerPrefs.DeleteKey(KEY_YUAN);
        PlayerPrefs.DeleteKey(KEY_DRAWCOUNT);
        PlayerPrefs.DeleteKey(KEY_PITY_SSR);
        PlayerPrefs.DeleteKey(KEY_PITY_UR);

        foreach (GachaRarity rarity in System.Enum.GetValues(typeof(GachaRarity)))
        {
            // 预留较大范围，确保所有 rarityId 的奖池和持有计数都被清空
            for (int rarityId = 0; rarityId < 64; rarityId++)
            {
                PlayerPrefs.DeleteKey($"GachaPool_{rarity}_{rarityId}");
                PlayerPrefs.DeleteKey($"GachaCount_{rarity}_{rarityId}");
                PlayerPrefs.DeleteKey($"GachaPoolMilestone_{rarity}_{rarityId}");
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
