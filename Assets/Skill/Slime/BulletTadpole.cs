using TMPro;
using UnityEngine;

/// <summary>
/// 阴/阳史莱姆的「蝌蚪状能量射弹」。
///
/// 规格：伤害较低但数量很多（初始 6 发/轮）。阴=黑色，阳=白色。
///
/// 为什么不继承 Bulletbase：
///   Bulletbase 的移动依赖 Rigidbody + FixedUpdate，并在 GetFather() 里强制
///   FreezePositionY、把出生点抬到 y>=1，还会用 transform.localScale *= size 累乘。
///   蝌蚪要的是"出生即散开、贴地飞行、大量并发、零GC"，自己走一套轻量
///   距离检测更可控，也避免几十发射弹同时上物理引擎。
///
///   代价是必须自己做伤害结算——因此这里严格复刻了 Bulletbase.OnTriggerEnter 的
///   全部规则：闪避→ 伤害公式 → 暴击 → 防御 → 门挑战增伤 → 最低 1 点 →
///   结算埋点 → 飘字 → 变红 → 全局吸血 → 击杀。任何一条漏掉都会造成
///   "这个技能的数值口径和别的技能不一样"的隐性BUG。
/// </summary>
public class BulletTadpole : MonoBehaviour
{
    [Header("运行时由技能注入")]
    public int   damage = 1;
    public float speed = 12f;
    public float lifetime = 2.2f;
    public int   pass = 0;              // 额外穿透次数（0 = 命中一次即消失）
    public bool  isYin = true;
    public string trackingSkillName = SlimeFactionAssets.SKILL_SHARED_DISPLAY;

    [Header("命中判定")]
    [Tooltip("命中半径。蝌蚪很小但数量多，半径略大以保证手感不空枪。")]
    public float hitRadius = 0.55f;

    [Header("射程")]
    [Tooltip("最大飞行距离（世界单位）。由技能按 attackRadius × 1.5 注入，超出即消失。")]
    public float maxTravel = 13.5f;

    [Header("追踪")]
    [Tooltip("每秒最大转向角度。越大越黏人；0 = 完全直线不追踪。")]
    public float homingDegPerSec = 130f;
    [Tooltip("追踪目标丢失后重新索敌的间隔（秒）。避免每帧全场扫描。")]
    public float retargetInterval = 0.15f;

    private Vector3 _dir = Vector3.right;
    private Attribute _playerAttr;
    private Transform _enemyLayer;
    private SpriteRenderer _sr;
    private float _spin;// 尾巴摆动相位
    private bool  _launched;
    private float _traveled;             // 已飞行距离，用于射程判定
    private Transform _homingTarget;     // 当前追踪目标
    private float _retargetTimer;
    /// <summary>已命中集合。仅在 pass&gt;0（真的会穿透）时才惰性分配，省掉大量 GC。</summary>
    private System.Collections.Generic.HashSet<enemy> _hitSet;

    /// <summary>
    /// 发射。dir 只取水平分量并归一化；y 分量由调用方保证（贴地飞行）。
    /// </summary>
    /// <param name="initialTarget">
    /// 初始追踪目标。传入后蝌蚪会边飞边小幅修正方向咬住它；
    /// 为 null 时会在 retargetInterval 后自行索敌。
    /// </param>
    public void Launch(Vector3 dir, Attribute playerAttr, Transform enemyLayer,
                       Transform initialTarget = null)
    {
        dir.y = 0f;
        _dir = dir.sqrMagnitude < 0.0001f ? Vector3.right : dir.normalized;
        _playerAttr = playerAttr;
        _enemyLayer = enemyLayer;
        _homingTarget = initialTarget;
        _spin = Random.Range(0f, Mathf.PI * 2f);
        _traveled = 0f;

        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null)
        {
            _sr.sprite = SlimeFactionAssets.TadpoleOf(isYin);
            // 排序层高于地面与敌人，避免被地图sprite 吃掉（项目其它 sprite 常见 0~30）
            _sr.sortingOrder = 90;
        }

        // 45° 俯视视角：让蝌蚪朝飞行方向，同时保留场景的X 倾斜
        ApplyFacing();
        _launched = true;
    }

    private void ApplyFacing()
    {
        float angle = Mathf.Atan2(_dir.z, _dir.x) * Mathf.Rad2Deg;
        // 与 Bulletbase 一致：X=45 保留俯视倾斜，Z 控制平面朝向。
        //
        // 【2026-08 修复】+180°：源图里蝌蚪的**头朝左、尾朝右**，
        //   而 angle=0 对应"朝 +X（右）"，直接套用会让蝌蚪尾巴朝着飞行方向倒着游。
        //   这里补一个半圈把头掰回前进方向。
        transform.rotation = Quaternion.Euler(45f, 0f, angle + 180f);
    }

    private void Update()
    {
        if (!_launched) return;

        float dt = Time.deltaTime;

        UpdateHoming(dt);

        // 蝌蚪游动：主方向匀速 + 垂直方向正弦摆尾，视觉上像真的在"游"
        _spin += dt * 14f;
        Vector3 perp = new Vector3(-_dir.z, 0f, _dir.x);
        Vector3 step = _dir * speed * dt + perp * (Mathf.Cos(_spin) * 0.9f * dt);
        transform.position += step;

        // 射程判定：只累加主方向位移（摆尾的横向抖动不该算进射程）
        _traveled += speed * dt;
        if (_traveled >= maxTravel) { Destroy(gameObject); return; }

        lifetime -= dt;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        TryHit();
    }

    /// <summary>
    /// 轻度追踪：每帧把飞行方向朝目标**限速旋转**，而不是直接对准。
    ///
    /// 用限速转向（homingDegPerSec）而非直接LookAt 的原因：
    ///   直接对准会让蝌蚪像制导导弹一样必中，既破坏"低伤高量"的定位，
    ///   也让走位失去意义。限速转向表现为"略带追踪"—— 近处的敌人基本咬得住，
    ///   横向高速掠过的敌人则会被甩掉，符合需求里的"略带追踪"。
    /// </summary>
    private void UpdateHoming(float dt)
    {
        if (homingDegPerSec <= 0f) return;

        // 目标失效（死亡/被回收/变成友军）→ 触发重新索敌
        if (_homingTarget != null)
        {
            if (!_homingTarget.gameObject.activeInHierarchy) _homingTarget = null;
            else
            {
                enemy en = _homingTarget.GetComponent<enemy>();
                if (en == null || en.health <= 0 || en.rolestate == enemy.state.dead
                    || en._mindControlledFlag)
                    _homingTarget = null;
            }
        }

        if (_homingTarget == null)
        {
            _retargetTimer -= dt;
            if (_retargetTimer <= 0f)
            {
                _retargetTimer = retargetInterval;
                _homingTarget = FindNearestTarget();
            }
            if (_homingTarget == null) return;
        }

        Vector3 want = _homingTarget.position - transform.position;
        want.y = 0f;
        if (want.sqrMagnitude < 0.0001f) return;
        want.Normalize();

        // 限速转向
        float maxRad = homingDegPerSec * Mathf.Deg2Rad * dt;
        _dir = Vector3.RotateTowards(_dir, want, maxRad, 0f).normalized;
        ApplyFacing();
    }

    /// <summary>
    /// 在剩余射程内找最近敌人。搜索半径取"还能飞多远"，
    /// 避免去追一个注定飞不到的目标（那只会让蝌蚪拐个没意义的弯）。
    /// </summary>
    private Transform FindNearestTarget()
    {
        if (_enemyLayer == null) return null;

        float remain = Mathf.Max(0.5f, maxTravel - _traveled);
        float rSq = remain * remain;
        Vector3 me = transform.position;
        float bestSq = float.MaxValue;
        Transform best = null;

        int cnt = _enemyLayer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = _enemyLayer.GetChild(i);
            if (t == null) continue;

            // 同TryHit：先算距离，只对候选者GetComponent
            Vector3 d = t.position - me; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq > rSq || sq >= bestSq) continue;

            enemy en = t.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == enemy.state.dead) continue;
            if (en._mindControlledFlag) continue;

            bestSq = sq;
            best = t;
        }
        return best;
    }

    private void TryHit()
    {
        if (_enemyLayer == null) return;

        Vector3 me = transform.position;
        float rSq = hitRadius * hitRadius;
        int cnt = _enemyLayer.childCount;

        for (int i = 0; i < cnt; i++)
        {
            Transform t = _enemyLayer.GetChild(i);
            if (t == null) continue;

            // 【性能关键】先做纯 Transform 的平方距离判定，只有进入命中半径才GetComponent。
            //   满配时一次齐射有 16发 × 2 条鱼 = 32 发蝌蚪同时在场，
            //   若像常规写法那样"先 GetComponent 再判距离"，在 600 敌人的蝙蝠潮里就是
            //   32 × 600 = 19200 次 GetComponent/帧（约 2ms，直接吃掉整个帧预算的12%）。
            //   反过来先判距离后，实际 GetComponent 次数几乎恒为 0~2次/帧。
            Vector3 d = t.position - me; d.y = 0f;
            if (d.sqrMagnitude > rSq) continue;

            enemy en = t.GetComponent<enemy>();
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;
            // 亡者领域：不打被控制的友军
            if (en._mindControlledFlag) continue;
            // 龙王进场/转阶段无敌期：与Bulletbase 同样在源头拦截，避免飘字/音效先播出来
            if (en is DragonBoss db && db.IsInvincible) continue;
            // 同一发蝌蚪不重复打同一只怪（穿透时才会走到第二只）
            if (_hitSet != null && _hitSet.Contains(en)) continue;

            if (pass > 0)
            {
                // 只有真的会穿透时才需要"已命中集合"，避免每发蝌蚪都白白分配一个 HashSet
                if (_hitSet == null) _hitSet = new System.Collections.Generic.HashSet<enemy>();
                _hitSet.Add(en);
            }

            DealDamage(en);

            pass -= 1;
            if (pass < 0) { Destroy(gameObject); return; }
        }
    }

    /// <summary>
    /// 伤害结算。严格对齐 Bulletbase.OnTriggerEnter 的口径，逐条注释说明为什么需要。
    /// </summary>
    private void DealDamage(enemy en)
    {
        // 1) 敌人闪避
        float evaRoll = Random.value * 100f;
        if (en.EVA > evaRoll)
        {
            MissNumber.Show(en.atknumber, en.transform.position);
            return;
        }

        // 2) 基础伤害公式：技能伤害 ×(1 + 玩家攻击力 × 0.1)
        float atk = _playerAttr != null ? _playerAttr.atk : 0f;
        float final = damage * (1f + atk * 0.1f);

        // 3) 暴击
        bool isCrit = false;
        if (_playerAttr != null && _playerAttr.CR > Random.value * 100f)
        {
            final *= _playerAttr.CD / 100f;
            isCrit = true;
        }

        // 4) 减防御
        final -= en.def;

        // 5) SSR「白色杀手」：对门挑战怪增伤 20%
        if (en is GateChallengeEnemy && EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 1))
            final *= 1.2f;

        // 6) 至少 1 点，避免高防御导致 0/负伤
        if (final < 1f) final = 1f;
        int dealt = (int)final;

        // 7) 结算面板埋点。共享显示名，避免"阴史莱姆/阳史莱姆"在结算里拆成两行——
        //    它们本质是同一套输出，玩家关心的是这套技能总共打了多少。
        GameSessionTracker.Instance?.RecordDamage(trackingSkillName, dealt);

        en.health -= dealt;

        // 8) 伤害飘字（阴=紫、阳=金；暴击统一金色，与全局规则一致）
        if (DamageNumberSettings.Visible && en.atknumber != null)
        {
            GameObject num = Instantiate(en.atknumber, en.transform.position, default);
            num.transform.localScale *= DamageNumberSettings.SizeScale;
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = dealt.ToString();
                if (isCrit) txt.color = new Color32(255, 215, 0, 255);
                else txt.color = isYin ? new Color32(178, 120, 255, 255)
                                       : new Color32(255, 245, 200, 255);
            }
        }

        AudioManager.PlaySfx(AudioManager.SfxKey.Hit);
        en.startturnred();

        // 9) SSR_10 饮血剑：全局吸血
        EquipmentInitializer.TryAllSourceLifesteal(dealt, en.atknumber, en.transform.position);

        if (en.health <= 0) en.Destroy1();
    }
}
