using UnityEngine;
using TMPro;

/// <summary>
/// 蘑菇宝宝宠物状态机：
/// Follow  跟随玩家
/// Hide    有敌人靠近时隐藏待机
/// Pop     敌人贴近时钻出攻击
/// Cooldown 攻击后冷却并回到跟随
/// </summary>
public class MushroomBabyPet : MonoBehaviour
{
    public enum PetState
    {
        Follow,
        Hide,
        Pop,
        Cooldown
    }

    [Header("引用")]
    public Player owner;
    public Transform enemyLayer;
    public Animator animator;

    [Header("跟随")]
    public float followDistance = 1.6f;
    public float followSpeed = 1.25f;
    public float catchUpSnapDistance = 36f;
    [Tooltip("与目标跟随点的最小保持距离，小于该值就不再贴近。")]
    public float followDeadZone = 1.35f;
    [Tooltip("与玩家中心保持的最小距离，防止走到玩家脚下/中心。")]
    public float minDistanceToOwner = 1.4f;
    [Tooltip("跟随延迟（秒）：值越大，越像慢半拍拖尾跟随。")]
    public float followDelay = 0.95f;

    [Header("触发范围")]
    [Tooltip("进入 Hide 的距离阈值（较小，防止过早趴下）。")]
    public float hideEnterRange = 4.5f;
    [Tooltip("Hide 状态保持阈值（较大，形成滞后，避免反复蹲起）。")]
    public float hideKeepRange = 6.0f;
    public float popTriggerRange = 3.4f;

    [Header("攻击")]
    public int popDamage = 30;
    public float popDuration = 0.2f;
    public float popCooldown = 2.5f;
    public float popAttackRadius = 0.7f;

    [Header("表现")]
    public Vector3 followOffset = new Vector3(-1.2f, 0f, 0f);
    [Tooltip("保持宠物倾斜视角，避免看起来扁平。")]
    public bool forceTiltX45 = true;

    [Header("Hide 稳定性")]
    [Tooltip("检测不到敌人后，至少等待这段时间再退出 Hide，避免反复趴下/站起抖动。")]
    public float hideReleaseDelay = 0.8f;

    [Header("Hide/Peek 动画")]
    public float peekIntervalMin = 1.2f;
    public float peekIntervalMax = 2.8f;

    [Header("Hide → Pop 自动出击")]
    [Tooltip("Hide 状态停留这么久后，无论敌人是否进入 popTriggerRange，都会朝当前最近的敌人 Pop 一次。")]
    public float hideAutoPopDelay = 1.0f;
    [Tooltip("Hide 时挑选 Pop 目标的搜索半径（应 ≥ hideKeepRange，覆盖整个 Hide 圈）。")]
    public float hidePopSearchRange = 7.0f;

    public PetState currentState { get; private set; } = PetState.Follow;

    private float _cooldownTimer = 0f;
    private float _stateTimer = 0f;
    private enemy _currentTarget;
    private Vector3 _popTargetPos;
    private SpriteRenderer _sr;
    private Collider _col;
    private float _peekTimer = 0f;
    private float _noEnemyTimer = 0f;
    private readonly System.Collections.Generic.List<TrailPoint> _ownerTrail = new System.Collections.Generic.List<TrailPoint>();

    private struct TrailPoint
    {
        public float time;
        public Vector3 pos;
    }

    private static readonly int ANIM_IS_MOVING = Animator.StringToHash("isMoving");
    private static readonly int ANIM_IS_HIDDEN = Animator.StringToHash("isHidden");
    private static readonly int ANIM_POP = Animator.StringToHash("pop");
    private static readonly int ANIM_PEEK = Animator.StringToHash("peek");

    protected virtual void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _col = GetComponent<Collider>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
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
            case PetState.Follow:
                UpdateFollow();
                break;
            case PetState.Hide:
                UpdateHide();
                break;
            case PetState.Pop:
                UpdatePop();
                break;
            case PetState.Cooldown:
                UpdateCooldown();
                break;
        }
    }

    private void UpdateFollow()
    {
        SetVisible(true);
        bool isMoving = MoveToOwnerOffset();
        UpdateFacing(owner.transform.position.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: isMoving);

        enemy nearest = FindNearestAliveEnemy(hideEnterRange);
        if (nearest != null)
        {
            _currentTarget = nearest;
            EnterState(PetState.Hide);
        }
    }

    private void UpdateHide()
    {
        SetVisible(true);
        if (_currentTarget != null) UpdateFacing(_currentTarget.transform.position.x);
        UpdateAnimatorFlags(isHidden: true, isMoving: false);
        UpdatePeekTrigger();

        // 用更大的 hidePopSearchRange 兜底搜索：即使敌人没进 hideKeepRange，
        // 只要 Hide 计时到 hideAutoPopDelay 也会强制冒头攻击，避免无限趴下。
        enemy nearestKeep = FindNearestAliveEnemy(hideKeepRange);
        if (nearestKeep == null)
        {
            _noEnemyTimer += Time.deltaTime;
            if (_noEnemyTimer >= hideReleaseDelay)
            {
                _currentTarget = null;
                EnterState(PetState.Follow);
            }
            return;
        }

        _noEnemyTimer = 0f;
        _currentTarget = nearestKeep;
        if (_cooldownTimer > 0f) return;

        float d = Vector3.Distance(transform.position, _currentTarget.transform.position);
        // 1) 敌人贴近 → 立即 Pop
        if (d <= popTriggerRange)
        {
            EnterState(PetState.Pop);
            return;
        }
        // 2) 敌人在 Hide 范围内但还没贴近 → Hide 蓄力到 hideAutoPopDelay 后强制 Pop
        if (_stateTimer >= hideAutoPopDelay)
        {
            enemy popTarget = FindNearestAliveEnemy(hidePopSearchRange) ?? _currentTarget;
            _currentTarget = popTarget;
            EnterState(PetState.Pop);
        }
    }

    private void UpdatePop()
    {
        SetVisible(true);
        UpdateFacing(_popTargetPos.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: false);
        transform.position = Vector3.MoveTowards(transform.position, _popTargetPos, followSpeed * 2f * Time.deltaTime);

        if (_stateTimer >= popDuration)
        {
            DealPopDamage();
            _cooldownTimer = popCooldown;
            EnterState(PetState.Cooldown);
        }
    }

    private void UpdateCooldown()
    {
        SetVisible(true);
        bool isMoving = MoveToOwnerOffset();
        UpdateFacing(owner.transform.position.x);
        UpdateAnimatorFlags(isHidden: false, isMoving: isMoving);
        if (_cooldownTimer <= 0f)
            EnterState(PetState.Follow);
    }

    private void EnterState(PetState next)
    {
        currentState = next;
        _stateTimer = 0f;
        if (next != PetState.Hide) _noEnemyTimer = 0f;
        if (next == PetState.Hide) ResetPeekTimer();
        if (next == PetState.Pop)
        {
            _popTargetPos = _currentTarget != null ? _currentTarget.transform.position : transform.position;
            if (animator != null) animator.SetTrigger(ANIM_POP);
        }
    }

    private bool MoveToOwnerOffset()
    {
        // 延迟拖尾跟随：跟随玩家历史位置，可稳定拉开距离。
        Vector3 delayedOwnerPos = GetDelayedOwnerPosition();
        Vector3 target = delayedOwnerPos + followOffset;
        float moveBefore = Vector3.Distance(transform.position, target);

        if (moveBefore > followDeadZone)
        {
            // 保持恒定慢追：不再因为距离远而加速，确保“可被拉开后慢慢追上”。
            transform.position = Vector3.MoveTowards(
                transform.position, target, followSpeed * Time.deltaTime);
        }

        // 兜底：如果过近，沿“玩家->宠物”方向轻推开，避免踩到玩家中心点。
        Vector3 toPet = transform.position - owner.transform.position;
        toPet.y = 0f;
        float dOwner = toPet.magnitude;
        if (dOwner < minDistanceToOwner)
        {
            Vector3 pushDir = dOwner > 0.001f ? toPet / dOwner : new Vector3(-1f, 0f, -1f).normalized;
            Vector3 safePos = owner.transform.position + pushDir * minDistanceToOwner;
            safePos.y = transform.position.y;
            transform.position = Vector3.MoveTowards(transform.position, safePos, followSpeed * 1.2f * Time.deltaTime);
        }

        return moveBefore > followDeadZone;
    }

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
            // 亡者领域：宠物（蘑菇宝宝）不锁定/不攻击被控制为友军的敌人
            if (e._mindControlledFlag) continue;
            // 兜底：对象池/SetActive 会让 enemy.OnEnable 把 flag 清掉一次，加一道 GetComponent 兜底
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

    private void DealPopDamage()
    {
        if (enemyLayer == null) return;
        Vector3 hitCenter = _popTargetPos;

        foreach (Transform t in enemyLayer)
        {
            if (t == null) continue;
            enemy e = t.GetComponent<enemy>();
            if (e == null) continue;
            if (e.rolestate == enemy.state.dead) continue;
            // 亡者领域：蘑菇宝宝 pop 范围伤害也跳过友军
            if (e._mindControlledFlag) continue;
            if (t.GetComponent<MindControlled>() != null) continue;

            float d = Vector3.Distance(hitCenter, t.position);
            if (d > popAttackRadius) continue;

            e.health -= popDamage;
            if (e.atknumber != null && DamageNumberSettings.Visible)
            {
                GameObject number = Instantiate(e.atknumber, t.position, Quaternion.identity);
                var text = number.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (text != null) text.text = popDamage.ToString();
            }
            if (e.health <= 0) e.Destroy1();
        }
    }

    private void UpdateFacing(float targetX)
    {
        float dx = targetX - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;
        Vector3 s = transform.localScale;
        float absX = Mathf.Abs(s.x);
        s.x = dx >= 0 ? absX : -absX;
        transform.localScale = s;
    }

    private void SetVisible(bool visible)
    {
        if (_sr != null) _sr.enabled = visible;
        if (_col != null) _col.enabled = visible;
    }

    private void UpdateAnimatorFlags(bool isHidden, bool isMoving)
    {
        if (animator == null) return;
        animator.SetBool(ANIM_IS_HIDDEN, isHidden);
        animator.SetBool(ANIM_IS_MOVING, isMoving);
    }

    private void ResetPeekTimer()
    {
        _peekTimer = Random.Range(peekIntervalMin, peekIntervalMax);
    }

    private void UpdatePeekTrigger()
    {
        if (animator == null) return;
        _peekTimer -= Time.deltaTime;
        if (_peekTimer > 0f) return;
        animator.SetTrigger(ANIM_PEEK);
        ResetPeekTimer();
    }

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

    private void ApplyTiltX45()
    {
        if (!forceTiltX45) return;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
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

}
