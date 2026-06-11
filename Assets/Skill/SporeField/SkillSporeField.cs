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
    // 暗影岛/莫德凯撒系暗深邃绿色（替代原本的亮绿，与亡者领域复活遮罩同色系）
    public Color circleColor    = new Color(0.18f, 0.55f, 0.32f, 0.55f);

    // 亡者领域解锁后用的紫色（暗影岛"幽冥紫"）
    public static readonly Color TombDomainCircleColor = new Color(0.55f, 0.25f, 0.85f, 0.65f);
    // 亡者领域解锁后强制锁定的孢子领域半径
    public const float TombDomainLockedRadius = 10f;

    /// <summary>
    /// 是否已被亡者领域锁定（半径=10、紫色、且不再被 skillupgrade 改半径）。
    /// </summary>
    public bool IsLockedByTombDomain { get; private set; }

    private LineRenderer _circle;
    private float        _lastRadius = -1f;
    private Color        _lastCircleColor;
    private float        _tombProbeAccum; // 节流：每 0.5s 才探测一次"是否已学亡者领域"

    private void Start()
    {
        // === Bug 修复："显示分身攻击距离" 按钮无效 ===
        // 分身由 Instantiate(主玩家) 克隆而来，prefab snapshot 里已有 Start 第一次创建的
        // "SporeRangeCircle" 子物体；clone 再次 Start 又新建一个，造成两个圈，旧圈未注册到
        // AttackRangeIndicatorManager → toggle 不能控制它 → 看似切换无效。详见 SkillWindArrow.Start。
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && c.name == "SporeRangeCircle") Destroy(c.gameObject);
        }
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
        _lastCircleColor         = circleColor;

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, GetComponentInParent<Player>());
    }

    private void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
    }

    private void Update()
    {
        if (player != null)
            transform.position = player.transform.position;

        // 亡者领域已学习：强制把孢子领域锁定为半径 10 + 紫色。
        // 主路径已在 getnewskill_TombDomain.chocieupgrade 学完时直接调用 LockToTombDomainPalette()，
        // 这里仅作为兜底；用 0.5s 节流，避免每帧 GetComponent + 遍历 SkillList 造成卡顿。
        if (!IsLockedByTombDomain && player != null)
        {
            _tombProbeAccum += Time.deltaTime;
            if (_tombProbeAccum >= 0.5f)
            {
                _tombProbeAccum = 0f;
                Player p = player.GetComponent<Player>();
                if (p != null && SkillTombDomain.ResolveOnPlayer(p) != null)
                {
                    LockToTombDomainPalette();
                }
            }
        }

        if (!Mathf.Approximately(_lastRadius, attackRadius))
            DrawCircle();

        if (_circle != null && _lastCircleColor != circleColor)
        {
            _lastCircleColor = circleColor;
            _circle.startColor = circleColor;
            _circle.endColor   = circleColor;
        }
    }

    /// <summary>
    /// 亡者领域解锁后调用：把孢子领域固定为半径 10、紫色，且锁定不再被升级改半径。
    /// </summary>
    public void LockToTombDomainPalette()
    {
        IsLockedByTombDomain = true;
        attackRadius = TombDomainLockedRadius;
        circleColor  = TombDomainCircleColor;
        if (_circle != null)
        {
            _circle.startColor = circleColor;
            _circle.endColor   = circleColor;
            _lastCircleColor   = circleColor;
        }
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

    // enemylayer 静态缓存：每次释放孢子领域都做 GameObject.Find 是历史遗留，
    // 多个孢子领域玩家 / 高 CD 减少时（每秒释放数次）会显著吃帧。
    private static Transform _enemyLayerCached;
    private static Transform GetEnemyLayer()
    {
        if (_enemyLayerCached == null)
        {
            GameObject go = GameObject.Find("enemylayer");
            if (go != null) _enemyLayerCached = go.transform;
        }
        return _enemyLayerCached;
    }

    private List<Transform> GetEnemiesInRange()
    {
        List<Transform> result = new List<Transform>();
        Transform enemylayer = GetEnemyLayer();
        if (enemylayer == null) return result;

        Vector3 center = player.transform.position;
        float radiusSq = attackRadius * attackRadius;
        int n = enemylayer.childCount;
        for (int i = 0; i < n; i++)
        {
            Transform e = enemylayer.GetChild(i);
            if (e == null) continue;
            enemy en = e.GetComponent<enemy>();
            if (en == null) continue;
            if (en.rolestate == enemy.state.dead) continue;
            // 亡者领域：不伤害已经被控制为友军的敌人（直接读 flag，省 GetComponent）
            if (en._mindControlledFlag) continue;

            Vector3 d = e.position - center;
            // 平面距离：场景是 XZ 平面，孢子领域是圆，y 差异忽略
            float sq = d.x * d.x + d.z * d.z;
            if (sq <= radiusSq) result.Add(e);
        }
        return result;
    }
}
