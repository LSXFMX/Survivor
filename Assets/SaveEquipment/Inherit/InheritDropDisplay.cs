using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 继承装备掉落的**世界内展示**（策划案第 2 条）：
/// 「装备掉落的时候要在画面上出现展示其稀有度，然后从画面中消失，
///   为了让玩家一眼得知其稀有度」。
///
/// 表现分三段（总约 2.1s）：
///   1. 迸出   0.35s —— 从Boss 尸体位置向上弹起并放大，边框由稀有度着色
///   2. 悬停   1.10s —— 停在半空缓慢自转 + 呼吸，同时在下方打出稀有度名与主词条
///   3. 升空淡出 0.65s —— 向上飘走并淡出，"从画面中消失"
///
/// 高稀有度（无限超弦/ 奇点）额外附带：
///   • 全屏 Toast 播报（避免玩家没看到画面上的掉落）
///   • 更长的悬停时间与更大的展示尺寸（让顶级掉落有仪式感）
///
/// 本类不做拾取交互 —— 策划案的流程是"掉落即入库"，展示纯粹是反馈。
/// </summary>
public class InheritDropDisplay : MonoBehaviour
{
    private InheritItem _item;

    private SpriteRenderer _iconSr;
    private SpriteRenderer _borderSr;
    private TextMeshPro    _label;

    private const float POP_TIME   = 0.35f;
    private const float FADE_TIME  = 0.65f;

    /// <summary>
    /// 在世界坐标 pos 处播放一次掉落展示。
    /// 静态工厂：调用方（世界 Boss）不需要关心节点结构。
    /// </summary>
    public static void Show(InheritItem item, Vector3 pos)
    {
        if (item == null) return;

        var go = new GameObject($"InheritDrop_{item.rarity}");
        go.transform.position = pos + new Vector3(0f, 0.6f, 0f);
        var disp = go.AddComponent<InheritDropDisplay>();
        disp.Build(item);
    }

    private void Build(InheritItem item)
    {
        _item = item;

        Color rc = InheritEquipmentDefs.RarityColor(item.rarity);

        // ── 边框（在后，先渲染）──
        var borderGo = new GameObject("Border");
        borderGo.transform.SetParent(transform, false);
        _borderSr = borderGo.AddComponent<SpriteRenderer>();
        _borderSr.sprite = InheritEquipmentAssets.Border(item.rarity);
        _borderSr.color = rc;
        _borderSr.sortingOrder = 200;
        FitWorldSize(_borderSr, 1.5f);

        // ── 图标（在前）──
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(transform, false);
        _iconSr = iconGo.AddComponent<SpriteRenderer>();
        _iconSr.sprite = InheritEquipmentAssets.Icon(item.slot, item.rarity);
        _iconSr.sortingOrder = 201;
        FitWorldSize(_iconSr, 1.05f);

        // ── 文字标签 ──
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(transform, false);
        labelGo.transform.localPosition = new Vector3(0f, -1.05f, 0f);
        _label = labelGo.AddComponent<TextMeshPro>();
        _label.text = $"<color=#{InheritEquipmentDefs.RarityHex(item.rarity)}>" +
                      $"{InheritEquipmentDefs.RarityName(item.rarity)}</color>\n" +
                      $"<size=60%>{InheritEquipmentDefs.SlotName(item.slot)}  " +
                      $"{InheritEquipmentDefs.FormatStatLine(item.mainStat, item.mainValue)}</size>";
        _label.fontSize = 3.2f;
        _label.alignment = TextAlignmentOptions.Center;
        _label.sortingOrder = 202;
        // 45° 俯视：文字与相机对齐，避免趴在地上看不清
        labelGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        transform.rotation = Quaternion.Euler(45f, 0f, 0f);

        StartCoroutine(Play());
    }

    /// <summary>把 Sprite 缩放到指定世界尺寸（米），与源图分辨率解耦。</summary>
    private static void FitWorldSize(SpriteRenderer sr, float worldSize)
    {
        if (sr == null || sr.sprite == null) return;
        float w = sr.sprite.bounds.size.x;
        if (w <= 0.01f) return;
        float k = worldSize / w;
        sr.transform.localScale = new Vector3(k, k, k);
    }

    private IEnumerator Play()
    {
        // 顶级稀有度停留更久、展示更大，给足仪式感
        bool epic = _item.rarity >= InheritRarity.Superstring;
        float hold = epic ? 1.8f : 1.1f;
        float peak = epic ? 1.25f : 1.0f;

        Vector3 basePos = transform.position;
        Vector3 baseScale = transform.localScale;

        // ── 段 1：迸出（向上弹起 + 放大，带一点过冲）──
        float t = 0f;
        while (t < POP_TIME)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / POP_TIME);
            // 过冲曲线：先冲到 1.25 再回落到 peak，像"蹦出来"
            float s = Mathf.LerpUnclamped(0.2f, peak, 1f - Mathf.Pow(1f - k, 3f));
            s *= 1f + Mathf.Sin(k * Mathf.PI) * 0.25f;
            transform.localScale = baseScale * s;
            transform.position = basePos + new Vector3(0f, Mathf.Sin(k * Mathf.PI * 0.5f) * 0.9f, 0f);
            yield return null;
        }

        // 高稀有度补一条全屏播报（画面掉落可能被弹幕挡住）
        if (epic)
        {
            ToastManager.Show(
                $"<color=#{InheritEquipmentDefs.RarityHex(_item.rarity)}>" +
                $"掉落 {InheritEquipmentDefs.RarityName(_item.rarity)} " +
                $"{InheritEquipmentDefs.SlotName(_item.slot)}</color>");
        }

        // ── 段 2：悬停（缓慢自转 + 呼吸）──
        Vector3 holdPos = transform.position;
        t = 0f;
        while (t < hold)
        {
            t += Time.deltaTime;
            float breath = 1f + Mathf.Sin(t * 3f) * 0.05f;
            transform.localScale = baseScale * peak * breath;
            // 只让图标自转，边框保持正立（否则方框转起来很怪）
            if (_iconSr != null)
                _iconSr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 2f) * 12f);
            transform.position = holdPos + new Vector3(0f, Mathf.Sin(t * 2.2f) * 0.08f, 0f);
            yield return null;
        }

        // ── 段 3：升空淡出（"从画面中消失"）──
        Vector3 fadeFrom = transform.position;
        t = 0f;
        while (t < FADE_TIME)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / FADE_TIME);
            float a = 1f - k;
            SetAlpha(_iconSr, a);
            SetAlpha(_borderSr, a);
            if (_label != null) _label.alpha = a;
            transform.position = fadeFrom + new Vector3(0f, k * 1.6f, 0f);
            transform.localScale = baseScale * peak * (1f + k * 0.25f);
            yield return null;
        }

        Destroy(gameObject);
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = Mathf.Clamp01(a);
        sr.color = c;
    }
}
