using UnityEngine;

/// <summary>
/// 学习「地狱火」（UR 进化）：
///
/// === 2026-06 新版学习条件 ===
///   1. UR 抽卡装备「地狱火」（GachaEquipment id=5）已解锁
///   2. 已持有「火球术」(Skillbase.Skillname == "火球术")
///   3. 风箭 number >= 2
///   4. 火球术 number >= 2   ← 新增
///
/// 旧版只要求"风箭 >= 3 + 拥有火球术"，火球的多重数量不参与门槛。
/// 改新版的原因：地狱火多重公式现在等于"风箭多重 + 火球多重"（详见 SkillHellfire.cs），
/// 火球多重越高地狱火越强，因此让它也成为门槛的一部分，
/// 防止玩家随便升到 1 重火球就把它"喂"给地狱火、白白浪费火球的成长空间。
///
/// === 与夏无加成的关系 ===
/// 夏无开局：风箭 number=2、火球 number=3 → 两条门槛全部一进局就满足。
/// 其它角色：需主动升风箭到 2 重、火球到 2 重，并解锁地狱火 UR 抽卡装备。
/// </summary>
public class getnewskill_Hellfire : getnewskill
{
    public const string FireballSkillName = "火球术";
    public const int RequiredWindArrowMultishot = 2;   // 旧 3 → 新 2
    public const int RequiredFireballMultishot  = 2;   // 新增
    // 按策划约定：UR 编号1 对应 GachaEquipment id=5（风之形为 id=4）
    public const int RequiredUrEquipmentId = 5;
    // SSR「不忘初心」：历史映射可能出现 id=9 或 id=7，二者任一解锁都视为生效
    public const int KeepOriginalOnEvolutionEquipmentId = 9;
    public const int KeepOriginalOnEvolutionEquipmentFallbackId = 7;

    private void OnEnable()
    {
        // 升级卡图标用 UR 图替代（与风之形→UR/000、亡者领域→UR/002 一致）
        if (icon == null)
            icon = LoadUrIcon();
    }

    private static Sprite LoadUrIcon()
    {
        const string editorPath   = "像素幸存者资源包/存档装备图标/抽卡装备/UR/001.png";
        const string resourcesPath = "像素幸存者资源包/存档装备图标/抽卡装备/UR/001";
        var tex = RuntimeAssetLoader.LoadTexture(null, resourcesPath, editorPath);
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    public override bool IsAvailableInPool()
    {
        if (!base.IsAvailableInPool())
        {
            Debug.Log("[地狱火·候选] base.IsAvailableInPool() 失败");
            return false;
        }
        if (EquipmentSystem.Instance == null)
        {
            Debug.Log("[地狱火·候选] EquipmentSystem.Instance == null");
            return false;
        }
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, RequiredUrEquipmentId))
        {
            Debug.Log($"[地狱火·候选] UR 装备 id={RequiredUrEquipmentId}（地狱火）未解锁——必须先在抽卡里抽到「地狱火」UR 抽卡装备");
            return false;
        }

        Player pl = ResolvePlayer();
        if (pl == null || pl.SkillList == null)
        {
            Debug.Log("[地狱火·候选] 找不到 Player 或 Player.SkillList==null");
            return false;
        }

        Skillbase fireball = null;
        SkillWindArrow wa = null;
        foreach (Transform ski in pl.SkillList)
        {
            if (ski == null) continue;
            var s = ski.GetComponent<Skillbase>();
            if (s != null && s.Skillname == FireballSkillName)
                fireball = s;

            var wind = ski.GetComponent<SkillWindArrow>();
            if (wind != null)
                wa = wind;
        }

        if (fireball == null)
        {
            Debug.Log("[地狱火·候选] 玩家未持有「火球术」——地狱火是火球术的进化，必须先学到火球术");
            return false;
        }
        if (wa == null)
        {
            Debug.Log("[地狱火·候选] 玩家未持有「风箭」——地狱火进化需要风箭多重数量作为门槛");
            return false;
        }
        if (wa.number < RequiredWindArrowMultishot)
        {
            Debug.Log($"[地狱火·候选] 风箭 number={wa.number} < {RequiredWindArrowMultishot}（继续升风箭多重以达成）");
            return false;
        }
        if (fireball.number < RequiredFireballMultishot)
        {
            Debug.Log($"[地狱火·候选] 火球术 number={fireball.number} < {RequiredFireballMultishot}（继续升火球多重以达成）");
            return false;
        }
        Debug.Log("[地狱火·候选] 全部条件满足，进入卡池！");
        return true;
    }

    public override void chocieupgrade()
    {
        battleUI = GameObject.Find("BattleUI").GetComponent<battleUI>();
        Player pl = ResolvePlayer();
        player = pl;
        if (player == null || player.SkillList == null) return;

        Skillbase fireballSkill = null;
        SkillWindArrow windArrowSkill = null;
        foreach (Transform ski in player.SkillList)
        {
            if (ski == null) continue;
            Skillbase sb = ski.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == FireballSkillName)
                fireballSkill = sb;

            var wa = ski.GetComponent<SkillWindArrow>();
            if (wa != null)
                windArrowSkill = wa;
        }

        // === 关键判定：是否吞噬火球术 ===
        // 「不忘初心」SSR 已解锁 → 进化时保留火球术（不吞噬）
        // 否则 → 进化时销毁火球术（吞噬）
        bool keepOriginal = IsKeepOriginalUnlocked();
        bool fireballWillBeConsumed = !keepOriginal;

        // 创建进化技能
        GameObject hellfireObj = Instantiate(skill.gameObject, player.SkillList);
        SkillHellfire hellfire = hellfireObj.GetComponent<SkillHellfire>();
        if (hellfire != null)
        {
            // ApplyInheritanceSnapshot 内部根据 fireballWillBeConsumed 走两条公式：
            //   吞噬：地狱火 number = (风箭+火球) 的瞬间快照 + 之后只跟随风箭增量
            //          地狱火 CD/伤害 = 火球瞬间快照（之后不再同步）
            //   不吞噬：地狱火 number = 风箭实时 + 火球实时
            //          地狱火 CD/伤害 = 火球实时同步
            hellfire.ApplyInheritanceSnapshot(fireballSkill, windArrowSkill, fireballWillBeConsumed);
        }

        // 进化后默认删除基础技能（风箭相关不删；此处删除火球术）；若有SSR「不忘初心」则保留
        if (fireballSkill != null && fireballWillBeConsumed)
            Destroy(fireballSkill.gameObject);

        battleUI.RefreshSkill();
        closechoice();
    }

    /// <summary>
    /// 在 playerlayer 下找当前玩家（与 IsAvailableInPool / chocieupgrade 复用）。
    /// </summary>
    private static Player ResolvePlayer()
    {
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer == null) return null;
        foreach (Transform t in playerLayer)
        {
            if (t != null && t.CompareTag("Player"))
                return t.GetComponent<Player>();
        }
        if (playerLayer.childCount > 0)
            return playerLayer.GetChild(0).GetComponent<Player>();
        return null;
    }

    private bool IsKeepOriginalUnlocked()
    {
        if (EquipmentSystem.Instance == null) return false;
        return EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentId) ||
               EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, KeepOriginalOnEvolutionEquipmentFallbackId);
    }
}
