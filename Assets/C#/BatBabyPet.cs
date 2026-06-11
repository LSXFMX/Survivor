using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 蝙蝠宝宝（独立实现，不再继承 MushroomBabyPet）。
///
/// 行为：
///   Follow  跟随玩家（延迟拖尾、随玩家朝向镜像、悬浮起伏）
///   Attack  半径 detectRange 内有敌人时，扑向目标造成 attackDamage 伤害
///   Return  攻击后回到玩家身后跟随位置，再恢复跟随
///
/// 与血族血统使魔的关系：本类负责"宠物"行为（一只、贴身跟随、定期短距扑击）；
/// 血族血统技能 SkillBloodline 负责常驻使魔群（多只、轨道环绕、AOE 扫荡）。
/// 二者互不影响。
/// </summary>
public class BatBabyPet : MonoBehaviour
{
    public enum PetState { Follow, Attack, Return }

    [Header("引用")]
    public Player owner;
    public Transform enemyLayer;
    public Animator animator;

    [Header("跟随")]
    [Tooltip("跟随移动速度。")]
    public float followSpeed = 4.5f;
    [Tooltip("到达目标点的死区，小于该距离不再贴近。")]
    public float followDeadZone = 0.35f;
    [Tooltip("与玩家中心的最小距离，避免贴脸。")]
    public float minDistanceToOwner = 1.1f;
    [Tooltip("跟随延迟（秒）：值越大越像慢半拍拖尾跟随。")]
    public float followDelay = 0.35f;
    [Tooltip("跟随偏移（玩家身后位置）。")]
    public Vector3 followOffset = new Vector3(-1.4f, 1.4f, 0f);
    [Tooltip("根据玩家朝向（localScale.x 符号）镜像 followOffset.x，使蝙蝠始终位于玩家身后。")]
    public bool mirrorOffsetByFacing = true;
    [Tooltip("强制保持 X=45° 倾斜，统一战斗视角。")]
    public bool forceTiltX45 = true;

    [Header("攻击")]
    [Tooltip("敌人进入该半径才会发起冲撞攻击。")]
    public float detectRange = 5f;
    [Tooltip("固定伤害值。")]
    public int attackDamage = 30;
    [Tooltip("两次攻击之间的冷却。")]
    public float attackCooldown = 1.5f;
    [Tooltip("冲撞速度倍率（相对 followSpeed）。")]
    public float attackSpeedMultiplier = 4.0f;
    [Tooltip("返回速度倍率（相对 followSpeed）。")]
    public float returnSpeedMultiplier = 3.0f;
    [Tooltip("命中判定半径：飞到目标这么近时立即结算伤害。")]
    public float attackHitRadius = 0.55f;
    [Tooltip("单次扑击的最长持续时间（秒），超过则放弃返回。")]
    public float attackMaxDuration = 0.8f;

    [Header("飞行悬停")]
    [Tooltip("上下浮动幅度（米）。")]
    public float hoverAmplitude = 0.18f;
    [Tooltip("浮动频率（Hz）。")]
    public float hoverFrequency = 2.2f;

    public PetState currentState { get; private set; } = PetState.Follow;

    private float _cooldownTimer;
    private float _stateTimer;
    private enemy _attackTarget;
    private float _lastHoverApplied;
    private float _hoverPhaseSeed;

    private SpriteRenderer _sr;
    private Collider _col;

    private readonly List<TrailPoint> _ownerTrail = new List<TrailPoint>();
    private struct TrailPoint { public float time; public Vector3 pos; }

    private static readonly int ANIM_IS_MOVING = Animator.StringToHash("isMoving");
    private static readonly int ANIM_IS_HIDDEN = Animator.StringToHash("isHidden");
    private static readonly int ANIM_POP = Animator.StringToHash("pop");

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _col = GetComponent<Collider>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _hoverPhaseSeed = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Start()
    {
        if (owner == null) owner = FindObjectOfType<Player>();
        if (enemyLayer == null) enemyLayer = GameObject.Find("enemylayer")?.transform;
        IgnoreCollisionWithOwner();
        IgnoreCollisionWithEnemies();
        ApplyTiltX45();
    }

    private void Update()
    {
        if (owner == null) return;
        ApplyTiltX45();
        RecordOwnerTrail();

        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
        _stateTimer += Time.deltaTime;

        switch (currentState)
        {
            case PetState.Follow: UpdateFollow(); break;
            case PetState.Attack: UpdateAttack(); break;
            case PetState.Return: UpdateReturn(); break;
        }
    }

    // ── Follow ──────────────────────────────────────────
    private void UpdateFollow()
    {
        bool isMoving = MoveToFollowPoint(followSpeed);
        UpdateFacing(owner.transform.position.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: isMoving);

        if (_cooldownTimer > 0f) return;

        enemy nearest = FindNearestAliveEnemy(detectRange);
        if (nearest != null)
        {
            _attackTarget = nearest;
            EnterState(PetState.Attack);
        }
    }

    // ── Attack ──────────────────────────────────────────
    private void UpdateAttack()
    {
        // 目标可能在飞行途中死亡/被销毁
        if (_attackTarget == null || _attackTarget.rolestate == enemy.state.dead)
        {
            EnterState(PetState.Return);
            return;
        }

        // 超时保护：避免目标无限漂移导致蝙蝠永远追不上
        if (_stateTimer > attackMaxDuration)
        {
            // 最后兜底：仍然在范围内则就地结算
            TryDealDamage(_attackTarget);
            EnterState(PetState.Return);
            return;
        }

        Vector3 targetPos = _attackTarget.transform.position;
        targetPos.y = transform.position.y;

        UpdateFacing(targetPos.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: true);

        float step = followSpeed * attackSpeedMultiplier * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, step);

        if (Vector3.Distance(transform.position, targetPos) <= attackHitRadius)
        {
            TryDealDamage(_attackTarget);
            EnterState(PetState.Return);
        }
    }

    // ── Return ──────────────────────────────────────────
    private void UpdateReturn()
    {
        bool isMoving = MoveToFollowPoint(followSpeed * returnSpeedMultiplier);
        UpdateFacing(owner.transform.position.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: isMoving);

        // 距离玩家身后跟随点足够近就回到 Follow
        Vector3 home = GetFollowTargetPos();
        if (Vector3.Distance(transform.position, home) <= followDeadZone * 1.5f)
            EnterState(PetState.Follow);
    }

    // ── 状态切换 ────────────────────────────────────────
    private void EnterState(PetState next)
    {
        currentState = next;
        _stateTimer = 0f;
        if (next == PetState.Attack)
        {
            if (animator != null) animator.SetTrigger(ANIM_POP);
        }
        else if (next == PetState.Return)
        {
            _cooldownTimer = attackCooldown;
            _attackTarget = null;
        }
    }

    // ── 跟随计算 ────────────────────────────────────────
    private Vector3 GetFollowTargetPos()
    {
        Vector3 delayedOwnerPos = GetDelayedOwnerPosition();
        Vector3 effectiveOffset = followOffset;
        if (mirrorOffsetByFacing && owner != null && owner.transform.localScale.x < 0f)
            effectiveOffset.x = -effectiveOffset.x;
        return delayedOwnerPos + effectiveOffset;
    }

    private bool MoveToFollowPoint(float moveSpeed)
    {
        Vector3 target = GetFollowTargetPos();
        float d = Vector3.Distance(transform.position, target);

        if (d > followDeadZone)
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);

        // 避免贴脸：保持距离玩家中心至少 minDistanceToOwner
        if (owner != null)
        {
            Vector3 toPet = transform.position - owner.transform.position;
            toPet.y = 0f;
            float dOwner = toPet.magnitude;
            if (dOwner < minDistanceToOwner)
            {
                Vector3 pushDir = dOwner > 0.001f ? toPet / dOwner : new Vector3(-1f, 0f, -1f).normalized;
                Vector3 safePos = owner.transform.position + pushDir * minDistanceToOwner;
                safePos.y = transform.position.y;
                transform.position = Vector3.MoveTowards(transform.position, safePos, moveSpeed * 1.2f * Time.deltaTime);
            }
        }
        return d > followDeadZone;
    }

    private void RecordOwnerTrail()
    {
        if (owner == null) return;
        float now = Time.time;
        _ownerTrail.Add(new TrailPoint { time = now, pos = owner.transform.position });

        float minKeepTime = now - Mathf.Max(0.05f, followDelay) - 0.6f;
        int removeCount = 0;
        for (int i = 0; i < _ownerTrail.Count; i++)
        {
            if (_ownerTrail[i].time < minKeepTime) removeCount++;
            else break;
        }
        if (removeCount > 0) _ownerTrail.RemoveRange(0, removeCount);
    }

    private Vector3 GetDelayedOwnerPosition()
    {
        if (owner == null) return transform.position;
        if (_ownerTrail.Count == 0) return owner.transform.position;

        float targetTime = Time.time - Mathf.Max(0.05f, followDelay);
        TrailPoint prev = _ownerTrail[0];
        for (int i = 1; i < _ownerTrail.Count; i++)
        {
            TrailPoint cur = _ownerTrail[i];
            if (cur.time >= targetTime)
            {
                float dt = cur.time - prev.time;
                if (dt <= 0.0001f) return cur.pos;
                float t = Mathf.Clamp01((targetTime - prev.time) / dt);
                return Vector3.Lerp(prev.pos, cur.pos, t);
            }
            prev = cur;
        }
        return _ownerTrail[_ownerTrail.Count - 1].pos;
    }

    // ── 敌人查询/伤害 ───────────────────────────────────
    private enemy FindNearestAliveEnemy(float range)
    {
        if (enemyLayer == null || enemyLayer.childCount == 0) return null;
        float best = range;
        enemy bestEnemy = null;
        foreach (Transform t in enemyLayer)
        {
            if (t == null) continue;
            enemy e = t.GetComponent<enemy>();
            if (e == null) continue;
            if (e.rolestate == enemy.state.dead) continue;
            // 亡者领域：宠物（蝙蝠宝宝）不锁定被控制为友军的敌人。
            // 双保险：flag 可能因为对象池/SetActive 流程被 enemy.OnEnable 临时重置，
            // 这里再用 GetComponent<MindControlled>() 兜底一遍（性能损耗仅在跟随阶段每帧一次）。
            if (e._mindControlledFlag) continue;
            if (t.GetComponent<MindControlled>() != null) continue;

            float d = Vector3.Distance(transform.position, t.position);
            if (d <= best)
            {
                best = d;
                bestEnemy = e;
            }
        }
        return bestEnemy;
    }

    private void TryDealDamage(enemy e)
    {
        if (e == null || e.rolestate == enemy.state.dead || e.health <= 0) return;

        e.health -= attackDamage;
        if (e.atknumber != null && DamageNumberSettings.Visible)
        {
            GameObject number = Instantiate(e.atknumber, e.transform.position, Quaternion.identity);
            var text = number.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (text != null) text.text = attackDamage.ToString();
        }
        e.startturnred();
        if (e.health <= 0) e.Destroy1();
    }

    // ── 动画 / 视觉 ─────────────────────────────────────
    private void UpdateFacing(float targetX)
    {
        float dx = targetX - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;
        Vector3 s = transform.localScale;
        float absX = Mathf.Abs(s.x);
        s.x = dx >= 0 ? absX : -absX;
        transform.localScale = s;
    }

    private void UpdateAnimatorFlags(bool isHidden, bool isMoving)
    {
        if (animator == null) return;
        // 蝙蝠没有 Hide/Peek 状态；保持兼容地把 isHidden 强制为 false
        animator.SetBool(ANIM_IS_HIDDEN, isHidden);
        animator.SetBool(ANIM_IS_MOVING, isMoving);
    }

    private void ApplyTiltX45()
    {
        if (!forceTiltX45) return;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private void LateUpdate()
    {
        // 撤销上一帧叠加的浮动并重新计算，保持 logical Y 不被持续累积
        Vector3 p = transform.position;
        p.y -= _lastHoverApplied;

        // Attack 阶段不浮动，避免冲撞瞬间出现违和上下抖动
        float dy = 0f;
        if (currentState != PetState.Attack)
        {
            float t = Time.time * hoverFrequency * 2f * Mathf.PI + _hoverPhaseSeed;
            dy = Mathf.Sin(t) * hoverAmplitude;
        }
        p.y += dy;
        transform.position = p;
        _lastHoverApplied = dy;
    }

    // ── 碰撞屏蔽 ────────────────────────────────────────
    private void IgnoreCollisionWithOwner()
    {
        if (owner == null) return;

        Collider[] petColliders = GetComponentsInChildren<Collider>(true);
        Collider[] ownerColliders = owner.GetComponentsInChildren<Collider>(true);
        foreach (var petCol in petColliders)
        {
            if (petCol == null) continue;
            foreach (var ownerCol in ownerColliders)
            {
                if (ownerCol == null) continue;
                Physics.IgnoreCollision(petCol, ownerCol, true);
            }
        }
    }

    private void IgnoreCollisionWithEnemies()
    {
        Collider[] petColliders = GetComponentsInChildren<Collider>(true);
        if (petColliders == null || petColliders.Length == 0) return;

        enemy[] enemies = FindObjectsOfType<enemy>();
        foreach (var e in enemies)
        {
            if (e == null) continue;
            Collider[] enemyColliders = e.GetComponentsInChildren<Collider>(true);
            foreach (var petCol in petColliders)
            {
                if (petCol == null) continue;
                foreach (var enemyCol in enemyColliders)
                {
                    if (enemyCol == null) continue;
                    Physics.IgnoreCollision(petCol, enemyCol, true);
                }
            }
        }
    }
}
