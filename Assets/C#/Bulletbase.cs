using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;

public class Bulletbase : MonoBehaviour
{
    public int damage;
    public int level;
    public float lifetime;
    public int pass;
    public float speed;
    public float size;
    public Skillbase fatherskill;
    public Attribute player;
    public bool cango = false;//子弹是否可以发射
    public Rigidbody rb;
    public Transform enemy;
    public GameObject role;//目标角色
    public Vector3 distance;
    //获取子弹所属技能的参数
    public virtual void GetFather()
    {
        damage = fatherskill.damage;
        level = fatherskill.level;
        lifetime = fatherskill.lifetime;
        pass = fatherskill.pass;
        speed = fatherskill.speed;
        size = fatherskill.size;
        player = GameObject.Find("playerlayer").transform.GetChild(0).GetComponent<Attribute>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;          // 禁用重力，防止火球埋进地里
        rb.constraints = RigidbodyConstraints.FreezePositionY  // 锁定Y轴位置
                       | RigidbodyConstraints.FreezeRotation;  // 锁定旋转
        enemy = GameObject.Find("enemylayer").transform;
        transform.localScale = transform.localScale * size;
    }
    private void OnTriggerEnter(Collider other)
    {
        enemy enemy = other.GetComponent<enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<enemy>();
        if (enemy == null) return;

        if (enemy.health > 0)
        {
            // 闪避判定：EVA 为闪避概率（0~100）
            float evaRoll = UnityEngine.Random.value * 100;
            if (enemy.EVA > evaRoll)
            {
                // 闪避成功，不造成伤害，但仍消耗穿透
                pass -= 1;
                if (pass < 0) Destroy();
                return;
            }

            float finaldamage = damage + player.atk;
            float random = UnityEngine.Random.value * 100;
            if (player.CR > random)
            {
                finaldamage = finaldamage * (player.CD / 100);
            }
            finaldamage -= enemy.def;
            enemy.health -= damage;
            enemy.health -= (int)finaldamage;
            GameObject atknumber = enemy.atknumber;
            GameObject number = Instantiate(atknumber, enemy.transform.position, default);
            number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = ((int)finaldamage).ToString();
            enemy.startturnred();
            if (enemy.health <= 0)
            {
                enemy.Destroy1();
            }
        }

        pass -= 1;
        if (pass < 0)
        {
            Destroy();
        }
    }
    public void Destroy()
    {
        Destroy(gameObject);
    }
    public void getrole()
    {
        float shortestdis = 999999;
        Transform shortestrole = null;
        if (enemy.childCount > 0)
        {
            foreach (Transform item in enemy)
            {
                Vector3 i = item.position;
                float distance = Vector3.Distance(i, transform.position);
                if (distance < shortestdis)
                {
                    shortestdis = distance;
                    shortestrole = item;
                }
            }
            role = shortestrole.gameObject;
        }
        if(role !=null)
        {
            Vector3 postion1 = role.transform.position;//目标坐标
            Vector3 postion2 = transform.position;//自己坐标
            distance = postion1 - postion2 + new Vector3(0, 2f, 0); // 无条件抬高2f
        }
        else
        {
            Vector3 postion1 = transform.position + new Vector3(1, 0, 0);//目标坐标
            Vector3 postion2 = transform.position;//自己坐标
            distance = postion1 - postion2;
        }
    }
    void FixedUpdate()
    {
        if (cango)
        {
            Vector3 vect = new Vector3(distance.x, 0, distance.z).normalized * speed;
            rb.velocity = vect;
            float angle = Mathf.Atan2(distance.z, distance.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            // 不再每帧重新寻找目标，方向在发射时固定
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0)
            {
                Destroy();
            }
        }
    }
}
