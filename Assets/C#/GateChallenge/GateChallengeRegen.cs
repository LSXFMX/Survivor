using UnityEngine;

/// <summary>
/// 门挑战怪专用「自然回血」组件（独立挂载，不改动 enemy 基类的 Update 链，避免影响其它模式敌人）。
/// 每秒回复 = 最大血量 × <see cref="_pctPerSec"/>；用浮点累积，满 1 点再加到 int 血量上。
/// </summary>
public class GateChallengeRegen : MonoBehaviour
{
    private enemy _e;
    private float _pctPerSec;   // 每秒回血比例（0.0002 = 0.02%）
    private float _accum;       // 不足 1 点的回血累积

    public void Init(enemy e, float pctPerSec)
    {
        _e = e;
        _pctPerSec = Mathf.Max(0f, pctPerSec);
        _accum = 0f;
    }

    private void FixedUpdate()
    {
        if (_e == null || _pctPerSec <= 0f) return;
        if (_e.health <= 0 || _e.health >= _e.healthmax) return;

        _accum += _e.healthmax * _pctPerSec * Time.fixedDeltaTime;
        if (_accum >= 1f)
        {
            int add = Mathf.FloorToInt(_accum);
            _accum -= add;
            _e.health = Mathf.Min(_e.healthmax, _e.health + add);
        }
    }
}
