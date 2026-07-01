using UnityEngine;

/// <summary>
/// 风箭子弹：匀速直线追踪目标。目标途中被销毁时，落点附近再解析敌人。
/// formOfWindSource 由 SkillWindArrow 发射时注入，避免运行时找不到风之形。
/// </summary>
public class BulletWindArrow : Bulletbase
{
    private Transform _target;
    private float _elapsed;
    private float _totalTime;
    private Vector3 _startPos;
    static int s_diagSetTarget;

    /// <summary>已处理命中（Trigger 或飞到终点），避免重复结算。</summary>
    bool _impactFinished;

    /// <summary>由 SkillWindArrow 在 Instantiate 后赋值，勿依赖兄弟遍历查找。</summary>
    public SkillFormOfWind formOfWindSource;

    [SerializeField] float impactSnapRadius = 2.2f;

    public override void GetFather()
    {
        base.GetFather();
        if (fatherskill != null && fatherskill.player != null)
        {
            var attr = fatherskill.player.GetComponent<Attribute>();
            if (attr != null) player = attr;
        }
        rb.useGravity = false;
        rb.isKinematic = true;

        // 风箭染色统一改为「按所选角色身份」分流，由 PlayerSkinSkillBuff.ApplySkinTintToWindArrowBullet
        // 在 SkillWindArrow.Useskill 实例化子弹后调用。
        // 之前这里有一段「学了亡者领域 → 染紫」的逻辑（TryApplyTombDomainTint），实际并未真正生效，
        // 且会与角色身份染色冲突，已整体删除。
    }

    public void SetTarget(Transform target)
    {
        _target = target;
        _startPos = transform.position;
        _elapsed = 0f;

        Vector3 endPos = target != null ? target.position : transform.position + Vector3.forward * 5f;
        float dist = Vector3.Distance(_startPos, endPos);
        _totalTime = dist / Mathf.Max(speed, 0.1f);
        if (_totalTime < 1e-4f)
            _totalTime = 0.08f;

        if (s_diagSetTarget < 6)
        {
            s_diagSetTarget++;
            FormOfWindDebug.Err("SetTarget", $"#{s_diagSetTarget} dist={dist:F3} totalTime={_totalTime:F4} speed={speed}");
        }
    }

    protected override void OnTriggerEnter(Collider other)
    {
        if (_impactFinished || !cango) return;

        enemy hit = other.GetComponent<enemy>();
        if (hit == null) hit = other.GetComponentInParent<enemy>();
        if (hit == null || hit.health <= 0 || hit.rolestate == global::enemy.state.dead) return;
        // 亡者领域：风箭不打被控制为友军的目标
        if (hit.GetComponent<MindControlled>() != null) return;
        // 已占领营地：风箭飞行途中碰到友方营地的 collider 不应触发命中（2026-06 修复）。
        // Camp 继承自 enemy，hit 即同一组件，as 强转即可，无需再 GetComponent。
        Camp hitCamp = hit as Camp;
        if (hitCamp != null && hitCamp.IsCaptured) return;

        _impactFinished = true;
        ApplyWindArrowImpact(hit, transform.position);
        Destroy();
    }

    void FixedUpdate()
    {
        if (!cango || _totalTime <= 0f) return;

        _elapsed += Time.fixedDeltaTime;
        float t = Mathf.Clamp01(_elapsed / _totalTime);

        Vector3 endPos = _target != null ? _target.position : transform.position;
        Vector3 newPos = Vector3.Lerp(_startPos, endPos, t);

        Vector3 dir = endPos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        transform.position = newPos;

        bool arrived = t >= 1f || _elapsed >= _totalTime - 1e-5f;
        if (arrived)
        {
            if (!_impactFinished)
            {
                _impactFinished = true;
                Vector3 impactPos = transform.position;
                enemy e = ResolveEnemyForImpact(impactPos);
                if (e != null)
                    ApplyWindArrowImpact(e, impactPos);
                else if (SkillFormOfWind.DebugTrace)
                    Debug.Log("[风箭HitTarget] 落点附近无存活敌人");
            }
            Destroy();
        }
    }

    /// <summary>与敌人重叠（Trigger）或飞到终点时共用：风之形 + 伤害。</summary>
    void ApplyWindArrowImpact(enemy e, Vector3 impactPos)
    {
        if (e == null || player == null) return;

        Transform hitTransform = e.transform;

        // 风之形必须在闪避判定之前触发：否则高 EVA 敌人会导致 TryProc 永不执行。
        if (fatherskill is SkillWindArrow wa)
        {
            if (SkillFormOfWind.DebugTrace)
                Debug.Log("[风之形·入口] 调用 TryProc");
            SkillFormOfWind.TryProcOnWindArrowHit(ResolveOwnerPlayer(), wa, hitTransform, impactPos, formOfWindSource);
        }

        if (e.EVA > UnityEngine.Random.value * 100f)
        {
            if (SkillFormOfWind.DebugTrace) Debug.Log("[风箭HitTarget-2] 目标闪避");
            // 敌人闪避成功：在敌人位置弹青蓝色 Miss
            MissNumber.Show(e.atknumber, e.transform.position);
            return;
        }

        // 伤害公式：技能基础伤害 × (1 + 攻击力 × 0.1)，再走暴击与防御
        float finaldamage = damage * (1f + player.atk * 0.1f);
        bool isCrit = false;
        if (player.CR > UnityEngine.Random.value * 100f)
        {
            finaldamage *= player.CD / 100f;
            isCrit = true;
        }
        finaldamage -= e.def;

        e.health -= (int)finaldamage;

        if (DamageNumberSettings.Visible)
        {
            GameObject num = Instantiate(e.atknumber, hitTransform.position, default);
            var txt = num.transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>();
            txt.text = ((int)finaldamage).ToString();
            if (isCrit) txt.color = new Color32(255, 215, 0, 255);
        }
        e.startturnred();
        if (e.health <= 0) e.Destroy1();
    }

    enemy ResolveEnemyForImpact(Vector3 impactPos)
    {
        if (_target != null)
        {
            var e = _target.GetComponent<enemy>();
            // 亡者领域：原锁定目标若已被控制为友军（玩家中途复活了它），改为就近重选，避免打到友军
            // 已占领营地同样需要避开（2026-06 修复）：发射瞬间是敌方营地，飞行途中被占领则不再命中。
            if (e != null && e.health > 0 && e.rolestate != global::enemy.state.dead && !e._mindControlledFlag)
            {
                Camp camp = e as Camp;
                if (camp == null || !camp.IsCaptured)
                    return e;
            }
        }

        return FindClosestLivingEnemyNear(impactPos, impactSnapRadius);
    }

    static enemy FindClosestLivingEnemyNear(Vector3 p, float maxDist)
    {
        Transform layer = GameObject.Find("enemylayer")?.transform;
        if (layer == null) return null;

        float maxSq = maxDist * maxDist;
        enemy best = null;
        float bestSq = maxSq;

        foreach (Transform t in layer)
        {
            var en = t.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == global::enemy.state.dead) continue;
            // 亡者领域：风箭着弹后吸附敌人时，跳过友军 boss/小怪
            if (en._mindControlledFlag) continue;
            // 已占领营地：着弹吸附就近敌人时跳过友方营地（2026-06 修复）
            Camp camp = en as Camp;
            if (camp != null && camp.IsCaptured) continue;

            Vector3 d = t.position - p;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = en;
            }
        }

        return best;
    }

    Player ResolveOwnerPlayer()
    {
        if (fatherskill != null && fatherskill.player != null)
        {
            var go = fatherskill.player;
            var p = go.GetComponent<Player>();
            if (p == null) p = go.GetComponentInParent<Player>();
            if (p != null) return p;
        }
        if (player == null) return null;
        var p2 = player.GetComponent<Player>();
        if (p2 == null) p2 = player.GetComponentInParent<Player>();
        return p2;
    }
}
