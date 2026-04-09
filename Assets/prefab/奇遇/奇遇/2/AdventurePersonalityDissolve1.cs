using UnityEngine;

/// <summary>
/// 奇遇2：人格解离
/// 效果：克隆玩家，所有现有玩家血量平分给克隆体
/// 每次触发都会再平分一次
/// </summary>
public class AdventurePersonalityDissolve : AdventureOptionBase
{
    public override void Execute()
    {
        Transform playerlayer = GameObject.Find("playerlayer")?.transform;
        if (playerlayer == null || playerlayer.childCount == 0)
        {
            base.Execute();
            return;
        }

        // 先把所有现有玩家的 health 和 healthmax 减半
        foreach (Transform t in playerlayer)
        {
            Player p = t.GetComponent<Player>();
            if (p == null) continue;
            p.healthmax = Mathf.Max(1, p.healthmax / 2);
            p.health    = Mathf.Clamp(p.health / 2, 1, p.healthmax);
            
            // 确保原始玩家有"Player"标签
            t.tag = "Player";
        }

        // 以第一个玩家为模板克隆
        GameObject original = playerlayer.GetChild(0).gameObject;
        Vector3 offset = new Vector3(1.5f, 0, 0);
        GameObject clone = Instantiate(original, original.transform.position + offset,
                    original.transform.rotation, playerlayer);
        
        // 关键：标记为分身
        clone.tag = "Clone";
        
        // 复制玩家数据
        Player clonePlayer = clone.GetComponent<Player>();
        if (clonePlayer != null)
        {
            clonePlayer.healthmax = original.GetComponent<Player>().healthmax;
            clonePlayer.health = original.GetComponent<Player>().health;
        }

        base.Execute();
    }
}