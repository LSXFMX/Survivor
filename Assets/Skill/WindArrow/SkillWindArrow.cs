using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 风箭技能：在攻击范围内检测敌人，按多重数量同时发射追踪箭
/// </summary>
public class SkillWindArrow : Skillbase
{
    [Header("风箭专属")]
    public float attackRadius = 10f;

    [Header("范围圆圈")]
    public int circleSegments = 64;
    public Color circleColor = new Color(1f, 1f, 1f, 0.3f);

    private LineRenderer _circle;
    private float _lastRadius = -1f;

    private void Start()
    {
        // 创建 LineRenderer 画圆
        GameObject circleObj = new GameObject("AttackRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop = true;
        _circle.useWorldSpace = false;
        _circle.widthMultiplier = 0.05f;
        _circle.positionCount = circleSegments;
        _circle.material = new Material(Shader.Find("Sprites/Default"));
        _circle.startColor = circleColor;
        _circle.endColor = circleColor;

        DrawCircle();
    }

    private void Update()
    {
        // 跟随玩家位置
        if (player != null)
            transform.position = player.transform.position;

        // 半径变化时重绘
        if (!Mathf.Approximately(_lastRadius, attackRadius))
            DrawCircle();
    }

    private void DrawCircle()
    {
        if (_circle == null) return;
        _lastRadius = attackRadius;
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = i * 2f * Mathf.PI / circleSegments;
            _circle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * attackRadius,
                0f,
                Mathf.Sin(angle) * attackRadius));
        }
    }

    public override IEnumerator Useskill()
    {
        CDkey = 0;

        List<Transform> targets = GetEnemiesInRange();
        if (targets.Count == 0) yield break;

        int count = Mathf.Min(number, targets.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject newbullet = Instantiate(bullet, player.transform.position, Quaternion.identity);
            BulletWindArrow b = newbullet.GetComponent<BulletWindArrow>();
            b.fatherskill = this;
            b.GetFather();
            b.SetTarget(targets[i]);
            b.cango = true;
            yield return new WaitForSeconds(interval);
        }
    }

    private List<Transform> GetEnemiesInRange()
    {
        List<Transform> result = new List<Transform>();
        Transform enemylayer = GameObject.Find("enemylayer")?.transform;
        if (enemylayer == null) return result;

        foreach (Transform e in enemylayer)
        {
            float dist = Vector3.Distance(player.transform.position, e.position);
            if (dist <= attackRadius)
                result.Add(e);
        }
        return result;
    }
}
