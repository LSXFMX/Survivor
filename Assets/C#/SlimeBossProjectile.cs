using UnityEngine;
using TMPro;

/// <summary>
/// 史莱姆 Boss 的子物体投射物（分裂长剑 / 分裂弓箭通用）。
/// 由 SlimeBoss.SpawnFan() 生成并 Launch()：沿固定方向直线飞行，命中范围内玩家造成伤害，
/// 支持穿透次数与生命周期。使用距离检测而非物理碰撞，稳定且无需碰撞体依赖。
/// </summary>
public class SlimeBossProjectile : MonoBehaviour
{
    [Header("飞行参数（由 Launch 覆盖）")]
    public float speed = 14f;
    public int   damage = 50;
    public float lifetime = 3.5f;
    public int   pass = 0;          // 额外穿透次数（0 = 命中一次即销毁）
    public float hitRadius = 0.9f;  // 命中判定半径（会随缩放放大）

    private Vector3 _dir = Vector3.right;
    private bool _launched = false;
    private float _baseTiltX = 45f;
    private Transform _playerlayer;
    private float _hitCooldown = 0f;
    private GameObject _atknumber;

    /// <summary>发射：设定方向/伤害/速度/生命周期，并激活飞行。</summary>
    public void Launch(Vector3 dir, int dmg, float spd, float life)
    {
        dir.y = 0f;
        _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.right;
        damage = dmg; speed = spd; lifetime = life;
        _launched = true;

        _baseTiltX = transform.rotation.eulerAngles.x;
        FaceDir();
    }

    void Awake()
    {
        GameObject pl = GameObject.Find("playerlayer");
        if (pl != null) _playerlayer = pl.transform;
        // 备份伤害数字预制体（从任意敌人身上借用不可靠，改由自身可选字段/查找）
    }

    private void FaceDir()
    {
        // Z 旋转 = 方向角。angle=180°（向左）时旋转180°，月牙C自然从开口朝右翻转为开口朝左
        // （与箭矢相同的Z旋转逻辑，保持一致）
        float angle = Mathf.Atan2(_dir.z, _dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(_baseTiltX, 0f, angle);
    }

    void FixedUpdate()
    {
        if (!_launched) return;
        float dt = Time.fixedDeltaTime;

        transform.position += _dir * speed * dt;

        lifetime -= dt;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        if (_hitCooldown > 0f) { _hitCooldown -= dt; return; }
        if (_playerlayer == null) return;

        float r = hitRadius * Mathf.Max(0.3f, Mathf.Abs(transform.lossyScale.y));
        foreach (Transform p in _playerlayer)
        {
            var pl = p.GetComponent<Player>();
            if (pl == null || pl.health <= 0) continue;
            if (Vector3.Distance(p.position, transform.position) > r) continue;

            // 冲刺无敌
            if (pl.IsDashInvincibleActive) continue;
            // 闪避
            if (pl.EVA > Random.value * 100f) { ShowMiss(p.position); continue; }

            int dmg = Mathf.Max(1, damage - (int)pl.def);
            pl.health -= dmg;
            ShowDamage(p.position, dmg);
            pl.startturnred();
            if (pl.health <= 0) pl.death();

            pass -= 1;
            _hitCooldown = 0.15f;
            if (pass < 0) { Destroy(gameObject); return; }
        }
    }

    private void EnsureAtkNumber()
    {
        if (_atknumber != null || _playerlayer == null) return;
        // 借用玩家层第一个 Player 的伤害数字不现实；改从场景内任意敌人借用一次
        var anyEnemy = FindObjectOfType<enemy>();
        if (anyEnemy != null) _atknumber = anyEnemy.atknumber;
    }

    private void ShowDamage(Vector3 pos, int dmg)
    {
        if (!DamageNumberSettings.Visible) return;
        EnsureAtkNumber();
        if (_atknumber == null) return;
        GameObject n = Instantiate(_atknumber, pos, default);
        n.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = dmg.ToString();
    }

    private void ShowMiss(Vector3 pos)
    {
        EnsureAtkNumber();
        if (_atknumber != null) MissNumber.Show(_atknumber, pos);
    }
}
