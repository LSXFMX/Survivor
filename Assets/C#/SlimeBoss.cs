using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 史莱姆社群 Boss（N12 关底）。继承 enemy，复用受伤/掉落框架，自带完整状态机。
/// 身体动画通过 Animator.Play(stateName) 切换：Walk / Devour / Transform / DragonWalk / Death。
///
/// 策划案能力：
///  1. 合体吞噬：每 6s 吞噬身边一只小史莱姆，每次 +5% 最大生命并回血、+5% 攻击，累计 absorbedCount。
///  2. absorbedCount ≥ 5：召唤「史莱姆剑」作为手持子物体挂在身侧；到点挥砍打出大范围剑气（不是飞剑）。
///  3. absorbedCount ≥ 10：召唤「史莱姆弓」作为手持子物体；到点拉弓射出箭矢。
///     （武器是装饰物，本体不造成伤害；挥砍/拉弓时有动画，然后释放射弹/剑气。）
///  4. 血量 <10% 触发一次「合体塑形·史莱姆巨龙」终形态：向前吞噬 → 回复 5 成生命 → 变龙。
///     变龙后 Boss 停止移动，朝玩家持续喷吐大量大面积史莱姆吐息，并自身持续掉血直到死亡（=通关）。
/// </summary>
public class SlimeBoss : enemy
{
    [Header("体型")]
    public float bossScale   = 1.6f;   // 史莱姆王形态缩放
    public float dragonScale  = 2.6f;  // 巨龙终形态缩放

    [Header("速度")]
    public float slimeSpeed  = 3f;     // 史莱姆王移动速度

    [Header("合体吞噬（每 6s 吞噬小史莱姆合体）")]
    public float mergeInterval = 6f;
    public float mergeRadius   = 9f;
    [Range(0f, 0.3f)] public float mergeHealthPct = 0.02f;
    [Range(0f, 0.3f)] public float mergeAtkPct    = 0.02f;

    [Header("手持武器 · 解锁条件")]
    public int swordUnlockCount = 5;    // 吸满 5 只 → 手持剑
    public int bowUnlockCount   = 10;   // 吸满 10 只 → 手持弓
    public float swordCd = 6f;          // 挥砍间隔
    public float bowCd   = 6f;          // 拉弓间隔
    public float weaponScale = 0.5f;    // 手持武器相对 Boss 的缩放

    [Header("手持武器 · 精灵与子弹")]
    public Sprite     heldSwordSprite;    // 史莱姆剑（手持装饰）
    public Sprite     heldBowSprite;      // 史莱姆弓（手持装饰）
    public GameObject swordQiPrefab;      // 剑气（大范围）子物体
    public GameObject arrowPrefab;        // 箭矢子物体

    [Header("剑气参数")]
    public float swordQiSpeed     = 12f;
    public float swordQiLifetime  = 1.6f;
    public float swordQiDamageMul = 0.8f;

    [Header("弓箭参数")]
    public int   arrowCount     = 5;
    public float arrowSpread    = 35f;
    public float arrowSpeed     = 18f;
    public float arrowLifetime  = 3f;
    public float arrowDamageMul = 0.4f;

    [Header("巨龙终形态（血量 <10% 触发一次）")]
    [Range(0.01f, 0.5f)] public float dragonHpThreshold = 0.10f;
    [Range(0f, 1f)]      public float dragonHealPct     = 0.5f;   // 回复 5 成
    public float dragonDrainDuration = 8f;   // 变龙后在这么多秒内持续掉血到 0（即死亡=通关）

    [Header("史莱姆吐息（巨龙形态持续喷吐）")]
    public GameObject slimeBreathPrefab;
    public float breathInterval  = 1.0f;
    public int   breathCount     = 6;
    public float breathSpread    = 70f;
    public float breathSpeed     = 9f;
    public float breathLifetime  = 3f;
    public float breathDamageMul = 0.7f;

    [Header("Boss UI")]
    [HideInInspector] public battleUI battleUI;

    private enum Phase { Slime, Transforming, Dragon, Dead }
    private Phase phase = Phase.Slime;

    /// <summary>亡者领域复活时强制重置为史莱姆形态（修复龙形态残留问题）。</summary>
    public void ResetToSlimeForm()
    {
        phase = Phase.Slime;
        Sca = bossScale;
        speed = Mathf.RoundToInt(slimeSpeed);
        transform.localScale = new Vector3(Sca, Sca, Sca);
        if (anim != null) { anim.speed = 1f; PlayAnim("Walk"); }
    }

    private Animator anim;
    private SpriteRenderer _sr;
    private string curAnim = "";
    private bool  busy = false;
    private float busyWatchdog = 0f;

    private int   absorbedCount = 0;
    private float cdMerge = 0f;
    private float cdSword = 0f;
    private float cdBow   = 0f;
    private float dmgCooldown = 0f;

    // 手持武器
    private Transform _swordObj;
    private Transform _bowObj;
    private bool _swordSwinging = false;
    private bool _bowDrawing    = false;
    private Material _spriteMat;

    // 巨龙
    private float _dragonTimer = 0f;
    private float _dragonStartHealth = 0f;
    private float _breathTimer = 0f;

    // 地面模拟重力
    private float groundY; private bool groundYSet = false;
    private float _vy = 0f; private const float GRAVITY = 45f;

    // ── 生命周期 ──
    protected new void OnEnable()
    {
        playerlayer = GameObject.Find("playerlayer")?.transform;
        anim = GetComponent<Animator>();
        _sr  = GetComponent<SpriteRenderer>();
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.mass = 501f;

        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier);
            health    = healthmax;
            atk       = Mathf.RoundToInt(atk * cfg.atkMultiplier);
        }

        phase = Phase.Slime;
        Sca = bossScale;
        transform.localScale = new Vector3(Sca, Sca, Sca);
        speed = Mathf.RoundToInt(slimeSpeed);
        rolestate = state.move;
        curAnim = "";
        absorbedCount = 0;
        cdMerge = mergeInterval;
        cdSword = swordCd; cdBow = bowCd;
        if (anim != null) anim.speed = 1f;
        PlayAnim("Walk");
    }

    private void PlayAnim(string n)
    {
        if (curAnim == n) return;
        curAnim = n;
        if (anim != null) { anim.speed = 1f; anim.Play(n, 0, 0f); }
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (role != null) { groundY = role.transform.position.y; groundYSet = true; }
        if (groundYSet)
        {
            Vector3 pp = transform.position;
            _vy -= GRAVITY * Time.deltaTime;
            float ny = pp.y + _vy * Time.deltaTime;
            if (ny <= groundY) { ny = groundY; _vy = 0f; }
            pp.y = ny;
            transform.position = pp;
        }
    }

    private void FaceTarget()
    {
        if (role == null) return;
        float dx = role.transform.position.x - transform.position.x;
        float s = Sca;
        transform.localScale = dx >= 0 ? new Vector3(s, s, s) : new Vector3(-s, s, s);
    }

    private Vector3 FacingDir() => transform.localScale.x >= 0 ? Vector3.right : Vector3.left;

    // ── 主循环 ──
    protected override void FixedUpdate()
    {
        if (phase == Phase.Dead) return;
        if (GetComponent<MindControlled>() != null) return;

        if (busy)
        {
            busyWatchdog += Time.fixedDeltaTime;
            if (busyWatchdog > 9f)
            {
                StopAllCoroutines();
                busy = false; busyWatchdog = 0f;
                _swordSwinging = false; _bowDrawing = false;
                if (anim != null) anim.speed = 1f;
                curAnim = "";
                PlayAnim(phase == Phase.Dragon ? "DragonWalk" : "Walk");
            }
            return;
        }
        busyWatchdog = 0f;

        float dt = Time.fixedDeltaTime;

        if (phase == Phase.Slime)
        {
            if (role == null) { getrole(); return; }
            FaceTarget();
            PlayAnim("Walk");
            MoveToward(role.transform.position, slimeSpeed, dt);

            // 血量 <10% → 触发巨龙终形态（最高优先级，只触发一次）
            if (health <= healthmax * dragonHpThreshold)
            {
                StartCoroutine(TransformRoutine());
                return;
            }

            // 合体吞噬
            cdMerge -= dt;
            if (cdMerge <= 0f)
            {
                cdMerge = mergeInterval;
                StartCoroutine(MergeRoutine());
                return;
            }

            // 解锁手持武器（一次性）
            if (absorbedCount >= swordUnlockCount && _swordObj == null) CreateSword();
            if (absorbedCount >= bowUnlockCount   && _bowObj   == null) CreateBow();

            // 手持剑：到点挥砍打出剑气（不打断移动，武器独立协程）
            if (_swordObj != null && !_swordSwinging)
            {
                cdSword -= dt;
                if (cdSword <= 0f) { cdSword = swordCd; StartCoroutine(SwordSwingRoutine()); }
            }
            // 手持弓：到点拉弓射箭
            if (_bowObj != null && !_bowDrawing)
            {
                cdBow -= dt;
                if (cdBow <= 0f) { cdBow = bowCd; StartCoroutine(BowDrawRoutine()); }
            }
        }
        else if (phase == Phase.Dragon)
        {
            // 停止移动，只面向玩家
            if (role != null) FaceTarget();
            PlayAnim("DragonWalk");

            // 持续喷吐史莱姆吐息
            _breathTimer -= dt;
            if (_breathTimer <= 0f) { _breathTimer = breathInterval; BreathVolley(); }

            // 自身持续掉血直到死亡（=通关）
            _dragonTimer += dt;
            float t = dragonDrainDuration > 0f ? _dragonTimer / dragonDrainDuration : 1f;
            health = Mathf.RoundToInt(Mathf.Lerp(_dragonStartHealth, 0f, Mathf.Clamp01(t)));
            if (t >= 1f || health <= 0)
            {
                health = 0;
                Destroy1();
            }
        }
    }

    private void MoveToward(Vector3 target, float spd, float dt)
    {
        Vector3 d = target - transform.position; d.y = 0;
        if (d.sqrMagnitude < 0.01f) return;
        transform.position += d.normalized * spd * dt;
    }

    // ── 合体吞噬 ──
    private IEnumerator MergeRoutine()
    {
        busy = true;
        PlayAnim("Devour");
        GameObject prey = FindNearbySmallSlime();
        yield return new WaitForSeconds(0.35f);
        if (prey != null) Destroy(prey);

        int hpGain = Mathf.Max(1, Mathf.RoundToInt(healthmax * mergeHealthPct));
        healthmax += hpGain;
        health = Mathf.Min(healthmax, health + hpGain);
        atk = Mathf.Max(atk + 1, Mathf.RoundToInt(atk * (1f + mergeAtkPct)));
        absorbedCount++;

        yield return new WaitForSeconds(0.3f);
        busy = false;
        PlayAnim("Walk");
    }

    private GameObject FindNearbySmallSlime()
    {
        Transform layer = transform.parent;
        if (layer == null) return null;
        GameObject best = null; float bestDist = mergeRadius;
        foreach (Transform t in layer)
        {
            if (t == transform) continue;
            var e = t.GetComponent<enemy>();
            if (e == null || e is SlimeBoss) continue;
            if (e.rolename != "史莱姆") continue;
            float d = Vector3.Distance(t.position, transform.position);
            if (d <= bestDist) { bestDist = d; best = t.gameObject; }
        }
        return best;
    }

    // ── 手持武器创建 ──
    private Material SpriteMat()
    {
        if (_spriteMat == null) _spriteMat = new Material(Shader.Find("Sprites/Default"));
        return _spriteMat;
    }

    private Transform CreateHeldWeapon(string name, Sprite sprite, Vector3 localPos, float restZ)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, restZ);
        go.transform.localScale = Vector3.one * weaponScale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.material = SpriteMat();
        sr.sortingOrder = (_sr != null ? _sr.sortingOrder : 1) + 2;
        return go.transform;
    }

    private void CreateSword()
    {
        if (heldSwordSprite == null) return;
        _swordObj = CreateHeldWeapon("HeldSword", heldSwordSprite, new Vector3(0.42f, 0.28f, 0f), -25f);
    }
    private void CreateBow()
    {
        if (heldBowSprite == null) return;
        _bowObj = CreateHeldWeapon("HeldBow", heldBowSprite, new Vector3(0.5f, -0.05f, 0f), 0f);
    }

    // ── 挥剑 → 大范围剑气 ──
    private IEnumerator SwordSwingRoutine()
    {
        if (_swordObj == null) yield break;
        _swordSwinging = true;

        // 蓄力后仰
        yield return LerpZ(_swordObj, -25f, -75f, 0.14f);
        // 猛挥
        yield return LerpZ(_swordObj, -75f, 95f, 0.12f);

        // 打出剑气（世界方向 = 面向方向）
        if (swordQiPrefab != null)
        {
            Vector3 spawnPos = transform.position + FacingDir() * 1.0f + Vector3.up * 0.5f;
            GameObject obj = Instantiate(swordQiPrefab, spawnPos, Quaternion.Euler(45, 0, 0));
            var proj = obj.GetComponent<SlimeBossProjectile>();
            if (proj != null) { proj.flipFacing = true; proj.Launch(FacingDir(), Mathf.RoundToInt(atk * swordQiDamageMul), swordQiSpeed, swordQiLifetime); }
        }

        // 收回
        yield return LerpZ(_swordObj, 95f, -25f, 0.18f);
        _swordSwinging = false;
    }

    // ── 拉弓 → 射箭 ──
    private IEnumerator BowDrawRoutine()
    {
        if (_bowObj == null) yield break;
        _bowDrawing = true;

        // 拉弓：弓身微微向后 + 轻微压扁
        Vector3 baseScale = Vector3.one * weaponScale;
        Vector3 basePos   = _bowObj.localPosition;
        float t = 0f;
        while (t < 0.28f)
        {
            t += Time.fixedDeltaTime;
            float k = Mathf.Clamp01(t / 0.28f);
            _bowObj.localScale = new Vector3(baseScale.x * (1f - 0.15f * k), baseScale.y * (1f + 0.1f * k), baseScale.z);
            _bowObj.localPosition = basePos + new Vector3(-0.08f * k, 0f, 0f);
            yield return new WaitForFixedUpdate();
        }

        // 射箭：朝玩家方向扇形
        FireArrows();

        // 回位
        _bowObj.localScale = baseScale;
        _bowObj.localPosition = basePos;
        _bowDrawing = false;
    }

    private void FireArrows()
    {
        if (arrowPrefab == null || role == null) return;
        Vector3 toP = role.transform.position - transform.position; toP.y = 0;
        Vector3 baseDir = toP.sqrMagnitude > 0.01f ? toP.normalized : FacingDir();
        float baseAngle = Mathf.Atan2(baseDir.z, baseDir.x) * Mathf.Rad2Deg;
        float start = baseAngle - arrowSpread * 0.5f;
        float step  = arrowCount > 1 ? arrowSpread / (arrowCount - 1) : 0f;
        Vector3 spawnPos = transform.position + baseDir * 0.8f + Vector3.up * 0.4f;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(atk * arrowDamageMul));
        for (int i = 0; i < arrowCount; i++)
        {
            float ang = (start + step * i) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;
            GameObject obj = Instantiate(arrowPrefab, spawnPos, Quaternion.Euler(45, 0, 0));
            var proj = obj.GetComponent<SlimeBossProjectile>();
            if (proj != null) proj.Launch(dir, dmg, arrowSpeed, arrowLifetime);
        }
    }

    private IEnumerator LerpZ(Transform tr, float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur && tr != null)
        {
            t += Time.fixedDeltaTime;
            float z = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
            tr.localRotation = Quaternion.Euler(0f, 0f, z);
            yield return new WaitForFixedUpdate();
        }
        if (tr != null) tr.localRotation = Quaternion.Euler(0f, 0f, to);
    }

    // ── 巨龙终形态变身 ──
    private IEnumerator TransformRoutine()
    {
        if (phase != Phase.Slime) yield break;
        phase = Phase.Transforming;
        busy = true;
        try
        {
            // 收起手持武器
            if (_swordObj != null) Destroy(_swordObj.gameObject);
            if (_bowObj != null)   Destroy(_bowObj.gameObject);

            PlayAnim("Devour");
            GameObject prey = FindNearbySmallSlime();
            yield return new WaitForSeconds(0.4f);
            if (prey != null) Destroy(prey);

            PlayAnim("Transform");
            yield return new WaitForSeconds(1.4f);
        }
        finally
        {
            // 回复 5 成生命 → 进入巨龙（停止移动 + 持续掉血）
            health = Mathf.Min(healthmax, health + Mathf.RoundToInt(healthmax * dragonHealPct));
            _dragonStartHealth = health;
            _dragonTimer = 0f;
            _breathTimer = 0.3f;
            phase = Phase.Dragon;
            Sca = dragonScale;
            transform.localScale = new Vector3(Sca, Sca, Sca);
            if (anim != null) anim.speed = 1f;
            curAnim = "";
            PlayAnim("DragonWalk");
            busy = false;
        }
    }

    // ── 史莱姆吐息：朝玩家喷出大量大面积史莱姆 ──
    private void BreathVolley()
    {
        if (slimeBreathPrefab == null) return;
        Vector3 baseDir;
        if (role != null)
        {
            Vector3 toP = role.transform.position - transform.position; toP.y = 0;
            baseDir = toP.sqrMagnitude > 0.01f ? toP.normalized : FacingDir();
        }
        else baseDir = FacingDir();

        float baseAngle = Mathf.Atan2(baseDir.z, baseDir.x) * Mathf.Rad2Deg;
        float start = baseAngle - breathSpread * 0.5f;
        float step  = breathCount > 1 ? breathSpread / (breathCount - 1) : 0f;
        Vector3 spawnPos = transform.position + baseDir * 1.2f + Vector3.up * 0.8f;
        int dmg = Mathf.Max(1, Mathf.RoundToInt(atk * breathDamageMul));
        for (int i = 0; i < breathCount; i++)
        {
            // 每颗吐息加一点随机散布，营造"大量喷吐"的杂乱感
            float jitter = Random.Range(-4f, 4f);
            float ang = (start + step * i + jitter) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)).normalized;
            float spd = breathSpeed * Random.Range(0.85f, 1.15f);
            GameObject obj = Instantiate(slimeBreathPrefab, spawnPos, Quaternion.Euler(45, 0, 0));
            var proj = obj.GetComponent<SlimeBossProjectile>();
            if (proj != null) proj.Launch(dir, dmg, spd, breathLifetime);
        }
    }

    // 接触伤害
    protected override void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("enemy")) return;
        if (phase == Phase.Transforming || phase == Phase.Dead) return;
        if (Time.time - dmgCooldown < 0.3f) return;
        dmgCooldown = Time.time;
        base.OnCollisionEnter(collision);
    }

    // 死亡 → 触发通关
    public override void Destroy1()
    {
        if (phase == Phase.Dead) return;
        phase = Phase.Dead;
        rolestate = state.dead;
        busy = true;
        StopAllCoroutines();
        if (_swordObj != null) Destroy(_swordObj.gameObject);
        if (_bowObj != null)   Destroy(_bowObj.gameObject);
        battleUI?.OnBossDefeated();
        PlayAnim("Death");
        foreach (var col in GetComponents<Collider>()) col.enabled = false;
        if (expstone != null) Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
        StartCoroutine(DieAfter());
    }

    private IEnumerator DieAfter()
    {
        yield return new WaitForSeconds(1.4f);
        Destroy(gameObject);
    }
}
