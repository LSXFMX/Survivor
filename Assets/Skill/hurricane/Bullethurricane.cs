using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class Bullethurricane : Bulletbase
{
    public float orientation;//朝向角度
    void FixedUpdate()
    {
        if (cango)//position拼写错了（汗
        {
            // 安全检查：如果 rb 未初始化（例如预制体变更导致 Rigidbody 缺失），
            // 直接跳过，与基类 Bulletbase.FixedUpdate 的防护模式保持一致。
            if (rb == null) return;

            float angle = orientation * Mathf.Deg2Rad;
            Vector3 vec = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
            rb.velocity = vec * speed;
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0)
            {
                Destroy();
            }
        }
    }


}
