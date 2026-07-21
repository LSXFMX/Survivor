using System.Collections;
using UnityEngine;

/// <summary>
/// 门挑战专用怪物。
/// - 死亡立刻销毁（无2秒等待）
/// - 通知 GateChallengeManager
/// - 高物理质量（800），不会被玩家/小怪推来推去，体现"守门人"的厚重感
/// </summary>
public class GateChallengeEnemy : enemy
{
    /// <summary>enemy.OnEnable 默认设 mass=5（小怪级），Start 覆写为精英级质量。</summary>
    private void Start()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 800f;
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
        rolestate = state.dead;

        // 通知管理器
        GateChallengeManager.Instance?.OnEnemyKilled();

        // 立刻销毁，不等动画
        Destroy(gameObject);
    }
}
