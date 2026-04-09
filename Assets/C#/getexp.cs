using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

public class getexp : MonoBehaviour
{
    public Player player;
    public bool flytoplayer = false;

    /// <summary>经验石触发次数倍率（奇遇6可设为2）</summary>
    public static int triggerMultiplier = 1;

    private void OnEnable()
    {
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            Player p = collision.gameObject.GetComponent<Player>();
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
        Collider[] colliders = Physics.OverlapSphere(transform.position, player.PickupRadius);
        if (colliders.Contains(player.GetComponent<CapsuleCollider>()))
        {
            //执行飞向玩家
            flytoplayer=true;
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
