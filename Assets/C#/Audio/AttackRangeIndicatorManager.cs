using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局攻击范围显示开关（仅主玩家；分身不显示攻击范围）。
///
/// 各技能（SkillWindArrow/SkillSporeField/SkillBloodline 等）在 Start 中创建出范围圆圈 LineRenderer 后调用
/// <see cref="Register(LineRenderer, Player)"/> 注册；当玩家在设置中切换显示开关时，所有已注册的圆圈
/// 会同步 enabled。
///
/// 持久化：PlayerPrefs 键 "Settings.AttackRangeVisible" (int 0/1，默认 1)
/// </summary>
public static class AttackRangeIndicatorManager
{
    private const string PpKeyOwner = "Settings.AttackRangeVisible";

    private static bool _visible = PlayerPrefs.GetInt(PpKeyOwner, 1) != 0;

    private struct Entry
    {
        public LineRenderer lr;
        public Player owner;
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

    /// <summary>
    /// 注册一个范围圆圈 LineRenderer。重复注册不会重复添加。
    /// 分身（tag=="Clone"）的圈直接忽略，永不显示。
    /// </summary>
    /// <param name="lr">技能创建的 LineRenderer 圈。</param>
    /// <param name="owner">该技能依附的 Player；clone 不会被注册。</param>
    public static void Register(LineRenderer lr, Player owner = null)
    {
        if (lr == null) return;

        // 分身不显示攻击范围——直接关掉 LineRenderer 并跳过注册
        if (owner != null && owner.gameObject != null && owner.gameObject.CompareTag("Clone"))
        {
            lr.enabled = false;
            if (lr.gameObject != null) lr.gameObject.SetActive(false);
            return;
        }

        // 清理已被销毁的引用
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            if (_circles[i].lr == null) _circles.RemoveAt(i);
        }

        // 去重
        for (int i = 0; i < _circles.Count; i++)
        {
            if (_circles[i].lr == lr)
            {
                _circles[i] = new Entry { lr = lr, owner = owner };
                ApplyEntry(_circles[i]);
                return;
            }
        }

        _circles.Add(new Entry { lr = lr, owner = owner });
        ApplyEntry(_circles[_circles.Count - 1]);
    }

    private static void ApplyEntry(Entry e)
    {
        if (e.lr == null) return;
        e.lr.enabled = _visible;
        var go = e.lr.gameObject;
        if (go != null && go.activeSelf != _visible) go.SetActive(_visible);
    }

    public static void Unregister(LineRenderer lr)
    {
        if (lr == null) return;
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            if (_circles[i].lr == lr) _circles.RemoveAt(i);
        }
    }

    private static void ApplyToAll()
    {
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            var e = _circles[i];
            if (e.lr == null) { _circles.RemoveAt(i); continue; }
            e.lr.enabled = _visible;
            var go = e.lr.gameObject;
            if (go != null && go.activeSelf != _visible)
                go.SetActive(_visible);
        }
    }
}
