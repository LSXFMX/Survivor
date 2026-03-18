using UnityEngine;

/// <summary>
/// 奇遇1：女娲补天失败了？
/// 效果：为每个 Spawnpoint 添加两个刷怪点，并提高 20% 最大刷怪上限
/// </summary>
public class AdventureNuwaFailed : AdventureOptionBase
{
    [Tooltip("刷怪点 cube prefab，添加为 Spawnpoint 的子对象")]
    public GameObject spawnPointCubePrefab;

    public override void Execute()
    {
        Spawnpoint[] spawnpoints = FindObjectsOfType<Spawnpoint>();
        foreach (Spawnpoint sp in spawnpoints)
        {
            // 提高 20% 最大刷怪上限（至少 +1）
            int increase = Mathf.Max(1, Mathf.RoundToInt(sp.maxenemy * 0.2f));
            sp.maxenemy += increase;

            // 添加两个刷怪点 cube
            if (spawnPointCubePrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    // 在 Spawnpoint 附近随机偏移生成新刷怪点
                    Vector3 offset = new Vector3(
                        Random.Range(-5f, 5f),
                        0,
                        Random.Range(-5f, 5f)
                    );
                    Instantiate(spawnPointCubePrefab, sp.transform.position + offset,
                                Quaternion.identity, sp.transform);
                }
            }
        }

        base.Execute(); // 恢复时间
    }
}
