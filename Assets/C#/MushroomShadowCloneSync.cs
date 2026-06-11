using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SSR 6「影分身之术」(equipmentSystemId = 8)：让分身实时（按比例）同步主控的全部属性与技能。
///
/// 策划表（截图）：
///   6 影分身之术  分身会时刻(按比例)同步主控的属性与技能  抽卡次数 > 300
///
/// 实现：
///   - 该组件由 <see cref="AdventurePersonalityDissolve"/> 在「装备已解锁」时附加到分身 GO 上；
///   - 每帧把 owner 的属性 100% 复制到 clone（覆盖掉奇遇里设的 30% 衰减），实现"按比例同步"；
///   - 每隔 <see cref="syncInterval"/> 秒同步一次技能：补齐分身缺的技能（来自奇遇"随机一半"
///     被裁掉的部分）→ 让分身最终拥有主控的全部技能与同等数值。
///
/// 与策划其它 SSR 的协作：
///   - SSR9「三清化一」：分身位置已被 ShadowCloneInvisibility 钉在主体上，本组件不动 position；
///   - SSR8「我与我与我」：场上可能有 2 个分身，每个分身各挂一份 sync，互不干扰。
/// </summary>
public class MushroomShadowCloneSync : MonoBehaviour
{
    public Player owner;
    public Player clone;

    /// <summary>技能同步节拍（属性是逐帧同步，技能开销大、按节拍同步）。</summary>
    public float syncInterval = 0.05f;

    private float _syncTimer;

    private void Awake()
    {
        if (clone == null) clone = GetComponent<Player>();
    }

    private void Update()
    {
        if (owner == null || clone == null) return;
        if (owner.SkillList == null || clone.SkillList == null) return;

        SyncAttributes();

        _syncTimer += Time.deltaTime;
        if (_syncTimer >= syncInterval)
        {
            _syncTimer = 0f;
            SyncSkills();
        }
    }

    /// <summary>属性按 100% 比例同步（覆盖掉 AdventurePersonalityDissolve 中的 30% 衰减）。</summary>
    private void SyncAttributes()
    {
        clone.atk    = owner.atk;
        clone.def    = owner.def;
        clone.speed  = owner.speed;
        clone.CR     = owner.CR;
        clone.CD     = owner.CD;
        clone.EVA    = owner.EVA;
        clone.DR     = owner.DR;
        clone.regen  = owner.regen;
        clone.healthmax = owner.healthmax;
        clone.health    = Mathf.Min(owner.health, owner.healthmax);
        clone.exp       = owner.exp;
        clone.expmax    = owner.expmax;
        clone.level     = owner.level;
        clone.dashUnlocked            = owner.dashUnlocked;
        clone.dashCooldown            = owner.dashCooldown;
        clone.dashInvincibleUnlocked  = owner.dashInvincibleUnlocked;
    }

    private void SyncSkills()
    {
        var ownerMap = BuildSkillMap(owner.SkillList);
        var cloneMap = BuildSkillMap(clone.SkillList);

        foreach (var kv in ownerMap)
        {
            Skillbase ownerSkill = kv.Value;
            if (!cloneMap.TryGetValue(kv.Key, out Skillbase cloneSkill) || cloneSkill == null)
            {
                // 主控有但分身没有 → 把主控的技能 prefab 实例化一份给分身。
                // 这正是策划"按比例同步技能"的体现：被奇遇"随机一半"裁掉的部分被补回来。
                GameObject newSkillObj = Instantiate(ownerSkill.gameObject, clone.SkillList);
                cloneSkill = newSkillObj.GetComponent<Skillbase>();
            }
            if (cloneSkill != null) CopySkillData(ownerSkill, cloneSkill);
        }

        // 分身有但主控没有 → 删除（可能是先前装备组合带来的旧技能残留）
        foreach (var kv in cloneMap)
        {
            if (!ownerMap.ContainsKey(kv.Key) && kv.Value != null)
                Destroy(kv.Value.gameObject);
        }
    }

    private Dictionary<string, Skillbase> BuildSkillMap(Transform skillRoot)
    {
        var map = new Dictionary<string, Skillbase>();
        foreach (Transform t in skillRoot)
        {
            if (t == null) continue;
            Skillbase s = t.GetComponent<Skillbase>();
            if (s == null) continue;

            string key = string.IsNullOrEmpty(s.Skillname) ? t.name : s.Skillname;
            if (!map.ContainsKey(key)) map.Add(key, s);
        }
        return map;
    }

    private void CopySkillData(Skillbase src, Skillbase dst)
    {
        dst.Skillname = src.Skillname;
        dst.CDtime    = src.CDtime;
        dst.CDkey     = src.CDkey;
        dst.damage    = src.damage;
        dst.level     = src.level;
        dst.lifetime  = src.lifetime;
        dst.pass      = src.pass;
        dst.speed     = src.speed;
        dst.number    = src.number;
        dst.bullet    = src.bullet;
        dst.size      = src.size;
        dst.interval  = src.interval;
        dst.angel     = src.angel;
        dst.isfaceenemy = src.isfaceenemy;
        dst.icon      = src.icon;

        SkillWindArrow srcWind = src as SkillWindArrow;
        SkillWindArrow dstWind = dst as SkillWindArrow;
        if (srcWind != null && dstWind != null)
            dstWind.attackRadius = srcWind.attackRadius;

        SkillSporeField srcSpore = src as SkillSporeField;
        SkillSporeField dstSpore = dst as SkillSporeField;
        if (srcSpore != null && dstSpore != null)
            dstSpore.attackRadius = srcSpore.attackRadius;

        // === 亡者领域专属字段同步 ===
        // SkillTombDomain 在主玩家身上由 chocieupgrade 调用 ApplyInheritanceSnapshot 写入运行时数据；
        // 若不同步过去，分身那份会用 prefab 默认值，与主玩家行为不一致。
        SkillTombDomain srcTomb = src as SkillTombDomain;
        SkillTombDomain dstTomb = dst as SkillTombDomain;
        if (srcTomb != null && dstTomb != null)
        {
            dstTomb.minionLifetime               = srcTomb.minionLifetime;
            dstTomb.minionDecayPerSecond         = srcTomb.minionDecayPerSecond;
            dstTomb.bossLeashDistance            = srcTomb.bossLeashDistance;
            dstTomb.worldBossHealOnPlayerDamage  = srcTomb.worldBossHealOnPlayerDamage;
            dstTomb.hasInheritanceSnapshot       = srcTomb.hasInheritanceSnapshot;
            dstTomb.inheritedSporeDamage         = srcTomb.inheritedSporeDamage;
            dstTomb.inheritedSporeCD             = srcTomb.inheritedSporeCD;
            dstTomb.inheritedSporeRadius         = srcTomb.inheritedSporeRadius;
            dstTomb.inheritedFormOfWindEnabled   = srcTomb.inheritedFormOfWindEnabled;
        }
    }
}
