using UnityEngine;

/// <summary>
/// 奇遇10「源木收集者」运行时被动：
/// 每持有 100 源木，攻击力 +1、防御力 +1（动态实时生效，源木增减时随时重算）。
/// 挂在主玩家身上，通过 delta 增量方式修改 atk/def，避免重复叠加。
/// </summary>
public class YuanmuCollectorBuff : MonoBehaviour
{
    private Player _player;
    private int _appliedBonus = 0; // 当前已施加的加成点数（= 上次的 源木/100）

    private void Awake()
    {
        _player = GetComponent<Player>();
    }

    private void Update()
    {
        if (_player == null || YuanMuManager.Instance == null) return;

        int target = YuanMuManager.Instance.Current / 100; // 每 100 源木 +1
        if (target != _appliedBonus)
        {
            int delta = target - _appliedBonus;
            _player.atk += delta;
            _player.def += delta;
            _appliedBonus = target;
        }
    }
}
