using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「奇点」稀有度的流动星河边框动效。
///
/// 只有 <see cref="InheritRarity.Singularity"/> 的格子会挂这个组件，
/// 其余稀有度零开销。
///
/// 做法：不逐帧改贴图（那会每帧重新上传纹理，很贵），
/// 而是循环推进 <see cref="Image.color"/> 的色相，
/// 让程序化生成的星点边框在冷白→粉→紫→青之间缓慢流动，形成"宇宙星河"观感。
///
/// 单独成文件的原因：Unity 要求 MonoBehaviour 所在文件名与类名一致，
/// 否则无法在 Inspector 里挂载（虽然 AddComponent仍可用，但会有隐性限制）。
/// </summary>
[RequireComponent(typeof(Image))]
public class InheritRarityBorder : MonoBehaviour
{
    [Tooltip("色相循环一周所需秒数。")]
    public float cycleSeconds = 4f;

    [Tooltip("饱和度。偏低更接近'星河冷白'，偏高更像霓虹。")]
    public float saturation = 0.35f;

    private Image _img;

    private void Awake() { _img = GetComponent<Image>(); }

    private void OnEnable()
    {
        if (_img == null) _img = GetComponent<Image>();
    }

    private void Update()
    {
        if (_img == null) return;

        // 用 unscaledTime：装备界面通常在 timeScale = 0 的暂停态下打开，
        // 用Time.time 会导致动效完全静止。
        float t = (Time.unscaledTime / Mathf.Max(0.1f, cycleSeconds)) % 1f;
        _img.color = Color.HSVToRGB(t, Mathf.Clamp01(saturation), 1f);
    }
}
