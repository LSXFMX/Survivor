using UnityEngine;

public class YuanMuManager : MonoBehaviour
{
    public static YuanMuManager Instance { get; private set; }

    private int _current = 0;
    public int perSecond = 0; // 每秒自动增加的源木量（可被奇遇修改）

    public int Current => _current;

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
