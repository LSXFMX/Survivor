using UnityEngine;

/// <summary>
/// 阴鱼 / 阳鱼 —— 召唤在角色身旁的伴生体，是阴/阳史莱姆技能的可视化载体。
///
/// 规格：「在角色旁边召唤一个太极中的阴鱼(阳鱼)，每隔一段时间向周围敌人
///发射大量蝌蚪状能量射弹」。
///
/// 本类只负责"一条鱼"的表现与发射：
///   • 常态：绕玩家做慢速公转+ 自身呼吸缩放 +轻微自转，像悬浮的太极鱼；
///   • 发射：由技能脚本调用 <see cref="FireVolley"/>，向四周扇形均分射出蝌蚪；
///   • 合体/分解：由 <see cref="TaijiSlimeController"/> 通过 <see cref="SetMergeProgress"/>
///     驱动位置与透明度，本类不自己决定何时合体。
///
/// 之所以把"合体插值"做成外部驱动的 0~1 进度而不是内部协程：
///   合体要求阴鱼与阳鱼**严格同步、镜像对称**地旋进中心。若两条鱼各跑一个协程，
///   帧抖动会让它们错位（一条已到中心另一条还在半路），太极图案就拼不圆。
///   由控制器统一算进度、两条鱼读同一个 t，能保证像素级对称。
/// </summary>
public class YinYangFish : MonoBehaviour
{
    [Header("极性")]
    public bool isYin = true;

    [Header("公转（常态环绕玩家）")]
    [Tooltip("绕玩家公转半径。")]
    public float orbitRadius = 1.35f;
    [Tooltip("公转角速度（度/秒）。阴阳反向旋转，形成太极的相对运动感。")]
    public float orbitSpeed = 42f;
    [Tooltip("悬浮高度。放在角色腰部而非头顶，避免挡住头部与血条。")]
    public float orbitHeight = 0.45f;
    [Tooltip("跟随玩家的插值速度。")]
    public float followLerp = 9f;

    [Header("呼吸")]
    public float breathAmplitude = 0.08f;
    public float breathFrequency = 1.1f;

    [Header("尺寸")]
    [Tooltip("鱼的世界尺寸（米）。与源图分辨率无关，由 sprite.bounds 反算缩放。")]
    public float fishWorldSize = 0.85f;
    [Tooltip("精灵Z 轴旋转（度）。源图为横躺，转 90° 让阴阳鱼竖立。")]
    public float fishZRotation = 90f;

    private Transform _owner;
    private SpriteRenderer _sr;
    private float _orbitAngle;
    private float _breathPhase;
    private float _mergeT;          // 0=常态分离, 1=完全合体于中心
    private Vector3 _mergeCenter;
    private bool _hasMergeCenter;
    /// <summary>把 1024px 源图换算到 fishWorldSize 米的基准缩放系数。</summary>
    private float _baseScale = 1f;

    /// <summary>当前的世界位置目标（供控制器读取，用于对齐太极本体）。</summary>
    public Vector3 CurrentOrbitTarget { get; private set; }

    public void Setup(Transform owner, bool yin, float startAngleDeg)
    {
        _owner = owner;
        isYin = yin;
        _orbitAngle = startAngleDeg;
        _breathPhase = Random.Range(0f, Mathf.PI * 2f);

        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null)
        {
            var go = new GameObject("FishSprite");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
        }
        _sr.sprite = SlimeFactionAssets.FishOf(isYin);
        _sr.sortingOrder = 88;

        // 声明式尺寸：告诉它"我要 0.85 米宽"，而不是手调一个和分辨率绑死的倍率
        _baseScale = SlimeFactionAssets.WorldSizeScale(_sr.sprite, fishWorldSize);
        _sr.transform.localScale = Vector3.one * _baseScale;

        // 阴阳反向公转
        if (!isYin) orbitSpeed = -Mathf.Abs(orbitSpeed);
        else orbitSpeed = Mathf.Abs(orbitSpeed);
    }

    /// <summary>
    /// 设置合体进度。0=正常公转；1=完全收拢到center。
    /// 合体过程中同时：半径收缩 → 0、公转加速（旋进感）、透明度淡出。
    /// </summary>
    public void SetMergeProgress(float t, Vector3 center)
    {
        _mergeT = Mathf.Clamp01(t);
        _mergeCenter = center;
        _hasMergeCenter = true;
    }

    /// <summary>完全隐藏（合体完成后由控制器接管显示太极本体）。</summary>
    public void SetVisible(bool visible)
    {
        if (_sr != null) _sr.enabled = visible;
    }

    private void Update()
    {
        if (_owner == null) return;

        float dt = Time.deltaTime;

        // 合体时公转加速，制造"旋进漩涡"观感
        float speedMul = 1f + _mergeT * 5f;
        _orbitAngle += orbitSpeed * speedMul * dt;

        // 半径随合体进度收缩到 0
        float r = orbitRadius * (1f - _mergeT);

        Vector3 pivot = _hasMergeCenter && _mergeT > 0f
            ? Vector3.Lerp(_owner.position, _mergeCenter, _mergeT)
            : _owner.position;

        float rad = _orbitAngle * Mathf.Deg2Rad;
        Vector3 target = pivot + new Vector3(
            Mathf.Cos(rad) * r,
            orbitHeight,
            Mathf.Sin(rad) * r * 0.6f); // z 压扁 0.6，贴合 45° 俯视透视

        CurrentOrbitTarget = target;

        // 合体时直接吸附（不插值），避免"该到位了却还在追"导致太极拼不圆
        transform.position = _mergeT > 0.01f
            ? target
            : Vector3.Lerp(transform.position, target, Mathf.Clamp01(followLerp * dt));

        // 【2026-08 修复】Z 轴+90°：源图里阴/阳鱼是**横躺**的（阴阳分界线水平），
        //而太极的阴阳鱼本应竖立（分界线垂直）。这里整体转 90° 立起来。
        transform.rotation = Quaternion.Euler(45f, 0f, fishZRotation);

        // 呼吸缩放 + 合体时整体缩小（都叠在 _baseScale 之上，保持绝对尺寸语义）
        _breathPhase += dt * breathFrequency * Mathf.PI * 2f;
        float breath = 1f + Mathf.Sin(_breathPhase) * breathAmplitude;
        float shrink = 1f - _mergeT * 0.45f;
        if (_sr != null)
            _sr.transform.localScale = Vector3.one * (_baseScale * breath * shrink);

        // 合体后半段淡出，把画面交给太极本体
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = _mergeT < 0.6f ? 1f : Mathf.InverseLerp(1f, 0.6f, _mergeT);
            _sr.color = c;
        }
    }

    /// <summary>
    /// 齐射蝌蚪。
    ///
    /// 【2026-08 重做】旧版是"同一帧内在360° 均分 count 发"，结果就是玩家看到的
    ///   **网状乱射**：一瞬间炸出一圈星芒，大半射弹朝着没有敌人的方向白跑。
    ///
    /// 新版三条改动：
    ///   1. **逐发间隔**：用协程按 shotInterval 依次发射，形成"哒哒哒"的连射节奏；
    ///   2. **朝敌人打**：每一发从"射程内的存活敌人"里挑一个作为目标
    ///      （优先最近的几只，轮着分配），只加很小的随机散布；
    ///      完全没有敌人时才退化为扇形散射当作待机表演；
    ///   3. **携带追踪目标**：把选定目标交给 BulletTadpole，由它限速转向咬住。
    /// </summary>
    public void FireVolley(int count, int damage, float bulletSpeed, float bulletLifetime,
                           int bulletPass, GameObject bulletTemplate,
                           Attribute playerAttr, Transform enemyLayer,
                           System.Collections.Generic.List<Transform> targets,
                           float maxTravel, float shotInterval = 0.06f)
    {
        if (bulletTemplate == null || count <= 0) return;

        // 目标列表要复制一份：调用方传进来的是 SkillYinYangSlime 内部复用的
        // _reuseTargets（每次搜敌都会 Clear），协程跨帧执行期间会被覆盖。
        var snapshot = new System.Collections.Generic.List<Transform>();
        if (targets != null)
        {
            for (int i = 0; i < targets.Count && i < 12; i++)
                if (targets[i] != null) snapshot.Add(targets[i]);
        }

        StartCoroutine(VolleyRoutine(count, damage, bulletSpeed, bulletLifetime, bulletPass,
                bulletTemplate, playerAttr, enemyLayer, snapshot,
                                     maxTravel, shotInterval));
    }

    private System.Collections.IEnumerator VolleyRoutine(
        int count, int damage, float bulletSpeed, float bulletLifetime, int bulletPass,
        GameObject bulletTemplate, Attribute playerAttr, Transform enemyLayer,
        System.Collections.Generic.List<Transform> targets,
        float maxTravel, float shotInterval)
    {
        for (int i = 0; i < count; i++)
        {
            // 鱼自己或模板被销毁（技能被遗忘 / 场景切换）→ 立刻停火
            if (this == null || bulletTemplate == null) yield break;

            Vector3 origin = transform.position;

            //轮流分配目标：第 i 发打第 (i % 目标数) 个敌人。
            // 这样多发不会全挤在同一只身上，也不需要额外的分配算法。
            Transform tgt = null;
            for (int k = 0; k < targets.Count; k++)
            {
                Transform cand = targets[(i + k) % targets.Count];
                if (cand == null || !cand.gameObject.activeInHierarchy) continue;
                enemy ce = cand.GetComponent<enemy>();
                if (ce == null || ce.health <= 0 || ce.rolestate == enemy.state.dead) continue;
                if (ce._mindControlledFlag) continue;
                tgt = cand;
                break;
            }

            Vector3 dir;
            if (tgt != null)
            {
                dir = tgt.position - origin; dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) dir = Vector3.right;
                dir.Normalize();
                // 很小的散布（±8°）：保留"一群蝌蚪"的自然感，又不至于打空
                float jitter = Random.Range(-8f, 8f) * Mathf.Deg2Rad;
                dir = new Vector3(
                    dir.x * Mathf.Cos(jitter) - dir.z * Mathf.Sin(jitter), 0f,
                    dir.x * Mathf.Sin(jitter) + dir.z * Mathf.Cos(jitter));
            }
            else
            {
                // 无敌人：沿公转切线扇形散开，纯表演
                float a = (_orbitAngle + 90f + i * 24f) * Mathf.Deg2Rad;
                dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }

            GameObject go = Instantiate(bulletTemplate, origin, Quaternion.identity);
            go.SetActive(true);
            BulletTadpole tp = go.GetComponent<BulletTadpole>();
            if (tp == null) tp = go.AddComponent<BulletTadpole>();

            tp.isYin     = isYin;
            tp.damage    = damage;
            tp.speed     = bulletSpeed;
            tp.lifetime  = bulletLifetime;
            tp.pass      = bulletPass;
            tp.maxTravel = maxTravel;
            tp.Launch(dir, playerAttr, enemyLayer, tgt);

            if (shotInterval > 0f) yield return new WaitForSeconds(shotInterval);
        }
    }
}
