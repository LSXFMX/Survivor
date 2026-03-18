using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bulletdarkgear : Bulletbase
{
    public float radius;//环绕半径
    public float initialangle=0;//初始环绕角度

    public override void GetFather()
    {
        damage = fatherskill.damage;
        level = fatherskill.level;
        lifetime = fatherskill.lifetime;
        pass = fatherskill.pass;
        speed = fatherskill.speed;
        size = fatherskill.size;
        radius = fatherskill.GetComponent<Skilldarkgear>().radius;
        player = GameObject.Find("playerlayer").transform.GetChild(0).GetComponent<Attribute>();
        rb = GetComponent<Rigidbody>();
        enemy = GameObject.Find("enemylayer").transform;
    }

    void FixedUpdate()
    {
        if(cango)
        {
            initialangle += speed * Time.fixedDeltaTime;
            Vector3 postion1 = player.transform.position + new Vector3(radius * Mathf.Cos(initialangle), 0, radius * Mathf.Sin(initialangle));
        
            transform.position = postion1;

            Vector3 facevector =new Vector3(Mathf.Cos(initialangle), 0,Mathf.Sin(initialangle)).normalized;
            transform.forward = new Vector3(0, 0, facevector.z + 180);
            //transform.right = facevector;
            lifetime -= Time.fixedDeltaTime; 
            if (lifetime <= 0)
            {
                Destroy();
            }
        }
    } 
}
