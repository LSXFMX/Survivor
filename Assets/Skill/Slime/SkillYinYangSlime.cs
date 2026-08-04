using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 阴史莱姆 / 阳史莱姆 —— 史莱姆社群好感度技能。
///
/// 一个类支撑两个技能：靠 <see cref="isYin"/> 区分极性，Skillname 分别为
/// "阴史莱姆" / "阳史莱姆"。规格要求"技能学习卡不共享（两个独立），但升级卡共享"，
/// 因此必须是两个独立的 Skillbase 实例（各自有 Skillname，各自被 PlayerHasSkill 识别），
/// 而不是一个技能内部带两条鱼。
///
/// 行为：
///   • 单独持有时：在玩家身旁召唤 1 条对应极性的鱼，每 CDtime 秒齐射 number 发蝌蚪。
///   • 同时持有阴+阳时：由 <see cref="TaijiSlimeController"/> 接管，替换为「太极史莱姆」，
///     两条鱼合体/分解并轮流使用「太极印」与「双色齐射」两种攻击方式。
///     此时本技能的 Useskill() 会被控制器抑制（IsSuppressedByTaiji），避免
///     两套逻辑同时开火导致伤害翻倍。
///
/// 世界 Boss 好感度加成（WorldBossManager.ApplySlimeSlimeBonus）：
///   ≥40 CDtime × 0.8 ／ ≥60 attackRadius +10 ／ ≥80 number +5
/// </summary>
public class SkillYinYangSlime : Skillbase
{
    [Header("阴/阳史莱姆 专属")]
    [Tooltip("true=阴史莱姆（黑色蝌蚪），false=阳史莱姆（白色蝌蚪）")]
    public bool isYin = true;

    [Tooltip("搜敌 / 射弹覆盖半径。也是范围圈的半径。")]
    public float attackRadius = 9f;

    [Tooltip("逐发射弹的间隔（秒）。0 = 同帧齐射。默认 0.06s 形成连射节奏，而非一次性炸开。")]
    public float shotInterval = 0.06f;

    [Header("范围圆圈")]
    public int circleSegments = 48;

    private LineRenderer _circle;
    private float _lastRadius = -1f;
    private Transform _ownerTransform;
    private YinYangFish _fish;
    private GameObject _bulletTemplate;

    /// <summary>本技能召唤的鱼（供 TaijiSlimeController 取用做合体）。</summary>
    public YinYangFish Fish => _fish;

    /// <summary>
    /// 是否被太极史莱姆接管。被接管时本技能不再自己发射，
    /// 但仍然保留鱼实体（由控制器驱动合体动画）与范围圈。
    /// </summary>
    public bool IsSuppressedByTaiji { get; set; }

    private static Transform s_playerLayerCache;
    private static Transform s_enemyLayerCache;

    // 搜敌复用容器，避免每次施放new List 产生 GC
    private readonly List<Transform> _reuseTargets = new List<Transform>(32);
    private readonly List<float> _reuseDistSq = new List<float>(32);

    private void Start()
    {
        if (string.IsNullOrEmpty(Skillname))
            Skillname = isYin ? SlimeFactionAssets.SKILL_YIN : SlimeFactionAssets.SKILL_YANG;

        // 首次施放缩短前摇，与 SkillBloodline / SkillParasite 一致：
        // 开局约 1s 就能看到第一轮齐射，避免"学了技能好久没反应"。
        if (level <= 1 && CDtime > 1f)
        {
            CDtime = 1f;
            CDkey = 1f;
        }

        if (icon == null) icon = SlimeFactionAssets.IconOf(isYin);

        ResolveOwnerPlayer();
        BuildBulletTemplate();

        // 清理 prefab snapshot / 分身克隆可能残留的旧范围圈。
        // 与 SkillWindArrow / SkillBloodline 同一个坑：分身由 Instantiate(主玩家) 克隆而来，
        // snapshot 里已带上一次 Start 创建的子物体，不清会出现两个圈。
        // （鱼不在子物体里—— 它挂在场景根，由 OnDestroy 负责回收。）
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && c.name == "SlimeRangeCircle") Destroy(c.gameObject);
        }

        CreateRangeCircle();
        EnsureFish();
    }

    private void CreateRangeCircle()
    {
        GameObject circleObj = new GameObject("SlimeRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop = true;
        _circle.useWorldSpace = false;
        _circle.widthMultiplier = 0.06f;
        _circle.positionCount = circleSegments;
        _circle.material = new Material(Shader.Find("Sprites/Default"));

        Color c = SlimeFactionAssets.ColorOf(isYin);
        c.a = 0.32f;
        _circle.startColor = c;
        _circle.endColor = c;

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, GetComponentInParent<Player>());
    }

    /// <summary>
    /// 蝌蚪射弹模板。运行时构造而非用 .prefab 资源：
    /// 本社群全部走代码注册（与狼人社群同思路），不依赖场景/资源配置，
    /// 避免"prefab 引用丢失导致技能静默失效"这类难查问题。
    /// 模板保持 inactive，只作为 Instantiate 的蓝本。
    /// </summary>
    private void BuildBulletTemplate()
    {
        if (_bulletTemplate != null) return;

        // 若外部（Inspector / 注册器）已经指定了 bullet，优先用它
        if (bullet != null) { _bulletTemplate = bullet; return; }

        GameObject go = new GameObject(isYin ? "TadpoleTemplate_Yin" : "TadpoleTemplate_Yang");
        go.transform.SetParent(transform, false);
        go.SetActive(false);

        var sprGo = new GameObject("Sprite");
        sprGo.transform.SetParent(go.transform, false);
        var sr = sprGo.AddComponent<SpriteRenderer>();
        sr.sprite = SlimeFactionAssets.TadpoleOf(isYin);
        sr.sortingOrder = 90;
        // 蝌蚪目标世界尺寸 0.45 米：够醒目但不会几十发糊满屏幕。
        // 用绝对尺寸换算而非硬编码倍率，理由见 SlimeFactionAssets.FitSpriteToWorldSize。
        SlimeFactionAssets.FitSpriteToWorldSize(sr, 0.45f);

        go.AddComponent<BulletTadpole>();

        _bulletTemplate = go;
        bullet = go;
    }

    /// <summary>确保伴生鱼存在（被销毁 / 首次创建时补齐）。</summary>
    private void EnsureFish()
    {
        if (_fish != null || _ownerTransform == null) return;

        // 【关键】鱼必须挂在**场景根**，绝不能作为技能（→SkillList→Player）的子物体。
        //   Player 靠 transform.localScale.x = -modelScale 翻转朝向（见 Player.cs），
        //   一旦作为子物体，玩家每次向左走都会把鱼整体镜像：
        //   sprite 翻转、公转方向反向、合体时阴阳错位拼不成太极。
        //   SkillBloodline 的使魔同样是 Instantiate 到根节点，这里保持一致。
        GameObject root = new GameObject(isYin ? "YinFish" : "YangFish");
        root.transform.position = _ownerTransform.position;

        _fish = root.AddComponent<YinYangFish>();
        // 阴从 180° 起，阳从 0° 起 —— 初始就呈对角，合体时能对称旋进
        _fish.Setup(_ownerTransform, isYin, isYin ? 180f : 0f);
    }

    private void Update()
    {
        ResolveOwnerPlayer();
        if (_ownerTransform != null)
            transform.position = _ownerTransform.position;

        if (!Mathf.Approximately(_lastRadius, attackRadius))
            DrawCircle();

        EnsureFish();
    }

    private void DrawCircle()
    {
        if (_circle == null) return;
        _lastRadius = attackRadius;
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = i * 2f * Mathf.PI / circleSegments;
            _circle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * attackRadius, 0f, Mathf.Sin(angle) * attackRadius));
        }
    }

    public override IEnumerator Useskill()
    {
        // 太极史莱姆已接管 → 本技能不单独开火（否则同一份number 会被打两遍）
        if (IsSuppressedByTaiji) { CDkey = 0; yield break; }

        ResolveOwnerPlayer();
        if (_ownerTransform == null) { CDkey = 0; yield break; }

        EnsureFish();
        if (_fish == null) { CDkey = 0; yield break; }

        // 【2026-08 修复】攻击范围此前形同虚设：不管范围内有没有敌人都照常齐射，
        //   而射弹又没有射程限制，于是"打全图"。
        //   现在改为：范围内没有活敌人就**不施放、不消耗冷却**。
        //
        //   但不能直接 return —— Player 每帧检测 `CDkey >= CDtime` 就会调Useskill，
        //   若 CDkey 保持满值，会变成每帧启动一次协程 + 每帧全场搜敌（600敌人时很浪费）。
        //   所以把 CDkey 回退一点点，形成 RetryDelay 秒的重试节流：
        //   既能在敌人进圈后很快开火，又不会空转。
        var targets = GetEnemiesInRangeSorted();
        if (targets.Count == 0)
        {
            CDkey = Mathf.Max(0f, CDtime - RetryDelay);
            yield break;
        }

        CDkey = 0;
        PlayCastSfx();

        Attribute attr = _ownerTransform.GetComponent<Attribute>();

        _fish.FireVolley(
            count: Mathf.Max(1, number),
            damage: damage,
            bulletSpeed: speed > 0.1f ? speed : 12f,
            bulletLifetime: lifetime > 0.1f ? lifetime : 2.2f,
            bulletPass: pass,
            bulletTemplate: _bulletTemplate,
            playerAttr: attr,
            enemyLayer: ResolveEnemyLayer(),
            targets: targets,
            maxTravel: BulletMaxTravel,
            shotInterval: shotInterval);

        yield break;
    }

    /// <summary>
    /// 射弹最大飞行距离 = 攻击范围 × 1.5。
    /// 这样"范围圈"既是索敌边界，也大致等于火力覆盖边界（留1.5 倍余量是为了
    /// 让刚好站在圈边、或在射弹飞行途中往外跑的敌人仍能被追到，不至于总是差一点）。
    /// </summary>
    public float BulletMaxTravel => attackRadius * 1.5f;

    /// <summary>范围内没有敌人时的重试间隔（秒）。</summary>
    private const float RetryDelay = 0.12f;

    /// <summary>
    /// 搜敌结果最多保留的目标数。
    /// FireVolley 内部也只取前 12 个，太极印每次只用最近 1 个，
    /// 因此没必要把圈内几百只怪全排一遍 —— 见 GetEnemiesInRangeSorted 的插入上限说明。
    /// </summary>
    private const int MaxTrackedTargets = 12;

    /// <summary>找attackRadius 内最近的敌人（不含亡者领域友军）。</summary>
    public Transform FindNearestEnemy()
    {
        Transform layer = ResolveEnemyLayer();
        if (layer == null || _ownerTransform == null) return null;

        Vector3 origin = _ownerTransform.position;
        float rSq = attackRadius * attackRadius;
        float bestSq = float.MaxValue;
        Transform best = null;

        int cnt = layer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = layer.GetChild(i);
            if (t == null) continue;

            // 先算距离再GetComponent（同 GetEnemiesInRangeSorted 的性能考量）
            Vector3 d = t.position - origin; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > rSq || sq >= bestSq) continue;

            enemy en = t.GetComponent<enemy>();
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;
            if (en._mindControlledFlag) continue;

            bestSq = sq;
            best = t;
        }
        return best;
    }

    /// <summary>
    /// 取 attackRadius 内的敌人，按距离升序，**最多 <see cref="MaxTrackedTargets"/> 个**。
    ///
    /// 两点性能考量（这个方法在无目标时每 0.12s 就会跑一次，敌人多时是热路径）：
    ///   ① 先算平方距离、只对进圈的候选者做 GetComponent
    ///      —— 蝙蝠潮 600 敌人时能把 GetComponent 从 600 次/调用降到十几次；
    ///   ② 插入排序限长到 12。原实现是无上限插入排序，若圈内有几百只怪就是
    ///      O(n²) ≈ 数十万次比较/调用，而调用方（FireVolley 取前 12、太极印取第 1）
    ///      根本用不到那么多，纯属浪费。
    /// </summary>
    public List<Transform> GetEnemiesInRangeSorted()
    {
        _reuseTargets.Clear();
        _reuseDistSq.Clear();

        Transform layer = ResolveEnemyLayer();
        if (layer == null || _ownerTransform == null) return _reuseTargets;

        Vector3 origin = _ownerTransform.position;
        float rSq = attackRadius * attackRadius;

        int cnt = layer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = layer.GetChild(i);
            if (t == null) continue;

            // ① 先做纯 Transform 距离筛选
            Vector3 d = t.position - origin; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > rSq) continue;

            // ② 列表已满且这只比现有最远的还远 → 直接丢弃，连GetComponent 都不做
            if (_reuseTargets.Count >= MaxTrackedTargets &&
                sq >= _reuseDistSq[_reuseTargets.Count - 1]) continue;

            enemy en = t.GetComponent<enemy>();
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;
            if (en._mindControlledFlag) continue;

            int idx = _reuseTargets.Count;
            for (int j = 0; j < _reuseTargets.Count; j++)
                if (sq < _reuseDistSq[j]) { idx = j; break; }

            _reuseTargets.Insert(idx, t);
            _reuseDistSq.Insert(idx, sq);

            // 限长：超出就砍掉最远的那个
            if (_reuseTargets.Count > MaxTrackedTargets)
            {
                _reuseTargets.RemoveAt(_reuseTargets.Count - 1);
                _reuseDistSq.RemoveAt(_reuseDistSq.Count - 1);
            }
        }
        return _reuseTargets;
    }

    public GameObject BulletTemplate
    {
        get { BuildBulletTemplate(); return _bulletTemplate; }
    }

    public Transform OwnerTransform
    {
        get { ResolveOwnerPlayer(); return _ownerTransform; }
    }

    public static Transform ResolveEnemyLayer()
    {
        if (s_enemyLayerCache == null)
        {
            var go = GameObject.Find("enemylayer");
            s_enemyLayerCache = go != null ? go.transform : null;
        }
        return s_enemyLayerCache;
    }

    private void ResolveOwnerPlayer()
    {
        if (player != null)
        {
            _ownerTransform = player.transform;
            return;
        }

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
            if (t != null && t.CompareTag("Player")) { picked = t; break; }
        }
        if (picked == null) picked = layer.GetChild(0);
        if (picked == null) return;

        _ownerTransform = picked;
        player = picked.gameObject;
    }

    /// <summary>场景重载时清空静态缓存（fake-null 残留会让技能拿不到 layer）。</summary>
    public static void ResetStaticCaches()
    {
        s_playerLayerCache = null;
        s_enemyLayerCache = null;
    }

    private void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
        if (_fish != null) Destroy(_fish.gameObject);
    }
}
