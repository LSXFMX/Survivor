using UnityEngine;

/// <summary>
/// 风箭子弹：匀速直线追踪目标，100%命中
/// </summary>
public class BulletWindArrow : Bulletbase
{
    private Transform _target;
    private float _elapsed;
    private float _totalTime;
    private Vector3 _startPos;

    public override void GetFather()
    {
        base.GetFather();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _startPos = transform.position;
        _elapsed = 0f;

        Vector3 endPos = target != null ? target.position : transform.position + Vector3.forward * 5f;
        float dist = Vector3.Distance(
            new Vector3(_startPos.x, 0, _startPos.z),
            new Vector3(endPos.x, 0, endPos.z));
        _totalTime = dist / Mathf.Max(speed, 0.1f);
    }

    void FixedUpdate()
    {
        if (!cango || _totalTime <= 0f) return;

        _elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _totalTime);

        // 实时追踪目标位置
        Vector3 endPos = _target != null ? _target.position : transform.position;
        Vector3 newPos = Vector3.Lerp(_startPos, endPos, t);
        newPos.y = _startPos.y; // 保持Y轴不变

        // 朝向目标（2D sprite，绕Z轴）
        Vector3 dir = endPos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        transform.position = newPos;

        if (t >= 1f)
        {
            HitTarget();
            Destroy();
        }
    }

    private void HitTarget()
    {
        if (_target == null) return;
        enemy e = _target.GetComponent<enemy>();
        if (e == null || e.health <= 0) return;

        if (e.EVA > UnityEngine.Random.value * 100f) return;

        float finaldamage = damage + player.atk;
        if (player.CR > UnityEngine.Random.value * 100f)
            finaldamage *= player.CD / 100f;
        finaldamage -= e.def;

        e.health -= (int)finaldamage;
        GameObject num = Instantiate(e.atknumber, _target.position, default);
        num.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text = ((int)finaldamage).ToString();
        e.startturnred();
        if (e.health <= 0) e.Destroy1();
    }
}
