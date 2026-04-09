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

            // 添加两个刷怪点 cube（作为 Spawnpoint 子对象，在其附近偏移）
            if (spawnPointCubePrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    // 直接作为子对象生成，localPosition 在 Spawnpoint 局部坐标系内偏移
                    GameObject cube = Instantiate(spawnPointCubePrefab, sp.transform);
                    cube.transform.localPosition = new Vector3(
                        Random.Range(-5f, 5f),
                        0f,
                        Random.Range(-5f, 5f)
                    );
                    cube.transform.localRotation = Quaternion.identity;
                }
            }
        }

        base.Execute(); // 恢复时间
    }
}
