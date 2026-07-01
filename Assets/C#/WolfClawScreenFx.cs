using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 全屏「利爪」特效：在最上层 ScreenSpaceOverlay Canvas 上铺满一张利爪图，
/// 快速逐帧播放（略微缩放 + 淡出），营造被撕咬一爪划过全屏的冲击感。
/// 用法：WolfClawScreenFx.Show(frames);  frames 为利爪序列帧（ClawFx1~4）。
/// 使用非缩放时间，游戏减速/顿帧时仍正常播放。
/// </summary>
public class WolfClawScreenFx : MonoBehaviour
{
    private Sprite[] _frames;

    public static void Show(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0) return;

        var go = new GameObject("WolfClawScreenFx");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // 压在绝大多数 UI 之上
        var gr = go.AddComponent<GraphicRaycaster>();
        gr.enabled = false; // 不拦截点击

        var fx = go.AddComponent<WolfClawScreenFx>();
        fx._frames = frames;
        fx.StartCoroutine(fx.Play());
    }

    private IEnumerator Play()
    {
        // 半透明红色底闪（增强冲击）
        var flashGo = new GameObject("flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashGo.transform.SetParent(transform, false);
        var frt = flashGo.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        var flash = flashGo.GetComponent<Image>();
        flash.raycastTarget = false;
        flash.color = new Color(0.6f, 0f, 0f, 0.35f);

        // 铺满屏幕的利爪图
        var imgGo = new GameObject("claw", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = false; // 拉伸铺满全屏
        img.sprite = _frames[0];

        // 逐帧播放利爪划过（放慢后更有碾压感）
        float perFrame = 0.13f;
        for (int i = 0; i < _frames.Length; i++)
        {
            img.sprite = _frames[i];
            float k = _frames.Length > 1 ? (float)i / (_frames.Length - 1) : 1f;
            rt.localScale = Vector3.one * Mathf.Lerp(1.18f, 1.02f, k); // 由外向内的划击感
            img.color = new Color(1f, 1f, 1f, 1f);
            flash.color = new Color(0.6f, 0f, 0f, Mathf.Lerp(0.35f, 0.12f, k));
            yield return new WaitForSecondsRealtime(perFrame);
        }

        // 淡出（放慢）
        float t = 0f, fade = 0.3f;
        while (t < fade)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(1f, 0f, t / fade);
            img.color = new Color(1f, 1f, 1f, a);
            flash.color = new Color(0.6f, 0f, 0f, 0.12f * a);
            yield return null;
        }
        Destroy(gameObject);
    }
}
