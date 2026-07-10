using UnityEngine;

/// <summary>
/// 玩家经验石拾取范围圆圈：跟随玩家，用 LineRenderer 画出 PickupRadius 的范围。
/// 注册到 AttackRangeIndicatorManager 以支持设置面板「显示攻击范围」开关统一控制。
/// 仅主玩家（tag="Player"，非 Clone）显示；分身不创建此圈，避免视觉混乱。
/// </summary>
public class ExpPickupRangeCircle : MonoBehaviour
{
    private LineRenderer _circle;
    private Player _player;
    private float _lastRadius = -1f;
    private const int SEGMENTS = 48;
    private static readonly Color _color = new Color(0.4f, 0.7f, 1f, 0.55f); // 钻石蓝，半透明

    void Start()
    {
        _player = GetComponent<Player>();
        if (_player == null) { Destroy(this); return; }
        // 只给主玩家画拾取圈，分身不画
        if (!gameObject.CompareTag("Player")) { Destroy(this); return; }

        GameObject circleObj = new GameObject("ExpPickupRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop = true;
        _circle.useWorldSpace = false;
        _circle.widthMultiplier = 0.06f;
        _circle.positionCount = SEGMENTS;
        _circle.material = new Material(Shader.Find("Sprites/Default"));
        _circle.startColor = _color;
        _circle.endColor   = _color;
        _circle.sortingOrder = -1; // 在玩家精灵下方

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, _player);
    }

    void Update()
    {
        if (_player == null || _circle == null) return;
        if (!Mathf.Approximately(_lastRadius, _player.PickupRadius))
            DrawCircle();
    }

    private void DrawCircle()
    {
        if (_circle == null || _player == null) return;
        float r = _player.PickupRadius;
        _lastRadius = r;
        for (int i = 0; i < SEGMENTS; i++)
        {
            float angle = i * 2f * Mathf.PI / SEGMENTS;
            _circle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * r,
                0f,
                Mathf.Sin(angle) * r));
        }
    }

    void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
    }
}
