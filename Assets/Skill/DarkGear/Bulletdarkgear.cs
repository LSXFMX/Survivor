using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bulletdarkgear : Bulletbase
{
    public float radius;//环绕半径
    public float initialangle=0;//初始环绕角度

    public override void GetFather()
    {
        // 【性能】原实现每颗齿轮子弹都做 2 次 GameObject.Find（且无 null 保护）。
        //   改为复用基类的静态缓存（EnsureStaticCache），但**不调用 base.GetFather()**——
        //   基类会锁 Rigidbody 的 Y 轴 / 抬高出生点 / 乘 size 缩放，
        //   而齿轮是 FixedUpdate 里直接写 transform.position 环绕，行为必须保持原样。
        damage = fatherskill.damage;
        level = fatherskill.level;
        lifetime = fatherskill.lifetime;
        pass = fatherskill.pass;
        speed = fatherskill.speed;
        size = fatherskill.size;
        radius = fatherskill.GetComponent<Skilldarkgear>().radius;
        ResolveCachedRefs(out player, out enemy);
        rb = GetComponent<Rigidbody>();
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
