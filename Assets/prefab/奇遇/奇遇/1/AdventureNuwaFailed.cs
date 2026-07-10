using UnityEngine;

/// <summary>
/// 奇遇1：女娲补天失败了？
/// 效果：为每个 Spawnpoint 添加两个刷怪点，并提高 20% 最大刷怪上限
///
/// 无尽模式：初始可选 2 次，每半小时额外 +1 次选择机会。
/// </summary>
public class AdventureNuwaFailed : AdventureOptionBase
{
    [Tooltip("刷怪点 cube prefab，添加为 Spawnpoint 的子对象")]
    public GameObject spawnPointCubePrefab;

    // 无尽模式多选追踪
    private static int   _nuwaSelectedCount = 0;
    private static float _nuwaRunStartTime  = 0f;

    public static void ResetRunCounter()
    {
        _nuwaSelectedCount = 0;
        _nuwaRunStartTime  = Time.time;
    }

    public override bool IsAvailableInCurrentDifficulty()
    {
        // 普通模式：沿用基类 oneShot 去重（单局只能选一次）
        if (DifficultyManager.Instance == null || !DifficultyManager.Instance.IsEndless)
            return base.IsAvailableInCurrentDifficulty();

        // 无尽模式：初始 2 次，每 30 分钟额外 +1 次
        if (_nuwaRunStartTime <= 0f) _nuwaRunStartTime = Time.time;
        int elapsedMin = (int)((Time.time - _nuwaRunStartTime) / 60f);
        int maxAllowed = 2 + elapsedMin / 30;
        return _nuwaSelectedCount < maxAllowed;
    }

    public override void Execute()
    {
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsEndless)
            _nuwaSelectedCount++;

        Spawnpoint[] spawnpoints = FindObjectsOfType<Spawnpoint>();
        foreach (Spawnpoint sp in spawnpoints)
        {
            int increase = Mathf.Max(1, Mathf.RoundToInt(sp.maxenemy * 0.2f));
            sp.maxenemy += increase;

            if (spawnPointCubePrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
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

        // 无尽模式不调用 base.Execute() 的 oneShot 去重（改为按上述计数控制），
        // 但需手动恢复时间流。
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsEndless)
        {
            battleUI bui = GameObject.Find("BattleUI")?.GetComponent<battleUI>();
            if (bui != null) bui.ResumeTime();
            else Time.timeScale = 1;
        }
        else
        {
            base.Execute();
        }
    }
}
