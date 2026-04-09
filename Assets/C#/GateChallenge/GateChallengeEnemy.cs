using System.Collections;
using UnityEngine;

/// <summary>
/// 门挑战专用怪物。
/// - 死亡立刻销毁（无2秒等待）
/// - 通知 GateChallengeManager
/// </summary>
public class GateChallengeEnemy : enemy
{
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
