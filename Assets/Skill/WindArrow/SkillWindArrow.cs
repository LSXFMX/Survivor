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

        // 2026-06 改动：亡者领域不再锁定风箭的攻击范围和颜色，
        // 移除了原有的"每 0.5s 探测亡者领域并调用 LockToTombDomainPalette()"兜底逻辑。

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
        // === 风箭内置最低 CD = 0.1s（2026-06 修复）===
        // 业务规则：策划 / 升级 / 词条可以把 CDtime 降到 0，但发射节流必须有最低保护，
        // 否则每帧都会触发 Player.Update 的 CDkey>=CDtime 判定 → 每帧启动协程 → 风箭刷屏导致瞬卡死。
        // 这里只在"实际发射环节"用 Max(CDtime, 0.1) 重置 CDkey，不去动 CDtime 本身——
        // 升级面板上仍显示策划设定的 CDtime（包括 0），用户不会感知到字段被偷偷夹住。
        const float WindArrowMinCD = 0.1f;
        float effectiveCD = Mathf.Max(CDtime, WindArrowMinCD);

        List<Transform> targets = GetEnemiesInRange();
        if (targets.Count == 0)
        {
            // 关键修复：没有目标时不要把 CDkey 直接清零——否则会出现"明明没打出箭但 CD 照算"，
            // 玩家会觉得"风箭哑火"。改为只回退一小段，下一帧/下一 Tick 立刻能再尝试找目标。
            CDkey = effectiveCD - WindArrowMinCD * 0.5f;
            if (CDkey < 0f) CDkey = 0f;
            yield break;
        }

        // 把 CDkey 设为「-(最低 CD - CDtime)」的等价形式：
        //   * CDtime >= 0.1 时：effectiveCD = CDtime，CDkey = 0 → 与旧行为一致
        //   * CDtime <  0.1 时：CDkey 被设成负值 -(0.1 - CDtime)，等 0.1s 后才会再次触发
        // FixedUpdate 里 CDkey += dt，到 CDtime 后才会再次 >= CDtime 触发下一次 Useskill。
        CDkey = CDtime - effectiveCD;

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
            // 已占领营地：占领后 tag/layer/_mindControlledFlag 均不变（与魅惑是两套机制），
            // 必须显式 skip，否则风箭会把自己的友方营地当敌人持续打（2026-06 修复）。
            // 用 as Camp 比 GetComponent<Camp> 更快——Camp 继承自 enemy，en 已就是同一组件。
            Camp camp = en as Camp;
            if (camp != null && camp.IsCaptured) continue;

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
