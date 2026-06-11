using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地狱火（UR 进化）：继承火球术 CD/伤害、继承风箭多重数；
/// 每轮在最近敌人上方召唤三叉戟下劈。
/// </summary>
public class SkillHellfire : Skillbase
{
    [Header("地狱火行为")]
    public float spawnHeight = 8f;
    public float strikeInterval = 0.04f;

    [Header("学习瞬间继承（只记录一次）")]
    public bool hasInheritanceSnapshot = false;
    public int inheritedWindArrowMultishot = 1;
    public int inheritedFireballDamage = 0;
    public float inheritedFireballCD = 0f;

    public void ApplyInheritanceSnapshot(Skillbase fireballSkill, SkillWindArrow windArrowSkill)
    {
        if (fireballSkill == null || windArrowSkill == null) return;

        inheritedFireballCD = Mathf.Max(0.05f, fireballSkill.CDtime);
        inheritedFireballDamage = Mathf.Max(1, fireballSkill.damage);
        inheritedWindArrowMultishot = Mathf.Max(1, windArrowSkill.number);
        hasInheritanceSnapshot = true;

        // 学习瞬间写入地狱火自身，不再时刻同步外部技能
        CDtime = inheritedFireballCD;
        damage = inheritedFireballDamage;
        number = inheritedWindArrowMultishot;
    }

    public override IEnumerator Useskill()
    {
        CDkey = 0f;
        if (player == null || bullet == null) yield break;

        // 按你的规则：风箭多重为实时继承（每次施法读取当前值）
        int currentWindArrowMultishot = GetCurrentWindArrowMultishot();
        int spawnCount = Mathf.Max(1, currentWindArrowMultishot);
        number = spawnCount;

        List<Transform> targets = GetClosestLivingEnemies(spawnCount);
        if (targets.Count == 0) yield break;

        for (int i = 0; i < spawnCount; i++)
        {
            Transform target = targets[i % targets.Count];
            if (target == null) continue;

            Vector3 spawnPos = target.position + Vector3.up * spawnHeight;
            GameObject go = Instantiate(bullet, spawnPos, Quaternion.identity);
            BulletHellTrident trident = go.GetComponent<BulletHellTrident>();
            if (trident == null)
            {
                Destroy(go);
                continue;
            }

            trident.Setup(this, target.GetComponent<enemy>(), damage);
            if (strikeInterval > 0f)
                yield return new WaitForSeconds(strikeInterval);
        }
    }

    int GetCurrentWindArrowMultishot()
    {
        if (player == null) return Mathf.Max(1, inheritedWindArrowMultishot);

        Player owner = player.GetComponent<Player>();
        if (owner == null || owner.SkillList == null)
            return Mathf.Max(1, inheritedWindArrowMultishot);

        foreach (Transform t in owner.SkillList)
        {
            if (t == null) continue;
            SkillWindArrow wa = t.GetComponent<SkillWindArrow>();
            if (wa != null) return Mathf.Max(1, wa.number);
        }

        // 极端情况下风箭不存在时回退到学习快照
        return Mathf.Max(1, inheritedWindArrowMultishot);
    }

    List<Transform> GetClosestLivingEnemies(int maxCount)
    {
        List<Transform> all = new List<Transform>();
        Transform enemyLayer = GameObject.Find("enemylayer")?.transform;
        if (enemyLayer == null || player == null) return all;

        Vector3 center = player.transform.position;
        foreach (Transform e in enemyLayer)
        {
            if (e == null) continue;
            enemy en = e.GetComponent<enemy>();
            if (en == null) continue;
            if (en.health <= 0 || en.rolestate == global::enemy.state.dead) continue;
            // 亡者领域：地狱火索敌跳过友军（被复活的小怪/boss 不在打击列表里）
            if (en._mindControlledFlag) continue;
            all.Add(e);
        }

        all.Sort((a, b) =>
        {
            float da = (a.position - center).sqrMagnitude;
            float db = (b.position - center).sqrMagnitude;
            return da.CompareTo(db);
        });

        if (all.Count > maxCount)
            all.RemoveRange(maxCount, all.Count - maxCount);
        return all;
    }
}
