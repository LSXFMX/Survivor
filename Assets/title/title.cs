using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class title : MonoBehaviour
{
    public Transform enemylayer;
    public GameObject fightscene;
    public GameObject savescene;
    public GameObject choiceUI;
    public GameObject campPrefab;       // 营地 prefab
    public Transform playerlayer;       // 用于获取玩家位置
    [SerializeField] private float campSpawnRadius = 20f;  // 生成半径
    [SerializeField] private float campMinDistance = 8f;   // 距玩家最近距离

    private void OnEnable()
    {
        savescene.SetActive(false);
        fightscene.SetActive(false);
        if (choiceUI != null) choiceUI.SetActive(false);
        Time.timeScale = 0f;
    }

    public void click_start()
    {
        if (enemylayer.childCount > 0)
        {
            foreach (Transform enemy in enemylayer)
                Destroy(enemy.gameObject);
        }
        if (choiceUI != null) choiceUI.SetActive(false);
        Time.timeScale = 1.0f;
        fightscene.SetActive(true);
        gameObject.SetActive(false);

        // 生成5个营地
        if (campPrefab != null)
        {
            Vector3 playerPos = playerlayer != null && playerlayer.childCount > 0
                ? playerlayer.GetChild(0).position
                : Vector3.zero;

            int spawned = 0;
            int attempts = 0;
            while (spawned < 5 && attempts < 100)
            {
                attempts++;
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist  = UnityEngine.Random.Range(campMinDistance, campSpawnRadius);
                float x = playerPos.x + Mathf.Cos(angle) * dist;
                float z = playerPos.z + Mathf.Sin(angle) * dist;

                // 从高处向下 Raycast，贴合地表
                Vector3 rayOrigin = new Vector3(x, 50f, z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f))
                {
                    Vector3 pos = new Vector3(x, hit.point.y + 1.5f, z);
                    Instantiate(campPrefab, pos, Quaternion.Euler(45, 0, 0), enemylayer);
                    spawned++;
                }
            }
        }
    }

    public void opensave()
    {
        savescene.SetActive(true);
    }

    public void closesave()
    {
        savescene?.SetActive(false);
    }

    public void exitgame()
    {
        Application.Quit();
    }
}
