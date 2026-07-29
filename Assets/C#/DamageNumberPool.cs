using UnityEngine;

/// <summary>
/// 伤害数字对象池。每秒几十次 Instantiate + Destroy 会产生大量 GC。
/// 改为预创建 N 个，循环复用。
/// </summary>
public static class DamageNumberPool
{
    private static GameObject[] _pool;
    private static int _index;
    const int POOL_SIZE = 60;

    /// <summary>从任意 enemy.atknumber 自动初始化池（只首次调用有效）。</summary>
    public static void EnsureInit(GameObject anyAtknumberPrefab, Transform parent = null)
    {
        if (_pool != null) return;
        if (anyAtknumberPrefab == null) return;

        _pool = new GameObject[POOL_SIZE];
        for (int i = 0; i < POOL_SIZE; i++)
        {
            var go = Object.Instantiate(anyAtknumberPrefab, parent);
            go.SetActive(false);
            _pool[i] = go;
        }
    }

    /// <summary>从池里取一个，返回顶层 transform。</summary>
    public static GameObject Get(Vector3 worldPos)
    {
        if (_pool == null) return null;

        GameObject go = _pool[_index];
        _index = (_index + 1) % POOL_SIZE;

        go.SetActive(false);  // 先关再开确保 reactivate
        go.transform.SetParent(null, false);
        go.transform.position = worldPos;
        go.SetActive(true);
        return go;
    }
}
