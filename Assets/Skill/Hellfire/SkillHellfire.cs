using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地狱火（UR 进化技能）：火球术 + 风箭 的融合形态。
/// 每轮在最近 N 个敌人上方召唤三叉戟下劈，N = 地狱火当前 number。
///
/// === 2026-06 改版：number / 伤害 / CD 的两套继承规则 ===
/// 由学习瞬间「火球术是否被吞噬」决定（吞噬与否取决于 SSR「不忘初心」是否解锁）：
///
/// (A) 吞噬分支 (fireballConsumed == true)：
///     - number     = (学习瞬间 风箭 + 火球) + (风箭当前 number - 学习瞬间风箭 number)
///                    即：基线锁定在学习时刻的"风箭+火球"，之后只跟随风箭多重的增量
///                    （火球已被吞噬，自然无法继续跟随）
///     - 伤害 / CD  = 学习瞬间火球的快照，之后永不变化
///
/// (B) 不吞噬分支 (fireballConsumed == false，玩家解锁了「不忘初心」)：
///     - number     = 风箭当前 number + 火球当前 number（双实时）
///     - 伤害 / CD  = 火球当前值（实时同步火球后续升级）
///
/// === 失效兜底 ===
/// 不吞噬分支下，若火球术引用丢失（理论上不会——「不忘初心」会保留它，
/// 但玩家可能通过其它机制误删，例如以后加的"重抽"功能），自动退化到吞噬分支的
/// 计算逻辑，使用最近一次同步到的伤害/CD 作为兜底值。
/// </summary>
public class SkillHellfire : Skillbase
{
    [Header("地狱火行为")]
    public float spawnHeight = 8f;
    public float strikeInterval = 0.04f;

    [Header("学习瞬间继承（只记录一次）")]
    public bool hasInheritanceSnapshot = false;

    /// <summary>学习瞬间火球术是否被吞噬（无「不忘初心」就吞）。</summary>
    public bool fireballConsumed = true;

    /// <summary>学习瞬间风箭 number 的快照（吞噬分支的"基线风箭值"）。</summary>
    public int inheritedWindArrowMultishot = 1;

    /// <summary>学习瞬间 风箭+火球 之和（吞噬分支的"基线 number"）。</summary>
    public int inheritedBaseNumberAtEvolution = 1;

    /// <summary>学习瞬间火球术伤害的快照（吞噬分支永久使用此值）。</summary>
    public int inheritedFireballDamage = 0;

    /// <summary>学习瞬间火球术 CD 的快照（吞噬分支永久使用此值）。</summary>
    public float inheritedFireballCD = 0f;

    /// <summary>
    /// 不吞噬分支下保留的火球术引用（用于每次施法实时读 damage/CDtime/number）。
    /// 吞噬分支下保持 null。
    /// </summary>
    private Skillbase _fireballRef = null;

    /// <summary>
    /// 学习时由 getnewskill_Hellfire.chocieupgrade 调用一次：写入快照 + 决定后续是否实时同步火球。
    /// </summary>
    /// <param name="fireballSkill">火球术 Skillbase 实例（吞噬分支下也要传入，仅用于读快照值）。</param>
    /// <param name="windArrowSkill">风箭实例。</param>
    /// <param name="fireballWillBeConsumed">true=本次进化会销毁火球术（默认/无「不忘初心」）；false=保留火球术（解锁了「不忘初心」）。</param>
    public void ApplyInheritanceSnapshot(Skillbase fireballSkill, SkillWindArrow windArrowSkill, bool fireballWillBeConsumed)
    {
        if (fireballSkill == null || windArrowSkill == null) return;

        fireballConsumed = fireballWillBeConsumed;

        // 快照（两个分支都需要写——不吞噬分支用作"火球引用丢失时的兜底"）
        inheritedFireballCD = Mathf.Max(0.05f, fireballSkill.CDtime);
        inheritedFireballDamage = Mathf.Max(1, fireballSkill.damage);
        inheritedWindArrowMultishot = Mathf.Max(1, windArrowSkill.number);
        int fbNum = Mathf.Max(1, fireballSkill.number);
        inheritedBaseNumberAtEvolution = inheritedWindArrowMultishot + fbNum;

        hasInheritanceSnapshot = true;

        // 不吞噬分支：保留火球术引用，后续实时同步它
        // 吞噬分支：保持 null（火球术 GameObject 即将被 Destroy）
        _fireballRef = fireballWillBeConsumed ? null : fireballSkill;

        // 学习瞬间立即把可见数值写到地狱火自身——
        // 即使玩家学完立刻看面板，也会看到与公式一致的结果。
        ApplySyncedStatsForFrame();

        Debug.Log($"[地狱火·快照] fireballConsumed={fireballConsumed} 风箭快照={inheritedWindArrowMultishot} 基线number(=风箭+火球)={inheritedBaseNumberAtEvolution} 火球CD={inheritedFireballCD} 火球伤害={inheritedFireballDamage}");
    }

    public override IEnumerator Useskill()
    {
        CDkey = 0f;
        if (player == null || bullet == null) yield break;

        // 每次施法前刷新 number / damage / CDtime，按当前分支的公式重算
        ApplySyncedStatsForFrame();

        int spawnCount = Mathf.Max(1, number);

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

    /// <summary>
    /// 按当前分支公式刷新 number / damage / CDtime，写回到 Skillbase 字段，
    /// 让外部（UI、CD 显示、伤害计算）都能直接读到正确值。
    /// </summary>
    private void ApplySyncedStatsForFrame()
    {
        int waNow = GetCurrentWindArrowMultishot();

        if (fireballConsumed)
        {
            // 吞噬分支
            //   number = 基线(风箭+火球) + (当前风箭 - 学习瞬间风箭)
            //   damage / CD = 学习瞬间快照
            int delta = waNow - inheritedWindArrowMultishot;
            number = Mathf.Max(1, inheritedBaseNumberAtEvolution + delta);
            damage = inheritedFireballDamage;
            CDtime = inheritedFireballCD;
        }
        else
        {
            // 不吞噬分支：实时跟随火球术
            int fbNum;
            int fbDmg;
            float fbCD;
            if (_fireballRef != null)
            {
                fbNum = Mathf.Max(1, _fireballRef.number);
                fbDmg = Mathf.Max(1, _fireballRef.damage);
                fbCD  = Mathf.Max(0.05f, _fireballRef.CDtime);
            }
            else
            {
                // 兜底：火球术引用丢失，退化为快照值（行为等同吞噬分支）
                // 同时尝试在 SkillList 里重新捕获一次（火球术可能被玩家在新一局重新获得，rare case）
                _fireballRef = TryRecaptureFireball();
                if (_fireballRef != null)
                {
                    fbNum = Mathf.Max(1, _fireballRef.number);
                    fbDmg = Mathf.Max(1, _fireballRef.damage);
                    fbCD  = Mathf.Max(0.05f, _fireballRef.CDtime);
                }
                else
                {
                    int fbNumSnapshot = Mathf.Max(1, inheritedBaseNumberAtEvolution - inheritedWindArrowMultishot);
                    fbNum = fbNumSnapshot;
                    fbDmg = inheritedFireballDamage;
                    fbCD  = inheritedFireballCD;
                }
            }
            number = Mathf.Max(1, waNow + fbNum);
            damage = fbDmg;
            CDtime = fbCD;
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

    /// <summary>不吞噬分支下，若火球术引用丢失，尝试在 SkillList 里按名字重新找回。</summary>
    private Skillbase TryRecaptureFireball()
    {
        if (player == null) return null;
        Player owner = player.GetComponent<Player>();
        if (owner == null || owner.SkillList == null) return null;
        foreach (Transform t in owner.SkillList)
        {
            if (t == null) continue;
            Skillbase sb = t.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == getnewskill_Hellfire.FireballSkillName)
                return sb;
        }
        return null;
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
            // 已占领营地：与风箭同理，地狱火也不应攻击友方营地（2026-06）
            Camp camp = en as Camp;
            if (camp != null && camp.IsCaptured) continue;
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
