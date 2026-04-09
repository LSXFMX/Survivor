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

        StartCoroutine(SporeRoutine());
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
                int atk = playerAttr != null ? (int)playerAttr.atk : 0;
                float finalDamage = damage + atk - targetEnemy.def;
                if (finalDamage < 1) finalDamage = 1;

                targetEnemy.health -= (int)finalDamage;

                if (targetEnemy.atknumber != null)
                {
                    GameObject num = Instantiate(
                        targetEnemy.atknumber,
                        targetEnemy.transform.position,
                        Quaternion.identity);
                    num.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = ((int)finalDamage).ToString();
                }

                targetEnemy.startturnred();
                if (targetEnemy.health <= 0)
                    targetEnemy.Destroy1();
            }
        }

        Destroy(gameObject);
    }
}
