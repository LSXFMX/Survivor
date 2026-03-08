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
        rb= GetComponent<Rigidbody>();
        enemy = GameObject.Find("enemylayer").transform;
        transform.localScale = transform.localScale * size;//调整子弹大小
    }
    //伤害事件
    private void OnTriggerEnter(Collider other)
    {
        pass -= 1;
        if (pass < 0)
        {
            Destroy();
        }
        if (other.CompareTag("enemy"))
        {
            enemy enemy = other.GetComponent<enemy>();
            if (enemy.health > 0)
            {
                float finaldamage=damage+player.atk;//最终伤害=伤害+角色攻击力
                float random = UnityEngine.Random.value *100;//暴击率0-100
                if (player.CR > random)
                {
                    finaldamage = finaldamage*(player.CD/100);//如若暴击，最终伤害乘以暴击伤害,暴击伤害为（120-无限）
                }
                finaldamage -= enemy.def;//最终伤害减去怪物防御力
                enemy.health -= damage;
                enemy.health -= (int)finaldamage;
                GameObject atknumber = enemy.atknumber;
                GameObject number = Instantiate(atknumber, other.transform.position, default);
                number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = ((int)finaldamage).ToString();
                other.GetComponent<enemy>().startturnred();
                if (enemy.health <= 0)
                {
                    enemy.Destroy1();
                    //enemy死亡
                }
            }
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
            distance = postion1 - postion2;
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
        if (cango ) //position拼写错了（汗
        {
            Vector3 vect = new Vector3(distance.x, 0, distance.z).normalized * speed;
            GetComponent<Rigidbody>().velocity = vect;//赋予向量
            float angle = Mathf.Atan2(distance.z, distance.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            if (role==null)
            {
                getrole();
            }
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0)
            {
                Destroy();
            }
        }
    }
}
