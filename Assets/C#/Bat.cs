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

    void Awake()
    {
        _rb  = GetComponent<Rigidbody>();
        _ani = GetComponent<Animator>();
    }

    protected new void OnEnable()
    {
        var baseOnEnable = typeof(enemy).GetMethod("OnEnable",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        baseOnEnable?.Invoke(this, null);

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
                else _state = BatState.fly;
                break;

            case BatState.fly:
                SetMove(true);
                if (role == null) { _state = BatState.idle; break; }

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
                break;

            case BatState.rise:
                // 由协程驱动
                break;
        }
    }

    private IEnumerator DiveRoutine()
    {
        if (_state == BatState.dive || _state == BatState.rise) yield break;

        _state       = BatState.dive;
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

        // 拉升：回到悬浮高度
        _state = BatState.rise;
        if (role != null)
        {
            Vector3 riseTarget = new Vector3(
                transform.position.x,
                role.transform.position.y + flyHeight,
                transform.position.z);

            while (Mathf.Abs(transform.position.y - riseTarget.y) > 0.1f)
            {
                if (rolestate == state.dead) yield break;
                transform.position = Vector3.MoveTowards(
                    transform.position, riseTarget, riseSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
        }

        _diveCooldown = diveInterval;
        _state        = BatState.fly;
    }

    // 俯冲时用 Trigger 检测伤害
    private void OnTriggerEnter(Collider other)
    {
        if (rolestate == state.dead) return;
        if (_state != BatState.dive) return; // 只在俯冲阶段造成伤害
        if (_hitThisDive) return;            // 每次俯冲只打一次
        if (!other.CompareTag("Player")) return;

        Player player = other.GetComponent<Player>();
        if (player == null || player.health <= 0) return;

        float evaRoll = UnityEngine.Random.value * 100f;
        if (player.EVA > evaRoll) return;

        _hitThisDive = true;
        player.health -= (int)atk;

        if (atknumber != null)
        {
            GameObject number = Instantiate(atknumber, other.transform.position, Quaternion.identity);
            number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = atk.ToString();
        }

        player.startturnred();
        if (player.health <= 0) player.death();
    }

    public override void Destroy1()
    {
        if (rolestate == state.dead) return;
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
