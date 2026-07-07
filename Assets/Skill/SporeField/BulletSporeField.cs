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

    /// <summary>玩家学了亡者领域时，把孢子动画染紫（沿用 SkillSporeField.TombDomainCircleColor，但 alpha=1 全不透明显示）。</summary>
    private void TryApplyTombDomainTint()
    {
        if (!IsTombDomainLearnedCached(playerAttr)) return;

        Color c = SkillSporeField.TombDomainCircleColor;
        Color tint = new Color(c.r, c.g, c.b, 1f);

        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] != null) srs[i].color = tint;
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
                if (targetEnemy.GetComponent<MindControlled>() != null)
                {
                    int before = targetEnemy.health;
                    targetEnemy.health = Mathf.Min(targetEnemy.healthmax, targetEnemy.health + dealt);
                    int actualHeal = targetEnemy.health - before;
                    if (actualHeal > 0) MindControlled.SpawnAllyHealNumber(targetEnemy, actualHeal);
                }
                else
                {
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
                // SSR_10 饮血剑：全局吸血（孢子领域 / 亡者领域伤害也吃 1%）
                EquipmentInitializer.TryAllSourceLifesteal(dealt, targetEnemy.atknumber, targetEnemy.transform.position);

                if (targetEnemy.health <= 0)
                {
                    // 亡者领域复活判定已统一移至 enemy.Destroy1 / WorldBossBase.Destroy1，
                    // 这里只需正常调用 Destroy1，由其内部决定走死亡流程还是复活为友军。
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
