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
    [Tooltip("相对玩家位置的偏移量。X 是水平偏移（负=左，正=右），Y 是高度，Z 是前后（45° 视角下负=前/正=后）。")]
    public Vector3 followOffset = new Vector3(-1.5f, 0.3f, -0.3f);
    [Tooltip("跟随的插值速度（越大越贴身）。")]
    public float followLerp = 8f;
    [Tooltip("是否根据玩家朝向镜像 followOffset.x。true=永远在玩家左肩（面朝方向的那一侧）")]
    public bool mirrorByFacing = true;
    [Tooltip("根据玩家朝向自动翻转 sprite 的 localScale.x（与玩家自身翻转一致）。45° 视角游戏中这是正确的。")]
    public bool mirrorSpriteByFacing = true;

    [Header("尺寸")]
    [Tooltip("sprite 渲染缩放（<1 更小，>1 更大）。用来把贴图调整到合适的视觉大小。")]
    public float spriteScale = 0.55f;

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

    private SpriteRenderer _sr;
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
        //
        // 【修复】之前走 BulletParasite.LoadSpriteFallback，内部会做 BFS 边缘泛洪抠背景：
        //   - 抠背景是运行时 CPU 密集操作（每帧图片遍历 + SetPixels32），首次加载会卡顿 0.5s+
        //   - 红月贴图本身带有深蓝近黑色背景，与红月主体反差不明显，BFS 容易过度抠掉红月边缘，导致"全透明"效果
        // 解决方案：直接用 Resources.Load<Sprite> 加载原始贴图（保留深蓝背景，在游戏场景里深蓝
        // 与灰暗背景融合良好，不会显得突兀），完全跳过运行时抠图，性能与视觉双赢。
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
        {
            if (s_petSpriteCache == null && !s_petSpriteTried)
            {
                s_petSpriteTried = true;
                s_petSpriteCache = Resources.Load<Sprite>("Wolf/RedMoonClonePet_sprite");
            }
            if (s_petSpriteCache != null) sr.sprite = s_petSpriteCache;
        }

        // 缩放 SpriteRenderer：让红月保持合适的视觉尺寸，不被 sprite 原始像素大小直接撑爆屏幕
        _sr = sr;
        if (_sr != null && spriteScale > 0f && spriteScale != 1f)
        {
            _sr.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);
        }
    }

    private void Start()
    {
        if (owner == null) owner = FindObjectOfType<Player>();
        IgnoreCollisionWithOwner();
    }

    private void Update()
    {
        if (owner == null) return;

        // 跟随：追到 owner 左肩（面朝方向那一侧）
        Vector3 offset = followOffset;
        Transform ot = owner.transform;
        if (mirrorByFacing && ot.localScale.x < 0f)
            offset.x = -offset.x;
        Vector3 target = ot.position + offset;

        Vector3 curPos = transform.position;
        curPos = Vector3.Lerp(curPos, target, Mathf.Clamp01(followLerp * Time.deltaTime));
        transform.position = curPos;

        // 45° 倾斜视角
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        // 根据玩家朝向翻转 sprite（与玩家自身 flip 逻辑一致：sprite 的 localScale.x 取反）
        if (mirrorSpriteByFacing && _sr != null)
        {
            Vector3 s = _sr.transform.localScale;
            s.x = Mathf.Abs(s.x) * (ot.localScale.x < 0f ? -1f : 1f);
            _sr.transform.localScale = s;
        }

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
