using UnityEngine;

/// <summary>
/// 继承装备的系统整合入口。集中放"钩子"，避免把继承装备的逻辑
/// 零散地塞进 WorldBossManager / EquipmentInitializer / ArchiveManager 里。
///
/// 三个接入点：
///   1. <see cref="TryDropFromWorldBoss"/> —— 世界 Boss 死亡时调用（掉落）
///   2. <see cref="ApplyToPlayerOnRunStart"/> —— 开局把装备属性加到玩家身上
///   3. <see cref="AttachUIToArchiveContainer"/> —— 给存档界面的空容器挂上 UI
/// </summary>
public static class InheritEquipmentHooks
{
    /// <summary>
    /// 世界 Boss 掉落判定（策划案第 2 条）。
    /// 掉率由<see cref="InheritEquipmentGenerator.DropChance"/> 决定（N9+恒 100%）。
    /// 命中后：生成装备 → 播放世界内稀有度展示 → 入库（可能被自动分解）。
    /// </summary>
    /// <param name="worldPos">Boss 死亡位置，用于播放掉落展示。</param>
    public static void TryDropFromWorldBoss(Vector3 worldPos)
    {
        var mgr = InheritEquipmentManager.Ensure();
        if (mgr == null) return;

        float chance = InheritEquipmentGenerator.DropChance();
        if (Random.value > chance)
        {
            Debug.Log($"[Inherit] 世界Boss 未掉落继承装备（掉率 {chance:P0}）");
            return;
        }

        // 掉落件数：无尽模式按塔层递增（每 25 层 +1 件，满 100 层 5 件）
        int count = InheritEquipmentGenerator.DropCount();
        for (int i = 0; i < count; i++)
 {
   InheritItem item = InheritEquipmentGenerator.Generate();
     if (item == null) continue;

        // 多件时错开一点位置，避免展示动画完全重叠
            Vector3 showPos = count > 1
        ? worldPos + new Vector3((i - (count - 1) * 0.5f) * 1.6f, 0f, 0f)
                : worldPos;

          // 先播展示：即使随后被自动分解，玩家也该看到"掉了什么"
    InheritDropDisplay.Show(item, showPos);

         bool kept = mgr.Acquire(item);
            Debug.Log($"[Inherit] 掉落 {item.DisplayName}（{i + 1}/{count}）" +
   $"（力量值 {item.dropPower:0.#}，" +
    $"主词条 {InheritEquipmentDefs.FormatStatLine(item.mainStat, item.mainValue)}，" +
    $"副词条 {item.subStats?.Count ?? 0} 条，" +
         $"{(kept ? "已入库" : "已自动分解")}）");
        }
    }

    /// <summary>
    /// 开局把继承装备的全部加成应用到玩家。
    /// 由 EquipmentInitializer 在其它装备加成之后调用 ——
    /// 放最后是因为继承装备是"终局装备"，语义上叠在基础装备之上。
    /// </summary>
    public static void ApplyToPlayerOnRunStart(Attribute player)
    {
        if (player == null) return;
        var mgr = InheritEquipmentManager.Ensure();
        mgr?.ApplyToPlayer(player);
    }

    /// <summary>
    /// 给存档界面的「继承装备」容器挂上 UI。
    ///
    /// 场景里那个容器（1030×851）是个**完全空的节点**，
    /// 所以这里在它下面建一个铺满的子节点并挂 <see cref="InheritEquipmentUI"/>。
    /// 幂等：已挂过就直接返回。
    ///
    /// 【踩过的坑】这5 个分类容器是互相复制出来的，每个都挂着
    /// HorizontalLayoutGroup（childControlWidth/Height = 0）——那是给
    /// EquipmentIcon 图标格子横排用的。自动布局会强行把子物体的 anchor 改成
    /// (0,1) 并只写 anchoredPosition、不写 size，于是"anchor 铺满 + sizeDelta=0"
    /// 的整块面板会被压成 0×0，所有文字溢出成一条竖线。
    /// 继承装备是整块自绘面板、完全不需要自动布局，直接把它关掉。
    /// </summary>
    public static void AttachUIToArchiveContainer(GameObject container)
    {
        if (container == null) return;
        if (container.GetComponentInChildren<InheritEquipmentUI>(true) != null) return;

        InheritEquipmentManager.Ensure();

        InheritEquipmentUI.DisableParentAutoLayout(container.transform as RectTransform);

        var go = new GameObject("InheritEquipmentPanel", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(container.transform, false);

        go.AddComponent<InheritEquipmentUI>();   // 自身 OnEnable 里会把 rect 铺满并自愈
        Debug.Log("[Inherit] 继承装备面板已挂载到存档界面容器");
    }

    /// <summary>场景重载时清理素材缓存。</summary>
    public static void ResetSceneCaches()
    {
        InheritEquipmentAssets.ResetCaches();
    }
}
