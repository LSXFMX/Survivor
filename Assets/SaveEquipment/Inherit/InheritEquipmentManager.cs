using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>存档用的序列化容器（JsonUtility 不支持顶层 List，必须包一层）。</summary>
[Serializable]
public class InheritSaveData
{
    /// <summary>
    /// **所有**装备实体（在穿的+ 在库的都在这里）。
    /// 单一数据源：靠 <see cref="equippedUids"/> 区分"在穿 / 在库"，
    /// 避免维护两份列表导致存档丢件。
    /// </summary>
    public List<InheritItem> items = new List<InheritItem>();

    /// <summary>六个槽位上正在穿戴的装备 uid；空串表示该槽位为空。索引 = InheritSlot。</summary>
    public List<string> equippedUids = new List<string>();

    public int  materials;
    public bool autoSalvage;
}

/// <summary>
/// 继承装备系统的运行时大脑：仓库 / 装备栏 / 分解 / 重铸 / 自动分解 / 存档 / 属性结算。
///
/// 单例 + 跨场景常驻（DontDestroyOnLoad），因为：
///   • 掉落发生在战斗场景，而穿戴与分解发生在主菜单存档界面；
///   • 属性需要在每局开局时重新加到玩家身上。
///
/// ── 数据结构决策 ──
///曾经的写法是"仓库列表 + 穿戴列表"两份，结果穿戴中的装备不在被序列化的
///   仓库列表里 → 中途 Save() 会把在穿装备整件丢掉。
///   现在改为**单一数据源**：所有实体都在 <see cref="InheritSaveData.items"/>，
///   `equippedUids` 只是六个指针。UI 需要的"仓库列表"由过滤得到。
/// </summary>
public class InheritEquipmentManager : MonoBehaviour
{
    public static InheritEquipmentManager Instance { get; private set; }

    private const string SAVE_KEY = "InheritEquipSave_v1";

    /// <summary>仓库容量上限（不含在穿的6 件）。超出后新装备会被强制分解。</summary>
    public const int WAREHOUSE_CAP = 120;

    private InheritSaveData _data = new InheritSaveData();

    /// <summary>重铸材料（策划案称"材料"）。</summary>
    public int Materials => _data.materials;

    /// <summary>自动分解开关（策划案第10 条，可手动开关）。</summary>
    public bool AutoSalvage
    {
        get => _data.autoSalvage;
        set { _data.autoSalvage = value; Save(); OnChanged?.Invoke(); }
    }

    /// <summary>仓库 / 装备栏 / 材料发生任何变化时触发，UI 据此刷新。</summary>
    public event Action OnChanged;
    /// <summary>获得新装备（已入库）时触发。掉落展示用。</summary>
    public event Action<InheritItem> OnItemAcquired;

    // 复用容器，避免每次 UI 刷新都new List
    private readonly List<InheritItem> _warehouseCache = new List<InheritItem>();

    // ─────────────────────────── 生命周期 ───────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    /// <summary>保证单例存在。战斗场景与主菜单都可能先访问，谁先谁创建。</summary>
    public static InheritEquipmentManager Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("InheritEquipmentManager");
        return go.AddComponent<InheritEquipmentManager>();
    }

    // ─────────────────────────── 存档 ───────────────────────────

    /// <summary>
    /// 重新从PlayerPrefs 读取存档并广播变更。
    /// 【存档槽切换】<see cref="SaveSlotManager"/> 换槽后 PlayerPrefs 里的
    /// InheritEquipSave_v1 已经换成另一个档，但 _data 还是旧档的仓库内容 —— 必须重读。
    /// </summary>
    public void ReloadFromPrefs()
    {
        Load();
        OnChanged?.Invoke();
    }

    private void Load()
    {
        _data = new InheritSaveData();
        EnsureSlotList();
        string json = PlayerPrefs.GetString(SAVE_KEY, "");
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var loaded = JsonUtility.FromJson<InheritSaveData>(json);
            if (loaded != null)
            {
                _data = loaded;
                _data.items        ??= new List<InheritItem>();
                _data.equippedUids ??= new List<string>();
                EnsureSlotList();
                PurgeInvalid();
            }
        }
        catch (Exception ex)
        {
            // 存档损坏不能让游戏起不来：重置为空并保留日志
            Debug.LogWarning($"[Inherit] 存档解析失败，已重置：{ex.Message}");
            _data = new InheritSaveData();
            EnsureSlotList();
        }
    }

    private void EnsureSlotList()
    {
        _data.equippedUids ??= new List<string>();
        while (_data.equippedUids.Count < InheritEquipmentDefs.SLOT_COUNT)
            _data.equippedUids.Add("");
        // 多出来的截掉（防止旧版存档槽位数不一致）
        while (_data.equippedUids.Count > InheritEquipmentDefs.SLOT_COUNT)
            _data.equippedUids.RemoveAt(_data.equippedUids.Count - 1);
    }

    /// <summary>清理脏数据：null 实体、指向不存在实体的槽位、槽位与装备类型不匹配。</summary>
    private void PurgeInvalid()
    {
        _data.items.RemoveAll(i => i == null || string.IsNullOrEmpty(i.uid));

        // 注：副词条允许重复（只有五种属性但可重复 roll，如两条暴击率），
        // 因此这里不做去重清洗。

        for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
        {
            string uid = _data.equippedUids[s];
            if (string.IsNullOrEmpty(uid)) continue;

            var it = _data.items.Find(i => i.uid == uid);
            // 找不到实体，或实体槽位与该格不符→ 清空该槽位
            if (it == null || (int)it.slot != s) _data.equippedUids[s] = "";
        }
    }

    public void Save()
    {
        try
        {
            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Inherit] 存档写入失败：{ex.Message}");
        }
    }

    // ─────────────────────────── 查询 ───────────────────────────

    /// <summary>取某槽位正在穿戴的装备；无则返回 null。</summary>
    public InheritItem GetEquipped(InheritSlot slot)
    {
        EnsureSlotList();
        string uid = _data.equippedUids[(int)slot];
        if (string.IsNullOrEmpty(uid)) return null;
        return _data.items.Find(i => i != null && i.uid == uid);
    }

    /// <summary>该 uid 是否正在被穿戴。</summary>
    public bool IsEquipped(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return false;
        EnsureSlotList();
        for (int i = 0; i < _data.equippedUids.Count; i++)
            if (_data.equippedUids[i] == uid) return true;
        return false;
    }

    /// <summary>
    /// 仓库列表（= 全部实体中未被穿戴的部分）。
    /// 返回的是内部复用列表，调用方**不要长期持有**，只在当帧遍历。
    /// </summary>
    public List<InheritItem> GetWarehouse()
    {
        _warehouseCache.Clear();
        foreach (var it in _data.items)
        {
            if (it == null) continue;
            if (IsEquipped(it.uid)) continue;
            _warehouseCache.Add(it);
        }
        return _warehouseCache;
    }

    /// <summary>当前仓库件数（不含在穿的）。</summary>
    public int WarehouseCount
    {
        get
        {
            int n = 0;
            foreach (var it in _data.items)
                if (it != null && !IsEquipped(it.uid)) n++;
            return n;
        }
    }

    // ─────────────────────────── 获得装备 ───────────────────────────

    /// <summary>
    /// 获得一件装备。先走自动分解判定（策划案第 10 条），通过则入库；
    /// 仓库满则强制分解并提示。
    /// 返回 true = 真的入库；false = 被自动分解 / 仓库满分解。
    /// </summary>
    public bool Acquire(InheritItem item)
    {
        if (item == null) return false;

        if (_data.autoSalvage && ShouldAutoSalvage(item))
        {
            int gain = InheritEquipmentGenerator.SalvageValue(item);
            _data.materials += gain;
            Save();
            OnChanged?.Invoke();
            ToastManager.Show($"<color=#999999>[自动分解] {item.DisplayName} → 材料 +{gain}</color>");
            return false;
        }

        if (WarehouseCount >= WAREHOUSE_CAP)
        {
            int gain = InheritEquipmentGenerator.SalvageValue(item);
            _data.materials += gain;
            Save();
            OnChanged?.Invoke();
            ToastManager.Show($"<color=#FF8080>仓库已满（{WAREHOUSE_CAP}），自动分解 → 材料 +{gain}</color>");
            return false;
        }

        _data.items.Add(item);
        Save();
        OnItemAcquired?.Invoke(item);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 自动分解判定（策划案第 10 条）。
    ///   • 该槽位没有在穿装备 → 不分解（先让玩家有东西穿）
    ///   • 稀有度更低→ 分解
    ///   • 稀有度相同但主词条更低 → 分解
    ///   • 稀有度与主词条**完全相同 → 不分解**（策划案明确"属性相同不分解"）
    ///   • 更强 → 不分解
    /// </summary>
    public bool ShouldAutoSalvage(InheritItem incoming)
    {
        if (incoming == null) return false;
        var cur = GetEquipped(incoming.slot);
        if (cur == null) return false;

        if (incoming.rarity < cur.rarity) return true;
        if (incoming.rarity > cur.rarity) return false;

        // 同稀有度：比主词条。用 epsilon 判"相同"，避免浮点误差误伤
        const float EPS = 0.001f;
        if (Mathf.Abs(incoming.mainValue - cur.mainValue) <= EPS) return false;
        return incoming.mainValue < cur.mainValue;
    }

    // ─────────────────────────── 穿戴 / 卸下 ───────────────────────────

    /// <summary>穿戴一件装备。原本穿着的自动退回仓库（同一份列表，只改指针）。</summary>
    public void Equip(InheritItem item)
    {
        if (item == null) return;
        EnsureSlotList();

        // 实体必须在 items 里（防御性：外部传进未入库的实例）
        if (!_data.items.Contains(item)) _data.items.Add(item);

        _data.equippedUids[(int)item.slot] = item.uid;
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>卸下某槽位装备（退回仓库 = 只清指针）。</summary>
    public void Unequip(InheritSlot slot)
    {
        EnsureSlotList();
        if (string.IsNullOrEmpty(_data.equippedUids[(int)slot])) return;
        _data.equippedUids[(int)slot] = "";
        Save();
        OnChanged?.Invoke();
    }

    /// <summary>
    /// 一键装备：为 6 个槽位各挑一件**最优**装备穿上。
    ///
    /// 排序规则（与 <c>IsWorseThanEquipped</c> 的比较口径一致）：
    ///   ① 稀有度高者优先（奇点 &gt; 星系 &gt; … &gt; 原子）；
    ///   ② 同稀有度比主词条数值，高者优先；
    ///   ③ 再相同则比副词条条数（多者优先），最后比掉落力量值。
    ///
    /// 为什么不按"总战力"评分：不同主词条（攻击 / 血量 / 暴击…）量纲完全不同，
    /// 没有公允的换算权重；而玩家的直觉预期就是"穿稀有度最高、主词条最大的那件"，
    /// 与需求描述一致，也便于自己看懂结果。
    ///
    /// 已穿着的装备也参与比较 —— 若它本来就是最优的，就保持不动。
    /// 只在真的有变更时 Save +触发一次 OnChanged（避免刷 6 次 UI）。
    /// </summary>
    /// <returns>实际换上的件数（0 = 本来就已是最优配置）。</returns>
    public int EquipBestAll()
    {
        EnsureSlotList();

        int changed = 0;
        for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
        {
            var slot = (InheritSlot)s;

            InheritItem best = null;
            foreach (var it in _data.items)
            {
                if (it == null || it.slot != slot) continue;
                // 只考虑"未被别的槽位占用"的：同slot 的装备不可能被别的槽位穿着，
                // 所以这里等价于"仓库里的 + 本槽位正在穿的"，无需额外过滤。
                if (best == null || IsBetter(it, best)) best = it;
            }

            if (best == null) continue;// 该槽位没有任何装备
            if (_data.equippedUids[s] == best.uid) continue;         // 已经是最优，不动

            _data.equippedUids[s] = best.uid;
            changed++;
        }

        if (changed > 0)
        {
            Save();
            OnChanged?.Invoke();
        }
        return changed;
    }

    /// <summary>a 是否比 b 更优（供<see cref="EquipBestAll"/> 排序用）。</summary>
    private static bool IsBetter(InheritItem a, InheritItem b)
    {
        if (a.rarity != b.rarity) return a.rarity > b.rarity;

        const float EPS = 0.001f;
        if (Mathf.Abs(a.mainValue - b.mainValue) > EPS) return a.mainValue > b.mainValue;

        int ca = a.subStats != null ? a.subStats.Count : 0;
        int cb = b.subStats != null ? b.subStats.Count : 0;
        if (ca != cb) return ca > cb;

        return a.dropPower > b.dropPower;
    }

    // ─────────────────────────── 分解 / 重铸 ───────────────────────────

    /// <summary>分解一件装备，返还材料。穿戴中的会先自动卸下。</summary>
    public int Salvage(InheritItem item)
    {
        if (item == null) return 0;

        // 穿戴中 → 先卸下再分解（比"禁止分解"更顺手，玩家不用先点卸下）
        if (IsEquipped(item.uid))
        {
            EnsureSlotList();
            for (int s = 0; s < _data.equippedUids.Count; s++)
                if (_data.equippedUids[s] == item.uid) _data.equippedUids[s] = "";
        }

        int gain = InheritEquipmentGenerator.SalvageValue(item);
        _data.items.Remove(item);
        _data.materials += gain;
        Save();
        OnChanged?.Invoke();
        return gain;
    }

    /// <summary>一键分解仓库内所有"低于在穿装备"的装备（不动在穿的）。</summary>
    public int SalvageAllInferior()
    {
        int total = 0;
        for (int i = _data.items.Count - 1; i >= 0; i--)
        {
            var it = _data.items[i];
            if (it == null) { _data.items.RemoveAt(i); continue; }
            if (IsEquipped(it.uid)) continue;      // 在穿的跳过
            if (!ShouldAutoSalvage(it)) continue;

            total += InheritEquipmentGenerator.SalvageValue(it);
            _data.items.RemoveAt(i);
        }
        if (total > 0)
        {
            _data.materials += total;
            Save();
            OnChanged?.Invoke();
        }
        return total;
    }

    /// <summary>
    /// 重铸某件装备的副词条。材料不足返回 false。
    /// 允许重铸穿戴中的装备（暗黑同款体验：直接强化在穿的）。
    /// </summary>
    public bool Reforge(InheritItem item)
    {
        if (item == null) return false;
        int cost = InheritEquipmentGenerator.ReforgeCost(item);
        if (_data.materials < cost) return false;

        _data.materials -= cost;
        InheritEquipmentGenerator.ApplyReforge(item);
        Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>调试 / 补偿用：直接加材料。</summary>
    public void AddMaterials(int n)
    {
        if (n == 0) return;
        _data.materials = Mathf.Max(0, _data.materials + n);
        Save();
        OnChanged?.Invoke();
    }

    // ─────────────────────────── 属性结算 ───────────────────────────

    /// <summary>
    /// 把六个槽位上所有装备的主词条 + 副词条累加到玩家身上。
    /// 由 EquipmentInitializer 在开局调用一次。
    ///
    /// 注意 <see cref="Attribute.EVA"/> 在项目里是 <c>int</c>，
    /// 而闪避词条可以带小数（策划案要求 2 位小数）。这里先用 float 累加，
    /// 最后一次性 RoundToInt 写回，避免"每件装备各自截断"造成的精度损失
    /// （例如三件 0.7 闪避各自截断成 0 会白丢 2.1 点）。
    /// </summary>
    public void ApplyToPlayer(Attribute player)
    {
        if (player == null) return;

        float addAtk = 0f, addDef = 0f, addCR = 0f, addCD = 0f, addEva = 0f;
        int   addHp  = 0;

        for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
        {
            var it = GetEquipped((InheritSlot)s);
            if (it == null) continue;

            Accum(it.mainStat, it.mainValue,
                  ref addAtk, ref addDef, ref addCR, ref addCD, ref addEva, ref addHp);

            if (it.subStats == null) continue;
            foreach (var sub in it.subStats)
            {
                if (sub == null) continue;
                Accum(sub.stat, sub.value,
                      ref addAtk, ref addDef, ref addCR, ref addCD, ref addEva, ref addHp);
            }
        }

        player.atk += addAtk;
        player.def += addDef;
        player.CR  += addCR;
        player.CD  += addCD;
        player.EVA += Mathf.RoundToInt(addEva);   // 累加后统一取整
        if (addHp > 0)
        {
            player.healthmax += addHp;
            player.health    += addHp;
        }

        if (addAtk > 0f || addHp > 0 || addDef > 0f || addCR > 0f || addCD > 0f || addEva > 0f)
        {
            Debug.Log($"[Inherit] 继承装备加成已应用：atk+{addAtk:0.##} def+{addDef:0.##} " +
                      $"hp+{addHp} CR+{addCR:0.##} CD+{addCD:0.##} EVA+{addEva:0.##}");
        }
    }

    private static void Accum(InheritStat stat, float v,
                              ref float atk, ref float def, ref float cr,
                              ref float cd, ref float eva, ref int hp)
    {
        switch (stat)
        {
            case InheritStat.Attack:   atk += v; break;
            case InheritStat.Defense:  def += v; break;
            case InheritStat.CritRate: cr  += v; break;
            case InheritStat.CritDmg:  cd  += v; break;
            case InheritStat.Evade:    eva += v; break;
            case InheritStat.Health:   hp  += Mathf.RoundToInt(v); break;
        }
    }

    /// <summary>面板用：汇总当前全套装备提供的总加成（用于"总览"显示）。</summary>
    public void GetTotals(out float atk, out float def, out int hp,
                          out float cr, out float cd, out float eva)
    {
        atk = def = cr = cd = eva = 0f; hp = 0;
        for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
        {
            var it = GetEquipped((InheritSlot)s);
            if (it == null) continue;
            Accum(it.mainStat, it.mainValue, ref atk, ref def, ref cr, ref cd, ref eva, ref hp);
            if (it.subStats == null) continue;
            foreach (var sub in it.subStats)
                if (sub != null) Accum(sub.stat, sub.value, ref atk, ref def, ref cr, ref cd, ref eva, ref hp);
        }
    }
}
