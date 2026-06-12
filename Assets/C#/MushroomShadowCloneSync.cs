using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SSR 6「影分身之术」(equipmentSystemId = 8)：让分身实时同步主控 30% 的属性。
///
/// 策划表（截图）：
///   6 影分身之术  分身会时刻(按比例)同步主控的属性与技能  抽卡次数 > 300
///
/// 实现：
///   - 该组件由 <see cref="AdventurePersonalityDissolve"/> 在「装备已解锁」时附加到分身 GO 上；
///   - 每帧把 owner 的属性 × 30% 写入 clone，实现"实时按比例同步"；
///   - 无 SSR6 时分身属性在创建瞬间固定（30% 一次性），有 SSR6 后变为每帧动态跟踪 30%。
///   - 技能列表保持创建时的"随机一半"不变（SSR6 不补齐也不删除技能），
///     但已有技能的数值会跟随本体实时同步（同名技能按主控数值 × 30% 比例覆写）。
///
/// 与策划其它 SSR 的协作：
///   - SSR9「三清化一」：分身被彻底销毁，技能搬到本体 SkillListClone；此时 sync 组件也被销毁。
///   - SSR8「我与我与我」：场上可能有 2 个分身，每个分身各挂一份 sync，互不干扰。
/// </summary>
public class MushroomShadowCloneSync : MonoBehaviour
{
    public Player owner;
    public Player clone;

    /// <summary>分身继承的属性比例（策划写死 30%）。</summary>
    private const float SYNC_RATIO = 0.30f;

    /// <summary>技能数值同步节拍（属性逐帧同步，技能开销大、按节拍同步）。</summary>
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
            SyncSkillValues();
        }
    }

    /// <summary>属性按 30% 比例实时同步主控。</summary>
    private void SyncAttributes()
    {
        clone.atk    = owner.atk    * SYNC_RATIO;
        clone.def    = owner.def    * SYNC_RATIO;
        clone.speed  = Mathf.Max(1, Mathf.RoundToInt(owner.speed * SYNC_RATIO));
        clone.CR     = owner.CR     * SYNC_RATIO;
        clone.CD     = owner.CD     * SYNC_RATIO;
        clone.EVA    = Mathf.RoundToInt(owner.EVA * SYNC_RATIO);
        clone.DR     = owner.DR     * SYNC_RATIO;
        clone.regen  = Mathf.RoundToInt(owner.regen * SYNC_RATIO);
        // 血量上限与主体对半分（策划：克隆体与角色血量上限对半分），这是奇遇固有设定，不按 30%
        // 当前血量按比例缩放，不超过上限
        clone.health    = Mathf.Clamp(Mathf.RoundToInt(owner.health * SYNC_RATIO), 1, clone.healthmax);
        // 等级/经验保持完整复制（避免分身触发升级三选一）
        clone.exp       = owner.exp;
        clone.expmax    = owner.expmax;
        clone.level     = owner.level;
        clone.dashUnlocked            = owner.dashUnlocked;
        clone.dashCooldown            = owner.dashCooldown;
        clone.dashInvincibleUnlocked  = owner.dashInvincibleUnlocked;
    }

    /// <summary>
    /// 同步分身已有技能的数值（不补齐也不删除技能，保持"随机一半"列表不变）。
    /// 对于分身已有的技能，按主控同名技能的数值写入（技能本身的 damage/CD 等字段，
    /// 实际战斗伤害 = damage × atk，atk 已经是 30%，所以技能数值保持与主控一致即可）。
    /// </summary>
    private void SyncSkillValues()
    {
        var ownerMap = BuildSkillMap(owner.SkillList);

        foreach (Transform t in clone.SkillList)
        {
            if (t == null) continue;
            Skillbase cloneSkill = t.GetComponent<Skillbase>();
            if (cloneSkill == null) continue;

            string key = string.IsNullOrEmpty(cloneSkill.Skillname) ? t.name : cloneSkill.Skillname;
            if (ownerMap.TryGetValue(key, out Skillbase ownerSkill) && ownerSkill != null)
            {
                CopySkillData(ownerSkill, cloneSkill);
            }
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

    /// <summary>把主控技能数值复制到分身技能（1:1 复制，因为实际伤害 = damage × atk，atk 已是 30%）。</summary>
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
