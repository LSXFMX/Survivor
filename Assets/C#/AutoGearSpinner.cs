using UnityEngine;

/// <summary>
/// 自动模式按钮的齿轮旋转驱动：只旋转齿轮图标本身（挂在齿轮 GameObject 上），不影响文字。
/// enabled=true 时持续旋转；用 unscaledDeltaTime，暂停（timeScale=0）时也能转。
/// </summary>
public class AutoGearSpinner : MonoBehaviour
{
    public float degreesPerSecond = 120f;

    private void Update()
    {
        transform.Rotate(0f, 0f, -degreesPerSecond * Time.unscaledDeltaTime);
    }
}
