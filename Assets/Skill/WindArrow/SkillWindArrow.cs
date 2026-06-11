using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 风箭技能：在攻击范围内检测敌人，按多重数量同时发射追踪箭
/// </summary>
public class SkillWindArrow : Skillbase
{
    [Header("风箭专属")]
    // 序列化默认 = 10（与 prefab WindArrowskill.prefab 同步）。
    // 仅南筱风（SKIN_NANXIAOFENG）开局走 Start 分支拉到 20；
    // 无罪（SKIN_TOMB）开局走 PlayerSkinSkillBuff.ApplyTombBuffStats 拉到 20。
    public float attackRadius = 10f;

    [Header("范围圆圈")]
    public int circleSegments = 64;
    public Color circleColor = new Color(1f, 1f, 1f, 0.3f);

    // 亡者领域解锁后用的紫色（与 SkillSporeField 同色系，呼应 UR 主题）
    public static readonly Color TombDomainCircleColor = new Color(0.55f, 0.25f, 0.85f, 0.65f);
    // 亡者领域解锁后强制锁定的风箭半径
    public const float TombDomainLockedRadius = 10f;

    /// <summary>
    /// 是否已被亡者领域锁定（半径=10、紫色、且不再被升级改半径）。
    /// </summary>
    public bool IsLockedByTombDomain { get; private set; }

    private LineRenderer _circle;
    private float _lastRadius = -1f;
    private Color _lastCircleColor;
    private float _tombProbeAccum;

    private void Start()
    {
        // ===== 角色相关默认范围调整（2026-06 重写：默认 10，南筱风专属 20）=====
        // 旧版策略：南筱风走 prefab 默认值（15），其他角色压到 10。
        // 新版策略：
        //   - 默认所有角色开局风箭范围 = 10
        //   - 仅"南筱风"（SKIN_NANXIAOFENG=1）特例为 20，体现"风系角色对风箭天然加成"
        //   - 无罪（SKIN_TOMB=3）不在此处覆写：由 PlayerSkinSkillBuff.ApplyTombBuffStats
        //     在下一帧把 attackRadius 拉到 TOMB_INITIAL_ATTACK_RADIUS（=20）；
        //     这里若强行写 10 也不影响后续 Buff 覆写，但为保持各角色"初始值唯一来源"
        //     清晰，无罪分支跳过本 Start 覆写，由 Buff 统一控制。
        // 玩家通过升级仍能继续把 attackRadius 加到任何高度，不会与升级累加冲突。
        if (PlayerSkinSkillBuff.CurrentSkinIndex < 0)
            PlayerSkinSkillBuff.PrimeCurrentSkinIndexFromPrefs();
        int curSkin = PlayerSkinSkillBuff.CurrentSkinIndex;
        if (curSkin == PlayerSkinSkillBuff.SKIN_NANXIAOFENG)
        {
            // 南筱风：专属初始范围 20
            attackRadius = 20f;
        }
        else if (curSkin != PlayerSkinSkillBuff.SKIN_TOMB)
        {
            // 其它非无罪角色（琪露诺/夏无等）：默认初始范围 10
            attackRadius = 10f;
        }
        // 无罪走 PlayerSkinSkillBuff.ApplyTombBuffStats（下一帧覆写为 20），此处不动

        // 创建 LineRenderer 画圆
        // === Bug 修复："显示分身攻击距离"按钮无效 ===
        // 分身由 AdventurePersonalityDissolve1.Instantiate(original) 整体克隆而来：克隆时
        // 主玩家的 SkillWindArrow.Start 已经跑过 → 子节点 "AttackRangeCircle"（带 LineRenderer）
        // 已经存在于 prefab snapshot 中 → Instantiate 会把它一起复制到 clone 上。
        // 接着 clone.SetActive(true) 触发 clone 的 SkillWindArrow.Start 再跑一次，**又新建一个**
        // "AttackRangeCircle"——clone 下就有两个圈：
        //   ①克隆来的旧圈（LineRenderer 未注册到 AttackRangeIndicatorManager → toggle 无法控制）
        //   ②本次 Start 新建的圈（注册了 → toggle 能控制）
        // 玩家切"显示分身攻击距离" 时旧圈始终显示 → 切换看似无效。
        // 修复：Start 入口先把所有同名孤儿子物体销毁，确保最终只有这次注册的那个圈存在。
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && c.name == "AttackRangeCircle") Destroy(c.gameObject);
        }
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
        _lastCircleColor = circleColor;

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, GetComponentInParent<Player>());
    }

    private void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
    }

    private void Update()
    {
        // 跟随玩家位置
        if (player != null)
            transform.position = player.transform.position;

        // 亡者领域已学习：强制把风箭锁定为半径 10 + 紫色范围圈。
        // 主路径已在 getnewskill_TombDomain.chocieupgrade 学完时直接调用 LockToTombDomainPalette()，
        // 这里用 0.5s 节流的兜底，避免每帧 GetComponent + 遍历 SkillList。
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

        // 半径变化时重绘
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
    /// 亡者领域解锁后调用：把风箭固定为半径 10、紫色，且锁定不再被升级改半径。
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

        SkillFormOfWind formOfWind = FindFormOfWindUnderSkillList();

        int count = Mathf.Min(number, targets.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject newbullet = Instantiate(bullet, player.transform.position, Quaternion.identity);
            BulletWindArrow b = newbullet.GetComponent<BulletWindArrow>();
            b.fatherskill = this;
            b.formOfWindSource = formOfWind;
            b.GetFather();
            b.SetTarget(targets[i]);
            b.cango = true;

            // ===== 风箭粒子按角色身份染色 =====
            // 颜色规则（详见 PlayerSkinSkillBuff.ApplySkinTintToWindArrowBullet）：
            //   琪露诺 → prefab 原色（不染色）
            //   南筱风 → 青绿色
            //   夏无   → 红色
            //   无罪   → 紫黑色
            // 之前 BulletWindArrow 里还有一段「学了亡者领域 → 染紫」的逻辑，实际未生效且会冲突，已删除。
            PlayerSkinSkillBuff.ApplySkinTintToWindArrowBullet(newbullet);

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
            // 跳过已死亡的敌人
            enemy en = e.GetComponent<enemy>();
            if (en != null && en.rolestate == enemy.state.dead) continue;
            // 亡者领域：跳过被控制为友军的敌人（风箭不应该打到自己复活出来的盟友）
            // 直接读 enemy._mindControlledFlag，比 GetComponent<MindControlled> 快
            if (en != null && en._mindControlledFlag) continue;

            float dist = Vector3.Distance(player.transform.position, e.position);
            if (dist <= attackRadius)
                result.Add(e);
        }
        return result;
    }

    /// <summary>在玩家 SkillList 下递归查找风之形（与是否和风箭同父节点无关）。</summary>
    SkillFormOfWind FindFormOfWindUnderSkillList()
    {
        if (player == null) return null;
        var pl = player.GetComponent<Player>();
        if (pl == null) pl = player.GetComponentInParent<Player>();
        if (pl == null || pl.SkillList == null) return null;
        return pl.SkillList.GetComponentInChildren<SkillFormOfWind>(true);
    }
}
