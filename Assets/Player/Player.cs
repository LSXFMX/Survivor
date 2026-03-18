using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Attribute
{
    public Material material;
    public Material red;
    private Rigidbody rb;
    public Animator ani;
    public battleUI battleUI;
    public Transform SkillList;
    public float PickupRadius;//拾取范围
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 8.0f;//质量：是否容易被推飞
        Physics.gravity = new Vector3(0, -30f, 0);
    }
    public void levelup()
    {
        level += 1;
        healthmax += 20;
        health = healthmax;
        exp = 0;
        expmax += 20;
        battleUI.openchoice();
    }
    // Update is called once per frame
    void Update()
    {
        float hmove = Input.GetAxis("Horizontal");
        float vmove =Input.GetAxis("Vertical");
        rb.velocity=new Vector3(hmove,0,vmove).normalized*speed;

        if (hmove != 0 || vmove != 0)
        {
            ani.SetBool("ismove", true);
        }
        if(hmove ==0&&vmove == 0)
        {
            ani.SetBool("ismove", false);
        }
        if(hmove > 0)
        {
            transform.localScale = new Vector3(1, 1, 1);
        }
        if (hmove < 0)
        {
            transform.localScale = new Vector3(-1, 1, 1);
        }
        if (SkillList.childCount > 0)
        {
            foreach(Transform Skill in SkillList)
            {
                Skillbase s = Skill.GetComponent<Skillbase>();
                s.player = gameObject;
                if (s.CDkey >= s.CDtime)
                {
                    StartCoroutine(s.Useskill());
                }
            }
        }
    }
    public void startturnred()
    {
        StartCoroutine(turnred());
    }

    public IEnumerator turnred()
    {
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = red;
        yield return new WaitForSeconds(0.3f);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = material;
    }

    public void death()
    {
        
    }
}
