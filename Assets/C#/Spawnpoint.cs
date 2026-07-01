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

    [Header("N9+ 狼人社群")]
    public GameObject wolfPrefab; // 拖入狼人 prefab，N9~N13 自动加入刷怪池

    [Header("N11+ 史莱姆社群")]
    public GameObject slimePrefab; // 拖入史莱姆 prefab，N11~N13 自动加入刷怪池

    [Header("N10 狼人社群Boss")]
    public GameObject wolfBossPrefab; // 拖入狼人Boss prefab，进入 N10 时开局生成一只

    void Start()
    {
        if (DifficultyManager.Instance == null) return;
        string label = DifficultyManager.Instance.Current.label;

        // N5~N8 难度将蝙蝠加入刷怪池
        if (batPrefab != null &&
            (label == "N5" || label == "N6" || label == "N7" || label == "N8"))
            enemy.Add(batPrefab);

        // N9 起加入狼人社群小怪
        if (wolfPrefab != null &&
            (label == "N9" || label == "N10" || label == "N11" || label == "N12" || label == "N13"))
            enemy.Add(wolfPrefab);

        // N11 起加入史莱姆社群小怪
        if (slimePrefab != null &&
            (label == "N11" || label == "N12" || label == "N13"))
            enemy.Add(slimePrefab);

        // N10：开局生成一只狼人社群Boss（一次性，不进刷怪池，避免刷出多只）
        if (wolfBossPrefab != null && label == "N10")
        {
            Vector3 pos = transform.childCount > 0 ? transform.GetChild(0).position : transform.position;
            Instantiate(wolfBossPrefab, pos, Quaternion.Euler(45, 0, 0), enemylayer);
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
