using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 世界Boss基类：以往关底Boss的克隆体。
/// - 生成在地图固定位置，初始处于待机状态（不追玩家）
/// - 玩家进入激活范围后才开始战斗
/// - 击败后触发社群解锁，提供局内加成
///
/// Inspector 配置：
/// - activateRange  : 玩家靠近多少距离激活，默认 15
/// - faction        : 该Boss对应的社群
/// - battleUI       : 由 WorldBossManager 赋值
/// </summary>
public class WorldBossBase : enemy
{
    [Header("世界Boss设置")]
    public float       activateRange = 15f;
    public FactionType faction       = FactionType.Mushroom;

    [HideInInspector] public battleUI battleUI;
    [HideInInspector] public WorldBossManager worldBossManager;

    protected bool _activated = false;

    private Animator _ani;

    protected new void OnEnable()
    {
        // 手动初始化父类私有字段
        var playerlayer = GameObject.Find("playerlayer")?.transform;
        typeof(enemy).GetField("playerlayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(this, playerlayer);

        _ani = GetComponent<Animator>();

        // 世界Boss不受难度倍率影响，保持 prefab 原始数值
        // （如需倍率可在子类 override）
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 未激活：检测玩家距离
        if (!_activated)
        {
            if (role == null) getrole();
            if (role != null)
            {
                float dist = Vector3.Distance(transform.position, role.transform.position);
                if (dist <= activateRange)
                    Activate();
            }
            return;
        }

        // 激活后：执行正常 enemy 逻辑
        base.FixedUpdate();
    }

    protected virtual void Activate()
    {
        _activated = true;
        ToastManager.Show($"世界Boss已激活！");
        Debug.Log($"[WorldBoss] {faction} 世界Boss激活");
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
        rolestate = state.dead;

        _ani?.SetTrigger("dead");
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        if (expstone != null)
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        // 通知世界Boss管理器
        worldBossManager?.OnWorldBossDefeated(faction);

        StartCoroutine(Destroy2());
    }
}
