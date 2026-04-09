using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抽卡结果条目的蛇形流动边框（Shader版）。
/// 自动在条目上创建一个覆盖整个条目的 RawImage，使用 GachaBorder Shader。
///
/// Inspector 参数：
/// - snakeSpeed  : 蛇爬速度（圈/秒）
/// - snakeLength : 蛇身长度（0~1）
/// - borderWidth : 边框粗细（0~0.1，相对于UV）
/// </summary>
public class GachaItemBorder : MonoBehaviour
{
    [Header("蛇形参数")]
    public float snakeSpeed  = 0.8f;
    public float snakeLength = 0.2f;
    public float borderWidth = 0.025f;
    public float glowWidth   = 0.15f;
    public float brightness  = 2.5f;

    private RawImage  _rawImage;
    private Material  _mat;
    private float     _progress = 0f;

    void Awake()
    {
        // 创建覆盖整个条目的 RawImage
        var go = new GameObject("BorderEffect", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _rawImage = go.GetComponent<RawImage>();
        _rawImage.raycastTarget = false;

        // 加载 Shader 并创建 Material
        Shader shader = Shader.Find("UI/GachaBorder");
        if (shader != null)
        {
            _mat = new Material(shader);
            _rawImage.material = _mat;
        }
        else
        {
            Debug.LogWarning("[GachaItemBorder] 找不到 UI/GachaBorder Shader");
        }
    }

    public void SetRarity(GachaRarity rarity)
    {
        if (_mat == null) return;

        Color color = rarity switch
        {
            GachaRarity.R   => new Color(0.3f, 0.6f, 1.0f),
            GachaRarity.SR  => new Color(0.7f, 0.3f, 1.0f),
            GachaRarity.SSR => new Color(1.0f, 0.85f, 0.0f),
            GachaRarity.UR  => new Color(1.0f, 0.2f, 0.2f),
            _               => Color.white
        };

        _mat.SetColor("_Color",       color);
        _mat.SetFloat("_BorderWidth", borderWidth);
        _mat.SetFloat("_SnakeLen",    snakeLength);
        _mat.SetFloat("_GlowWidth",   glowWidth);
        _mat.SetFloat("_Brightness",  brightness);
    }

    void Update()
    {
        if (_mat == null) return;
        _progress = (_progress + snakeSpeed * Time.unscaledDeltaTime) % 1f;
        _mat.SetFloat("_Progress", _progress);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }
}
