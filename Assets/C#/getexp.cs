using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using UnityEngine;

public class getexp : MonoBehaviour
{
    public Player player;
    public bool flytoplayer = false;
    private void OnEnable()
    {
        player = GameObject.Find("playerlayer").transform.GetChild(0).gameObject.GetComponent<Player>();
    }
    private void OnCollisionEnter(Collision collision)//玩家碰撞经验石
    {
        if(collision.gameObject.CompareTag("Player"))
        {
            Player p = collision.gameObject.GetComponent<Player>();
            p.exp += 10+player.DR;
            if(p.exp>=p.expmax)
            {
                p.exp = 0;
                p.levelup();
            }
            Destroy(gameObject);
        }
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
