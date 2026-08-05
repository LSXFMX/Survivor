using UnityEngine;

/// <summary>
/// 继承装备系统的调试工具（仅编辑器可见）。
///
/// 挂到场景里任意物体上即可通过右键菜单快速验证系统，
/// 不必真的去打世界 Boss 刷装备。
/// </summary>
public class InheritEquipmentDebug : MonoBehaviour
{
    [Header("批量生成")]
    [Tooltip("一次生成多少件随机装备。")]
    public int generateCount = 12;

    [Header("指定生成")]
    public InheritSlot   testSlot   = InheritSlot.Weapon;
    public InheritRarity testRarity = InheritRarity.Singularity;
    [Tooltip("模拟的难度力量值。13 = N13；更高模拟无尽后期。")]
    public float testPower = 13f;

    [ContextMenu("① 随机生成一批装备（按当前难度）")]
    public void GenerateRandomBatch()
    {
        var m = InheritEquipmentManager.Ensure();
        int kept = 0;
        for (int i = 0; i < generateCount; i++)
        {
            var it = InheritEquipmentGenerator.Generate();
            if (m.Acquire(it)) kept++;
        }
        Debug.Log($"[InheritDebug] 生成 {generateCount} 件，入库 {kept} 件，" +
                  $"当前仓库 {m.WarehouseCount} 件，材料 {m.Materials}");
    }

    [ContextMenu("② 生成一件指定槽位/稀有度的装备")]
    public void GenerateSpecific()
    {
        var m = InheritEquipmentManager.Ensure();
        var it = InheritEquipmentGenerator.Generate(testSlot, testRarity, testPower);
        m.Acquire(it);
        Debug.Log($"[InheritDebug] 生成 {it.DisplayName}：" +
                  $"主 {InheritEquipmentDefs.FormatStatLine(it.mainStat, it.mainValue)}，" +
                  $"副 {DescribeSubs(it)}");
    }

    [ContextMenu("③ 打印数值区间抽样表（校验平衡性）")]
    public void PrintBalanceTable()
    {
        var sb = new System.Text.StringBuilder(2048);
        sb.AppendLine("[InheritDebug] 主词条区间抽样（每格200 次取min/avg/max）");
        sb.AppendLine("槽位/主词条        稀有度      @N1                @N13");

        foreach (InheritSlot slot in System.Enum.GetValues(typeof(InheritSlot)))
        {
            var stat = InheritEquipmentDefs.MainStatOf(slot);
            foreach (InheritRarity r in System.Enum.GetValues(typeof(InheritRarity)))
            {
                sb.Append($"{InheritEquipmentDefs.SlotName(slot),-4}/{InheritEquipmentDefs.StatName(stat),-5} " +
                          $"{InheritEquipmentDefs.RarityName(r),-6} ");
                sb.Append(Sample(stat, r, 1f));
                sb.Append("   ");
                sb.Append(Sample(stat, r, 13f));
                sb.AppendLine();
            }
        }

        sb.AppendLine();
        sb.AppendLine("掉落概率：");
        for (int n = 1; n <= 13; n++)
            sb.Append($"N{n}={Mathf.Clamp01(n / 9f):P0}  ");
        sb.AppendLine("  无尽=100%");

        Debug.Log(sb.ToString());
    }

    private static string Sample(InheritStat stat, InheritRarity r, float power)
    {
        float min = float.MaxValue, max = float.MinValue, sum = 0f;
        const int N = 200;
        for (int i = 0; i < N; i++)
        {
            float v = InheritEquipmentGenerator.RollMainValue(stat, r, power);
            min = Mathf.Min(min, v); max = Mathf.Max(max, v); sum += v;
        }
        return $"{min,7:0.##}~{max,-7:0.##}(均{sum / N,6:0.##})";
    }

    [ContextMenu("④ 打印稀有度权重分布")]
    public void PrintRarityWeights()
    {
        var sb = new System.Text.StringBuilder(1024);
        sb.AppendLine("[InheritDebug] 稀有度分布（每档 2000 次抽样）");
        float[] powers = { 1f, 5f, 9f, 13f, 19f, 28f };
        foreach (float p in powers)
        {
            var cnt = new int[InheritEquipmentDefs.RARITY_COUNT];
            for (int i = 0; i < 2000; i++) cnt[(int)InheritEquipmentGenerator.RollRarity(p)]++;
            string label = p <= 13f ? $"N{(int)p}" : $"无尽(power={p})";
            sb.Append($"{label,-16}");
            for (int i = 0; i < cnt.Length; i++)
                sb.Append($"{InheritEquipmentDefs.RarityName((InheritRarity)i)}:{cnt[i] / 20f,5:0.#}%  ");
            sb.AppendLine();
        }
        Debug.Log(sb.ToString());
    }

    [ContextMenu("⑤ 加5000 材料")]
    public void AddMaterials() => InheritEquipmentManager.Ensure().AddMaterials(5000);

    [ContextMenu("⑥ 清空继承装备存档")]
    public void WipeSave()
    {
        PlayerPrefs.DeleteKey("InheritEquipSave_v1");
        PlayerPrefs.Save();
        Debug.Log("[InheritDebug] 继承装备存档已清空（需重启游戏生效）");
    }

    private static string DescribeSubs(InheritItem it)
    {
        if (it.subStats == null || it.subStats.Count == 0) return "无";
        var parts = new string[it.subStats.Count];
        for (int i = 0; i < it.subStats.Count; i++)
            parts[i] = InheritEquipmentDefs.FormatStatLine(it.subStats[i].stat, it.subStats[i].value);
        return string.Join(" / ", parts);
    }
}
