using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 亡者领域（孢子领域 UR 进化）：
/// - 解锁后，被「孢子领域」击杀的敌人会立即复活为友军（小怪存活一定时间持续掉血；
///   世界 Boss 被永久控制，但仍可被敌方攻击杀回去）。
/// - 触发条件：敌人在死亡前一段时间内受到过孢子领域伤害。
/// - 友军外观：暗影岛深绿色覆盖层（暗深邃风格）。
/// 
/// 本身没有主动技能效果（CDtime 极大、Useskill 直接 yield break），
/// 仅作为「装备/能力」标记存在于玩家技能列表里——是否生效由 BulletSporeField/MindControlled 检测。
/// </summary>
public class SkillTombDomain : Skillbase
{
    [Header("亡者领域专属")]
    [Tooltip("被控制的小怪存活时长（秒）。世界 Boss 永久。")]
    public float minionLifetime = 15f;

    [Tooltip("被控制的小怪每秒掉自身 maxHP 的百分比（0.02 = 2%）")]
    [Range(0f, 1f)] public float minionDecayPerSecond = 0.02f;

    [Tooltip("被控制的世界 Boss 距玩家的最远距离（超过会触发回归行为）。\n" +
             "2026-06：从 12 翻倍到 24，再翻倍到 48。新行为分两段：\n" +
             "  • leash < d ≤ leash×2(=96)：boss 朝玩家走回，不瞬移\n" +
             "  • d > leash×2：判定为\"距离不正常的远\"，才瞬移回 leash 边缘\n" +
             "避免中等脱离距离上的顿挫传送，又防止异常物理推飞导致超长跋涉。")]
    public float bossLeashDistance = 48f;

    [Tooltip("玩家受伤时治疗每个被控制世界 Boss 的血量")]
    public int worldBossHealOnPlayerDamage = 30;

    [Header("学习快照（用于属性继承）")]
    public bool hasInheritanceSnapshot = false;
    public int inheritedSporeDamage = 0;
    public float inheritedSporeCD = 0f;
    public float inheritedSporeRadius = 0f;
    public int inheritedFormOfWindEnabled = 0; // 1=有

    // ============================================================
    //  无罪（SKIN_TOMB）专属：每分钟 +1 范围（上限 20）的渐进成长
    //  ----------------------------------------------------------
    //  机制目标：
    //    亡者领域是无罪的本命技能；玩家撑过的每一分钟都会扩展其控场范围，营造
    //    "随时间膨胀的死亡领域"质感，呼应"AI 控制友军"主题。
    //  作用对象：
    //    身上保留的「孢子领域」实例的 attackRadius——亡者领域本身没有半径字段，
    //    它的可视范围圈和实际伤害判定都由 SkillSporeField 提供。
    //    （为此 getnewskill_TombDomain.chocieupgrade 在无罪皮肤下不会销毁孢子领域。）
    //  上限：
    //    20 unit。锁定时初始 10，每分钟 +1，10 分钟后达 20 即停止。
    //  与原有"锁定不可升级"机制的关系：
    //    SkillSporeField.IsLockedByTombDomain 只阻挡 skillupgrade.cs 写半径，不阻挡
    //    SkillTombDomain 自身定时器写——因此本机制与"学完亡者领域后不再被升级卡改半径"
    //    的设计相容；同时本次还把"任意孢子领域升级卡"全部从卡池剔除（ChoiceUI），
    //    彻底消除"玩家手动升级 vs 时间增长"两条写半径路径的潜在冲突。
    // ============================================================

    /// <summary>无罪专属：每多少秒给孢子领域 attackRadius +1。</summary>
    public const float TombGrowthIntervalSeconds = 60f;
    /// <summary>无罪专属：孢子领域 attackRadius 渐进成长上限。</summary>
    public const float TombGrowthRadiusCap = 20f;
    /// <summary>无罪专属：每次成长的增量。</summary>
    public const float TombGrowthRadiusStep = 1f;

    /// <summary>本局累积秒数（仅无罪皮肤启用），到 60 触发一次 +1，再清零；不依赖 PlayerPrefs。</summary>
    private float _tombGrowthAccum = 0f;
    /// <summary>仅无罪皮肤启用本机制——其它角色就算通过特殊路径学到亡者领域也不享受成长。</summary>
    private bool _tombGrowthEnabled = false;
    /// <summary>缓存的孢子领域引用，避免每帧 GetComponentInChildren；丢失时重新解析。</summary>
    private SkillSporeField _cachedSporeField;

    void Awake()
    {
        // 它本身没有主动施法节奏，CD 拉到极大避免 base FixedUpdate 误触发。
        CDtime = 1e30f;
        CDkey = 0f;
    }

    void Start()
    {
        // 仅当玩家是无罪（SKIN_TOMB）时启用"每分钟 +1 范围（上限 20）"成长机制。
        // 其它角色就算通过特殊路径学到亡者领域也保持原有静态行为，不享受成长。
        // 注意：CurrentSkinIndex 在 PlayerSkinSkillBuff.Awake 已被填好；亡者领域是后续学习
        // 才 Instantiate 出来的，这里读到的值一定是稳定的当前皮肤。
        bool isTombSkin = (PlayerSkinSkillBuff.CurrentSkinIndex == PlayerSkinSkillBuff.SKIN_TOMB);

        // B2 修复（分身奇遇 × 亡者领域）：人格解离克隆出来的分身会通过 MushroomShadowCloneSync 被
        // 复制一份 SkillTombDomain 实例到分身 SkillList 下。如果分身侧也启用成长，会让分身上的
        // SporeField 半径每 60s 也 +1、同时频繁触发 SporeField.DrawCircle 重绘——
        //   • 这份"分身脚下圈圈"在玩家视觉上无意义；
        //   • 分身随时会因人格解离/受击销毁，半径白涨；
        //   • CPU 浪费（DrawCircle 走 LineRenderer 重建）。
        // 通过 transform 上溯找宿主 Player，仅在宿主 tag=="Player"（主玩家本体）时启用成长。
        // 这样：主玩家身上那份 SkillTombDomain 仍正常成长，分身上那份保持静默。
        bool isHostedByMainPlayer = ResolveHostIsMainPlayer();

        _tombGrowthEnabled = isTombSkin && isHostedByMainPlayer;
        if (_tombGrowthEnabled)
        {
            Debug.Log($"[亡者领域·成长] 已启用：每 {TombGrowthIntervalSeconds:F0}s 给孢子领域 attackRadius +{TombGrowthRadiusStep}（上限 {TombGrowthRadiusCap}）");
        }
        else if (isTombSkin && !isHostedByMainPlayer)
        {
            // 仅 debug 一行，避免分身上重复刷日志。
            Debug.Log("[亡者领域·成长] 检测到本实例挂在分身上（tag != Player），跳过成长机制以避免与主玩家本体重复");
        }
    }

    /// <summary>
    /// 上溯找到挂载本 SkillTombDomain 的 Player 宿主，判断其 tag 是否为 "Player"（主玩家本体）。
    /// SkillList 在 Player 下、SkillTombDomain 又在 SkillList 下，所以宿主 Player 就是 parent.parent。
    /// 兜底用 GetComponentInParent 防止 prefab 层级变更。
    /// </summary>
    private bool ResolveHostIsMainPlayer()
    {
        Player host = GetComponentInParent<Player>();
        if (host == null) return true; // 找不到宿主退化为"允许"，避免误关主玩家的成长
        return host.gameObject.CompareTag("Player");
    }

    /// <summary>
    /// 注意：这里的 FixedUpdate 会**遮蔽**基类 Skillbase.FixedUpdate（基类不是 virtual）。
    /// 由于亡者领域 CDtime=1e30、Useskill 是 yield break，跳过基类的 CDkey 累加无任何副作用。
    /// </summary>
    void FixedUpdate()
    {
        if (!_tombGrowthEnabled) return;

        // 解析 / 缓存孢子领域：玩家身上的 SkillSporeField 实例。
        // 在无罪皮肤 + 已学亡者领域 的情况下，getnewskill_TombDomain.chocieupgrade 已被改为
        // **不**销毁孢子领域 → 这里能稳定取到。
        if (_cachedSporeField == null)
        {
            // 先查同一 SkillList 父节点下的兄弟（比 ResolveOnPlayer 廉价）
            Transform parent = transform.parent;
            if (parent != null)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    var sf = parent.GetChild(i).GetComponent<SkillSporeField>();
                    if (sf != null) { _cachedSporeField = sf; break; }
                }
            }
            // 没找到说明孢子领域还没生成或被意外销毁，本帧跳过；下一物理帧再尝试。
            if (_cachedSporeField == null) return;
        }

        // 已达上限：不再累计 / 不再写入，保持稳定值——避免浮点累加抖动反复触发 SporeField.DrawCircle。
        if (_cachedSporeField.attackRadius >= TombGrowthRadiusCap)
        {
            _cachedSporeField.attackRadius = TombGrowthRadiusCap; // 钳到精确值
            return;
        }

        _tombGrowthAccum += Time.fixedDeltaTime;
        if (_tombGrowthAccum >= TombGrowthIntervalSeconds)
        {
            _tombGrowthAccum -= TombGrowthIntervalSeconds;
            float before = _cachedSporeField.attackRadius;
            float after  = Mathf.Min(TombGrowthRadiusCap, before + TombGrowthRadiusStep);
            _cachedSporeField.attackRadius = after;
            // SkillSporeField.Update 内部有 `if (!Mathf.Approximately(_lastRadius, attackRadius)) DrawCircle();`
            // 自动重绘范围圈，无需此处手动调 DrawCircle。
            Debug.Log($"[亡者领域·成长] 经过 {TombGrowthIntervalSeconds:F0}s，孢子领域 attackRadius {before:F1} → {after:F1}（上限 {TombGrowthRadiusCap}）");
        }
    }

    public override IEnumerator Useskill()
    {
        yield break;
    }

    /// <summary>学习瞬间继承孢子领域 / 风之形的关键属性</summary>
    public void ApplyInheritanceSnapshot(SkillSporeField sporeField, SkillFormOfWind formOfWind)
    {
        if (sporeField != null)
        {
            inheritedSporeDamage = Mathf.Max(1, sporeField.damage);
            inheritedSporeCD = Mathf.Max(0.05f, sporeField.CDtime);
            inheritedSporeRadius = Mathf.Max(1f, sporeField.attackRadius);
        }
        inheritedFormOfWindEnabled = formOfWind != null ? 1 : 0;
        hasInheritanceSnapshot = true;
    }

    /// <summary>玩家是否已学习「亡者领域」（供 BulletSporeField / 全局检测用）</summary>
    public static SkillTombDomain ResolveOnPlayer(Player player)
    {
        if (player == null || player.SkillList == null) return null;
        foreach (Transform t in player.SkillList)
        {
            if (t == null) continue;
            var td = t.GetComponent<SkillTombDomain>();
            if (td != null) return td;
        }
        return null;
    }
}
