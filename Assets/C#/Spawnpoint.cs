using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawnpoint : MonoBehaviour
{
    public Transform enemylayer;
    public battleUI b;
    public List<GameObject> enemy;
    public float SpawnTimer;
    public int maxenemy;
    public float timer;

    [Header("N5+ 蝙蝠")]
    public GameObject batPrefab; // 拖入蝙蝠 prefab，N5~N8 自动加入刷怪池

    void Start()
    {
        // N5~N8 难度将蝙蝠加入刷怪池
        if (batPrefab != null && DifficultyManager.Instance != null)
        {
            string label = DifficultyManager.Instance.Current.label;
            if (label == "N5" || label == "N6" || label == "N7" || label == "N8")
                enemy.Add(batPrefab);
        }
    }

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

    public void Spawn()
    {
        if(enemylayer.childCount < maxenemy)
        {
            Instantiate(randomobj(), getrandompoint().position, Quaternion.Euler(45, 0, 0), enemylayer);
        }
    }

    public GameObject randomobj()
    {
        int random = Random.Range(0, enemy.Count);
        return enemy[random];
    }

    public Transform getrandompoint()
    {
        int random = Random.Range(0, transform.childCount);
        return transform.GetChild(random);
    }
}
