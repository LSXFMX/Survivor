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

    [Header("自然回血")]
    [Tooltip("每秒按 healthmax 的百分比自然回血。被亡者领域操控后失效（MindControlled 一旦挂上，FixedUpdate 短路，回血不再 tick）。")]
    public float naturalHealPctPerSecond = 0f; // 关底蝙蝠Boss无回血（避免异常回血，吸血能力由世界Boss继承）

    [HideInInspector] public battleUI battleUI;
    // 累积小数血量，避免 RoundToInt 在 0.02×3000=60 这种整除场景之外丢精度
    private float _healAccum;

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
        // 父类私有字段已改为 protected，直接赋值
        playerlayer = GameObject.Find("playerlayer")?.transform;

        _ani = GetComponent<Animator>();

        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 500f;
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

        // 亡者领域：被控制为友军后，跳过回血（等价于"失去自然回血词条"）。
        // `role` 已被 MindControlled 设为敌方目标 → 直接让下面的状态机（move/dash/summon）自然运转
        if (GetComponent<MindControlled>() == null) TickNaturalHeal();

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
            var mc = GetComponent<MindControlled>();
            for (int i = 0; i < summonCount; i++)
            {
                float offsetX = (i % 2 == 0 ? 1f : -1f) * (i / 2 + 1) * 3f;
                Vector3 pos = new Vector3(
                    transform.position.x + offsetX,
                    _fixedY,
                    transform.position.z);
                GameObject obj = Instantiate(batPrefab, pos, Quaternion.Euler(45, 0, 0), _batLayer);
                // 如果Boss被亡者领域控制，召唤的蝙蝠也变为被控友军
                if (mc != null && obj != null)
                {
                    var batEn = obj.GetComponent<enemy>();
                    if (batEn != null)
                    {
                        batEn._mindControlledFlag = true;
                        var batMC = obj.AddComponent<MindControlled>();
                        batMC.isWorldBoss = false;
                    }
                }
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

    /// <summary>
    /// 关底 Boss 自然回血：每帧按 fixedDeltaTime 累积 `healthmax × naturalHealPctPerSecond × dt`，
    /// 累积到 ≥1 时回填整数到 health（最多到 healthmax）。
    ///
    /// 失效条件（在调用方已生效，不需要这里再判）：
    ///   • 已死亡：FixedUpdate 顶部 _state==dead 直接 return；
    ///   • 被亡者领域操控：MindControlled 存在时短路 return（"失去自然回血词条"语义）；
    ///   • busy 协程（冲刺/召唤）期间：FixedUpdate _busy return → 战斗演出阶段不回血，
    ///     防止 boss 在冲刺逃跑/召唤无敌帧里偷血。
    /// </summary>
    private void TickNaturalHeal()
    {
        if (naturalHealPctPerSecond <= 0f) return;
        if (health <= 0 || health >= healthmax) return;
        _healAccum += healthmax * naturalHealPctPerSecond * Time.fixedDeltaTime;
        if (_healAccum >= 1f)
        {
            int gain = (int)_healAccum;
            _healAccum -= gain;
            health = Mathf.Min(healthmax, health + gain);
        }
    }

    // ── 死亡 ──────────────────────────────────────────
    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被孢子领域伤害过，统一复活拦截（BossBat 不调 base.Destroy1，必须在此自行拦截。
        // WorldBossBat 调 base.Destroy1() 经此入口；命中复活时，外层子类会继续执行 OnWorldBossDefeated——
        // 故 WorldBossBat.Destroy1 自身也已加了相同前置拦截，提前 return。这里只服务普通 BossBat。）
        // _reviveAttempted 防重入：WorldBossBat 已在外层投过一次，进入这里就不能再投第二次。
        if (!_reviveAttempted)
        {
            _reviveAttempted = true;
            if (TombDomainHook.TryReviveAsAlly(this))
            {
                Debug.Log($"[亡者领域] 吸血鬼领主 {gameObject.name} 被复活为友军");
                return;
            }
        }

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

        // 首次击败吸血鬼领主（蝙蝠社群 Boss）里程碑：解锁成就装备5「吸血鬼大君」+ 蝙蝠好感度 +10（仅首次）
        if (EquipmentSystem.Instance != null)
        {
            bool alreadyUnlockedLord = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 5);
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 5);
            if (!alreadyUnlockedLord)
            {
                ToastManager.Show("吸血鬼大君启程已记录！蝙蝠社群好感度 +10");
                if (FavorManager.Instance != null)
                {
                    FavorManager.Instance.AddFavor(FactionType.Bat, 10);
                    ToastManager.Show($"蝙蝠社群好感度 +10（当前：{FavorManager.Instance.GetFavor(FactionType.Bat)}）");
                }
                else
                {
                    int cur = UnityEngine.PlayerPrefs.GetInt("Favor_Bat", 0);
                    int next = Mathf.Clamp(cur + 10, 0, 100);
                    UnityEngine.PlayerPrefs.SetInt("Favor_Bat", next);
                    UnityEngine.PlayerPrefs.Save();
                    ToastManager.Show($"蝙蝠社群好感度 +10（当前：{next}）");
                }
            }
        }

        // 每次击败蝙蝠Boss → 好感度 +1
        if (FavorManager.Instance != null)
        {
            FavorManager.Instance.AddFavor(FactionType.Bat, 1);
            int newFavor = FavorManager.Instance.GetFavor(FactionType.Bat);
            ToastManager.Show($"蝙蝠社群好感度 +1（当前：{newFavor}）");
        }
        else
        {
            string key = "Favor_Bat";
            int cur = PlayerPrefs.GetInt(key, 0);
            int next = Mathf.Clamp(cur + 1, 0, 100);
            PlayerPrefs.SetInt(key, next);
            PlayerPrefs.Save();
            ToastManager.Show($"蝙蝠社群好感度 +1（当前：{next}）");
        }

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
