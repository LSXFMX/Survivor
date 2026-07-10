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

    [Header("N4+ 蝙蝠")]
    public GameObject batPrefab; // 拖入蝙蝠 prefab，N4 起自动加入刷怪池

    [Header("N9+ 狼人社群")]
    public GameObject wolfPrefab; // 拖入狼人 prefab，N9~N13 自动加入刷怪池

    [Header("N11+ 史莱姆社群")]
    public GameObject slimePrefab; // 拖入史莱姆 prefab，N11~N13 自动加入刷怪池

    [Header("N12 史莱姆社群Boss（测试用）")]
    public GameObject slimeBossPrefab; // 拖入史莱姆Boss prefab，N12 开局生成一只供测试

    void Start()
    {
        // 兜底：场景用外部脚本写入的 slimeBossPrefab 可能因 Unity 序列化缓存未命中为 null，
        // 优先从同场景的 battleUI 组件复制引用（battleUI 一定存在且已正确序列化）。
        if (slimeBossPrefab == null && b != null) slimeBossPrefab = b.slimeBossPrefab;

        if (DifficultyManager.Instance == null) return;
        string label = DifficultyManager.Instance.Current.label;
        bool endless = DifficultyManager.Instance.IsEndless;

        // 无尽模式：根据已通关的关卡解锁对应的敌人类型
        if (endless)
        {
            var crm = ClearRecordManager.Instance;
            if (batPrefab != null && crm != null && crm.GetClearCount("N4") > 0)
                enemy.Add(batPrefab);
            if (wolfPrefab != null && crm != null && crm.GetClearCount("N9") > 0)
                enemy.Add(wolfPrefab);
            if (slimePrefab != null && crm != null && crm.GetClearCount("N11") > 0)
                enemy.Add(slimePrefab);
        }
        else
        {
            // N4 起将蝙蝠加入刷怪池
            if (batPrefab != null && 
                (label == "N4" || label == "N5" || label == "N6" || label == "N7" || label == "N8"
              || label == "N9" || label == "N10" || label == "N11" || label == "N12" || label == "N13"))
                enemy.Add(batPrefab);

            // N9 起加入狼人社群小怪
            if (wolfPrefab != null &&
                (label == "N9" || label == "N10" || label == "N11" || label == "N12" || label == "N13"))
                enemy.Add(wolfPrefab);

            // N11 起加入史莱姆社群小怪
            if (slimePrefab != null &&
                (label == "N11" || label == "N12" || label == "N13"))
                enemy.Add(slimePrefab);

            // N12 开局生成一只史莱姆社群Boss（测试用）
            if (slimeBossPrefab != null && label == "N12")
            {
                Vector3 pos = transform.childCount > 0 ? transform.GetChild(0).position : transform.position;
                var obj = Instantiate(slimeBossPrefab, pos, Quaternion.Euler(45, 0, 0), enemylayer);
                var boss = obj.GetComponent<SlimeBoss>();
                if (boss != null && b != null) boss.battleUI = b;
                Debug.Log("[Spawn] N12 开局已生成史莱姆社群Boss（测试模式）");
            }
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
