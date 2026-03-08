using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
public class enemy : Attribute
{
    public GameObject atknumber;
    public state rolestate;
    public GameObject role;//目标角色（玩家）
    private Transform playerlayer;//玩家层
    private Animator ani;
    public float Sca;//角色缩放大小，用于控制转向
    public Material material;
    public Material red;
    public GameObject expstone;
    public enum state
    {
        idle,
        move,
        dead,
    }
    void OnEnable()
    {
        playerlayer = GameObject.Find("playerlayer").transform;
        ani= GetComponent<Animator>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rolestate != state.dead)
        {
            if (collision.gameObject.CompareTag("Player"))
            {
                Player Player = collision.gameObject.GetComponent<Player>();
                if (Player.health > 0)
                {
                    Player.health -= atk;
                    GameObject number = Instantiate(atknumber, collision.transform.position, default);
                    number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = atk.ToString();
                    collision.gameObject.GetComponent<Player>().startturnred();
                    if (Player.health <= 0)
                    {
                        Player.death();//玩家死亡
                    }
                }
            }
        }  
    }
    public void getrole()
    {
        float shortestdis = 999999;
        Transform shortestrole = null;
        if(playerlayer.childCount>0)
        {
            foreach(Transform item in playerlayer)
            {
                Vector3 i = item.position;
                float distance =Vector3.Distance(i,transform.position);
                if(distance < shortestdis)
                {
                    shortestdis = distance;
                    shortestrole = item;
                }
            }
            role = shortestrole.gameObject;
        }
    }

    private void FixedUpdate()
    {
        if(role != null)
        {
            float chazhi = role.transform.position.x-transform.position.x;
            if(chazhi > 0)
            {
                transform.localScale = new Vector3(Sca, Sca, Sca);
            }
            else
            {
                transform.localScale = new Vector3(-1*Sca, Sca, Sca);
            }
        }

        switch(rolestate)
        {
            case state.idle:
                ani.SetBool("ismove", false);

                if (role == null)
                {
                    getrole();
                }
                else
                {
                    rolestate = state.move;
                }


                break;
            case state.move:
                ani.SetBool("ismove", true);
                if (role == null)
                {
                    rolestate = state.idle;
                }
                else
                {
                    //1.获取目标坐标和自己坐标
                    //2.设置移动向量，并赋值给刚体
                    Vector3 postion1=role.transform.position;//目标坐标
                    Vector3 postion2=transform.position;//自己坐标
                    Vector3 distance =postion1 - postion2;
                    Vector3 vect =new Vector3(distance.x, 0, distance.z).normalized*speed;
                    transform.position += vect * Time.fixedDeltaTime;
                }


                break;
            case state.dead:

                break;
        }
    }
    public void startturnred()
    {
        StartCoroutine(turnred());
    }

    public IEnumerator turnred()
    {
        transform.GetComponent<SpriteRenderer>().material = red;
        yield return new WaitForSeconds(0.3f);
        transform.GetComponent<SpriteRenderer>().material = material;
    }

    public void Destroy1()
    {
        if (rolestate != state.dead)
        {
            rolestate =state.dead;
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
            ani.SetTrigger("dead");
            StartCoroutine(Destroy2());
        }
        
    }
    public IEnumerator Destroy2()
    {
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
}
