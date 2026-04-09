using System.Collections;
using UnityEngine;

/// <summary>
/// 蝙蝠社群Boss
///
/// Animator 参数：
/// - ismove   (Bool)    行走
/// - isattack (Bool)    冲刺劈砍
/// - issummon (Bool)    召唤
/// - dead     (Trigger) 死亡
/// </summary>
public class BossBat : enemy
{
    [Header("Boss 体型")]
    public float bossScale = 15f;

    [Header("冲刺劈砍")]
    public float dashRange    = 8f;
    public float dashSpeed    = 25f;
    public float dashDistance = 15f;

    [Header("召唤蝙蝠")]
    public float      summonRange    = 12f;
    public float      summonCooldown = 10f;
    public int        summonCount    = 2;
    public GameObject batPrefab;

    [HideInInspector] public battleUI battleUI;

    // ── 状态 ──────────────────────────────────────────
    private enum BossState { idle, move, dash, summon, dead }
    private BossState _state    = BossState.idle;
    private bool      _busy     = false; // 协程运行中，FixedUpdate 不干预

    private Animator  _ani;
    private Rigidbody _rb;
    private float     _summonTimer    = 0f;
    private float     _damageCooldown = 0f;
    private float     _fixedY;
    private Transform _batLayer;

    // ── 初始化 ────────────────────────────────────────
    protected new void OnEnable()
    {
        // 手动初始化父类私有字段
        var playerlayer = GameObject.Find("playerlayer")?.transform;
        typeof(enemy).GetField("playerlayer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(this, playerlayer);

        _ani = GetComponent<Animator>();
        _rb  = GetComponent<Rigidbody>();

        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier);
            health    = healthmax;
            atk       = Mathf.RoundToInt(atk * cfg.atkMultiplier);
        }

        Sca = bossScale;
        transform.localScale = new Vector3(Sca, Sca, Sca);

        if (_rb != null)
        {
            _rb.useGravity  = false;
            // 锁定 Y 和旋转，防止飘起来
            _rb.constraints = RigidbodyConstraints.FreezePositionY
                            | RigidbodyConstraints.FreezeRotation;
            _rb.mass = 10f;
        }

        _fixedY      = transform.position.y;
        _summonTimer = summonCooldown; // 开局可立即召唤

        // 运行时自动查找 enemylayer
        if (_batLayer == null)
            _batLayer = GameObject.Find("enemylayer")?.transform;
    }

    // ── 主循环 ────────────────────────────────────────
    protected override void FixedUpdate()
    {
        if (_state == BossState.dead) return;
        if (_busy) return; // 协程运行中，不干预

        // 保持 Y 轴固定
        if (_rb != null)
            _rb.velocity = Vector3.zero;
        transform.position = new Vector3(transform.position.x, _fixedY, transform.position.z);

        _summonTimer += Time.fixedDeltaTime;

        switch (_state)
        {
            case BossState.idle:
                SetAnim(false, false, false);
                if (role == null) getrole();
                else _state = BossState.move;
                break;

            case BossState.move:
                if (role == null) { _state = BossState.idle; break; }

                // 朝向翻转
                float s  = Mathf.Abs(transform.localScale.x);
                float dx = role.transform.position.x - transform.position.x;
                transform.localScale = dx >= 0
                    ? new Vector3(s, s, s)
                    : new Vector3(-s, s, s);

                // Z 轴对齐：快速平移到玩家 Z（用较大速度，但不是瞬移）
                float dz = role.transform.position.z - transform.position.z;
                if (Mathf.Abs(dz) > 0.05f)
                {
                    float zStep = Mathf.Sign(dz) * speed * 3f * Time.fixedDeltaTime;
                    transform.position = new Vector3(
                        transform.position.x,
                        _fixedY,
                        transform.position.z + zStep);
                }

                float hDist = Mathf.Abs(dx);

                // 近距离 → 冲刺劈砍
                if (hDist <= dashRange)
                {
                    StartCoroutine(DashRoutine());
                    break;
                }

                // 远距离 + CD 结束 → 召唤
                if (hDist >= summonRange && _summonTimer >= summonCooldown)
                {
                    StartCoroutine(SummonRoutine());
                    break;
                }

                // 普通追踪（只移动 X）
                SetAnim(true, false, false);
                transform.position += new Vector3(
                    Mathf.Sign(dx) * speed * Time.fixedDeltaTime, 0, 0);
                break;
        }
    }

    // ── 冲刺劈砍 ──────────────────────────────────────
    private IEnumerator DashRoutine()
    {
        _busy  = true;
        _state = BossState.dash;
        SetAnim(false, true, false);

        if (role == null) { EndBusy(BossState.move); yield break; }

        float dirX    = Mathf.Sign(role.transform.position.x - transform.position.x);
        float traveled = 0f;

        while (traveled < dashDistance)
        {
            if (_state == BossState.dead) yield break;
            float step = dashSpeed * Time.fixedDeltaTime;
            transform.position += new Vector3(dirX * step, 0, 0);
            traveled += step;
            yield return new WaitForFixedUpdate();
        }

        EndBusy(BossState.move);
    }

    // ── 召唤蝙蝠 ──────────────────────────────────────
    private IEnumerator SummonRoutine()
    {
        _busy        = true;
        _state       = BossState.summon;
        _summonTimer = 0f;
        SetAnim(false, false, true);

        yield return new WaitForSeconds(1.5f);

        if (_state == BossState.dead) yield break;

        if (batPrefab != null && _batLayer != null)
        {
            for (int i = 0; i < summonCount; i++)
            {
                float offsetX = (i % 2 == 0 ? 1f : -1f) * (i / 2 + 1) * 3f;
                Vector3 pos = new Vector3(
                    transform.position.x + offsetX,
                    _fixedY,
                    transform.position.z);
                Instantiate(batPrefab, pos, Quaternion.Euler(45, 0, 0), _batLayer);
            }
        }

        EndBusy(BossState.move);
    }

    // ── 碰撞伤害（仅冲刺阶段）────────────────────────
    protected override void OnCollisionEnter(Collision collision)
    {
        if (_state != BossState.dash) return;
        if (Time.time - _damageCooldown < 0.2f) return;
        _damageCooldown = Time.time;
        base.OnCollisionEnter(collision);
    }

    // ── 死亡 ──────────────────────────────────────────
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
        rolestate = state.dead;
        _state    = BossState.dead;
        _busy     = false;
        StopAllCoroutines();

        SetAnim(false, false, false);
        _ani?.SetTrigger("dead");

        if (_rb != null) _rb.velocity = Vector3.zero;
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
        battleUI?.OnBossDefeated();
        StartCoroutine(Destroy2());
    }

    // ── 工具 ──────────────────────────────────────────
    private void SetAnim(bool move, bool attack, bool summon)
    {
        if (_ani == null) return;
        _ani.SetBool("ismove",   move);
        _ani.SetBool("isattack", attack);
        _ani.SetBool("issummon", summon);
    }

    private void EndBusy(BossState nextState)
    {
        SetAnim(false, false, false);
        _state = nextState;
        _busy  = false;
    }
}
