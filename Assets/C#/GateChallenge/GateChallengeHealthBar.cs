using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 门挑战怪血条，挂在怪物子对象的 Canvas 上。
/// 自动跟随怪物，始终朝向固定角度。
/// </summary>
public class GateChallengeHealthBar : MonoBehaviour
{
    public Image fillImage;
    private GateChallengeEnemy _enemy;

    private void Awake()
    {
        _enemy = GetComponentInParent<GateChallengeEnemy>();
    }

    private void Update()
    {
        if (_enemy == null || _enemy.healthmax <= 0) return;
        float ratio = Mathf.Clamp01((float)_enemy.health / _enemy.healthmax);
        if (fillImage != null) fillImage.fillAmount = ratio;
    }

    private void LateUpdate()
    {
        // 抵消父物体旋转，保持血条朝向固定
        transform.rotation = Quaternion.Euler(20f, 0f, 0f);
    }
}
