using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 孢子子弹：不继承 Bulletbase，独立实现。
/// 生成在敌人身上，播放动画后造成伤害再销毁。
/// </summary>
public class BulletSporeField : MonoBehaviour
{
    [Header("孢子动画")]
    public float animationDuration = 0.6f;

    // 由 SkillSporeField 赋值
    [HideInInspector] public int     damage;
    [HideInInspector] public enemy   targetEnemy;
    [HideInInspector] public Attribute playerAttr;
    [HideInInspector] public bool    _isHealSpore; // true=治疗复活友军, false=伤害敌人

    /// <summary>
    /// 结算统计用技能名，由 SkillSporeField 注入（一般为"孢子领域"）。
    /// 孢子子弹不继承 Bulletbase，没有 fatherskill 字段，所以必须单独传一份技能名，
    /// 否则 GameSessionTracker 无从得知这份伤害归属哪个技能。
    /// </summary>
    [HideInInspector] public string skillNameForTracking;

    private void Start()
    {
        // 跟随目标
        if (targetEnemy != null)
            transform.SetParent(targetEnemy.transform);

        // 若玩家已学习亡者领域，则把孢子动画染成"幽冥紫"，与紫色范围圈、友军紫环呼应。
        // 注意：sporefield.anim 只动 m_Sprite，不动 m_Color，因此运行时设置 color tint 会贯穿全部帧。
        TryApplyTombDomainTint();

        StartCoroutine(SporeRoutine());
    }

    /// <summary>
    /// 可选：按实例覆盖染色。置 null 用全局 SkillSporeField.TombDomainCircleColor；
    /// 亡者领域自爆等场景仅希望单次实例换色时，由调用方赋值，不污染共享预制体/全局色。
    /// </summary>
    public Color? tintColorOverride;

    /// <summary>玩家学了亡者领域时，把孢子动画染紫（沿用 SkillSporeField.TombDomainCircleColor，但 alpha=1 全不透明显示）。</summary>
    private void TryApplyTombDomainTint()
    {
        Color? finalTint = tintColorOverride;
        if (!finalTint.HasValue)
        {
            // 无显式覆盖：仅当已学亡者领域才染成全局紫
            if (!IsTombDomainLearnedCached(playerAttr)) return;
            Color c = SkillSporeField.TombDomainCircleColor;
            finalTint = new Color(c.r, c.g, c.b, 1f);
        }

        Color tint = finalTint.Value;

        // SpriteRenderer.color 是乘法混合：绿色贴图×紫色仍偏绿。
        // 这里把颜色烘焙进贴图副本，保证最终呈现为紫色。
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            if (tintColorOverride.HasValue && srs[i].sprite != null)
            {
                var newSpr = TintSpriteCopy(srs[i].sprite, tint);
                if (newSpr != null) srs[i].sprite = newSpr;
                srs[i].color = Color.white;
            }
            else
            {
                srs[i].color = tint;
            }
        }
    }

    /// <summary>
    /// 亡者领域自爆等单实例染色：动画可能逐帧切换 sprite，
    /// 这里在 Update 里对"当前 sprite"补染（带静态缓存，避免重复读像素开销）。
    /// 仅当 tintColorOverride 有值时启用，正常孢子攻击零开销。
    /// </summary>
    private Sprite _lastTintedSprite;
    private Color? _lastTint;
    private static readonly System.Collections.Generic.Dictionary<(Sprite, Color32), Sprite> s_tintCache
        = new System.Collections.Generic.Dictionary<(Sprite, Color32), Sprite>();

    private void Update()
    {
        if (!tintColorOverride.HasValue) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;
        if (sr.sprite == _lastTintedSprite && _lastTint == tintColorOverride) return;

        Color t = tintColorOverride.Value;
        var key = (sr.sprite, (Color32)new Color(t.r, t.g, t.b, t.a));
        if (!s_tintCache.TryGetValue(key, out var tinted))
        {
            tinted = TintSpriteCopy(sr.sprite, t);
            if (tinted != null) s_tintCache[key] = tinted;
        }
        if (tinted != null)
        {
            sr.sprite = tinted;
            sr.color = Color.white;
        }
        _lastTintedSprite = sr.sprite;
        _lastTint = tintColorOverride;
    }

    /// <summary>复制贴图并把每个像素与 tint 相乘（保证最终是纯 tint 色）</summary>
    private static Sprite TintSpriteCopy(Sprite src, Color tint)
    {
        try
        {
            Texture2D srcTex = src.texture;
            if (srcTex == null) return null;
            int w = Mathf.Max(1, srcTex.width), h = Mathf.Max(1, srcTex.height);
            var rt = RenderTexture.GetTemporary(w, h, 0);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            readable.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            Color[] px = readable.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                px[i] = new Color(
                    Mathf.Clamp01(c.r * tint.r),
                    Mathf.Clamp01(c.g * tint.g),
                    Mathf.Clamp01(c.b * tint.b),
                    c.a * tint.a);
            }
            readable.SetPixels(px);
            readable.Apply();
            readable.name = src.name + "_tint";

            return Sprite.Create(readable, src.rect,
                new Vector2(src.pivot.x / (float)src.rect.width, src.pivot.y / (float)src.rect.height),
                src.pixelsPerUnit);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SporeField] 贴图染色失败：{ex.Message}");
            return null;
        }
    }

    // 缓存"玩家是否已学亡者领域"——孢子子弹一秒可能生成几十发，逐发遍历 SkillList 是卡顿主因之一。
    // 0.5s 内复用结果，足以覆盖"刚学完"和"还没学"两种状态切换的滞后；缓存键用 Player 实例，
    // 切换玩家/重开局时会自动失效。
    private static Player _cachedPlayer;
    private static bool   _cachedHasTomb;
    private static float  _cachedExpireTime;
    private const  float  _cacheTtl = 0.5f;

    private static bool IsTombDomainLearnedCached(Attribute attr)
    {
        if (attr == null) return false;
        Player p = attr.GetComponent<Player>();
        if (p == null) return false;

        if (p == _cachedPlayer && Time.time < _cachedExpireTime)
            return _cachedHasTomb;

        _cachedPlayer = p;
        _cachedHasTomb = SkillTombDomain.ResolveOnPlayer(p) != null;
        _cachedExpireTime = Time.time + _cacheTtl;
        return _cachedHasTomb;
    }

    private IEnumerator SporeRoutine()
    {
        yield return new WaitForSeconds(animationDuration);

        if (targetEnemy != null && targetEnemy.health > 0
            && targetEnemy.rolestate.ToString() != "dead")
        {
            float evaRoll = UnityEngine.Random.value * 100f;
            if (targetEnemy.EVA <= evaRoll)
            {
                float atk = playerAttr != null ? playerAttr.atk : 0f;
                // 伤害公式：技能基础伤害 × (1 + 攻击力 × 0.1)，走暴击与防御（与 Bulletbase 通用公式一致）
                float finalDamage = damage * (1f + atk * 0.1f);
                bool isCrit = false;
                if (playerAttr != null && playerAttr.CR > UnityEngine.Random.value * 100f)
                {
                    finalDamage *= playerAttr.CD / 100f;
                    isCrit = true;
                }
                finalDamage -= targetEnemy.def;
                if (finalDamage < 1f) finalDamage = 1f;

                int dealt = (int)finalDamage;

                // 亡者领域：若目标已是复活友军（MindControlled），孢子治疗而非伤害
                if (_isHealSpore)
                {
                    // 治疗孢子：命中复活友军 → 回血 + 绿色飘字
                    int before = targetEnemy.health;
                    targetEnemy.health = Mathf.Min(targetEnemy.healthmax, targetEnemy.health + dealt);
                    int actualHeal = targetEnemy.health - before;
                    if (actualHeal > 0)
                    {
                        MindControlled.SpawnAllyHealNumber(targetEnemy, actualHeal);
                        // 【2026-08 新增】治疗量纳入结算统计
                        GameSessionTracker.Instance?.RecordHealing(actualHeal);
                    }
                }
                else
                {
                    // 【2026-08 修复】孢子领域伤害此前完全没有埋点。
                    //   孢子领域是亡者领域的前置/伤害来源，它不走 Bulletbase（独立实现），
                    //   所以从来没被 GameSessionTracker 统计到 ——
                    //   这是"亡者领域总输出未计算"的另一半原因。
                    //   技能名从 skillNameForTracking 取（由 SkillSporeField 注入），
                    //   为空时兜底显示"孢子领域"，保证结算页永远不会出现空行。
                    GameSessionTracker.Instance?.RecordDamage(
                        string.IsNullOrEmpty(skillNameForTracking) ? "孢子领域" : skillNameForTracking,
                        dealt);

                    targetEnemy.health -= dealt;

                    if (targetEnemy.atknumber != null && DamageNumberSettings.Visible)
                    {
                        GameObject num = Instantiate(
                            targetEnemy.atknumber,
                            targetEnemy.transform.position,
                            Quaternion.identity);
                        num.transform.localScale *= DamageNumberSettings.SizeScale;
                        var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                        txt.text = dealt.ToString();
                        if (isCrit) txt.color = new Color32(255, 215, 0, 255);
                    }

                    targetEnemy.startturnred();
                }
                // 标记：在死亡前一段时间内受过孢子领域伤害（用于亡者领域复活判定）
                TombDomainHook.MarkSporeDamage(targetEnemy);
                // SSR_10 饮血剑：全局吸血
                EquipmentInitializer.TryAllSourceLifesteal(dealt, targetEnemy.atknumber, targetEnemy.transform.position);

                if (targetEnemy.health <= 0)
                {
                    targetEnemy.Destroy1();
                }
            }
            else
            {
                // 敌人闪避成功：在敌人位置弹青蓝色 Miss
                MissNumber.Show(targetEnemy.atknumber, targetEnemy.transform.position);
            }
        }

        Destroy(gameObject);
    }
}
