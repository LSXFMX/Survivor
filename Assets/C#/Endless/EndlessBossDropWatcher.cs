using UnityEngine;

/// <summary>
/// 无尽模式 Boss 掉落兜底。
///
/// 为什么需要它：
///   无尽模式的社群 Boss 由 <c>battleUI.SpawnRandomCommunityBoss</c> 动态生成。
///   正常路径下会注入 <c>worldBossManager</c> +<c>faction</c>，Boss 死亡时经
///   <c>WorldBossManager.OnWorldBossDefeated</c> 触发继承装备掉落。
///   但当 <c>WorldBossManager.Instance</c> 未就绪时会走**回退分支**，用的是
///   关底 Boss 预制体（MushroomBoss / BatBoss / WolfBoss / SlimeBoss），
///   这些对象身上**没有 WorldBoss* 组件**，压根不会调 OnWorldBossDefeated
///   —— 于是打死了也不掉继承装备。
///
///   这个组件就挂在那种"非世界Boss组件"的无尽 Boss 上，轮询血量，
///   死亡瞬间直接调一次掉落判定。仅在确实没有 WorldBoss* 组件时才挂，
///   因此不会与正常路径重复掉落。
/// </summary>
public class EndlessBossDropWatcher : MonoBehaviour
{
    private enemy _enemy;
    private bool _fired;

    private void Awake()
    {
        _enemy = GetComponent<enemy>();
    }

    private void Update()
    {
        if (_fired || _enemy == null) return;
        if (_enemy.health > 0) return;

        _fired = true;
        InheritEquipmentHooks.TryDropFromWorldBoss(transform.position);
        Debug.Log("[Inherit] 无尽模式 Boss 死亡（回退路径），已执行继承装备掉落判定");
    }
}
