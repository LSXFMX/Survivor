using UnityEngine;

/// <summary>
/// SSR 9「三清化一」专属组件：让分身位置与本体始终完全重合，且本体不可见。
///
/// 策划表（截图）：
///   9 三清化一  分身位置与本体重合且隐身，只保留技能效果  抽卡次数 > 450
///
/// ✦ 设计语义（重要 — 别再"修"成无条件强制对齐了）：
///   - **没解锁 SSR9** → 本组件**根本不会被挂上**（见 <see cref="AdventurePersonalityDissolve"/> 第 201 行
///     `if (IsSsrUnlocked(SSR_TRINITY_FUSION_ID))` 才 AddComponent）；
///     此时分身有独立的移动 / AI，必然与本体存在距离 —— 这是策划设计，**不是 BUG**。
///   - **解锁 SSR9** → 本组件被挂上 + AdventurePersonalityDissolve 同时把分身 SetParent 到本体下，
///     localPosition/Rotation/Scale 锁零；分身 transform 由父子继承自动跟随，绝对零偏移。
///
///   因此本组件做的事 = "SSR9 才有的事"：
///     1) Awake 时一次性关闭分身的渲染 / 碰撞 / 物理（隐身 + 不被打 + 不抖）；
///     2) LateUpdate 兜底位置同步——尽管父子关系已经让位置自动同步，但仍冗余写一道
///        localPosition=zero，防御"分身被某些系统短暂 unparent / Animator 微抖" 等极端情况。
///        这条兜底只对"挂着本组件的分身"生效，**不会**影响没 SSR9 的普通分身。
///     3) owner 销毁（主体阵亡）→ 自我销毁，避免悬空子物体。
///
/// 与 SSR6 / SSR8 的协作：
///   - SSR6「影分身之术」：MushroomShadowCloneSync 不动 transform，安全；
///   - SSR8「我与我与我」：场上最多两个分身，若同时持有 SSR9 则每个分身都各自挂到本体下，
///                       与本体三者重叠在一起。
/// </summary>
public class ShadowCloneInvisibility : MonoBehaviour
{
    /// <summary>主体引用：仅用于在主体销毁时自我清理。位置同步已经由父子 transform 关系完成，无需用到。</summary>
    public Player owner;

    /// <summary>是否在 Awake 时一次性关闭渲染/物理（无 owner 也立即生效）。</summary>
    public bool hideOnAwake = true;

    /// <summary>
    /// 每 N 帧重新扫描一次"动态新增的 Renderer / Collider2D"。
    /// 目的：防御 Awake 之后才被动态 AddComponent 的渲染/碰撞组件——例如某些技能、装备、Buff 会
    /// 在游戏中后期挂上新的 SpriteRenderer / Collider2D；如果只在 Awake 扫一次，那些组件会漏网。
    /// 16 帧 ≈ 0.27s @60fps，对玩家不可感知，但能确保新挂的组件最多 0.27s 内被关闭。
    /// </summary>
    private const int FULL_RESCAN_INTERVAL_FRAMES = 16;

    /// <summary>缓存当前已知的 Renderer / Collider2D，LateUpdate 中每帧条件写入，避免每帧 GetComponentsInChildren 产生 GC。</summary>
    private Renderer[] _cachedRenderers;
    private Collider2D[] _cachedColliders;
    private int _framesSinceRescan;

    private void Awake()
    {
        if (hideOnAwake) ApplyInvisibility();
    }

    /// <summary>
    /// 关闭所有 Renderer / Collider2D 的可视与碰撞，并把 Rigidbody2D 改 Kinematic。
    /// 注意：UI Image / Canvas 不在这里关——分身身上一般也没有 UI 子节点，但即便有也属于
    /// 玩家专属信息（HP 条等），由调用方决定是否要隐藏。
    ///
    /// 本方法会刷新 <see cref="_cachedRenderers"/> / <see cref="_cachedColliders"/> 缓存，
    /// 供 LateUpdate 的"持续维持"逻辑使用。
    /// </summary>
    private void ApplyInvisibility()
    {
        // 渲染层：关闭所有 Renderer（含 SpriteRenderer / MeshRenderer / LineRenderer 之类）
        _cachedRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in _cachedRenderers)
        {
            if (r == null) continue;
            r.enabled = false;
        }

        // 物理层：碰撞器全部禁用，避免被敌人攻击到 / 推动主体
        _cachedColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in _cachedColliders)
        {
            if (c == null) continue;
            c.enabled = false;
        }

        // 刚体：改 Kinematic 防止与主体叠层后产生抖动；保留组件方便其它脚本读 Velocity
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// 持续维持"隐身 + 关碰撞"状态。每帧 LateUpdate 调用。
    ///
    /// 解决两类漏网场景：
    ///   A) <b>Awake 之后才被 AddComponent 的新 Renderer / Collider2D</b>
    ///      —— 例如某些技能 / 装备 / Buff 在游戏中期动态挂上新组件。Awake 那一次扫描扫不到。
    ///      → 每 <see cref="FULL_RESCAN_INTERVAL_FRAMES"/> 帧重新 <c>GetComponentsInChildren</c> 一次刷新缓存。
    ///   B) <b>某个外部系统把 Renderer.enabled / Collider2D.enabled 又改回 true</b>
    ///      —— 例如 dash 子脚本、复活流程、Animator 事件等。
    ///      → 每帧扫一遍缓存里的组件，发现 enabled=true 立即写回 false（条件写入，零开销）。
    ///
    /// 性能：场上最多 2 个 SSR9 分身（SSR8 解锁后），缓存命中下基本是纯遍历，开销可忽略。
    /// </summary>
    private void MaintainInvisibility()
    {
        // 定期重扫，捕获动态新增的组件
        _framesSinceRescan++;
        if (_framesSinceRescan >= FULL_RESCAN_INTERVAL_FRAMES || _cachedRenderers == null || _cachedColliders == null)
        {
            _framesSinceRescan = 0;
            _cachedRenderers = GetComponentsInChildren<Renderer>(true);
            _cachedColliders = GetComponentsInChildren<Collider2D>(true);
        }

        // Renderer：被外部重新启用 → 立即关闭
        for (int i = 0; i < _cachedRenderers.Length; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null && r.enabled) r.enabled = false;
        }

        // Collider2D：被外部重新启用 → 立即关闭。
        // 关键 ——「碰撞盒永远关闭」是 SSR9 分身的硬性约束：
        //   * 分身不能被敌人攻击（不应承担伤害 / 触发死亡）；
        //   * 分身不能推动主体（避免与本体的 Rigidbody2D 叠加产生抖动 / 位移）；
        //   * 分身不能触发任何 OnCollision / OnTrigger 事件（例如拾取道具、撞 Boss 触发剧情）；
        // 任何让 collider 重新启用的外部修改都会被这一行立刻撤销。
        for (int i = 0; i < _cachedColliders.Length; i++)
        {
            var c = _cachedColliders[i];
            if (c != null && c.enabled) c.enabled = false;
        }
    }

    /// <summary>
    /// SSR9 专属：监听主体存活状态 + 兜底位置同步。
    ///
    /// 重要边界（防止后续被误改成"通用强制对齐"）：
    ///   本方法只在**挂着本组件**的 GameObject 上执行；而本组件只在 SSR9 解锁时由
    ///   <see cref="AdventurePersonalityDissolve"/> AddComponent。
    ///   因此这里写的 "强制把分身坐标拉回 owner" 仅是 SSR9 的语义，不会影响
    ///   "没解锁 SSR9 的普通分身"（它们身上根本没这个组件）。
    ///
    /// 兜底原理：
    ///   - 父子 transform 关系已经让位置自动同步，理论上不需要本兜底；
    ///   - 但 Player 自身可能受 Animator / 物理 / 子脚本影响产生 localPosition 微抖；
    ///   - 极少数边界情况（例如分身被某些系统短暂 unparent 又 reparent）会让父子链断开；
    ///   - 写在 LateUpdate（所有 Update / FixedUpdate / 动画更新之后）—— Unity 里最晚的位置同步点，
    ///     宁可冗余写一次 transform 也保证 SSR9 "0 偏移" 的策划承诺。
    /// </summary>
    private void LateUpdate()
    {
        if (owner == null || owner.gameObject == null)
        {
            // owner 被销毁（主体死亡）→ 自我销毁，避免悬空子物体保留
            Destroy(gameObject);
            return;
        }

        // —— 持续维持"隐身 + 关碰撞"——
        // 即便 Awake 已经扫过一遍，仍要在 LateUpdate 兜底，防御：
        //   1) Awake 之后才被 AddComponent 的新 Renderer / Collider2D；
        //   2) 外部系统把 enabled 重新写回 true（dash、复活、Animator 事件等）。
        // 注意：这一步在父子位置同步之"前"做，保证就算这一帧分身被外部移动，碰撞盒仍已关闭。
        MaintainInvisibility();

        // —— 位置同步兜底 ——
        if (transform.parent == owner.transform)
        {
            if (transform.localPosition != Vector3.zero) transform.localPosition = Vector3.zero;
            if (transform.localRotation != Quaternion.identity) transform.localRotation = Quaternion.identity;
        }
        else
        {
            // 父级被解除：直接对齐世界坐标
            Vector3 op = owner.transform.position;
            if (transform.position != op) transform.position = op;
            if (transform.rotation != owner.transform.rotation) transform.rotation = owner.transform.rotation;
        }
    }
}
