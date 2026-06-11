using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局攻击范围显示开关。
/// 各技能（SkillWindArrow/SkillSporeField/SkillBloodline 等）在 Start 中创建出范围圆圈 LineRenderer 后调用
/// <see cref="Register(LineRenderer, Player)"/> 注册；当玩家在设置中切换显示开关时，所有已注册的圆圈
/// 会同步 enabled。
///
/// 分身识别：传入的 owner 若 tag == "Clone" 则视为分身，使用独立的 <see cref="CloneVisible"/> 开关；
/// 否则使用主体的 <see cref="Visible"/> 开关。这样设置面板可以分别控制两类范围圈是否显示。
///
/// 持久化：
///   - 主体：PlayerPrefs 键 "Settings.AttackRangeVisible"      (int 0/1，默认 1)
///   - 分身：PlayerPrefs 键 "Settings.AttackRangeVisibleClone" (int 0/1，默认 1)
/// </summary>
public static class AttackRangeIndicatorManager
{
    private const string PpKeyOwner = "Settings.AttackRangeVisible";
    private const string PpKeyClone = "Settings.AttackRangeVisibleClone";

    private static bool _visible      = PlayerPrefs.GetInt(PpKeyOwner, 1) != 0;
    private static bool _cloneVisible = PlayerPrefs.GetInt(PpKeyClone, 1) != 0;

    private struct Entry
    {
        public LineRenderer lr;
        public Player owner;
        // 仅作为兜底（owner 已被销毁时使用注册时刻的快照），运行期 ApplyToAll 优先
        // 使用 owner.gameObject.CompareTag("Clone") 实时判定，避免出现"分身在 Start
        // 时 tag 还是 Player → 永久注册成主体 → 切'分身'按钮无效"这种时序坑。
        // 历史 bug：影分身/人格解离后注册时序不稳，曾导致两个按钮表现像合并到一个。
        public bool isCloneSnapshot;
    }

    private static readonly List<Entry> _circles = new List<Entry>();

    /// <summary>主体玩家的攻击范围是否显示。</summary>
    public static bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            PlayerPrefs.SetInt(PpKeyOwner, value ? 1 : 0);
            ApplyToAll();
        }
    }

    /// <summary>分身（tag == "Clone"）的攻击范围是否显示。</summary>
    public static bool CloneVisible
    {
        get => _cloneVisible;
        set
        {
            _cloneVisible = value;
            PlayerPrefs.SetInt(PpKeyClone, value ? 1 : 0);
            ApplyToAll();
        }
    }

    /// <summary>
    /// 注册一个范围圆圈 LineRenderer。重复注册不会重复添加。
    /// </summary>
    /// <param name="lr">技能创建的 LineRenderer 圈。</param>
    /// <param name="owner">该技能依附的 Player；为 null 时按主体处理。</param>
    public static void Register(LineRenderer lr, Player owner = null)
    {
        if (lr == null) return;

        bool isClone = ResolveIsClone(owner);

        // 清理已被销毁的引用
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            if (_circles[i].lr == null) _circles.RemoveAt(i);
        }

        // 已存在则只更新 owner（避免重复添加）
        for (int i = 0; i < _circles.Count; i++)
        {
            if (_circles[i].lr == lr)
            {
                _circles[i] = new Entry { lr = lr, owner = owner, isCloneSnapshot = isClone };
                ApplyEntry(_circles[i]);
                return;
            }
        }

        _circles.Add(new Entry { lr = lr, owner = owner, isCloneSnapshot = isClone });
        ApplyEntry(_circles[_circles.Count - 1]);
    }

    /// <summary>对单个 entry 同时设 lr.enabled 与父 GameObject.SetActive，保证开关绝对生效。</summary>
    private static void ApplyEntry(Entry e)
    {
        if (e.lr == null) return;
        bool visible = ResolveVisibility(e);
        e.lr.enabled = visible;
        var go = e.lr.gameObject;
        if (go != null && go.activeSelf != visible) go.SetActive(visible);
    }

    public static void Unregister(LineRenderer lr)
    {
        if (lr == null) return;
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            if (_circles[i].lr == lr) _circles.RemoveAt(i);
        }
    }

    /// <summary>
    /// 当某个圈的归属（owner 是否分身）发生变化时，技能可重新调用此方法更新可见性。
    /// 实际通常一旦 spawn 后 owner 类型不会再变，无需主动调用。
    /// </summary>
    public static void RefreshVisibility(LineRenderer lr)
    {
        if (lr == null) return;
        for (int i = 0; i < _circles.Count; i++)
        {
            if (_circles[i].lr == lr)
            {
                var e = _circles[i];
                // 同步刷新一次快照（兜底用），实际可见性总是按 ResolveVisibility 实时算。
                e.isCloneSnapshot = ResolveIsClone(e.owner);
                _circles[i] = e;
                ApplyEntry(e);
                return;
            }
        }
    }

    /// <summary>
    /// 实时判定一条 entry 当前是分身还是主体：
    ///   1) owner 仍然有效 → 直接读 owner.gameObject.CompareTag("Clone")（运行期 tag 已稳定）。
    ///   2) owner 已被销毁/丢失 → 退化使用注册时刻的快照 isCloneSnapshot。
    /// 这样无论分身在 Awake/Start 阶段 tag 是否被及时改成 Clone，玩家在设置面板里
    /// 切换两个 toggle 都能立刻按当前真实归属分组生效，不会再出现"两个按钮控制同一组圈"。
    /// </summary>
    private static bool ResolveVisibility(Entry e)
    {
        bool isClone;
        if (e.owner != null && e.owner.gameObject != null)
            isClone = e.owner.gameObject.CompareTag("Clone");
        else
            isClone = e.isCloneSnapshot;
        return isClone ? _cloneVisible : _visible;
    }

    private static bool ResolveIsClone(Player owner)
    {
        if (owner == null) return false;
        // GameObject 已被销毁时 owner != null（fake-null）但实际无效 → 视作主体
        if (owner.gameObject == null) return false;
        return owner.gameObject.CompareTag("Clone");
    }

    private static void ApplyToAll()
    {
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            var e = _circles[i];
            if (e.lr == null) { _circles.RemoveAt(i); continue; }
            bool visible = ResolveVisibility(e);
            e.lr.enabled = visible;
            // === Bug 修复（"显示分身攻击距离"按钮无效）双保险 ===
            // 旧实现仅设 lr.enabled，但在以下场景里 LineRenderer.enabled 切换不一定让
            // 圈消失/出现：
            //   1) 某些 Unity 版本 LineRenderer 残留 mesh 缓存，关闭 enabled 仍能看见上一帧渲染；
            //   2) 圈的父 GO（AttackRangeCircle 子物体）下还有别的渲染组件被技能脚本临时加上；
            //   3) 分身复活/换皮链路把 LineRenderer.enabled 重置为 true，造成"切了开关又看到圈"
            //      的玩家反馈。
            // 加上父 GameObject.SetActive(visible) 等价的硬开关：父节点 inactive 整组停渲，
            // 完全杜绝任何残影或脚本回写 enabled 的可能性。注意切回 active 时 LineRenderer.enabled
            // 已是 true，恢复无缝。
            var go = e.lr.gameObject;
            if (go != null && go.activeSelf != visible)
            {
                go.SetActive(visible);
            }
        }
    }
}
