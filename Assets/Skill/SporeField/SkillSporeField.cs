using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 孢子领域技能：每隔 CDtime 秒，对攻击范围内所有敌人同时生成毒孢子造成伤害。
/// 绿色圆圈标注攻击范围。
///
/// Skillbase 字段对应：
/// - CDtime       : 冷却时间
/// - damage       : 攻击伤害
/// - attackRadius : 攻击范围（在本脚本里单独声明）
/// </summary>
public class SkillSporeField : Skillbase
{
    [Header("孢子领域专属")]
    public float attackRadius = 8f;

    [Header("范围圆圈")]
    public int   circleSegments = 64;
    public Color circleColor    = new Color(0f, 1f, 0.2f, 0.4f); // 绿色

    private LineRenderer _circle;
    private float        _lastRadius = -1f;

    private void Start()
    {
        GameObject circleObj = new GameObject("SporeRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop             = true;
        _circle.useWorldSpace    = false;
        _circle.widthMultiplier  = 0.08f;
        _circle.positionCount    = circleSegments;
        _circle.material         = new Material(Shader.Find("Sprites/Default"));
        _circle.startColor       = circleColor;
        _circle.endColor         = circleColor;

        DrawCircle();
    }

    private void Update()
    {
        if (player != null)
            transform.position = player.transform.position;

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

        foreach (Transform target in targets)
        {
            if (target == null) continue;

            // 跳过已死亡的敌人
            enemy en = target.GetComponent<enemy>();
            if (en != null && en.rolestate == enemy.state.dead) continue;

            // 在敌人位置生成孢子子弹
            GameObject spore = Instantiate(bullet, target.position, Quaternion.identity);
            BulletSporeField b = spore.GetComponent<BulletSporeField>();
            if (b != null)
            {
                b.damage      = damage;
                b.targetEnemy = en;
                b.playerAttr  = player.GetComponent<Attribute>();
            }
        }

        yield break;
    }

    private List<Transform> GetEnemiesInRange()
    {
        List<Transform> result = new List<Transform>();
        Transform enemylayer = GameObject.Find("enemylayer")?.transform;
        if (enemylayer == null) return result;

        foreach (Transform e in enemylayer)
        {
            enemy en = e.GetComponent<enemy>();
            if (en != null && en.rolestate == enemy.state.dead) continue;

            float dist = Vector3.Distance(player.transform.position, e.position);
            if (dist <= attackRadius)
                result.Add(e);
        }
        return result;
    }
}
