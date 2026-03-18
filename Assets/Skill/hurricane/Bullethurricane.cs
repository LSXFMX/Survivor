using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;

public class Bullethurricane : Bulletbase
{
    public float orientation;//≥ØœÚΩ«∂»
    void FixedUpdate()
    {
        if (cango)//position∆¥–¥¥Ì¡À£®∫π
        {
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
