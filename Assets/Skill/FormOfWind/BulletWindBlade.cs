using UnityEngine;

/// <summary>
/// 风之形冲击波：仅在生成时确定水平朝向，之后直线移动；可穿透多名敌人（穿透次数来自技能 pass）。
/// </summary>
public class BulletWindBlade : Bulletbase
{
    [HideInInspector] public int overrideDamage = -1;

    Vector3 _dirXZ = Vector3.right;

    public void SetDamageOverride(int damageOverride)
    {
        overrideDamage = damageOverride;
    }

    /// <summary>世界方向，会投影到 XZ 平面并归一化。</summary>
    public void SetInitialDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 1e-6f)
            _dirXZ = Vector3.right;
        else
            _dirXZ = worldDirection.normalized;
    }

    public override void GetFather()
    {
        base.GetFather();
        if (overrideDamage >= 0)
            damage = overrideDamage;
        rb.useGravity = false;
        rb.isKinematic = false;

        ApplyMoveAndFacing();
    }

    void ApplyMoveAndFacing()
    {
        Vector3 flat = new Vector3(_dirXZ.x, 0f, _dirXZ.z);
        float s = flat.sqrMagnitude;
        if (s < 1e-6f) flat = Vector3.right;
        else flat *= 1f / Mathf.Sqrt(s);

        rb.velocity = flat * speed;
        // 预制体上刀刃/胶囊长轴为 X（Capsule direction 0），用 FromToRotation 让 +X 对齐水平飞行方向；
        // 若用 Euler(0,0,Atan2) 会把「前向 +Z」对准速度，视觉上会像横着飞。
        transform.rotation = Quaternion.FromToRotation(Vector3.right, flat);
    }

    void FixedUpdate()
    {
        if (!cango) return;

        // 直线冲击波：保持初始朝向（防止被其它力带偏）
        ApplyMoveAndFacing();

        lifetime -= Time.fixedDeltaTime;
        if (lifetime <= 0f)
            Destroy();
    }
}
