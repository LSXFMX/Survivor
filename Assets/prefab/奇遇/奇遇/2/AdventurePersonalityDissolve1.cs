using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 奇遇 2「人格解离」（重构版，严格按策划表执行）。
///
/// 策划表（截图）：
///   名称：人格解离
///   描述：你感觉自己有丝分裂了…
///   效果：克隆玩家，克隆体与角色血量上限对半分，克隆体继承玩家 30% 属性和玩家技能列表中随机一半技能
///
/// 与本奇遇联动的 SSR/抽卡装备：
///   SSR 6「影分身之术」(equipmentSystemId = 8)：分身实时（按比例）同步主控的属性与技能 —— 把
///                                              「30% / 半技能」升级为「100% / 全技能」。
///   SSR 8「我与我与我」(equipmentSystemId = 11)：本奇遇可触发第二次，场上最多同时存在 2 个分身。
///   SSR 9「三清化一」(equipmentSystemId = 12)：分身位置与本体重合且隐身（SpriteRenderer 关闭），
///                                            但 SkillList 继续运转 → 表现为"主体伤害翻倍"。
///
/// 历史 BUG 与本次修复：
///   - 旧实现把 healthmax 写成 healthmax/2 + 把整套属性 / 全部技能复制给分身 → 与策划表完全不符，
///     分身瞬间变成"满属性双开"，平衡度爆炸；
///   - 旧实现 oneShot 标记硬写死，"我与我与我"装备无法生效；
///   - 旧实现没考虑"场上已有分身时再次触发会无限分裂"——主体血量被反复减半到 1，玩家秒死。
///   本次实现严格按策划表，并把 SSR 联动改成"运行时只增不减"的纯加成。
/// </summary>
public class AdventurePersonalityDissolve : AdventureOptionBase
{
    /// <summary>SSR 6「影分身之术」在 EquipmentSystem.GachaEquipment 中的 id。</summary>
    public const int SSR_SHADOW_CLONE_TECHNIQUE_ID = 8;
    /// <summary>SSR 8「我与我与我」在 EquipmentSystem.GachaEquipment 中的 id。</summary>
    public const int SSR_MULTI_PERSONALITY_ID     = 11;
    /// <summary>SSR 9「三清化一」在 EquipmentSystem.GachaEquipment 中的 id。</summary>
    public const int SSR_TRINITY_FUSION_ID        = 12;

    /// <summary>无 SSR8 时本奇遇本局最多触发 1 次；有 SSR8 时本局最多触发 2 次（场上同时存在 2 个分身）。</summary>
    private const int CLONE_HARD_CAP_DEFAULT  = 1;
    private const int CLONE_HARD_CAP_WITH_SSR = 2;

    /// <summary>分身继承的属性比例（无 SSR6 时；策划表写死 30%）。</summary>
    private const float ATTRIBUTE_INHERIT_RATIO = 0.30f;

    /// <summary>
    /// 本局已成功触发的次数（仅本奇遇）。
    /// ⚠ 注意：基类的 <see cref="usedOptionsThisRun"/> 是按 optionName 字符串去重，第二次必然被拦下，
    ///   故本类不能依赖基类那个集合来允许"第二次"——必须维护一个独立计数。
    /// 在 <see cref="AdventureEventManager"/> 重置 oneShot 集合时同步调用 <see cref="ResetRunCounter"/>。
    /// </summary>
    private static int _executedThisRun = 0;

    /// <summary>新一局开始时由 AdventureEventManager 调用，把计数清零。</summary>
    public static void ResetRunCounter() => _executedThisRun = 0;

    /// <summary>
    /// 是否仍然可在本局出现：
    ///   - 未解锁 SSR8：本局最多触发 1 次（与基类 oneShot 等效）；
    ///   - 解锁  SSR8：本局最多触发 2 次（场上最终最多有 2 个分身）。
    /// 同时附加"分身实存数 < 上限"作为兜底（防止分身被销毁后又允许超出计数）。
    /// </summary>
    public override bool IsAvailableInCurrentDifficulty()
    {
        bool ssr8 = IsSsrUnlocked(SSR_MULTI_PERSONALITY_ID);
        int cap = ssr8 ? CLONE_HARD_CAP_WITH_SSR : CLONE_HARD_CAP_DEFAULT;

        // 已经触发到上限次数 → 本局再不出现（即使分身被打死也不补刀，保持数值上限恒定）
        if (_executedThisRun >= cap) return false;

        // 现存分身数也不能超过上限（双重保险）
        if (CountActiveClones() >= cap) return false;

        return true;
    }

    public override void Execute()
    {
        Transform playerlayer = GameObject.Find("playerlayer")?.transform;
        if (playerlayer == null || playerlayer.childCount == 0)
        {
            base.Execute();
            return;
        }

        // —— 1. 找到主玩家 —— //
        Transform mainPlayer = null;
        foreach (Transform t in playerlayer)
        {
            if (t != null && t.CompareTag("Player")) { mainPlayer = t; break; }
        }
        if (mainPlayer == null) mainPlayer = playerlayer.GetChild(0);

        Player ownerPlayer = mainPlayer.GetComponent<Player>();
        if (ownerPlayer == null) { base.Execute(); return; }

        // —— 2. 场上分身数量上限保护 —— //
        bool ssr8 = IsSsrUnlocked(SSR_MULTI_PERSONALITY_ID);
        int cloneCap = ssr8 ? CLONE_HARD_CAP_WITH_SSR : CLONE_HARD_CAP_DEFAULT;
        if (CountActiveClones() >= cloneCap)
        {
            // 已达上限就跳过克隆，仅消费奇遇槽位（base.Execute 也会标记 oneShot 集合）
            ToastManager.Show("[奇遇·人格解离] 已达到分身数量上限，无法再分裂");
            base.Execute();
            return;
        }

        // —— 3. 血量上限对半分（策划：克隆体与角色血量上限对半分）—— //
        // 注意：与旧版"对所有 playerlayer 子节点都减半"不同——策划表只说"角色与克隆体"，
        // 这里只动主玩家。已有分身的属性已被 SSR6/9 接管，不要再二次衰减。
        int mainHalfHpMax = Mathf.Max(1, ownerPlayer.healthmax / 2);
        int mainHalfHp    = Mathf.Clamp(ownerPlayer.health / 2, 1, mainHalfHpMax);
        ownerPlayer.healthmax = mainHalfHpMax;
        ownerPlayer.health    = mainHalfHp;

        // —— 4. Instantiate 一个 inactive 克隆 + 改 tag → 再激活 —— //
        // ✦ 历史 BUG（已修复）：
        //   旧实现走的是 `original.SetActive(false) → Instantiate(original) → original.SetActive(true)`
        //   这三连操作，目的是让 Instantiate 出来的 clone 在 Awake 之前能改 tag。但副作用是
        //   主玩家自身的 Collider / Rigidbody 经历一次"禁用→重启用"。在 timeScale 即将
        //   恢复到 1（AdventureUI.TryExecute 里 Execute() 之后立刻调 Hide → ResumeTime）的瞬间，
        //   主玩家身边粘着的敌人会被 Unity 物理系统当成"新接触"，重新触发一次
        //   OnCollisionEnter / OnCollisionStay —— 与第 92~95 行刚把主玩家血量减半叠加后，
        //   残血玩家可能被多个敌人同帧扣血至 health ≤ 0，立刻经由 enemy.OnCollisionEnter →
        //   Player.death() → battleUI.ReturnToMain(false) 误判失败。
        //
        // ✦ 修复思路：**主玩家全程不 SetActive(false)**。改用"先 Instantiate 到 inactive 父节点"
        //   的标准 Unity 套路 —— 子对象会跟随父节点的 inactive 状态、不会跑 Awake，
        //   我们改完 tag 后再把它移到 playerlayer 下并激活。
        GameObject original = mainPlayer.gameObject;
        Vector3 spawnOffset = new Vector3(1.5f, 0, 0);

        // 临时 holder：天生 inactive，让 Instantiate 进去的 clone 也处于 inactive 状态
        // （Unity 规则：子对象的"实际激活态"= 自身 activeSelf && 所有祖先 activeSelf）
        GameObject cloneHolder = new GameObject("__PersonalityDissolveCloneHolder");
        cloneHolder.SetActive(false);

        GameObject clone = Instantiate(original,
            original.transform.position + spawnOffset,
            original.transform.rotation,
            cloneHolder.transform);

        // 此时 clone 还没跑过 Awake（祖先 inactive），可以放心改 tag
        clone.tag = "Clone";

        // 移到正式 playerlayer 下；worldPositionStays = true 保持世界坐标不变
        clone.transform.SetParent(playerlayer, true);

        // 销毁临时 holder（已经没用了）
        Destroy(cloneHolder);

        // 现在 clone 的祖先链全部 active，再激活 clone 自身 → 走完整 Awake 流程，
        // 而 Awake 里看到 tag=="Clone" 不会再注入 PlayerSkinSkillBuff，行为正确。
        clone.SetActive(true);

        Player clonePlayer = clone.GetComponent<Player>();
        if (clonePlayer == null) { Destroy(clone); base.Execute(); return; }

        // 设定分身的主体引用，使分身 AI 跟随主体移动
        clonePlayer.cloneOwner = ownerPlayer;

        // —— 4.5 立即停掉分身物理推挤（防御：避免第一帧分身把主玩家撞跑） —— //
        // 背景：分身和主玩家共用同一份 Player prefab，Player.Update() 里所有 Player
        //       实例都会读 Input.GetAxis 并写 rb.velocity，于是分身和主玩家被同一套
        //       输入同时驱动；两者初始位置只差 (1.5,0,0)，加上 Rigidbody 默认非
        //       kinematic，分身在与主玩家叠到一起时会与主玩家互相施加碰撞反作用力，
        //       表现为"分身一直推着主角胡乱移动"。
        //
        //       Player.Start() 已经在 tag=="Clone" 时把 rb 改 kinematic + Player.Update
        //       也对 Clone 做了早返回，从根本上修复了该 bug。这里再冗余处理一次
        //       Rigidbody 是为了消除"奇遇触发 → Player.Start 还没跑 → 中间一帧物理"
        //       的极小时序窗口，让分身从 Instantiate 那一帧起就完全不参与物理推挤。
        var cloneRb = clone.GetComponent<Rigidbody>();
        if (cloneRb != null)
        {
            cloneRb.isKinematic     = true;
            cloneRb.velocity        = Vector3.zero;
            cloneRb.angularVelocity = Vector3.zero;
        }

        // —— 5. 属性继承（策划：30% 属性；SSR6 解锁后 MushroomShadowCloneSync 会逐帧拉到 100%）—— //
        // 血量：上限与主体同步为"对半"，当前血量取主体当前的 30%（与策划"30% 属性"一致）。
        clonePlayer.healthmax = mainHalfHpMax;
        clonePlayer.health    = Mathf.Clamp(Mathf.RoundToInt(mainHalfHp * ATTRIBUTE_INHERIT_RATIO),
                                            1, mainHalfHpMax);
        // 注意 Attribute 基类里 speed / EVA / regen 是 int，其它是 float，
        // 这三项必须显式 RoundToInt，否则 CS0266。
        clonePlayer.atk    = ownerPlayer.atk    * ATTRIBUTE_INHERIT_RATIO;
        clonePlayer.def    = ownerPlayer.def    * ATTRIBUTE_INHERIT_RATIO;
        clonePlayer.speed  = Mathf.Max(1, Mathf.RoundToInt(ownerPlayer.speed * ATTRIBUTE_INHERIT_RATIO));
        clonePlayer.CR     = ownerPlayer.CR     * ATTRIBUTE_INHERIT_RATIO;
        clonePlayer.CD     = ownerPlayer.CD     * ATTRIBUTE_INHERIT_RATIO;
        clonePlayer.EVA    = Mathf.RoundToInt(ownerPlayer.EVA   * ATTRIBUTE_INHERIT_RATIO);
        clonePlayer.DR     = ownerPlayer.DR     * ATTRIBUTE_INHERIT_RATIO;
        clonePlayer.regen  = Mathf.RoundToInt(ownerPlayer.regen * ATTRIBUTE_INHERIT_RATIO);
        // 等级/经验保持完整复制（不该按比例衰减，否则会触发分身升级三选一）
        clonePlayer.exp     = ownerPlayer.exp;
        clonePlayer.expmax  = ownerPlayer.expmax;
        clonePlayer.level   = ownerPlayer.level;
        clonePlayer.dashUnlocked            = ownerPlayer.dashUnlocked;
        clonePlayer.dashCooldown            = ownerPlayer.dashCooldown;
        clonePlayer.dashInvincibleUnlocked  = ownerPlayer.dashInvincibleUnlocked;

        // —— 6. 技能继承（策划：随机一半技能）—— //
        // 分身被 Instantiate 时把主玩家的 SkillList 整套子节点都克隆了，先随机抽取一半留下，其余销毁。
        // 注意：SkillList 上挂 PlayerSkinSkillBuff 自动赋予的"开局技能"也算在内——但 Player.Awake 里
        // 已经对 `!gameObject.CompareTag("Clone")` 做了门控，分身上不会再次跑 SKILL BUFF 注入，故这里
        // 只需保留 Instantiate 复制过来的子集。
        InheritHalfSkills(clonePlayer);

        // —— 7. SSR 6「影分身之术」：挂 sync 组件后逐帧把分身拉满 —— //
        if (IsSsrUnlocked(SSR_SHADOW_CLONE_TECHNIQUE_ID))
        {
            MushroomShadowCloneSync sync = clone.GetComponent<MushroomShadowCloneSync>();
            if (sync == null) sync = clone.AddComponent<MushroomShadowCloneSync>();
            sync.owner = ownerPlayer;
            sync.clone = clonePlayer;
            ToastManager.Show("[SSR·影分身之术] 分身将实时同步主控的全部属性与技能");
        }

        // —— 8. SSR 9「三清化一」：分身与本体位置重合 + 隐身 —— //
        // 关键修复（用户反馈：分身位置与本体存在偏移，要求"分毫不差"）：
        //   旧实现用 LateUpdate 把 transform.position = owner.position，但分身自己的
        //   Update（移动 / 物理 / 输入）会先于 LateUpdate 把它推开，造成可见的 1 帧抖动。
        //   现在改成 **直接把分身 GameObject 设为本体的子物体，并锁定 localPosition = Vector3.zero**。
        //   这样无论本体 transform 怎么变，子物体在世界空间永远与父物体严丝合缝，绝对零偏移。
        if (IsSsrUnlocked(SSR_TRINITY_FUSION_ID))
        {
            ShadowCloneInvisibility inv = clone.GetComponent<ShadowCloneInvisibility>();
            if (inv == null) inv = clone.AddComponent<ShadowCloneInvisibility>();
            inv.owner = ownerPlayer;
            // worldPositionStays = false：让 clone 立刻吸附到 parent 的 (0,0,0)
            // —— 也就是世界空间贴齐主体位置；之后 owner 移动，子物体跟着走，零偏移。
            clone.transform.SetParent(ownerPlayer.transform, false);
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale    = Vector3.one;
            ToastManager.Show("[SSR·三清化一] 分身与本体合一，仅保留技能效果");
        }

        // —— 9. 给主玩家打上一个短暂的死亡判定豁免窗口（兜底） —— //
        // 即使第 4 步的 inactive holder 方案已经消除"主玩家 collider 重启用"这一根因，
        // 这里仍保留 0.5s grace period 作为第二道防御：
        //   * 防止将来代码改动 / 新装备 / 新 SSR 在选完分身那一帧再次产生类似时序问题；
        //   * 主玩家血量已被减半到接近 1，若此时残血玩家被多个敌人重叠夹住，
        //     正常 OnCollisionEnter 也可能立即扣到 0 ——这不是 bug 而是设计本身的风险，
        //     给一个 0.5s 的"事件落地缓冲"对玩家来说是公平且可预期的。
        Player.MainPlayerDeathGraceUntilUnscaled = Time.unscaledTime + 0.5f;

        // —— 10. 本局触发计数 +1（决定本奇遇是否还能在剩下的奇遇中出现） —— //
        // 必须放在 base.Execute() 前；base.Execute() 会把 optionName 写入 usedOptionsThisRun，
        // 但那个集合是字符串去重，对"第二次"无效——真正起拦截作用的是这里的 _executedThisRun 计数。
        _executedThisRun++;

        base.Execute();
    }

    // =============================================================================================
    // 工具方法
    // =============================================================================================

    /// <summary>SkillList 中随机保留一半技能，其余销毁。</summary>
    private void InheritHalfSkills(Player clonePlayer)
    {
        if (clonePlayer == null || clonePlayer.SkillList == null) return;

        // 先收集所有有 Skillbase 的子节点
        var skills = new List<Transform>();
        foreach (Transform t in clonePlayer.SkillList)
        {
            if (t != null && t.GetComponent<Skillbase>() != null) skills.Add(t);
        }
        if (skills.Count <= 1) return; // 0 或 1 个技能就不删了

        // Fisher-Yates 洗牌后取一半（向上取整，保证至少留 1 个；策划"随机一半"含糊处理为「上取整」）
        for (int i = skills.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (skills[i], skills[j]) = (skills[j], skills[i]);
        }
        int keepCount = Mathf.CeilToInt(skills.Count * 0.5f);
        for (int i = keepCount; i < skills.Count; i++)
        {
            if (skills[i] != null) Destroy(skills[i].gameObject);
        }
    }

    /// <summary>
    /// 当前场景中"非主玩家"的 Player 组件数量（即分身数）。
    /// 注意：SSR9「三清化一」生效时，分身会被挂为本体的子物体，已不在 playerlayer 直接子节点下，
    /// 因此必须用 <c>FindObjectsOfType</c> 全场景扫描，而不能仅遍历 playerlayer.children。
    /// </summary>
    private static int CountActiveClones()
    {
        Player[] all = UnityEngine.Object.FindObjectsOfType<Player>();
        if (all == null || all.Length == 0) return 0;
        int n = 0;
        foreach (var p in all)
        {
            if (p == null) continue;
            // 主玩家 tag = "Player"，分身 tag = "Clone"
            if (p.gameObject.CompareTag("Clone")) n++;
        }
        return n;
    }

    private static bool IsSsrUnlocked(int id)
    {
        return EquipmentSystem.Instance != null &&
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, id);
    }
}
