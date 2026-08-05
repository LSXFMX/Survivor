using UnityEngine;

public class YuanMuManager : MonoBehaviour
{
    public static YuanMuManager Instance { get; private set; }

    private int _current = 0;
    public int perSecond = 0; // 每秒自动增加的源木量（可被奇遇修改）

    public int Current => _current;

    /// <summary>直接设置当前源木（仅用于无尽模式存档恢复，避免走 Add 触发统计）。</summary>
    public void SetCurrent(int value)
    {
        _current = Mathf.Max(0, value);
    }

    // 源木增加时触发，参数为增加量
    public static event System.Action<int> OnYuanMuAdded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        _current += amount;
        // 【2026-08】源木累计纳入结算统计。Add 是全项目唯一的源木增加入口
        //（击杀掉落 / 营地每秒 / 奇遇给源木都走这里），埋在此处天然全覆盖。
        GameSessionTracker.Instance?.RecordWood(amount);
        OnYuanMuAdded?.Invoke(amount);
    }

    /// <summary>扣除源木，成功返回 true，不足返回 false</summary>
    public bool Spend(int amount)
    {
        if (_current < amount) return false;
        _current -= amount;
        return true;
    }
}
