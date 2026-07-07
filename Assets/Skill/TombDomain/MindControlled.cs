using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 「亡者领域」复活/控制组件：附加在原本的 enemy（含 WorldBossBase）上，把它转成"被控制的友军"。
/// - 接管 FixedUpdate（替代 enemy.FixedUpdate），让它去攻击其他敌人；
/// - 暗影岛深绿覆盖层（区分敌人/友军）；
/// - 小怪每秒掉自身 maxHP×2% 血量；超过 lifetime 自动死亡；
/// - 世界 Boss 永久存在但血条仍可被打死；超过 leashDistance 拉回玩家身边；
/// - 提供静态方法：HealAllControlledBosses（玩家受伤时治疗）、FindHighPriorityTargetForEnemy
///   （让其他敌人优先攻击被控制的世界 Boss）、IsMindControlled（友军判定）。
///
/// === 性能优化要点（2026-06，配合"复活后倍率 2.5×、加头顶标记"修订）===
/// 旧版每个友军：
///   1) Update 里独立计算流光色 + 写 SpriteRenderer.color（每个组件一份 Update 调用）；
///   2) FixedUpdate 里调用 GameObject.Find("enemylayer") + 全层遍历找最近敌人，O(M) × 50Hz × N 友军；
///   3) 每个 FixedUpdate 写 5+ 个 SpriteRenderer 字段同步 overlay，多数帧无变化也照写；
///   4) overlay 用 new Material(...)，每个友军一份独立 material，破坏 sprite 合批。
/// 新版：
///   1) 流光由【单个 static driver(MonoBehaviour)】每帧统一推进所有友军颜色 → 只产生 1 次
///      Update callback，省下 N 次 Mono 回调成本；SpriteRenderer.color 走顶点色路径，仍然合批。
///   2) overlay 改用 sharedMaterial，所有友军共享一份 → sprite 合批不再被打断。
///   3) enemylayer Transform 静态缓存 + 失效自动重取。
///   4) FindAttackTarget 改为按时间节流（默认每 0.2s 搜一次），减小 50Hz × N 的扫描压力。
///   5) SyncOverlayFrame 仅在 sprite/flip 变化时写入，多数帧 0 写。
///
/// === 视觉新增 ===
/// 在每个友军头顶生成"亡者徽记"小图案（共享 sprite + 共享 material，从 Resources/UI/AllySkullMark
/// 加载本地 PNG 素材），表示该敌人被 AI 控制。徽记**作为友军的子对象**挂载（自动跟随移动），
/// 用反父级 lossyScale 抵消缩放 → **世界尺寸恒定 0.3 unit**：以小蘑菇头顶视觉为基准（这个尺寸
/// 在小怪上刚好够看清又不喧宾夺主），世界 Boss（lossyScale≈20）头顶也是同样 0.3 unit 的徽记
/// （Boss 体型大不需要徽记再放大才"显眼"——sprite 本身的紫色高对比已经足够辨识）。
/// 兼容性历史：v1 spriteWidth 比例缩放 → boss 上盖半屏；v3 跟随父级 lossyScale → boss 上 6 unit
/// 仍然过大盖屏。回到 v2 恒定 0.3 是经过多次迭代的最佳平衡点。
/// alpha 随流光"呼吸"——让玩家在远处也能一眼看出"这个怪是友军"。
/// 注：脚下紫色光圈（旧版 AllyRing）已于 2026-06 移除，统一改为只有头顶徽记一种识别符号。
/// </summary>
public class MindControlled : MonoBehaviour
{
    public bool isWorldBoss = false;
    public float minionLifetime = 15f;
    public float minionDecayPerSecond = 0.02f;
    // 2026-06 二次调整：12 → 24 → 48（再翻倍）。
    //   bossLeashDistance 现在仅作为"开始向玩家走过去"的临界距离，不再是瞬移阈值。
    //   超过 bossLeashDistance × bossTeleportFactor（默认 2×=96）才会判定为"距离不正常的远"
    //   触发瞬移。让友军 Boss 在中等脱离距离上自然向玩家走回，避免顿挫的瞬移视觉。
    public float bossLeashDistance = 48f;

    /// <summary>
    /// 距离 > bossLeashDistance × bossTeleportFactor 才触发瞬移；之间的区间走"步行回归"。
    /// 默认 2×：48 走 / 96 瞬移。
    /// </summary>
    public float bossTeleportFactor = 2f;

    /// <summary>被控制的世界Boss每2分钟扣除50%当前生命值（一次性扣除）。</summary>
    public float bossHpDecayInterval = 120f;
    private float _bossDecayTimer = 0f;

    /// <summary>
    /// 步行回归时的速度（unit/秒）。取 max(_en.speed, this) 后再乘 _allySpeedMultiplier，
    /// 确保即使原 boss 速度很慢，回归玩家时也不会"龟爬"。
    /// </summary>
    private const float _bossLeashWalkbackMinSpeed = 5f;

    // 友军覆盖层走"动态流光"：在三种基础色之间循环插值，让被控制的友军在战场上极易识别。
    // 选用「亡者紫 → 暗影绿 → 月华青」三色环，搭配脉动白光高亮，整体观感是"灵能流光"。
    private static readonly Color OverlayColorA = new Color(0.65f, 0.30f, 0.95f, 1f); // 幽冥紫
    private static readonly Color OverlayColorB = new Color(0.18f, 0.62f, 0.30f, 1f); // 暗影绿
    private static readonly Color OverlayColorC = new Color(0.20f, 0.85f, 0.95f, 1f); // 月华青
    // 流光周期（秒）：一整圈三色循环 + 高光脉动
    private const float OverlayCycleSeconds = 2.4f;
    // 高光脉动：在基础色上额外往白色拉的最大量（0~1），让友军每个周期闪一次"灵能爆光"
    private const float OverlayHighlightAmount = 0.35f;

    // 友军伤害数字：在 45° 视角的绿色友军身体上，原 prefab 的红色字会被身体遮住、绿字又会和身体融色。
    // 改用高亮的"魔法紫"，并抬到敌人头顶上方一点，避免被友军挡住。
    private static readonly Color AllyDamageColor = new Color(0.95f, 0.55f, 1.00f, 1f);
    // 头顶"亡者徽记"颜色：与流光统一的紫色基底，呼吸只调 alpha
    private static readonly Color AllyMarkColor = new Color(0.85f, 0.45f, 1.00f, 1f);
    // 注意：脚下紫色光圈（AllyRingColor / ApplyAllyRing）已于 2026-06 移除——
    //   策划反馈光圈视觉冗余，统一改为只有头顶徽记一种"被控制"识别符号。

    private enemy _en;
    private Rigidbody _rb;
    // 2026-06 修"被控制 boss 只播待机不播行走"：缓存 Animator，由 MindControlled 接管
    // 后亲自驱动 `ismove` 参数（enemy 基类 FixedUpdate 在 WorldBossBase 第 45 行被短路了，
    // 不会再 SetBool）。Setup 里赋值，FixedUpdate 里移动/停止时同步设置。
    private Animator _aniRef;
    private float _aliveTimer;
    private float _decayAccum;
    private float _attackCooldown;
    // 攻击间隔（秒）：原 1.2 太慢、加上绿色伤害字看不见 → 用户觉得"不知道在攻击"。
    // 友军移速 1.5×，攻击节奏也跟上，缩短到 0.5。
    private const float _attackInterval = 0.5f;
    private const float _attackRange = 2.0f;
    // 友军移速倍率：被复活后比原速略快，方便友军接战（之前 2.5× 太快，现回调到 1.5×）
    private const float _allySpeedMultiplier = 1.5f;
    // 流光时间轴：每个友军独立相位，避免一群友军同步闪烁过于刺眼
    private float _overlayPhase;

    // —— 索敌节流：每 _findInterval 秒重算一次最近敌人，期间复用 _cachedTarget ——
    private const float _findInterval = 0.2f;
    private float _findTimer;
    private Transform _cachedTarget;

    // 蝙蝠保留它原生的"飞行+俯冲"行为，由 Bat.FixedUpdate 自驱，
    // MindControlled 只每帧喂入"最近敌人 GameObject"作为 role，并切走视觉/血量管理。
    private Bat _batSelfDriven;

    private SpriteRenderer _baseRenderer;
    private SpriteRenderer _overlayRenderer;
    private SpriteRenderer _allyMark; // 头顶徽记（脚下紫环已移除）

    // 第一次实际创建头顶徽记时打一行日志，明确报告"标记世界尺寸 = X unit"
    private static bool _markSizeLogged;

    // —— SyncOverlayFrame 节流：仅在 sprite 或 flip 变化时写 ——
    private Sprite _lastSyncedSprite;
    private bool   _lastSyncedFlipX;
    private bool   _lastSyncedFlipY;

    // 头顶徽记世界直径（unit）——分"小怪 / 世界 Boss"两档。
    //   • 小怪（_markWorldDiameterMinion = 0.3）：恒定世界 0.3 unit（反抵消父级 lossyScale），
    //     因为小怪本身 lossyScale=1，"恒定 / 跟随"两种取法等价，定 0.3 后头顶视觉刚好。
    //   • 世界 Boss（_markBossLocalScale = 0.1）：**直接继承父级 lossyScale**（不抵消），
    //     boss 整体 lossyScale ≈ 20 → 世界直径 ≈ 2 unit，既明显大于小怪的 0.3，又远小于
    //     v3 时代 6 unit 的盖屏尺寸；徽记会随 boss 体型呼吸/缩放动画一起放大缩小，与"画面
    //     缩放同步"的策划诉求一致。
    //
    //   完整迭代史（避免后人重复踩坑）：
    //   • v1：spriteWidth × 比例 + 子物体继承 scale → 蘑菇王 lossyScale=20 上盖半屏。
    //   • v2：localScale = 1/lossyScale × 0.3，世界尺寸恒定 0.3。boss 上"太小"且不随体型变。
    //   • v3：localScale = 0.3 直接继承 lossyScale，boss 上 6 unit 盖屏。
    //   • v4：再回 v2 恒定 0.3。boss 和小怪同尺寸，被反馈"看不出主次、boss 该明显更大"。
    //   • v5（当前）：分档——小怪 v2，boss v3 但用更小基数 0.1（≈2 unit）。
    //     既保证"boss > 小怪"的视觉层级，又不会盖屏；且 boss 上跟随父级缩放，符合"随画面
    //     同步缩放"的诉求。
    private const float _markWorldDiameterMinion = 0.3f;
    // boss localScale（不抵消父级）：父级 lossyScale ≈ 20，sprite 几何尺寸 ≈ 2.56 unit
    //   （PNG 256×256 px / pixelsPerUnit=100），所以世界直径 = 2.56 × 0.04 × 20 ≈ 2 unit。
    //   v6 下调：v5 的 0.1 实测世界直径 = 2.56 × 0.1 × 20 ≈ 5 unit，徽记几乎覆盖 boss 半个身体
    //   （用户截图反馈"太大"），下调到 0.04 让世界直径回到 ~2 unit 的设计目标。
    private const float _markBossLocalScale     = 0.04f;
    // 头顶徽记距 sprite 顶端的世界抬升量（unit），让徽记"浮"在头顶之上而非压在头部里。
    //   • 2026-06 一路下调：0.05 → 0 → -0.05 → -0.1 → -0.5 → -1.0 → -2.0 → **分档（v6）**。
    //     v5 之前所有体型用同一常量 -2，对小怪（lossyScale=1）刚好下沉到视觉头顶；
    //     但 boss（lossyScale=20）的 sprite bounds 是按 20 倍放大的，"留白像素 + 特效像素"
    //     在世界空间也按 20 倍放大 → b.max.y 比"视觉头顶"高出十几个 unit，固定 -2 unit
    //     根本盖不住，徽记仍漂在 boss 头顶很高的空中。
    //   • v6：小怪保持 -2；boss 改用 `_markWorldLift × parentLossy.y × _bossLiftScale`
    //     按 boss 体型动态放大，让"视觉下沉量"与 boss 体型同比例缩放。
    //
    //   正值 = 抬高，负值 = 下沉。
    private const float _markWorldLift = -2f;
    // boss 抬升量倍率：boss 上 effectiveLift = _markWorldLift × parentLossy.y × _bossLiftScale。
    //   推算：boss lossyScale.y ≈ 20，b.max.y ≈ position.y + 10 unit。
    //     • 系数 0.30（v6 旧值） → effectiveLift = -12，b.max.y - 12 = position.y - 2，落在 boss 中下部
    //       /地面之间（用户截图反馈"太低"）；
    //     • 系数 0.15（v7 当前）→ effectiveLift = -6，b.max.y - 6 = position.y + 4，落到 boss 上半身/
    //       视觉头部位置——既明显是这只 boss 的徽记又不会下沉到地面。
    //     • 系数 1.0 → -40 会埋到地下；系数 0.5 → -20 ≈ boss 脚底。
    private const float _bossLiftScale = 0.15f;

    // 世界 Boss 被 leash 拽回时的 Y 处理策略：保留 Boss 原本的 Y，不参考玩家 Y。
    //   • v1：3D 向量 normalize → 跟玩家 Y → 半截埋地。
    //   • v2：max(boss.y, player.y) + 0.5 → 玩家在空中时 Boss 被顶到半空（截图所见）。
    //   • v3（当前实现）：直接 newPos.y = cur.y。Boss 原本就在地面 Y 上行走，传送只动 X/Z。
    //   保留这里的注释作为历史溯源；常量已删除（不再有任何"额外抬升"的需求）。

    // ===== 共享徽记资产：所有友军共用同一张 sprite + 同一个 material，可被批合并，无 GC =====
    // （脚下紫环资产 _sharedRingSprite 已于 2026-06 删除；_sharedMarkMaterial 仅服务头顶徽记。）
    private static Sprite   _sharedMarkSprite;
    private static Material _sharedMarkMaterial;
    private static Material _sharedOverlayMaterial; // overlay 走共享材质，避免逐 instance new Material

    // ===== enemylayer 缓存（避免 50Hz × N 友军 调 GameObject.Find） =====
    private static Transform _enemyLayerCached;

    private static readonly List<MindControlled> _all = new List<MindControlled>();
    public static IReadOnlyList<MindControlled> All => _all;

    public bool IsAlive => _en != null && _en.health > 0 && _en.rolestate != enemy.state.dead;

    /// <summary>对外暴露被控制的 enemy 实例（只读），用于 UI 读取血量/精灵等。</summary>
    public enemy Enemy => _en;

    // 复活演出冻结：true 时 FixedUpdate 跳过移动/索敌/SetBool，让 ReviveBossEffect 独占
    // Animator 控制权。仅用于"龙眼演出 + 反向死亡"阶段，演出结束后由 ReviveBossEffect 解冻。
    private bool _frozenForRevive;
    public void SetReviveFreeze(bool freeze) { _frozenForRevive = freeze; }

    void OnEnable()
    {
        _all.Add(this);
        EnsureFlowDriver();
        // 重新挂载/对象池 SetActive 后，enemy.OnEnable 会把 _mindControlledFlag 强制置 false。
        // MindControlled.OnEnable 执行更晚（脚本顺序无保证，靠 Component 顺序），但只要它跑过一次
        // 就把 flag 重新写回 true，避免宠物/玩家子弹/技能误打友军 Boss。
        if (_en == null) _en = GetComponent<enemy>();
        if (_en != null) _en._mindControlledFlag = true;
    }
    void OnDisable() { _all.Remove(this); }

    public void Setup(enemy en, bool boss, float lifetime, float decayPerSec, float leash)
    {
        _en = en;
        isWorldBoss = boss;
        minionLifetime = lifetime;
        minionDecayPerSecond = decayPerSec;
        bossLeashDistance = leash;
        _aliveTimer = 0f; _decayAccum = 0f;

        if (_en != null)
        {
            // 性能：通知 enemy 基类"我是友军了"，让它的 FixedUpdate / OnCollisionEnter / getrole
            // 直接读 flag，而不是每帧 GetComponent<MindControlled>()（N 只怪 × 50Hz 的大头）。
            _en._mindControlledFlag = true;

            // 关键修复：彩色蘑菇（孢子异变）会创建 __SporeMutationRenderer 子物体显示彩色 overlay，
            //   并把原 SpriteRenderer.enabled = false。如果不清理，复活后：
            //   (1) 七彩 overlay 仍开着，遮住了 MindControlled 的紫色友军 overlay → 视觉上"没复活"；
            //   (2) base SpriteRenderer.enabled 仍是 false → SyncOverlayFrameIfChanged 取不到 sprite。
            //   先调用 ClearSporeMutationColor 把 base 恢复 enable、关掉七彩 overlay，再让
            //   ApplyShadowIslesOverlay 接管视觉表现。
            _en.ClearSporeMutationColor();

            _en.rolestate = enemy.state.idle;
            _en.role = null; // 切断对玩家的索敌——MindControlled 自己来调度移动/攻击目标
            _en.health = Mathf.Max(1, _en.healthmax);
            foreach (var col in _en.GetComponents<Collider>()) col.enabled = true;
            _en.StopAllCoroutines();

            // 友军移速 ×1.5：让被复活的友军比原速略快接战
            // enemy.speed 是 int，需要显式 round 后回填，避免 float→int 的隐式转换错误
            _en.speed = Mathf.Max(1, Mathf.RoundToInt(_en.speed * _allySpeedMultiplier));

            // 蝙蝠特判：保留它原生的"飞行+俯冲"行为，不让 MindControlled 接管移动/攻击。
            // 只切换它的"友军模式开关"，OnTriggerEnter 会去打敌人而不是玩家。
            _batSelfDriven = _en as Bat;
            if (_batSelfDriven != null) _batSelfDriven.isAllyMode = true;

            // 把 Rigidbody 切为 kinematic，避免：
            //  1) 友军被其他 enemy 推开（用户描述的"互顶"现象）
            //  2) MovePosition 与物理推力打架
            // 这样 MindControlled 用 MovePosition 来"软推动"目标，不再被 PhysX 反弹。
            // 但蝙蝠是飞行单位，自己用 transform.position 直接移动，强行 kinematic 会破坏它的飞行
            // 反而让它"卡在原地"——所以蝙蝠保持原有 Rigidbody 设置不变。
            _rb = _en.GetComponent<Rigidbody>();
            if (_rb != null && _batSelfDriven == null)
            {
                _rb.velocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _rb.useGravity = false;
                _rb.isKinematic = true;
            }

            // 2026-06 修"被控制 boss / 友军只播待机不播行走"：
            //   原因：WorldBossBase.FixedUpdate 在检测到 MindControlled 后直接 return（第 45 行），
            //   普通 enemy.FixedUpdate 也走不到 case state.move/idle 的 SetBool 分支了——
            //   所以 Animator 参数 ismove 从此再没人改，永远停在默认 false → 只播 idle。
            //   解决：MindControlled 自己缓存 Animator，每次移动/停止时主动 SetBool。
            //   蝙蝠分支（_batSelfDriven）由 Bat 自己 SetMove，不在这里管。
            _aniRef = _en.GetComponent<Animator>();
        }
        ApplyShadowIslesOverlay();
        ApplyAllyHeadMark();
        // 注：脚下紫色光圈 ApplyAllyRing 已于 2026-06 移除——策划反馈光圈视觉冗余，
        //   统一改为只用头顶徽记一种"被控制"识别符号，且徽记按敌人体型动态适配。

        // 给每个友军一个随机相位，避免一群友军同步流光看起来像一片闪光弹
        _overlayPhase = UnityEngine.Random.value * OverlayCycleSeconds;
    }

    void FixedUpdate()
    {
        if (_en == null || _en.rolestate == enemy.state.dead || _en.health <= 0)
        {
            Destroy(this);
            return;
        }

        // 复活演出期间：不移动、不索敌、不改 Animator 参数；
        // 让 ReviveBossEffect 独占 Animator（反向死亡动画 → 走路）。
        if (_frozenForRevive) return;

        if (!isWorldBoss)
        {
            _aliveTimer += Time.fixedDeltaTime;
            _decayAccum += Time.fixedDeltaTime;
            if (_decayAccum >= 1f)
            {
                int loss = Mathf.Max(1, Mathf.RoundToInt(_en.healthmax * minionDecayPerSecond));
                _en.health -= loss;
                _decayAccum -= 1f;
                if (_en.health <= 0) { _en.Destroy1(); return; }
            }
            if (_aliveTimer >= minionLifetime) { _en.Destroy1(); return; }
        }

        // 世界Boss：每2分钟一次性扣除50%当前生命值
        if (isWorldBoss)
        {
            _bossDecayTimer += Time.fixedDeltaTime;
            if (_bossDecayTimer >= bossHpDecayInterval)
            {
                _bossDecayTimer = 0f;
                int decay = Mathf.Max(1, _en.health / 2);
                _en.health -= decay;
                if (_en.health <= 0) { _en.Destroy1(); return; }
            }
        }

        // 世界 Boss 的 leash 行为（2026-06 二次重构）：
        //   旧版：超过 leash 距离直接瞬移到 leash 边缘 → 玩家在中等距离脱离时也会看到顿挫的传送。
        //   新版分两段：
        //     (a) leash < d ≤ leash × bossTeleportFactor：boss 朝玩家"走过去"（用 MovePosition 推动），
        //         不再瞬移；当 d 自然降回 leash 之内时回归正常索敌行为。
        //     (b) d > leash × bossTeleportFactor：判定为"距离不正常的远"（被异常物理推飞 / 玩家
        //         穿越场景边缘等），此时再瞬移到 leash 边缘，避免 boss 走超长路径导致玩家长时间
        //         脱节。
        //   Y 轴策略沿用 v3：完全保留 Boss 原本的 Y，不参考玩家 Y（避免半截埋地或被顶到空中）。
        if (isWorldBoss)
        {
            Player p = FindLivingPlayer();
            if (p != null)
            {
                Vector3 cur = transform.position;
                Vector3 pp  = p.transform.position;
                Vector3 flatDelta = new Vector3(pp.x - cur.x, 0f, pp.z - cur.z);
                float d = flatDelta.magnitude;
                float teleportThreshold = bossLeashDistance * bossTeleportFactor;

                if (d > teleportThreshold)
                {
                    // (b) 距离不正常的远 → 瞬移到 leash 边缘
                    Vector3 dir = flatDelta.normalized;
                    float step = d - bossLeashDistance;
                    Vector3 newPos = cur + dir * step;
                    newPos.y = cur.y;
                    if (_rb != null) _rb.MovePosition(newPos); else transform.position = newPos;
                }
                else if (d > bossLeashDistance)
                {
                    // (a) 中等脱离距离 → 朝玩家走回（不瞬移）
                    Vector3 dir = flatDelta.normalized;
                    float spd = Mathf.Max(_bossLeashWalkbackMinSpeed, _en != null ? _en.speed : 0f);
                    float maxStep = spd * Time.fixedDeltaTime;
                    // 不要走过头：最多走到 leash 边缘后停下，让"走回"不会越过玩家
                    float step = Mathf.Min(maxStep, d - bossLeashDistance);
                    Vector3 newPos = cur + dir * step;
                    newPos.y = cur.y;
                    if (_rb != null) _rb.MovePosition(newPos); else transform.position = newPos;
                    // 朝向 + 行走动画：脸朝玩家，播 ismove
                    float sca = Mathf.Abs(_en != null && _en.Sca != 0 ? _en.Sca : 1f);
                    transform.localScale = new Vector3((dir.x >= 0 ? 1f : -1f) * sca, sca, sca);
                    if (_aniRef != null) _aniRef.SetBool("ismove", true);
                }
                // d ≤ leash：什么都不做，让下方索敌 / 攻击逻辑自然接管
            }
        }

        // 节流的索敌：每 _findInterval 秒重算一次（也在缓存目标失效时立刻重算）
        _findTimer -= Time.fixedDeltaTime;
        bool cacheInvalid = (_cachedTarget == null) ||
                            (_cachedTarget != null && _cachedTarget.gameObject != null
                                && _cachedTarget.gameObject.activeInHierarchy == false);
        if (_findTimer <= 0f || cacheInvalid)
        {
            _cachedTarget = FindAttackTarget();
            _findTimer = _findInterval;
        }
        Transform tgt = _cachedTarget;

        // 蝙蝠保留自身飞行+俯冲行为：把"最近敌人 GameObject"喂给 Bat.role 即可，
        // 由 Bat.FixedUpdate 自己去做飞行/俯冲/拉升，MindControlled 不接管移动与攻击。
        if (_batSelfDriven != null)
        {
            _en.role = (tgt != null) ? tgt.gameObject : null;
            SyncOverlayFrameIfChanged();
            return;
        }

        // 保险：每帧再切断一次对玩家的索敌（仅地面友军；蝙蝠分支已自管 role）
        _en.role = null;

        if (tgt != null)
        {
            float sca = Mathf.Abs(_en.Sca == 0 ? 1 : _en.Sca);
            float chazhi = tgt.position.x - transform.position.x;
            transform.localScale = new Vector3((chazhi > 0 ? 1 : -1) * sca, sca, sca);

            float dist = Vector3.Distance(transform.position, tgt.position);
            if (dist > _attackRange)
            {
                Vector3 dir = (tgt.position - transform.position); dir.y = 0; dir = dir.normalized;
                float spd = Mathf.Max(1f, _en.speed);
                Vector3 newPos = transform.position + dir * spd * Time.fixedDeltaTime;
                // 用 MovePosition 而不是直接改 transform.position，
                // kinematic Rigidbody + MovePosition 才能正确推开静态/动态 collider，不被反弹。
                if (_rb != null) _rb.MovePosition(newPos); else transform.position = newPos;
                // 驱动行走动画（修"被控制 boss 只播待机"）。Animator 可能为 null（无动画的简单怪），安全跳过。
                if (_aniRef != null) _aniRef.SetBool("ismove", true);
            }
            else
            {
                _attackCooldown -= Time.fixedDeltaTime;
                if (_attackCooldown <= 0f) { _attackCooldown = _attackInterval; DealMeleeHit(tgt); }
                // 进入近战范围 → 停下来打 → 切回待机动画
                if (_aniRef != null) _aniRef.SetBool("ismove", false);
            }
        }
        else
        {
            // 没有索敌目标 → 待机
            if (_aniRef != null) _aniRef.SetBool("ismove", false);
        }
        SyncOverlayFrameIfChanged();
    }

    public static void HealAllControlledBosses(int amount)
    {
        if (amount <= 0) return;
        for (int i = _all.Count - 1; i >= 0; i--)
        {
            MindControlled mc = _all[i];
            if (mc == null || !mc.isWorldBoss || !mc.IsAlive) continue;
            mc._en.health = Mathf.Min(mc._en.healthmax, mc._en.health + amount);
        }
    }

    public static Transform FindHighPriorityTargetForEnemy(Vector3 attackerPos)
    {
        Transform best = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < _all.Count; i++)
        {
            MindControlled mc = _all[i];
            if (mc == null || !mc.IsAlive) continue;
            float sq = (mc.transform.position - attackerPos).sqrMagnitude;
            // 优先世界 Boss
            if (mc.isWorldBoss) sq *= 0.25f;
            if (sq < bestSq) { bestSq = sq; best = mc.transform; }
        }
        return best;
    }

    public static bool IsMindControlled(Transform t)
    {
        if (t == null) return false;
        // 性能：先查 enemy 上已有的 flag，避免 GetComponent<MindControlled>()。
        // 玩家子弹索敌时每发都会跑这个，群战 N 只怪 × M 发子弹，差距明显。
        var en = t.GetComponent<enemy>();
        if (en != null) return en._mindControlledFlag;
        return t.GetComponent<MindControlled>() != null;
    }

    /// <summary>缓存 enemylayer Transform，避免每个 FixedUpdate 都做 GameObject.Find（O(scene)）。</summary>
    private static Transform GetEnemyLayer()
    {
        if (_enemyLayerCached == null)
        {
            GameObject go = GameObject.Find("enemylayer");
            if (go != null) _enemyLayerCached = go.transform;
        }
        return _enemyLayerCached;
    }

    private Transform FindAttackTarget()
    {
        Transform layer = GetEnemyLayer();
        if (layer == null) return null;
        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 self = transform.position;
        int n = layer.childCount;
        for (int i = 0; i < n; i++)
        {
            Transform e = layer.GetChild(i);
            if (e == null || e == transform) continue;
            enemy en = e.GetComponent<enemy>();
            if (en == null || en.health <= 0 || en.rolestate == enemy.state.dead) continue;
            if (en._mindControlledFlag) continue; // 是友军就跳过
            float sq = (e.position - self).sqrMagnitude;
            // 优先攻击敌方Boss（权值×0.3，距离感知更近）
            bool isBoss = en is BossMushroomMan || en is BossBat || en is WolfBoss || en is SlimeBoss
                       || en is WorldBossMushroomMan || en is WorldBossBat || en is WorldBossWolf || en is WorldBossSlime;
            float score = isBoss ? sq * 0.3f : sq;
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    private void DealMeleeHit(Transform target)
    {
        enemy en = target.GetComponent<enemy>();
        if (en == null || en.health <= 0) return;
        int dmg = Mathf.Max(1, (int)(_en.atk - en.def));
        en.health -= dmg;
        SpawnAllyDamageNumber(en, dmg);
        en.startturnred();
        // 亡者领域：标记"被友军打过"，让它在 Destroy1 时进入"友军击杀复活链路"（20%）
        TombDomainHook.MarkAllyDamage(en);
        if (en.health <= 0) en.Destroy1();
    }

    /// <summary>
    /// 友军造成伤害的飘字：抬到敌人头顶 + 放大 + 高亮紫色，
    /// 确保不会和绿色友军身体融色、不会被流光遮罩盖住，玩家一眼能看到"友军在打"。
    /// 由 MindControlled / Bat 友军模式 等共用——所以是 internal static。
    /// </summary>
    public static void SpawnAllyDamageNumber(enemy victim, int dmg)
    {
        if (victim == null || victim.atknumber == null) return;
        if (!DamageNumberSettings.Visible) return;

        // 抬到 sprite 包围盒顶部上方，避免被被打目标本身的精灵盖住
        Vector3 pos = victim.transform.position;
        SpriteRenderer sr = victim.GetComponent<SpriteRenderer>();
        if (sr != null) pos.y = sr.bounds.max.y + 0.25f;
        else            pos.y += 0.6f;

        GameObject num = Object.Instantiate(victim.atknumber, pos, Quaternion.identity);
        // 放大 1.4×，让"友军输出"更显眼
        num.transform.localScale *= 1.4f;
        var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.text = dmg.ToString();
            txt.color = AllyDamageColor;
            // 字号也直接放大，部分 atknumber prefab localScale 可能被忽略
            txt.fontSize *= 1.3f;
        }
    }

    /// <summary>
    /// 友军 Boss 的 leash 锚点：找到**最近的、活着的**玩家（不论是主玩家 Player 还是分身 Clone）。
    ///
    /// B3 修复（分身奇遇 × 亡者领域）：
    ///   旧实现 `t.CompareTag("Player")` 会漏掉人格解离生成的分身（tag="Clone"），导致：
    ///   主玩家在远处、分身被一群敌人围住时，被控制的世界 Boss 仍会朝主玩家 leash，
    ///   分身得不到友军 Boss 的支援。改成"取最近玩家"后：
    ///     • 没有分身时：等价于旧实现（唯一 Player 就是最近的）；
    ///     • 有分身时：友军 Boss 会自然向距它最近的玩家集结，实现"分身/主玩家都能享受亡者领域护卫"。
    ///
    /// 注意：这里只影响 leash（位置回归），治疗判定走 TombDomainHook.OnPlayerTookDamage 是另一个
    /// 路径——分身受伤同样会调 startturnred → OnPlayerTookDamage → HealAllControlledBosses，
    /// 对那条路径不需要改动，治疗机制本来就是"任一玩家受伤都治疗全部友军 Boss"。
    /// </summary>
    private Player FindLivingPlayer()
    {
        Transform pl = GameObject.Find("playerlayer")?.transform;
        if (pl == null) return null;

        Player best = null;
        float bestSq = float.MaxValue;
        Vector3 self = transform.position;
        foreach (Transform t in pl)
        {
            if (t == null) continue;
            // 同时接受主玩家（tag=Player）和人格解离分身（tag=Clone）
            if (!t.CompareTag("Player") && !t.CompareTag("Clone")) continue;
            Player p = t.GetComponent<Player>();
            if (p == null || p.health <= 0) continue;
            float sq = (t.position - self).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = p; }
        }
        return best;
    }

    private void ApplyShadowIslesOverlay()
    {
        if (_baseRenderer == null) _baseRenderer = _en.GetComponent<SpriteRenderer>();
        if (_baseRenderer == null) return;

        EnsureSharedOverlayMaterial();

        Transform old = transform.Find("__MindControlledOverlay");
        GameObject go = old != null ? old.gameObject : new GameObject("__MindControlledOverlay");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        _overlayRenderer = go.GetComponent<SpriteRenderer>();
        if (_overlayRenderer == null) _overlayRenderer = go.AddComponent<SpriteRenderer>();
        // 关键性能优化：用 sharedMaterial（不再 new Material），所有 overlay 共用 → 可 SRP 批合并
        _overlayRenderer.sharedMaterial = _sharedOverlayMaterial;
        _overlayRenderer.color = OverlayColorA; // 起始色，static driver 会每帧推进
        _overlayRenderer.enabled = true;
        _lastSyncedSprite = null; // 强制下次 sync
        SyncOverlayFrameIfChanged();

        // 与七彩蘑菇变异方案一致：原蘑菇等敌人材质可能不吃 SpriteRenderer.color，
        // 直接隐藏原图，让叠层(纯色 sprite)单独表现"被控制为友军"的视觉。
        _baseRenderer.enabled = false;
    }

    /// <summary>
    /// 公开版：复活演出（ReviveBossEffect）期间 MindControlled.FixedUpdate 被 _frozenForRevive
    /// 短路了，自然不会跑 SyncOverlayFrameIfChanged。但这段时间 Animator 正在反向播 dead
    /// → 正向播 move/idle，base SpriteRenderer.sprite **每帧都在变**。如果 overlay 不同步，
    /// 玩家看到的就是"死亡 pose 飘移"——即使 Animator 完全正确。
    ///
    /// ReviveBossEffect.Stage2/3 协程每帧调用本方法手动刷 overlay sprite，确保视觉跟上 Animator。
    /// 解冻后正常 FixedUpdate 会继续节流维护，无副作用。
    /// </summary>
    public void ForceSyncOverlayNow() { SyncOverlayFrameIfChanged(); }

    /// <summary>
    /// 节流版：仅当 sprite/flip 真的变化时才写入 overlay。
    /// 大多数静态 enemy（蘑菇 idle 等）一帧不变，省下 5+ 个 SpriteRenderer setter。
    /// </summary>
    private void SyncOverlayFrameIfChanged()
    {
        if (_overlayRenderer == null || _baseRenderer == null) return;
        Sprite s = _baseRenderer.sprite;
        bool fx = _baseRenderer.flipX;
        bool fy = _baseRenderer.flipY;
        if (s == _lastSyncedSprite && fx == _lastSyncedFlipX && fy == _lastSyncedFlipY) return;
        _overlayRenderer.sprite = s;
        _overlayRenderer.flipX = fx;
        _overlayRenderer.flipY = fy;
        _overlayRenderer.drawMode = _baseRenderer.drawMode;
        _overlayRenderer.size = _baseRenderer.size;
        _overlayRenderer.sortingLayerID = _baseRenderer.sortingLayerID;
        _overlayRenderer.sortingOrder = _baseRenderer.sortingOrder + 1;
        _lastSyncedSprite = s; _lastSyncedFlipX = fx; _lastSyncedFlipY = fy;
    }

    // 注：原 ApplyAllyRing（脚下紫色光圈）已删除。
    //   策划反馈：脚下圆环 + 身体流光 + 头顶徽记 三层视觉过于嘈杂，统一改为只保留头顶徽记。
    //   相关字段 _allyRing、AllyRingColor、_ringDiameterXxx 与圆环资产 _sharedRingSprite 也已一并清理。

    /// <summary>
    /// 头顶"亡者徽记"：从 Resources/UI/AllySkullMark 加载自定义 PNG（操纵灵魂的紫气丝线手套——
    /// 一只戴着深紫手套的手，从指尖垂下发光的紫色丝线、腕部缭绕灵魂紫气，象征"傀儡师在牵着这只
    /// 怪物的灵魂"；2026-06 由旧版"紫色骷髅+交叉骨"替换而来，文件名沿用以保留 .meta GUID 引用），
    /// 表示该敌人当前正被玩家 AI（亡者领域）控制。
    /// 共享 sprite + sharedMaterial，每个友军开销极低。
    ///
    /// === 父子关系：作为友军的子对象（跟随移动/朝向） ===
    /// 徽记 GameObject 挂在友军 transform 下，敌人移动/翻转时徽记自然跟随，无需额外位置同步代码。
    ///
    /// === 大小：小怪恒定 0.3 unit，世界 Boss 跟随父级 lossyScale（≈2 unit）===
    /// 完整迭代史见 `_markWorldDiameterMinion` / `_markBossLocalScale` 常量上方注释。
    /// 当前 v5 分档：
    ///   • 小怪：localScale = 0.3 × (1/lossyScale)，世界尺寸恒定 0.3 unit；
    ///   • 世界 Boss：localScale = 0.1（不抵消），继承父级 lossyScale ≈ 20 → 世界 ≈ 2 unit，
    ///     明显大于小怪、随 boss 缩放动画同步呼吸，但不像 v3 (6 unit) 那样盖屏。
    /// localPosition.y 永远按 1/parent.lossyScale.y 反算——位置不能跟尺寸一起放大，否则 boss
    /// 头顶徽记会被顶到天上去。
    ///
    /// === 头顶位置 ===
    /// sprite pivot 在 EnsureSharedMarkAssets 里被重定为 (0.5, 0.0)（图案"底端中点"为锚点），
    /// 这样把世界 Y = b.max.y + 微抬 反算到 localPosition.y 后，徽记就稳稳地"坐"在敌人头顶上方。
    /// </summary>
    private void ApplyAllyHeadMark()
    {
        EnsureSharedMarkAssets();

        // PNG 资源不可用（Resources/UI/AllySkullMark 缺失）→ 直接跳过头顶标记。
        if (_sharedMarkSprite == null) return;

        Bounds b = (_baseRenderer != null) ? _baseRenderer.bounds
                                            : new Bounds(transform.position, Vector3.one);

        // 父级世界缩放。lossyScale 体现"自父级层层继承下来的最终世界缩放"——蘑菇王整体 scale=20
        // 时这里就是 ~20。后面用它的倒数来做反向抵消：
        //   • 位置 invSy：让"距 sprite 顶端 lift unit"在世界空间是常量距离（**永远抵消位置**，
        //     否则 boss 头顶徽记会被 ×20 顶到天上）；
        //   • 尺寸：仅小怪用 invS 抵消获得恒定 0.3 unit；世界 Boss 不抵消，让徽记跟随 boss 体型
        //     一起缩放（×20 后 ≈ 2 unit，明显大于小怪、随画面同步呼吸）。
        Vector3 parentLossy = transform.lossyScale;
        float invSx = Mathf.Approximately(parentLossy.x, 0f) ? 1f : 1f / parentLossy.x;
        float invSy = Mathf.Approximately(parentLossy.y, 0f) ? 1f : 1f / parentLossy.y;
        float invSz = Mathf.Approximately(parentLossy.z, 0f) ? 1f : 1f / parentLossy.z;

        // 徽记的"世界顶部 Y"：以 sprite 包围盒顶端为基准 + 抬升量（不再随徽记直径变化）。
        // 配合 sprite pivot=(0.5, 0.0)（底端中点为锚点），徽记的底边正好坐在该 Y 高度。
        // ★ 抬升量分档：
        //   • 小怪：固定 _markWorldLift（-2 unit），sprite bounds 跟视觉差不多，-2 已经能压到头部；
        //   • boss：_markWorldLift × parentLossy.y × _bossLiftScale 动态缩放，
        //     boss sprite bounds 按 ×20 放大后顶端浮在 position+10 处，固定 -2 完全不够，
        //     必须按体型成比例下沉。boss 上 effectiveLift ≈ -2 × 20 × 0.3 = -12 unit。
        float effectiveLift = isWorldBoss
            ? _markWorldLift * Mathf.Abs(parentLossy.y) * _bossLiftScale
            : _markWorldLift;
        float markBottomWorldY = b.max.y + effectiveLift;
        // 反算到 localPosition.y：因为 child 实际世界 Y = parent.position.y + localY * parent.lossyScale.y。
        float headLocalY = (markBottomWorldY - transform.position.y) * invSy;

        Transform old = transform.Find("__MindControlledHeadMark");
        GameObject go = old != null ? old.gameObject : new GameObject("__MindControlledHeadMark");
        // 作为友军的子对象：跟随父对象移动/翻转，无需额外位置同步逻辑。
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, headLocalY, 0f);
        // 让徽记朝相机（保持 XY 平面，不绕 X 转 90°，否则在 45° 视角下会被"压扁"看不清）。
        // 用 localRotation = identity 保留与父对象**相同朝向**，但因为父对象只在 X 轴翻转
        // （localScale.x = ±sca）做朝向，sprite 自身会跟着翻——头顶徽记是对称图案，翻转无视觉差异。
        go.transform.localRotation = Quaternion.identity;
        // ★ 尺寸分档：
        //   • 世界 Boss：localScale 恒定 _markBossLocalScale (0.1)，**不抵消**父级 lossyScale，
        //     让徽记继承 boss ×20 缩放 → 世界直径 ≈ 2 unit，与画面缩放同步、明显大于小怪。
        //   • 小怪：localScale = _markWorldDiameterMinion (0.3) × 1/lossyScale，
        //     反抵消后世界直径恒定 0.3 unit。
        if (isWorldBoss)
        {
            go.transform.localScale = new Vector3(
                _markBossLocalScale, _markBossLocalScale, _markBossLocalScale);
        }
        else
        {
            go.transform.localScale = new Vector3(
                _markWorldDiameterMinion * invSx,
                _markWorldDiameterMinion * invSy,
                _markWorldDiameterMinion * invSz);
        }

        _allyMark = go.GetComponent<SpriteRenderer>();
        if (_allyMark == null) _allyMark = go.AddComponent<SpriteRenderer>();
        _allyMark.sprite         = _sharedMarkSprite;
        _allyMark.sharedMaterial = _sharedMarkMaterial;
        _allyMark.color          = AllyMarkColor;
        _allyMark.sortingOrder   = 32767;

        if (!_markSizeLogged)
        {
            if (isWorldBoss)
            {
                float worldDiameter = _markBossLocalScale * Mathf.Abs(parentLossy.x);
                Debug.Log($"[亡者领域·标记] 世界 Boss 头顶徽记：localScale={_markBossLocalScale:F2}（继承父级 lossyScale.x={parentLossy.x:F2}）" +
                          $" → 世界直径 ≈ {worldDiameter:F2} unit。");
            }
            else
            {
                Debug.Log($"[亡者领域·标记] 小怪头顶徽记固定世界尺寸={_markWorldDiameterMinion:F2} unit；" +
                          $"父级 lossyScale={parentLossy}，抵消后 localScale=({_markWorldDiameterMinion*invSx:F3},{_markWorldDiameterMinion*invSy:F3})。");
            }
            _markSizeLogged = true;
        }
    }

    /// <summary>
    /// 加载头顶徽记的共享 sprite + material（仅一次）。
    /// - sprite 从 Resources/UI/AllySkullMark.png 加载（图案为"紫气丝线手套"——历史命名沿用），
    ///   重定 pivot=(0.5, 0.0)（底端中点）；
    /// - material 用 Sprites/Default 共享，所有友军合批。
    /// </summary>
    private static void EnsureSharedMarkAssets()
    {
        if (_sharedMarkSprite != null && _sharedMarkMaterial != null) return;

        if (_sharedMarkMaterial == null)
        {
            var sh = Shader.Find("Sprites/Default");
            _sharedMarkMaterial = new Material(sh) { name = "MindControlledMarkMat" };
        }

        if (_sharedMarkSprite == null)
        {
            Sprite raw = Resources.Load<Sprite>("UI/AllySkullMark");
            if (raw != null && raw.texture != null)
            {
                Texture2D tex = raw.texture;
                // 重定 pivot 到底端中点 (0.5, 0.0)：让 ApplyAllyHeadMark 里
                //   localPosition.y = b.max.y + lift 时，徽记的"底边"就锚在头顶上方一点，
                //   整体图案就稳稳地"坐"在敌人头顶之上而不是穿插进头部里。
                _sharedMarkSprite = Sprite.Create(
                    tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.0f),
                    raw.pixelsPerUnit > 0f ? raw.pixelsPerUnit : 100f,
                    0,
                    SpriteMeshType.FullRect);
                _sharedMarkSprite.name = "AllySkullMarkBottomPivot";
            }
            else
            {
                Debug.LogWarning("[MindControlled] Resources/UI/AllySkullMark 未找到，" +
                                 "亡者领域友军不会显示头顶徽记。请确认 PNG 已导入到 " +
                                 "Assets/Resources/UI/AllySkullMark.png 且导入类型为 Sprite。");
                _sharedMarkSprite = null;
            }
        }
    }

    private static void EnsureSharedOverlayMaterial()
    {
        if (_sharedOverlayMaterial == null)
        {
            var sh = Shader.Find("Sprites/Default");
            _sharedOverlayMaterial = new Material(sh) { name = "MindControlledOverlayMat" };
        }
    }

    void OnDestroy()
    {
        // === 亡者领域·小怪死亡回血光电 ===
        // 被复活的小怪（非世界 Boss）死亡时，生成一颗青绿色光电飞向玩家，融入玩家身体后回 0.5% HP
        // （v2 策划调整：1% → 0.5%，详见 TombMinionDeathOrb.HealRatio 注释）。
        // 世界 Boss 不触发——它们设计上"永久存在"，被打死的语义是"敌方杀回去"，不应给玩家额外补给。
        // 触发条件：本次销毁是真实的"小怪友军死亡"——即 _en 不为空且 health 已归零或 rolestate=dead。
        //   排除以下情况避免误触发：
        //     • MindControlled 在 Setup 失败 / 玩家退出战斗等"未真正生效"路径下被销毁；
        //     • 玩家关 / 重开 / 跨场景销毁（此时整个 enemylayer 可能正在解构）。
        if (!isWorldBoss && _en != null && (_en.health <= 0 || _en.rolestate == enemy.state.dead))
        {
            Player healTarget = ResolveAnyPlayerForHeal();
            if (healTarget != null && healTarget.health > 0)
            {
                TombMinionDeathOrb.Spawn(_en.transform.position, healTarget);
            }
        }

        // 友军被销毁/还原时，把原 SpriteRenderer 恢复回来（虽然此时 enemy 多半也被 Destroy 了，
        // 但保留这段以防 MindControlled 单独被移除而 enemy 还在的边界情况）
        if (_baseRenderer != null) _baseRenderer.enabled = true;
        if (_overlayRenderer != null) Destroy(_overlayRenderer.gameObject);
        if (_allyMark != null) Destroy(_allyMark.gameObject);
        // 注：脚下紫环 _allyRing 已删除，无需清理。
        // 还原蝙蝠的友军模式开关，避免 enemy 还活着但 MindControlled 已消失时残留状态
        if (_batSelfDriven != null) _batSelfDriven.isAllyMode = false;
        // 性能 flag 同步还原（极少触发，但严谨保留）
        if (_en != null) _en._mindControlledFlag = false;
    }

    /// <summary>
    /// 给"小怪死亡回血光电"找一个活着的玩家（主玩家优先，否则取最近的分身）。
    /// 跨 tag 兼容（"Player" / "Clone"），与 <see cref="FindLivingPlayer"/> 同口径。
    /// </summary>
    private Player ResolveAnyPlayerForHeal()
    {
        Transform pl = GameObject.Find("playerlayer")?.transform;
        if (pl == null) return null;
        Player main = null;
        Player nearest = null;
        float bestSq = float.MaxValue;
        Vector3 self = transform.position;
        foreach (Transform t in pl)
        {
            if (t == null) continue;
            Player p = t.GetComponent<Player>();
            if (p == null || p.health <= 0) continue;
            if (t.CompareTag("Player")) { main = p; continue; }
            if (t.CompareTag("Clone"))
            {
                float sq = (t.position - self).sqrMagnitude;
                if (sq < bestSq) { bestSq = sq; nearest = p; }
            }
        }
        return main != null ? main : nearest;
    }

    // ============================================================
    //   静态流光驱动
    //   一个隐藏 GameObject 上的 driver 每帧计算一次 flowing 颜色，
    //   写入所有友军的 _overlayRenderer / _allyMark（用 MaterialPropertyBlock）。
    //   这样 N 个友军 → 主循环只跑 1 次 cos/lerp，而不是 N 次。
    // ============================================================
    private static MindControlledFlowDriver _driver;
    private static void EnsureFlowDriver()
    {
        if (_driver != null) return;
        var go = new GameObject("__MindControlledFlowDriver");
        go.hideFlags = HideFlags.HideAndDontSave;
        Object.DontDestroyOnLoad(go);
        _driver = go.AddComponent<MindControlledFlowDriver>();
    }

    /// <summary>
    /// 由 driver 在每个渲染帧调用一次：计算"全局基础流光色 + 高光因子"。
    /// 各友军可以叠加自己的相位偏移，做到"既统一又有错峰"。
    /// </summary>
    internal static void TickFlow(float dt)
    {
        // 推进所有友军 overlay 颜色
        for (int i = 0; i < _all.Count; i++)
        {
            MindControlled mc = _all[i];
            if (mc == null) continue;
            mc.UpdateOverlayColor(dt);
        }
    }

    private void UpdateOverlayColor(float dt)
    {
        if (_overlayRenderer == null) return;
        _overlayPhase += dt;
        float t = (_overlayPhase / OverlayCycleSeconds) % 1f;
        if (t < 0f) t += 1f;

        Color baseCol;
        if (t < 1f / 3f)
            baseCol = Color.Lerp(OverlayColorA, OverlayColorB, t * 3f);
        else if (t < 2f / 3f)
            baseCol = Color.Lerp(OverlayColorB, OverlayColorC, (t - 1f / 3f) * 3f);
        else
            baseCol = Color.Lerp(OverlayColorC, OverlayColorA, (t - 2f / 3f) * 3f);

        float pulse = (1f - Mathf.Cos(t * Mathf.PI * 2f)) * 0.5f;
        Color flowing = Color.Lerp(baseCol, Color.white, pulse * OverlayHighlightAmount);
        flowing.a = 1f;
        _overlayRenderer.color = flowing;

        // 头顶徽记：alpha 跟随 pulse 做"呼吸"，颜色固定为紫色基底
        if (_allyMark != null)
        {
            Color mark = AllyMarkColor;
            mark.a = 0.55f + 0.45f * pulse; // 0.55..1.0 呼吸
            _allyMark.color = mark;
        }
    }
}

/// <summary>
/// 隐藏的全局 driver：每渲染帧 tick 一次所有 MindControlled 的流光颜色。
/// 单 Update，对应所有友军，不会因为友军数量翻倍而 Update callback 翻倍。
/// </summary>
internal sealed class MindControlledFlowDriver : MonoBehaviour
{
    void Update()
    {
        MindControlled.TickFlow(Time.deltaTime);
    }
}
