using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawnpoint : MonoBehaviour
{
    public Transform enemylayer;
    public battleUI b;
    public List<GameObject> enemy;
    public float SpawnTimer;//刷怪间隔
    public int maxenemy;
    public float timer;


    void FixedUpdate()
    {
        if(b.startcount)
        {
            timer += Time.fixedDeltaTime;
            if(timer > SpawnTimer)
            {
                timer = 0;
                Spawn();
            }
        }
    }

    public void Spawn()//刷怪方法
    {
        if(enemylayer.childCount<maxenemy)
        {
            Instantiate(randomobj(), getrandompoint().position,Quaternion.Euler(45,0,0),enemylayer);
        }
    }

    public GameObject randomobj()//获取随机敌人对象
    {
        int random = Random.Range(0, enemy.Count);
        return enemy[random];
    }
    public Transform getrandompoint()//随机选择刷怪点
    {
        int random = Random.Range(0, transform.childCount);
        return transform.GetChild(random);
    }
}
