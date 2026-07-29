using UnityEngine;

/// <summary>
/// 红月分身宠物（FavorEquipment 8 「红月分身」觉醒后开局生成）。
///
/// 与 <see cref="BatBabyPet"/> 不同：本宠物是"暗种图腾"，不主动索敌、不产生伤害。
/// 真正的攻击行为由 EquipmentInitializer.ApplyFavorEquipment8_RedMoonClone 在开局
/// 追加一份 <see cref="SkillParasite"/> 到玩家 SkillList（number += 1）承担。
///
/// 行为：
///   - 悬浮在玩家头顶偏后位置（followOffset ≈ (0, 1.6, -0.2)）
///   - 缓慢上下呼吸（sin 波），产生"红月缓缓漂浮"的动感
///   - 慢速眨眼（若挂了 Animator，则 trigger "Blink"，否则无害跳过）
///   - 玩家死亡时不主动销毁（战斗结束由场景切换清理）
///
/// 关联文案："你是我的暗种，这不叫监视，叫保护。"
/// </summary>
public class RedMoonClonePet : MonoBehaviour
{
    [Header("引用")]
    public Player owner;
    public Animator animator;

    [Header("跟随")]
    [Tooltip("相对玩家的悬浮偏移（默认脑袋正上方偏后）。")]
    public Vector3 followOffset = new Vector3(0f, 1.6f, -0.2f);
    [Tooltip("跟随的插值速度（越大越贴身）。")]
    public float followLerp = 8f;
    [Tooltip("是否根据玩家朝向镜像 followOffset.x（玩家向左时红月飘到右后方，反之亦然）。")]
    public bool mirrorByFacing = true;
    [Tooltip("强制保持 X=45° 倾斜，统一战斗视角。")]
    public bool forceTiltX45 = true;

    [Header("呼吸悬浮")]
    [Tooltip("上下浮动幅度（米）。")]
    public float hoverAmplitude = 0.12f;
    [Tooltip("浮动频率（Hz）。")]
    public float hoverFrequency = 1.5f;

    [Header("眨眼（可选）")]
    [Tooltip("平均每次眨眼的间隔（秒）。挂了 Animator 且带 \"Blink\" trigger 才生效。")]
    public float blinkIntervalMin = 3f;
    public float blinkIntervalMax = 7f;

    private static readonly int ANIM_BLINK = Animator.StringToHash("Blink");
    // 【性能】红月分身 sprite 全局静态缓存：LoadSpriteFallback 内部含 Blit + BFS 抠图，很昂贵
    private static Sprite s_petSpriteCache;
    private static bool   s_petSpriteTried;

    private float _hoverPhaseSeed;
    private float _lastHoverApplied;
    private float _blinkTimer;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _hoverPhaseSeed = Random.Range(0f, Mathf.PI * 2f);
        ScheduleNextBlink();

        // sprite 自动从 Resources/Wolf/ 加载（避免 prefab 时序里 sprite 引用 GUID 不稳定）。
        // 只在 SpriteRenderer 存在但 sprite 缺失时才补，不覆盖手工配置。
        // 走 BulletParasite.LoadSpriteFallback，兼容 Unity 把 png 导入为 Texture2D 而非 Sprite 的情况。
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
        {
            if (s_petSpriteCache == null && !s_petSpriteTried)
            {
                s_petSpriteTried = true;
                s_petSpriteCache = BulletParasite.LoadSpriteFallback("Wolf/RedMoonClonePet_sprite");
            }
            if (s_petSpriteCache != null) sr.sprite = s_petSpriteCache;
        }
    }

    private void Start()
    {
        if (owner == null) owner = FindObjectOfType<Player>();
        IgnoreCollisionWithOwner();
        ApplyTiltX45();
    }

    // 【性能】倾斜角度只在 Start 里设一次；Update 里不必再写 transform.rotation。
    // 45° 倾斜是"死值"，除非有别的东西每帧改宠物 rotation，否则完全没必要每帧回填。

    private void Update()
    {
        if (owner == null) return;

        // 跟随：追到 owner 头顶偏后
        Vector3 offset = followOffset;
        Transform ot = owner.transform;
        if (mirrorByFacing && ot.localScale.x < 0f)
            offset.x = -offset.x;
        Vector3 target = ot.position + offset;

        // 直接位置合并进 LateUpdate 的呼吸计算，减少一次 transform.position 写入
        Vector3 curPos = transform.position;
        curPos = Vector3.Lerp(curPos, target, Mathf.Clamp01(followLerp * Time.deltaTime));
        transform.position = curPos;

        UpdateBlink();
    }

    private void LateUpdate()
    {
        // 呼吸悬浮：撤销上一帧再重新叠加，避免累积
        Vector3 p = transform.position;
        p.y -= _lastHoverApplied;
        float t = Time.time * hoverFrequency * 2f * Mathf.PI + _hoverPhaseSeed;
        float dy = Mathf.Sin(t) * hoverAmplitude;
        p.y += dy;
        transform.position = p;
        _lastHoverApplied = dy;
    }

    private void ApplyTiltX45()
    {
        if (!forceTiltX45) return;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    private void ScheduleNextBlink()
    {
        _blinkTimer = Random.Range(blinkIntervalMin, blinkIntervalMax);
    }

    private void UpdateBlink()
    {
        _blinkTimer -= Time.deltaTime;
        if (_blinkTimer > 0f) return;
        if (animator != null)
        {
            // 有 Blink trigger 就播一下；没有则静默跳过（不会报错）
            animator.ResetTrigger(ANIM_BLINK);
            animator.SetTrigger(ANIM_BLINK);
        }
        ScheduleNextBlink();
    }

    private void IgnoreCollisionWithOwner()
    {
        if (owner == null) return;
        Collider[] mine = GetComponentsInChildren<Collider>(true);
        Collider[] hers = owner.GetComponentsInChildren<Collider>(true);
        foreach (var a in mine)
        {
            if (a == null) continue;
            foreach (var b in hers)
            {
                if (b == null) continue;
                Physics.IgnoreCollision(a, b, true);
            }
        }
    }
}
