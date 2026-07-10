using UnityEngine;

/// <summary>
/// 奇遇10：源木收集者（money is power）
/// 效果：每持有 100 源木，增加一点攻击力和一点防御力（选择后动态实时生效）。
/// </summary>
public class AdventureOption10_YuanmuCollector : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "源木收集者";
        optionDescription = "money is power";
        effectDescription = "每持有100源木，增加一点攻击力和一点防御力（选择后动态实时生效）";
    }

    public override void Execute()
    {
        Player player = FindPlayer();
        if (player != null && player.GetComponent<YuanmuCollectorBuff>() == null)
        {
            player.gameObject.AddComponent<YuanmuCollectorBuff>();
            ToastManager.Show("源木收集者：每100源木 +1攻击 +1防御（实时生效）");
        }
        base.Execute();
    }

    private Player FindPlayer()
    {
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer == null) return null;
        foreach (Transform t in playerLayer)
        {
            if (t != null && t.CompareTag("Player"))
                return t.GetComponent<Player>();
        }
        if (playerLayer.childCount > 0)
            return playerLayer.GetChild(0).GetComponent<Player>();
        return null;
    }
}
