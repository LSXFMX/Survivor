using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

public class getexp : MonoBehaviour
{
    public Player player;
    public bool flytoplayer = false;

    /// <summary>经验石触发次数倍率（奇遇6可设为2）。每局开始时会被 battleUI 重置为 1。</summary>
    public static int triggerMultiplier = 1;

    // 【碰撞隔离】经验石只和玩家/地面碰撞，不与怪物/其他经验石碰撞（防止被挤飞）
    private static bool s_layerSetupDone;

    private void Awake()
    {
        // 全局只设一次：经验石层 7 (ExpGem) 与 敌人层 6 (Enemy) 互斥，也与自身互斥
        if (!s_layerSetupDone)
        {
            s_layerSetupDone = true;
            int expLayer  = LayerMask.NameToLayer("ExpGem");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (expLayer >= 0 && enemyLayer >= 0)
            {
                Physics.IgnoreLayerCollision(expLayer, enemyLayer, true);
                Physics.IgnoreLayerCollision(expLayer, expLayer,   true);
            }
        }

        // 把自己挪出 Default 层，让上面的层碰撞规则生效
        int myLayer = LayerMask.NameToLayer("ExpGem");
        if (myLayer >= 0) gameObject.layer = myLayer;
    }

    private void OnEnable()
    {
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            Player p = collision.gameObject.GetComponent<Player>();
            // 拾取音效
            AudioManager.PlaySfx(AudioManager.SfxKey.Pickup);
            if (triggerMultiplier <= 1)
            {
                // 单次触发，直接处理
                ApplyExp(p);
                Destroy(gameObject);
            }
            else
            {
                // 多次触发，用协程分帧处理，让UI体现出多次增长
                StartCoroutine(MultiTriggerRoutine(p));
            }
        }
    }

    private void ApplyExp(Player p)
    {
        p.exp += (int)(10 + player.DR);
        if(p.exp >= p.expmax)
        {
            p.exp = 0;
            p.levelup();
        }
    }

    private System.Collections.IEnumerator MultiTriggerRoutine(Player p)
    {
        for (int i = 0; i < triggerMultiplier; i++)
        {
            ApplyExp(p);
            yield return null; // 等一帧，让UI刷新
        }
        Destroy(gameObject);
    }

    void Update()
    {
        // 距离判断，避免每帧 Physics.OverlapSphere 带来的物理查询开销
        if (!flytoplayer && player != null)
        {
            float sqr = (player.transform.position - transform.position).sqrMagnitude;
            float r = player.PickupRadius;
            if (sqr <= r * r) flytoplayer = true;
        }
        if(flytoplayer)
        {
            Vector3 postion1 = player.transform.position;//目标坐标
            Vector3 postion2 = transform.position;//自己坐标
            Vector3 distance = postion1 - postion2;
            Vector3 vect = new Vector3(distance.x, 0, distance.z).normalized * 10;
            transform.position += vect * Time.deltaTime;
        }
    }
}
