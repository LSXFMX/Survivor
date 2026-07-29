using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 蝙蝠敌人：空中单位，悬浮飞行追踪玩家。
/// 进入俯冲范围后降低高度冲向玩家造成伤害，冲刺结束后拉回高空继续追踪。
///
/// Inspector 配置：
/// - flyHeight      : 正常飞行高度（相对玩家 Y 偏移），默认 2
/// - diveRange      : 触发俯冲的水平距离，默认 5
/// - diveSpeed      : 俯冲速度，默认 12
/// - diveInterval   : 两次俯冲之间的冷却时间（秒），默认 3
/// - riseSpeed      : 俯冲结束后拉升速度，默认 6
/// </summary>
public class Bat : enemy
{
    [Header("飞行设置")]
    public float flyHeight   = 2f;
    public float diveRange   = 5f;
    public float diveSpeed   = 12f;
    public float diveInterval = 3f;
    public float riseSpeed   = 6f;

    private Rigidbody _rb;
    private Animator _ani;

    private enum BatState { idle, fly, dive, rise }
    private BatState _state = BatState.idle;

    private float _diveCooldown = 0f;
    private bool  _hitThisDive  = false;

    // 状态卡死兜底（2026-06，亡者领域友军蝙蝠"复活后原地不动"修复）：
    // 进入 dive/rise 时记录时间戳；FixedUpdate 里若停留超过 _stuckThreshold 还没回到 fly/idle，
    // 强制重置状态机。覆盖"协程被异常打断 / role 中途丢失 / 拉升目标无法收敛"等所有卡死路径。
    private float _stateEnterTime = 0f;
    private const float _stuckThreshold = 8f;

    // 亡者领域：被复活为友军时由 MindControlled 设为 true。
    // 友军蝙蝠保留原有"飞行+俯冲"行为，只是攻击目标从 Player 换成最近敌人。
    [System.NonSerialized] public bool isAllyMode = false;

    void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _ani = GetComponent<Animator>();
    }

    // enemy.OnEnable 现在是 protected virtual，直接 base 调用即可，
    // 不再用反射（反射在 IL2CPP 剥离下会失败，导致蝙蝠初始化被跳过、复活后什么都不做）。
    protected override void OnEnable()
    {
        base.OnEnable();

        _rb  = GetComponent<Rigidbody>();
        // _ani 已在 Awake 赋值

        if (_rb != null)
        {
            _rb.useGravity  = false;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    protected override void FixedUpdate()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被控制为友军后，"飞行+俯冲"逻辑保留（用户要求与原蝙蝠一致），
        // 只是 role 由 MindControlled 每帧喂入最近敌人 GameObject，
        // 同时 OnTriggerEnter 改为伤害敌人而非 Player。
        // 注意：MindControlled 不再接管蝙蝠的移动，这里继续往下跑。

        // 朝向翻转（从当前缩放取绝对值，不依赖 Sca 字段）
        if (role != null)
        {
            float s  = Mathf.Abs(transform.localScale.x);
            float dx = role.transform.position.x - transform.position.x;
            transform.localScale = dx > 0
                ? new Vector3(s, s, s)
                : new Vector3(-s, s, s);
        }

        _diveCooldown -= Time.fixedDeltaTime;

        switch (_state)
        {
            case BatState.idle:
                SetMove(false);
                if (role == null) getrole();
                else { _state = BatState.fly; _stateEnterTime = Time.time; }
                break;

            case BatState.fly:
                SetMove(true);
                if (role == null) { _state = BatState.idle; _stateEnterTime = Time.time; break; }

                // 悬浮目标位置
                Vector3 flyTarget = new Vector3(
                    role.transform.position.x,
                    role.transform.position.y + flyHeight,
                    role.transform.position.z);

                transform.position = Vector3.MoveTowards(
                    transform.position, flyTarget, speed * Time.fixedDeltaTime);

                // 水平距离够近且冷却结束 → 开始俯冲
                float hDist = HorizontalDistance(role.transform.position);
                if (hDist <= diveRange && _diveCooldown <= 0f)
                    StartCoroutine(DiveRoutine());
                break;

            case BatState.dive:
                // 由协程驱动，FixedUpdate 不额外移动
                if (Time.time - _stateEnterTime > _stuckThreshold)
                {
                    // 协程异常 / 目标无法到达 → 强制收尾，下一帧从 fly 重新决策
                    StopAllCoroutines();
                    _diveCooldown = diveInterval;
                    _state = BatState.fly;
                    _stateEnterTime = Time.time;
                }
                break;

            case BatState.rise:
                // 由协程驱动；同样加卡死兜底
                if (Time.time - _stateEnterTime > _stuckThreshold)
                {
                    StopAllCoroutines();
                    _diveCooldown = diveInterval;
                    _state = BatState.fly;
                    _stateEnterTime = Time.time;
                }
                break;
        }
    }

    private IEnumerator DiveRoutine()
    {
        if (_state == BatState.dive || _state == BatState.rise) yield break;

        _state       = BatState.dive;
        _stateEnterTime = Time.time;
        _hitThisDive = false;

        // 锁定俯冲目标（玩家当前位置）
        Vector3 diveTarget = role != null ? role.transform.position : transform.position;

        // 俯冲：直线冲向目标
        while (Vector3.Distance(transform.position, diveTarget) > 0.3f)
        {
            if (rolestate == state.dead) yield break;
            Vector3 dir = (diveTarget - transform.position).normalized;
            transform.position += dir * diveSpeed * Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // 短暂停留
        yield return new WaitForSeconds(0.2f);

        // 拉升：回到悬浮高度。
        //   修 Bug（2026-06，亡者领域友军蝙蝠"原地不动"）：
        //     原代码 `if (role != null) { ... 拉升 ... }`，一旦俯冲过程中 role 被 MindControlled
        //     切走（目标敌人死亡 / 友军把它打死 / 缓存失效）就整个 rise 块被跳过，
        //     但 _state 早已被设为 rise、_diveCooldown 也未重置，FixedUpdate 里的 rise case 是
        //     空体（"由协程驱动"），导致蝙蝠永远卡在 rise 状态→看起来"原地不动"。
        //     新版：role 为空时也要把状态机收尾——直接以"原地拉到默认 flyHeight 高度"作为兜底，
        //     并保证最终 `_state = fly`、`_diveCooldown = diveInterval`。
        _state = BatState.rise;
        _stateEnterTime = Time.time;
        {
            // 优先用当前 role 的 Y 做基准；role 为空（场上无敌人/缓存空窗）时退化为
            // "在自身当前 Y 上叠加一个微抬"，避免无限循环。
            float baseY = (role != null) ? role.transform.position.y : transform.position.y;
            Vector3 riseTarget = new Vector3(
                transform.position.x,
                baseY + flyHeight,
                transform.position.z);

            // 用一个安全帧上限防止极端情况死循环（理论上不会触发，留作保底）
            int safetyFrames = 600; // 600 * 0.02s = 12s
            while (Mathf.Abs(transform.position.y - riseTarget.y) > 0.1f && safetyFrames-- > 0)
            {
                if (rolestate == state.dead) yield break;
                transform.position = Vector3.MoveTowards(
                    transform.position, riseTarget, riseSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
        }

        _diveCooldown = diveInterval;
        _state        = BatState.fly;
        _stateEnterTime = Time.time;
    }

    // 俯冲时用 Trigger 检测伤害
    private void OnTriggerEnter(Collider other)
    {
        if (rolestate == state.dead) return;
        if (_state != BatState.dive) return; // 只在俯冲阶段造成伤害
        if (_hitThisDive) return;            // 每次俯冲只打一次

        if (isAllyMode)
        {
            // 友军模式：撞到敌人才造成伤害（且不能打自己/别的友军）
            if (other.CompareTag("Player")) return;
            enemy en = other.GetComponent<enemy>();
            if (en == null || en == this) return;
            if (en.health <= 0 || en.rolestate == state.dead) return;
            if (en.GetComponent<MindControlled>() != null) return; // 不打友军

            int dmg = Mathf.Max(1, (int)(atk - en.def));
            _hitThisDive = true;
            // 亡者领域友军蝙蝠：攻击=治疗（tomb主题复活），弹绿色飘字
            if (isAllyMode)
            {
                int before = en.health;
                en.health = Mathf.Min(en.healthmax, en.health + dmg);
                int actualH = en.health - before;
                if (actualH > 0) MindControlled.SpawnAllyHealNumber(en, actualH);
            }
            else
            {
                en.health -= dmg;
                MindControlled.SpawnAllyDamageNumber(en, dmg);
            }
            en.startturnred();
            // 亡者领域：标记"被友军打过"，让它在 Destroy1 时进入"友军击杀复活链路"（20%）
            TombDomainHook.MarkAllyDamage(en);
            if (en.health <= 0) en.Destroy1();
            return;
        }

        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player == null || player.health <= 0) return;

        float evaRoll = UnityEngine.Random.value * 100f;
        if (player.EVA > evaRoll)
        {
            // 玩家闪避成功：在玩家位置弹青蓝色 Miss
            MissNumber.Show(atknumber, other.transform.position);
            return;
        }

        _hitThisDive = true;
        player.health -= (int)atk;

        if (atknumber != null && DamageNumberSettings.Visible)
        {
            GameObject number = Instantiate(atknumber, other.transform.position, Quaternion.identity);
            number.transform.localScale *= DamageNumberSettings.SizeScale;
            number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = atk.ToString();
        }

        player.startturnred();
        if (player.health <= 0) player.death();
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;

        // 亡者领域：被孢子领域伤害过，统一复活拦截（Bat 不调 base.Destroy1，所以必须在这自己拦截）
        if (!_reviveAttempted)
        {
            _reviveAttempted = true;
            if (TombDomainHook.TryReviveAsAlly(this))
            {
                Debug.Log($"[亡者领域] 蝙蝠 {gameObject.name} 被复活为友军");
                return;
            }
        }

        rolestate = state.dead;
        _state    = BatState.idle;
        SetMove(false);
        StopAllCoroutines();

        foreach (var col in GetComponents<Collider>())           col.enabled = false;
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;

        if (_rb != null) _rb.velocity = Vector3.zero;

        // 横倒代替死亡动画：Z 轴旋转 90 度，停止动画
        transform.rotation = Quaternion.Euler(0, 0, 90f);
        // 保底获取 Animator，防止 _ani 为 null
        var ani = _ani != null ? _ani : GetComponent<Animator>();
        if (ani != null) ani.enabled = false;

        Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
        StartCoroutine(Destroy2());
    }

    private void SetMove(bool isMove) => _ani?.SetBool("ismove", isMove);

    private float HorizontalDistance(Vector3 target)
    {
        Vector3 d = target - transform.position;
        return new Vector3(d.x, 0, d.z).magnitude;
    }
}
