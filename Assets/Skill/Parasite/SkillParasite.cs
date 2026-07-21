using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 命途:寄生 —— 狼人社群「月牙吊坠」(FavorEquipment 6) 觉醒后可学习的技能。
///
/// 表现：从玩家出发，向 <c>number</c> 位最近的敌人各发射一根「寄生触手」（像动漫寄生兽，手指像橡胶
/// 一样伸出，尖端是狼爪），命中造成伤害并附带 1% 自吸血，随后触手缩回玩家。
///
/// 装备联动（由 <see cref="BulletParasite"/> 运行时读取）：
///   - FavorEquipment 6 「月牙吊坠」   : 学习资格 + 好感度≥100 时 EquipmentInitializer 开局注入
///   - FavorEquipment 7 「寄生的暗种」 : 命中后向 3m 内另一位敌人再攻击一次
///   - FavorEquipment 8 「红月分身」   : EquipmentInitializer 开局 <c>number += 1</c> + 生成宠物
///
/// 世界 Boss 奖励叠加（由 WorldBossManager.ApplyParasiteBonus）：
///   - 好感度≥40 : CDtime × 0.8
///   - 好感度≥60 : attackRadius +10
///   - 好感度≥80 : number +1
///
/// 结构参照 SkillBloodline，字段 CDtime / damage / number / attackRadius / lifestealRatio。
/// </summary>
public class SkillParasite : Skillbase
{
    [Header("命途:寄生 专属")]
    [Tooltip("寄生触手的搜敌半径 / 最远伸出距离。")]
    public float attackRadius = 6f;

    [Tooltip("命中敌人后自带的固定吸血比例（默认 1%，与文案 \"自带 1% 吸血\" 对齐）。")]
    [Range(0f, 1f)]
    public float lifestealRatio = 0.01f;

    [Header("范围圆圈")]
    public int   circleSegments = 48;
    public Color circleColor    = new Color(0.9f, 0.15f, 0.2f, 0.35f);

    private LineRenderer _circle;
    private float _lastRadius = -1f;
    private Transform _ownerTransform;

    // 【性能】静态缓存 enemylayer + 施放临时 List/距离数组，避免每次 Useskill 时 FindObjectsOfType + New List
    private static Transform s_enemyLayerCache;
    private readonly List<Transform> _reuseTargets = new List<Transform>(32);
    private readonly List<float>     _reuseDistSq = new List<float>(32);

    /// <summary>
    /// 玩家当前是否已装备「寄生的暗种」(FavorEquipment 7)，即触手命中后需要弹射一次。
    /// 与 SkillBloodline.HasFavorEquipment004 同套路：装备已解锁 + 好感度门槛已到才生效。
    /// </summary>
    public bool HasBounceFromEquipment()
    {
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 7))
            return false;
        if (FavorManager.Instance == null)
            return UnityEngine.PlayerPrefs.GetInt("Favor_Wolf", 0) >= 50;
        return FavorManager.Instance.GetFavor(FactionType.Wolf) >= 50;
    }

    private void Start()
    {
        // 首次施放缩短前摇：开局约 1s 就能感受到技能（同 SkillBloodline 做法）
        if (level <= 1 && CDtime > 1f)
        {
            CDtime = 1f;
            CDkey  = 1f;
        }

        // icon 自动从 Resources 加载（避免 prefab 时期 sprite 引用 GUID 不稳定）；
        // 走 BulletParasite.LoadSpriteFallback，兼容 Texture2D 导入情形。
        if (icon == null)
            icon = BulletParasite.LoadSpriteFallback("Wolf/icon_parasite");

        ResolveOwnerPlayer();

        // 清理 prefab snapshot 里可能残留的旧圈（分身/克隆玩家时防重复）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && c.name == "ParasiteRangeCircle") Destroy(c.gameObject);
        }

        GameObject circleObj = new GameObject("ParasiteRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop            = true;
        _circle.useWorldSpace   = false;
        _circle.widthMultiplier = 0.06f;
        _circle.positionCount   = circleSegments;
        _circle.material        = new Material(Shader.Find("Sprites/Default"));
        _circle.startColor      = circleColor;
        _circle.endColor        = circleColor;

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, GetComponentInParent<Player>());
    }

    private void Update()
    {
        ResolveOwnerPlayer();
        if (_ownerTransform != null)
            transform.position = _ownerTransform.position;

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
        ResolveOwnerPlayer();
        if (_ownerTransform == null || bullet == null) yield break;

        List<Transform> targets = GetEnemiesInRange(); // 内部走复用 List，无 GC
        if (targets.Count == 0) yield break;

        bool bounce = HasBounceFromEquipment();
        int shots   = Mathf.Max(1, number);

        // 每根触手锁定一位不同敌人（不够 number 时循环最近敌人，保证 number 不"虚数量"）
        for (int i = 0; i < shots; i++)
        {
            Transform target = targets[i % targets.Count];
            if (target == null) continue;

            GameObject go = Instantiate(bullet, _ownerTransform.position, Quaternion.identity);
            BulletParasite tentacle = go.GetComponent<BulletParasite>();
            if (tentacle == null) tentacle = go.AddComponent<BulletParasite>();

            tentacle.fatherskill = this;
            tentacle.GetFather();
            tentacle.SetupParasite(
                ownerTr:    _ownerTransform,
                enemyTr:    target,
                lifestealRatio: lifestealRatio,
                bounceOnce: bounce,
                maxRange:   attackRadius);
            tentacle.cango = true;

            if (interval > 0f)
                yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// 【性能】搜敌重构：
    ///   1. 用 enemylayer 直接子物体遍历，替代 FindObjectsOfType（后者会遍历整个场景，600+ 敌人时非常慢）
    ///   2. 用 sqrMagnitude 比距离，避免每次开根号
    ///   3. 用 List 复用池 + 并列 distSq 数组做插入排序（<= 8 个敌人时比 Sort 更快，也无闭包分配）
    ///   4. attackRadius 内敌人一般远小于总敌人数，早剪枝更省
    /// </summary>
    private List<Transform> GetEnemiesInRange()
    {
        _reuseTargets.Clear();
        _reuseDistSq.Clear();
        if (_ownerTransform == null) return _reuseTargets;

        // 静态缓存 enemylayer
        if (s_enemyLayerCache == null)
        {
            var go = GameObject.Find("enemylayer");
            s_enemyLayerCache = go != null ? go.transform : null;
        }
        Transform layer = s_enemyLayerCache;
        if (layer == null) return _reuseTargets;

        Vector3 origin = _ownerTransform.position;
        float rSq = attackRadius * attackRadius;

        int cnt = layer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = layer.GetChild(i);
            if (t == null) continue;
            enemy en = t.GetComponent<enemy>();
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;
            if (en._mindControlledFlag) continue; // 亡者领域：不打友军

            Vector3 d = t.position - origin; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > rSq) continue;

            // 插入排序：目标数一般 <= number（默认 3~4），O(n²) 完全可接受
            int idx = _reuseTargets.Count;
            for (int j = 0; j < _reuseTargets.Count; j++)
            {
                if (sq < _reuseDistSq[j]) { idx = j; break; }
            }
            _reuseTargets.Insert(idx, t);
            _reuseDistSq.Insert(idx, sq);
        }

        return _reuseTargets;
    }

    private static Transform s_playerLayerCache;

    private void ResolveOwnerPlayer()
    {
        if (player != null)
        {
            _ownerTransform = player.transform;
            return;
        }

        // 【性能】静态缓存 playerlayer，避免每帧 GameObject.Find
        if (s_playerLayerCache == null)
        {
            var go = GameObject.Find("playerlayer");
            s_playerLayerCache = go != null ? go.transform : null;
        }
        Transform layer = s_playerLayerCache;
        if (layer == null || layer.childCount == 0) return;

        Transform picked = null;
        foreach (Transform t in layer)
        {
            if (t != null && t.CompareTag("Player"))
            {
                picked = t;
                break;
            }
        }
        if (picked == null) picked = layer.GetChild(0);
        if (picked == null) return;

        _ownerTransform = picked;
        player = picked.gameObject;
    }

    private void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
    }
}
