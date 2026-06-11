using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// UR 角色技能加成（局内 buff 注入器）。
///
/// 设计目标
/// --------
/// 当前游戏支持三种角色（PlayerPrefs key = "SelectedSkin"，0=琪露诺 / 1=南筱风 / 2=夏无）。
/// 琪露诺无任何加成（基准）；南筱风、夏无作为 UR 角色，应在自身"本命技能"上获得显著加成，
/// 使玩家选择 UR 角色不仅仅是换皮。
///
/// 加成清单（策划落定）
/// ====================
///   南筱风（skin == 1，风系角色）
///     - 风箭基础已强（prefab 默认 CDtime=0.3, number=5, attackRadius=15, damage=2）
///       南筱风的"加强"完全体现在风箭 prefab 自身的强力数值上（其它角色用同一份 prefab 也吃到这些数值，
///       所以严格来说风箭对所有角色都强；之前曾尝试在局内把 damage 2→3 给南筱风加成，
///       实测风箭 damage=2 已足够输出，多 1 点反而让前期清线过快、削弱别的技能选择空间，已删除）
///     - 入局自动获得「飓风」（若 SkillList 下尚未存在）
///
///   夏无（skin == 2，火/血族角色）
///     - 血族血统 number          1   → 5
///     - 血族血统 lifestealRatio  0.10 → 0.20 (吸血比例提升至 20%)
///     - 入局自动获得「火球术」（若 SkillList 下尚未存在）
///
/// 风箭子弹染色（按角色身份分流，所有皮肤共用入口 ApplySkinTintToWindArrowBullet）：
///     - 琪露诺（0）: prefab 原色（不染）
///     - 南筱风（1）: 青绿色
///     - 夏无  （2）: 红色
///     - 无罪  （3）: 紫黑色
///   染色由 SkillWindArrow.Useskill() 在 Instantiate 子弹后调用 ApplySkinTintToWindArrowBullet 完成。
///
/// 工作机制
/// --------
/// 1. 由 Player.Awake() 在每局开始时自动 AddComponent；本组件 Start() 协程化等待 SkillList
///    上的技能 prefab 完成 Awake，再修改它们的 number / attackRadius / lifestealRatio。
/// 2. 火球术 prefab 通过 #if UNITY_EDITOR 的 OnValidate 自动按文件名 fireballSkill.prefab
///    在项目里搜索并绑定到 _autoFireballPrefab（SerializeField），打包时作为资产依赖
///    自动打进运行时——无须手动拖拽，也不依赖 Resources/。
/// 3. 选中皮肤的"权威读取入口"：PlayerPrefs.GetInt("SelectedSkin", 0)。
///    本组件保留一个 public static int CurrentSkinIndex 缓存，供 SkillWindArrow 等无父类引用的脚本快速查询，
///    避免每发子弹都读 PlayerPrefs（Windows 平台这操作有 IO 抖动）。
/// </summary>
public class PlayerSkinSkillBuff : MonoBehaviour
{
    public const int SKIN_CIRNO = 0;
    public const int SKIN_NANXIAOFENG = 1;  // 南筱风
    public const int SKIN_XIAWU = 2;        // 夏无
    public const int SKIN_TOMB = 3;         // 无罪（UR_2 亡者领域）

    /// <summary>无罪专属：风箭/孢子领域 attackRadius 初始值。
    /// 设计：等于亡者领域 UR 进化的范围门槛（≥15），开局即满足前置条件 → 玩家下一次升级
    /// 池就会出现「学习亡者领域」选项；不再用 20 是为了把"超额优势"留给玩家自己通过升级累加。</summary>
    public const float TOMB_INITIAL_ATTACK_RADIUS = 15f;

    /// <summary>无罪专属：孢子领域 CDtime 初始值（秒）——比基础 prefab 默认 5s 更短，
    /// 强化"亡者领域 UR 角色"的释放节奏，让范围伤害更频繁触发，也让"被领域击杀→复活为友军"
    /// 的链路更高频地启动。</summary>
    public const float TOMB_SPORE_FIELD_CDTIME = 3f;

    /// <summary>缓存的当前皮肤索引（避免高频读 PlayerPrefs）。-1 表示尚未初始化。</summary>
    public static int CurrentSkinIndex { get; private set; } = -1;

    /// <summary>
    /// 给那些"在 PlayerSkinSkillBuff.Awake 之前就需要读 CurrentSkinIndex 的脚本"用的兜底初始化。
    /// 例如 SkillWindArrow.Start 里要根据皮肤决定 attackRadius——它的执行顺序可能早于 Buff Awake，
    /// 此时缓存还是 -1。本方法只是从 PlayerPrefs 同步一次值，无副作用，反复调用安全。
    /// </summary>
    public static void PrimeCurrentSkinIndexFromPrefs()
    {
        CurrentSkinIndex = PlayerPrefs.GetInt("SelectedSkin", 0);
    }

    [Tooltip("可选：夏无 spawn 时自动获得的火球术 prefab。\n" +
             "若留空，组件会按 OnValidate 自动指派 → 场景中现有「火球术」实例 → Resources 顺序回退。")]
    public GameObject fireballSkillPrefabFallback;

    [Tooltip("可选：南筱风 spawn 时自动获得的飓风 prefab。\n" +
             "若留空，组件会按 OnValidate 自动指派 → ChoiceUI 入口查询 → Resources 顺序回退。")]
    public GameObject hurricaneSkillPrefabFallback;

    [Tooltip("可选：无罪 spawn 时自动获得的孢子领域 prefab。\n" +
             "若留空，组件会按 OnValidate 自动指派 → ChoiceUI 入口查询 → Resources 顺序回退。")]
    public GameObject sporeFieldSkillPrefabFallback;

    [Tooltip("可选：无罪 spawn 时自动获得的风箭 prefab（用于亡者领域进化前置）。\n" +
             "若留空，组件会按 OnValidate 自动指派 → ChoiceUI 入口查询 → Resources 顺序回退。")]
    public GameObject windArrowSkillPrefabFallback;

    // ========== 测试开关：无罪开局直接自带「亡者领域」 ==========
    // 仅用于跳过"先学风箭+孢子领域 → 升级时学亡者领域"流程，方便快速验证亡者领域行为。
    // 正式发版前需要把此开关关掉（或整段移除），由升级流程正常获取亡者领域。
    [Header("测试用——开局直接给亡者领域")]
    [Tooltip("勾选后，SKIN_TOMB（无罪）开局会跳过\"风箭+孢子领域\"，直接获得亡者领域；\n" +
             "用于快速测试亡者领域 / 友军 AI / 流光遮罩等。正式版应关闭。")]
    public bool grantTombDomainOnStartForTesting = false;

    [Tooltip("可选：测试用——无罪 spawn 时直接获得的亡者领域 prefab。\n" +
             "留空时按 OnValidate 自动指派 → ChoiceUI 入口查询 → Resources 顺序回退。")]
    public GameObject tombDomainSkillPrefabFallback;

    [Tooltip("由 Editor OnValidate 自动按文件名 SkillTombDomain.prefab 在项目内搜索并绑定。")]
    [SerializeField] private GameObject _autoTombDomainPrefab;

    [Tooltip("由 Editor OnValidate 自动按文件名 fireballSkill.prefab 在项目内搜索并绑定。\n" +
             "打包后作为资产依赖被打进游戏，无须 Resources/。")]
    [SerializeField] private GameObject _autoFireballPrefab;

    [Tooltip("由 Editor OnValidate 自动按文件名 hurricaneSkill.prefab 在项目内搜索并绑定。\n" +
             "打包后作为资产依赖被打进游戏，无须 Resources/。")]
    [SerializeField] private GameObject _autoHurricanePrefab;

    [Tooltip("由 Editor OnValidate 自动按文件名 SporeFieldSkill.prefab 在项目内搜索并绑定。")]
    [SerializeField] private GameObject _autoSporeFieldPrefab;

    [Tooltip("由 Editor OnValidate 自动按文件名 WindArrowskill.prefab 在项目内搜索并绑定。")]
    [SerializeField] private GameObject _autoWindArrowPrefab;

    private Player _player;
    private bool _initialSkillGranted = false;

    void Awake()
    {
        _player = GetComponent<Player>();
        if (_player == null) _player = GetComponentInParent<Player>();
        // 立刻刷新一次缓存，便于子弹脚本读
        CurrentSkinIndex = PlayerPrefs.GetInt("SelectedSkin", 0);
    }

    /// <summary>
    /// 由 Player.Awake 在 AddComponent 之后立即调用，把"开局自带技能"的 Instantiate 提前到
    /// 任何 ChoiceUI.refresh() 之前完成，杜绝同名技能学习卡进入卡池导致重复学习的 bug。
    ///
    /// 数值加成（damage / number / lifestealRatio）依赖技能 prefab 已 Awake，
    /// 仍在 Start 协程内的下一帧执行，与本方法解耦。
    /// </summary>
    public void GrantInitialSkillNow()
    {
        if (_initialSkillGranted) return;
        if (_player == null || _player.SkillList == null) return;

        int skin = PlayerPrefs.GetInt("SelectedSkin", 0);
        CurrentSkinIndex = skin;

        switch (skin)
        {
            case SKIN_NANXIAOFENG:
                TryGrantHurricane();
                break;
            case SKIN_XIAWU:
                TryGrantFireBall();
                break;
            case SKIN_TOMB:
                // 无罪：默认开局自带风箭 + 孢子领域，作为亡者领域 UR 进化的前置技能
                TryGrantWindArrow();
                TryGrantSporeField();
                // 测试用：直接把「亡者领域」也发到手，跳过升级流程
                if (grantTombDomainOnStartForTesting)
                    TryGrantTombDomain();
                // 无罪专属 UI：左侧显示已复活 Boss 的头像 + 血条
                ResurrectedBossHUD.EnsureExist();
                break;
        }
        _initialSkillGranted = true;
    }

    IEnumerator Start()
    {
        if (_player == null || _player.SkillList == null)
        {
            Debug.LogWarning("[UR加成] PlayerSkinSkillBuff：_player 或 SkillList 为空，无法应用加成");
            yield break;
        }

        int skin = PlayerPrefs.GetInt("SelectedSkin", 0);
        CurrentSkinIndex = skin;
        Debug.Log($"[UR加成] PlayerSkinSkillBuff.Start: SelectedSkin={skin}");

        // 兜底：如果 Player.Awake 没调用 GrantInitialSkillNow（比如脚本被手动挂载），
        // 这里再尝试一次。已经授予过的话内部直接返回。
        GrantInitialSkillNow();

        // 等到下一帧再做数值加成，确保 SkillList 下的技能 prefab 都已 Awake 完成、字段初始化到位。
        yield return null;

        // ============== 风箭 CDtime 按皮肤分流 ==============
        // 策划意图：风箭基础 CD 应该是 1.0（手感"稳健中速"），只有南筱风作为风系 UR 才享受 0.3 的极速 CD。
        // 但风箭 prefab 默认 CDtime=0.3（历史遗留，prefab 不动以免破坏其他依赖路径——例如风之形进化里
        // 可能直接读 prefab 值）；所以由本组件在开局**按皮肤强制写回正确的 CD**：
        //   - 南筱风：0.3（保持 prefab 默认）
        //   - 其他人（琪露诺 / 夏无 / 无罪）：1.0
        //
        // 之前这里写过反向的"统一压到 0.3"兜底（基于一份错误诊断：以为所有角色 CD 都被某处偷偷改成 1），
        // 导致玩家选无罪开局风箭也是 0.3——和策划意图相反。本次彻底反转该逻辑。
        //
        // 仅在 level<=1（开局/刚学）时强制，避免覆盖玩家通过升级菜单主动选择的"降低 CD"加成。
        SkillWindArrow waUnify = _player.SkillList.GetComponentInChildren<SkillWindArrow>(true);
        if (waUnify != null && waUnify.level <= 1)
        {
            float target = (skin == SKIN_NANXIAOFENG) ? 0.3f : 1.0f;
            if (!Mathf.Approximately(waUnify.CDtime, target))
            {
                float before = waUnify.CDtime;
                waUnify.CDtime = target;
                // CDkey 同步到 target，让开局立刻能放第一发（避免白白等一个 CD 周期）
                waUnify.CDkey  = target;
                Debug.Log($"[UR加成·风箭CD按皮肤分流] skin={skin} CDtime {before:F2} → {target:F2}");
            }
        }

        // ============== 风箭 attackRadius 按皮肤分流（双保险）==============
        // 策划意图：默认所有角色风箭初始 attackRadius=10，仅南筱风开局特例为 20。
        // 第一道保险在 SkillWindArrow.Start 内按皮肤分支处理；这里是第二道保险，
        // 防止 prefab 被改回旧值、或某条额外路径在 Start 之后又把它改回去。
        // 仅在 level<=1（开局/刚学）时强制，避免覆盖玩家通过"提升风箭攻击范围"升级累加的 +5。
        // 无罪（SKIN_TOMB）的 15 由下方 ApplyTombBuffStats 单独负责，本兜底跳过。
        if (waUnify != null && waUnify.level <= 1 && skin != SKIN_TOMB && !waUnify.IsLockedByTombDomain)
        {
            float radiusTarget = (skin == SKIN_NANXIAOFENG) ? 20f : 10f;
            if (!Mathf.Approximately(waUnify.attackRadius, radiusTarget))
            {
                float before = waUnify.attackRadius;
                waUnify.attackRadius = radiusTarget;
                Debug.Log($"[UR加成·风箭范围按皮肤分流] skin={skin} attackRadius {before:F1} → {radiusTarget:F1}");
            }
        }

        // ============== 风箭 number（多重数量）按皮肤分流 ==============
        // 策划意图：
        //   - 南筱风：保持 prefab 默认 5（风系 UR 的"高多重"是核心强度来源）
        //   - 夏无：     压回 3（满足地狱火进化的 RequiredWindArrowMultishot=3，且不至于压得太低导致清线乏力）
        //   - 琪露诺：   压回 2（基础角色，把风箭定位为弱开局 → 中后期通过升级慢慢成长）
        //   - 无罪：     压回 2（强度集中在"开局自带风箭+孢子领域+亡者领域路线"，单技能数值故意保守）
        // 仅在 level<=1（开局/刚学）时强制，避免覆盖玩家通过"风箭增加一发"升级累加的 +1。
        // 注：夏无和无罪还会在各自 ApplyXxxBuffStats 里再写一次 number——这里先做统一兜底，
        //     具体 Apply 方法里写的同值仅作"语义上的显式声明"，不会产生冲突。
        if (waUnify != null && waUnify.level <= 1)
        {
            int numberTarget;
            switch (skin)
            {
                case SKIN_NANXIAOFENG: numberTarget = 5; break;
                case SKIN_XIAWU:       numberTarget = 3; break;
                case SKIN_TOMB:        numberTarget = 2; break;
                default:               numberTarget = 2; break; // 琪露诺
            }
            if (waUnify.number != numberTarget)
            {
                int before = waUnify.number;
                waUnify.number = numberTarget;
                Debug.Log($"[UR加成·风箭多重按皮肤分流] skin={skin} number {before} → {numberTarget}");
            }
        }

        switch (skin)
        {
            case SKIN_NANXIAOFENG:
                ApplyNanXiaoFengBuffStats();
                break;
            case SKIN_XIAWU:
                ApplyXiaWuBuffStats();
                break;
            case SKIN_TOMB:
                ApplyTombBuffStats();
                break;
            default:
                // 琪露诺：无加成
                break;
        }
    }

    /// <summary>
    /// 南筱风加成（仅"开局自带飓风"，授予已在 Start 协程同步段完成）。
    ///
    /// 历史遗留：曾在此处把风箭 damage 从 2→3，但实测 damage=2 已足够，
    /// 多 +1 反而让前期清线过快、压缩了其它技能选择空间，因此删掉。
    /// 现在数值层面南筱风**没有**任何风箭加成（风箭 prefab 自身的 5 多重 / 15 范围 / 0.3 CD
    /// 是基础数值，所有角色共享）；UR 价值集中在"开局自带飓风"这一独有内容上。
    /// </summary>
    private void ApplyNanXiaoFengBuffStats()
    {
        // 不再修改风箭 damage——保持 prefab 默认 2。
        // 方法体保留是为了兼容 Start 协程的 switch 调用结构，必要时未来再加新加成回这里。
    }

    /// <summary>夏无加成（仅数值部分，授予火球术已在 Start 协程同步段完成）。</summary>
    private void ApplyXiaWuBuffStats()
    {
        // 1) 血族数量 + 吸血比例
        SkillBloodline blood = _player.SkillList.GetComponentInChildren<SkillBloodline>(true);
        if (blood != null)
        {
            blood.number = 5;
            blood.lifestealRatio = 0.20f;
            Debug.Log("[UR加成] 夏无：血族 number→5, lifestealRatio→0.20");
        }
        else
        {
            Debug.Log("[UR加成] 夏无：玩家未持有血族血统，跳过血族加成");
        }

        // 2) 风箭多重数量默认 = 3
        //    与 getnewskill_Hellfire.RequiredWindArrowMultishot=3 配合：
        //    夏无是"地狱火"角色，开局风箭就具备进化为地狱火的多重门槛，
        //    只需再学习火球术即可在升级池里看到「学习地狱火」选项。
        //    注意：风箭 prefab 默认 number=5，这里把夏无强制压回 3 而不是上调，
        //    是为了让"非南筱风时风箭整体偏弱、需通过升级慢慢成长"的设计落实，
        //    避免夏无在风箭路线上反而比南筱风更强（南筱风的强度集中在 prefab 默认 5 多重 + 15 范围）。
        SkillWindArrow wa = _player.SkillList.GetComponentInChildren<SkillWindArrow>(true);
        if (wa != null)
        {
            wa.number = 3;
            Debug.Log("[UR加成] 夏无：风箭 number→3（满足地狱火进化多重门槛）");
        }
    }

    /// <summary>
    /// 无罪加成（数值部分）。开局自带风箭 + 孢子领域，且二者 attackRadius 都直接拉到 20。
    /// 由此「亡者领域 UR 进化」前置条件（风箭 attackRadius>=15、孢子领域 attackRadius>=15）开局即满足，
    /// 下一次升级池就会出现「学习亡者领域」选项——不依赖飓风/风之形（亡者领域不需要它们作为前置）。
    /// 注意：SkillWindArrow.Start 会按皮肤分支保留（无罪走 SKIN_TOMB 分支），
    /// 不会被强制压回 10，与本方法配合实现"开局直接 20"。
    /// </summary>
    private void ApplyTombBuffStats()
    {
        SkillWindArrow wa = _player.SkillList.GetComponentInChildren<SkillWindArrow>(true);
        if (wa != null)
        {
            wa.attackRadius = TOMB_INITIAL_ATTACK_RADIUS;
            // 显式声明：无罪风箭初始多重 = 2（与上方"风箭 number 按皮肤分流"兜底同值）。
            // 这里再写一次是为了在 Apply 方法层面集中体现"无罪所有数值"，方便策划查表；
            // 兜底块负责防御 prefab/其它路径改写，本行负责语义。
            wa.number = 2;
            Debug.Log($"[UR加成] 无罪：风箭 attackRadius→{TOMB_INITIAL_ATTACK_RADIUS}, number→2");
        }
        SkillSporeField sf = _player.SkillList.GetComponentInChildren<SkillSporeField>(true);
        if (sf != null)
        {
            sf.attackRadius = TOMB_INITIAL_ATTACK_RADIUS;
            // 无罪：孢子领域 CD 强制压到 3 秒（基础 prefab 默认 5s）。
            // 同步把 CDkey 也置为 CDtime，让开局立刻能放第一发，避免白白等一个 CD 周期。
            sf.CDtime = TOMB_SPORE_FIELD_CDTIME;
            sf.CDkey  = TOMB_SPORE_FIELD_CDTIME;
            Debug.Log($"[UR加成] 无罪：孢子领域 attackRadius→{TOMB_INITIAL_ATTACK_RADIUS}, CDtime→{TOMB_SPORE_FIELD_CDTIME}");
        }
    }

    private void TryGrantFireBall()
    {
        // 已经有了？跳过
        // 火球术 prefab 挂的是 Skillbase 基类（无专属脚本），所以按 Skillname 判定。
        foreach (Transform child in _player.SkillList)
        {
            Skillbase sb = child.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == "火球术")
            {
                Debug.Log("[UR加成] 夏无：玩家已有火球术，跳过授予");
                return;
            }
        }

        GameObject prefab = ResolveFireBallPrefab();
        if (prefab == null)
        {
            Debug.LogError("[UR加成] 夏无：无法找到火球术 prefab！请在 Player 上的 PlayerSkinSkillBuff 组件 " +
                           "Inspector 里手动指派 fireballSkillPrefabFallback。火球术授予失败。");
            return;
        }

        GameObject go = Instantiate(prefab, _player.SkillList);
        go.name = prefab.name; // 去掉 "(Clone)" 后缀
        // 触发 BattleUI 刷新
        if (_player.battleUI != null) _player.battleUI.RefreshSkill();
        Debug.Log($"[UR加成] 夏无：已自动获得火球术（prefab: {prefab.name}）");
    }

    private void TryGrantHurricane()
    {
        // 已经有了？跳过。飓风 prefab 挂的是 Skillbase 基类（无专属脚本），按 Skillname 判定。
        foreach (Transform child in _player.SkillList)
        {
            Skillbase sb = child.GetComponent<Skillbase>();
            if (sb != null && sb.Skillname == "飓风")
            {
                Debug.Log("[UR加成] 南筱风：玩家已有飓风，跳过授予");
                return;
            }
        }

        GameObject prefab = ResolveHurricanePrefab();
        if (prefab == null)
        {
            Debug.LogError("[UR加成] 南筱风：无法找到飓风 prefab！请在 Player 上的 PlayerSkinSkillBuff 组件 " +
                           "Inspector 里手动指派 hurricaneSkillPrefabFallback。飓风授予失败。");
            return;
        }

        GameObject go = Instantiate(prefab, _player.SkillList);
        go.name = prefab.name; // 去掉 "(Clone)" 后缀
        if (_player.battleUI != null) _player.battleUI.RefreshSkill();
        Debug.Log($"[UR加成] 南筱风：已自动获得飓风（prefab: {prefab.name}）");
    }

    private GameObject ResolveHurricanePrefab()
    {
        if (hurricaneSkillPrefabFallback != null) return hurricaneSkillPrefabFallback;
        if (_autoHurricanePrefab != null) return _autoHurricanePrefab;

        ChoiceUI cui = ChoiceUI.Instance;
        if (cui == null) cui = FindObjectOfType<ChoiceUI>(true);
        if (cui != null && cui.skillEntries != null)
        {
            foreach (var entry in cui.skillEntries)
            {
                if (entry == null || entry.learnSkillPrefab == null) continue;
                getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learn == null || learn.skill == null) continue;
                if (learn.skill.Skillname == "飓风")
                {
                    Debug.Log("[UR加成] 通过 ChoiceUI.skillEntries 找到飓风 prefab");
                    return learn.skill.gameObject;
                }
            }
        }

        var res = Resources.Load<GameObject>("hurricaneSkill");
        if (res != null) return res;

        Skillbase[] all = FindObjectsOfType<Skillbase>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Skillname == "飓风")
                return all[i].gameObject;
        }

        return null;
    }

    private void TryGrantSporeField()
    {
        // 已经持有孢子领域？跳过
        SkillSporeField existing = _player.SkillList.GetComponentInChildren<SkillSporeField>(true);
        if (existing != null)
        {
            Debug.Log("[UR加成] 无罪：玩家已有孢子领域，跳过授予");
            return;
        }

        GameObject prefab = ResolveSporeFieldPrefab();
        if (prefab == null)
        {
            Debug.LogError("[UR加成] 无罪：无法找到孢子领域 prefab！请在 Player 上的 PlayerSkinSkillBuff 组件 " +
                           "Inspector 里手动指派 sporeFieldSkillPrefabFallback。孢子领域授予失败。");
            return;
        }

        GameObject go = Instantiate(prefab, _player.SkillList);
        go.name = prefab.name;
        if (_player.battleUI != null) _player.battleUI.RefreshSkill();
        Debug.Log($"[UR加成] 无罪：已自动获得孢子领域（prefab: {prefab.name}）");
    }

    private void TryGrantWindArrow()
    {
        // 已经持有风箭？跳过
        SkillWindArrow existing = _player.SkillList.GetComponentInChildren<SkillWindArrow>(true);
        if (existing != null)
        {
            Debug.Log("[UR加成] 无罪：玩家已有风箭，跳过授予");
            return;
        }

        GameObject prefab = ResolveWindArrowPrefab();
        if (prefab == null)
        {
            Debug.LogError("[UR加成] 无罪：无法找到风箭 prefab！请在 Player 上的 PlayerSkinSkillBuff 组件 " +
                           "Inspector 里手动指派 windArrowSkillPrefabFallback。风箭授予失败。");
            return;
        }

        GameObject go = Instantiate(prefab, _player.SkillList);
        go.name = prefab.name;
        if (_player.battleUI != null) _player.battleUI.RefreshSkill();
        Debug.Log($"[UR加成] 无罪：已自动获得风箭（prefab: {prefab.name}）");
    }

    /// <summary>
    /// 测试用：直接给玩家「亡者领域」，但保留风箭和孢子领域，方便观察三个技能的同台表现。
    ///
    /// 与正式 getnewskill_TombDomain.chocieupgrade 的差异：
    ///   - 正式流程会删除孢子领域（除非「不忘初心」装备解锁）
    ///   - 测试场景下我们保留它，便于策划同时观察「亡者领域 + 孢子领域 + 风箭」组合
    ///   - 风箭仍按正式流程锁定为「亡者领域 紫色 + 半径 10」，避免画面闪烁两套配色
    /// </summary>
    private void TryGrantTombDomain()
    {
        // 已经持有？跳过
        SkillTombDomain existingTomb = _player.SkillList.GetComponentInChildren<SkillTombDomain>(true);
        if (existingTomb != null)
        {
            Debug.Log("[UR加成·测试] 无罪：玩家已有亡者领域，跳过授予");
            return;
        }

        GameObject prefab = ResolveTombDomainPrefab();
        if (prefab == null)
        {
            Debug.LogError("[UR加成·测试] 无罪：无法找到亡者领域 prefab！请在 Player 上的 PlayerSkinSkillBuff 组件 " +
                           "Inspector 里手动指派 tombDomainSkillPrefabFallback。亡者领域授予失败。");
            return;
        }

        // 实例化前先抓一份风箭/孢子领域引用，用于后续清理（与正式学习流程对齐）
        SkillSporeField sporeFieldSkill = _player.SkillList.GetComponentInChildren<SkillSporeField>(true);
        SkillFormOfWind formOfWindSkill = _player.SkillList.GetComponentInChildren<SkillFormOfWind>(true);
        SkillWindArrow  windArrowSkill  = _player.SkillList.GetComponentInChildren<SkillWindArrow>(true);

        GameObject go = Instantiate(prefab, _player.SkillList);
        go.name = prefab.name;
        SkillTombDomain td = go.GetComponent<SkillTombDomain>();
        if (td != null) td.ApplyInheritanceSnapshot(sporeFieldSkill, formOfWindSkill);

        // 测试场景下保留孢子领域：观察「亡者领域 + 孢子领域 + 风箭」并存的整体表现。
        // 正式升级流程会吞噬孢子领域；如需复现该副作用，注释掉下面整段保留逻辑、改为 Destroy 即可。
        // if (sporeFieldSkill != null) Destroy(sporeFieldSkill.gameObject);
        _ = sporeFieldSkill; // 仅作为引用占位，避免编译器认为该局部变量未使用

        // 风箭无论如何都保留，但锁定为亡者领域配色（紫色 + 半径 10），与正式流程一致
        if (windArrowSkill != null) windArrowSkill.LockToTombDomainPalette();

        if (_player.battleUI != null) _player.battleUI.RefreshSkill();
        Debug.Log($"[UR加成·测试] 无罪：已直接获得亡者领域（prefab: {prefab.name}），孢子领域 / 风箭一并保留（风箭已锁紫色）");
    }

    private GameObject ResolveTombDomainPrefab()
    {
        if (tombDomainSkillPrefabFallback != null) return tombDomainSkillPrefabFallback;
        if (_autoTombDomainPrefab != null) return _autoTombDomainPrefab;

        ChoiceUI cui = ChoiceUI.Instance;
        if (cui == null) cui = FindObjectOfType<ChoiceUI>(true);
        if (cui != null && cui.skillEntries != null)
        {
            foreach (var entry in cui.skillEntries)
            {
                if (entry == null || entry.learnSkillPrefab == null) continue;
                getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learn == null || learn.skill == null) continue;
                if (learn.skill.GetComponent<SkillTombDomain>() != null)
                {
                    Debug.Log("[UR加成·测试] 通过 ChoiceUI.skillEntries 找到亡者领域 prefab");
                    return learn.skill.gameObject;
                }
            }
        }

        var res = Resources.Load<GameObject>("SkillTombDomain");
        if (res != null) return res;

        Skillbase[] all = FindObjectsOfType<Skillbase>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetComponent<SkillTombDomain>() != null)
                return all[i].gameObject;
        }

        return null;
    }



    private GameObject ResolveSporeFieldPrefab()
    {
        if (sporeFieldSkillPrefabFallback != null) return sporeFieldSkillPrefabFallback;
        if (_autoSporeFieldPrefab != null) return _autoSporeFieldPrefab;

        ChoiceUI cui = ChoiceUI.Instance;
        if (cui == null) cui = FindObjectOfType<ChoiceUI>(true);
        if (cui != null && cui.skillEntries != null)
        {
            foreach (var entry in cui.skillEntries)
            {
                if (entry == null || entry.learnSkillPrefab == null) continue;
                getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learn == null || learn.skill == null) continue;
                if (learn.skill.Skillname == "孢子领域" || learn.skill.GetComponent<SkillSporeField>() != null)
                {
                    Debug.Log("[UR加成] 通过 ChoiceUI.skillEntries 找到孢子领域 prefab");
                    return learn.skill.gameObject;
                }
            }
        }

        var res = Resources.Load<GameObject>("SporeFieldSkill");
        if (res != null) return res;

        Skillbase[] all = FindObjectsOfType<Skillbase>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetComponent<SkillSporeField>() != null)
                return all[i].gameObject;
        }

        return null;
    }

    private GameObject ResolveWindArrowPrefab()
    {
        if (windArrowSkillPrefabFallback != null) return windArrowSkillPrefabFallback;
        if (_autoWindArrowPrefab != null) return _autoWindArrowPrefab;

        ChoiceUI cui = ChoiceUI.Instance;
        if (cui == null) cui = FindObjectOfType<ChoiceUI>(true);
        if (cui != null && cui.skillEntries != null)
        {
            foreach (var entry in cui.skillEntries)
            {
                if (entry == null || entry.learnSkillPrefab == null) continue;
                getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learn == null || learn.skill == null) continue;
                if (learn.skill.Skillname == "风箭" || learn.skill.GetComponent<SkillWindArrow>() != null)
                {
                    Debug.Log("[UR加成] 通过 ChoiceUI.skillEntries 找到风箭 prefab");
                    return learn.skill.gameObject;
                }
            }
        }

        var res = Resources.Load<GameObject>("WindArrowskill");
        if (res != null) return res;

        Skillbase[] all = FindObjectsOfType<Skillbase>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].GetComponent<SkillWindArrow>() != null)
                return all[i].gameObject;
        }

        return null;
    }

    private GameObject ResolveFireBallPrefab()
    {
        // 优先 1：Inspector 显式指派（Player.OnValidate 自动指派的也走这条）
        if (fireballSkillPrefabFallback != null) return fireballSkillPrefabFallback;

        // 优先 2：本组件 OnValidate 自动绑定（理论上 AddComponent 不会触发，仅用于手动挂载情况）
        if (_autoFireballPrefab != null) return _autoFireballPrefab;

        // 优先 3：从 ChoiceUI 的 skillEntries 列表里查名为「火球术」的 learnSkillPrefab
        // 这是最稳的运行时路径——主菜单/战斗场景都会有 ChoiceUI 实例，且 skillEntries 在 Inspector 里固定配好
        ChoiceUI cui = ChoiceUI.Instance;
        if (cui == null) cui = FindObjectOfType<ChoiceUI>(true);
        if (cui != null && cui.skillEntries != null)
        {
            foreach (var entry in cui.skillEntries)
            {
                if (entry == null || entry.learnSkillPrefab == null) continue;
                getnewskill learn = entry.learnSkillPrefab.GetComponent<getnewskill>();
                if (learn == null || learn.skill == null) continue;
                if (learn.skill.Skillname == "火球术")
                {
                    Debug.Log("[UR加成] 通过 ChoiceUI.skillEntries 找到火球术 prefab");
                    return learn.skill.gameObject;
                }
            }
        }

        // 优先 4：Resources 文件夹（项目里没有就跳过）
        var res = Resources.Load<GameObject>("fireballSkill");
        if (res != null) return res;

        // 优先 5：场景中其它玩家/选项 UI 挂着名为「火球术」的 Skillbase 实例
        Skillbase[] all = FindObjectsOfType<Skillbase>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].Skillname == "火球术")
                return all[i].gameObject;
        }

        return null;
    }

    /// <summary>
    /// 给一个刚 Instantiate 出来的风箭子弹按「当前所选角色身份」染色。
    /// 由 <see cref="SkillWindArrow.Useskill"/> 实例化子弹后调用。
    ///
    /// 颜色规则（按 PlayerPrefs["SelectedSkin"]）：
    ///   - <see cref="SKIN_CIRNO"/>（琪露诺）   → 不染色，保留 prefab 原色（白/默认青）；
    ///   - <see cref="SKIN_NANXIAOFENG"/>（南筱风） → 青绿色；
    ///   - <see cref="SKIN_XIAWU"/>（夏无）     → 红色；
    ///   - <see cref="SKIN_TOMB"/>（无罪）      → 紫黑色。
    ///
    /// 注意：之前 BulletWindArrow.GetFather 里还有一段「学了亡者领域 → 染紫」的逻辑
    /// （TryApplyTombDomainTint），实际并未真正生效且与本染色冲突，已在 BulletWindArrow.cs 中删除。
    /// 现在风箭颜色 = 角色身份，单一来源。
    ///
    /// 染色覆盖：
    ///   1) 子弹根节点 + 所有子物体的 SpriteRenderer.color
    ///      —— 兜底：把 sharedMaterial 强切到 Sprites/Default。原因是 prefab 里若挂的是 URP Lit
    ///      或自定义 shader，可能不读 _RendererColor / _Color，SpriteRenderer.color 改了也不渲染出来。
    ///   2) 所有 ParticleSystem：
    ///      a. 先 Stop(true, StopEmittingAndClear) 清空 prefab playOnAwake=1 在 Instantiate 当帧
    ///         已发射的"原色粒子"——这是之前染色看不到的根因之一：旧粒子用的是 prefab 默认 startColor，
    ///         main.startColor 修改对**已存在的粒子**无效，只对**未来新发射的粒子**有效。
    ///      b. 覆盖 main.startColor 为单色
    ///      c. 覆盖 colorOverLifetime（启用时，新发射粒子也会被原 Gradient 漂回去）
    ///      d. 覆盖 ParticleSystemRenderer 材质 tint（粒子贴图本身若为白底，必须靠材质上色才能看到颜色）
    ///      e. Play(false) 重启发射，新粒子全部带色
    ///   3) 所有 TrailRenderer 的 startColor / endColor
    /// </summary>
    public static void ApplySkinTintToWindArrowBullet(GameObject bullet)
    {
        if (bullet == null) return;
        // 缓存未初始化（例如 SkillWindArrow 比 PlayerSkinSkillBuff.Awake 更早执行）时再读一次
        if (CurrentSkinIndex < 0)
            CurrentSkinIndex = PlayerPrefs.GetInt("SelectedSkin", 0);

        // 按角色身份取色；琪露诺保留 prefab 原色，直接 return
        if (!TryGetWindArrowTintForSkin(CurrentSkinIndex, out Color tint)) return;

        Color tintFade = new Color(tint.r, tint.g, tint.b, 0f);

        // 1) 主体 + 子物体 SpriteRenderer
        //    把 sharedMaterial 强切到 Sprites/Default 保证 color 一定吃（与 PlayerSkinOverrider 同思路）
        Material spriteDefault = GetSharedSpritesDefaultForBullet();
        SpriteRenderer[] srs = bullet.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            // 若当前材质不是 Sprites/Default，切过去，避免自定义 shader 不读 color 通道
            if (spriteDefault != null)
            {
                var cur = srs[i].sharedMaterial;
                bool isSpriteDefault = cur != null && cur.shader != null && cur.shader.name == "Sprites/Default";
                if (!isSpriteDefault) srs[i].sharedMaterial = spriteDefault;
            }
            srs[i].color = tint;
        }

        // 2) 粒子系统
        ParticleSystem[] pss = bullet.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < pss.Length; i++)
        {
            ParticleSystem ps = pss[i];
            if (ps == null) continue;

            // (a) 清掉 prefab playOnAwake=1 在 Instantiate 那一瞬已发射的旧色粒子
            //     不清掉的话玩家会看到一团原色烟雾后面才慢慢出现新色，观感上"染色没生效"
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // (b) startColor 强制设为单色（覆盖 RandomBetweenTwoColors 等模式）
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(tint);

            // (c) colorOverLifetime 若开启则会把粒子重新乘成原来的 Gradient（白色），需要也覆盖
            var col = ps.colorOverLifetime;
            if (col.enabled)
            {
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[] {
                        new GradientColorKey(tint, 0f),
                        new GradientColorKey(tint, 1f),
                    },
                    new[] {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f), // 末端淡出，保留拖尾感
                    });
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }

            // (d) ParticleSystemRenderer 材质 tint。粒子贴图若是白底，main.startColor 已足够；
            //     但项目里部分粒子贴图带颜色（青色烟），必须把材质 _Color / _TintColor 也覆盖才能彻底变色。
            //     用静态字典缓存「原 sharedMaterial → 染色版材质」，避免每发风箭都 new Material 造成 GC。
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null) ApplySkinTintParticleMaterial(psr, tint);

            // (e) 重启发射；withChildren=false 因为我们外层已经 GetComponentsInChildren 单独处理过
            ps.Play(false);
        }

        // 3) TrailRenderer（如果有）
        TrailRenderer[] trs = bullet.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trs.Length; i++)
        {
            if (trs[i] == null) continue;
            trs[i].startColor = tint;
            trs[i].endColor = tintFade;
        }
    }

    /// <summary>
    /// 兼容旧名：保留 <c>ApplyXiaWuTintToWindArrowBullet</c> 入口，转发到按角色身份染色的新实现。
    /// 防止外部如果还在调用旧名时编译失败（理论上只有 SkillWindArrow.Useskill 一处，已同步改为新名）。
    /// </summary>
    [System.Obsolete("Use ApplySkinTintToWindArrowBullet instead; 风箭染色已统一改为按角色身份分流。")]
    public static void ApplyXiaWuTintToWindArrowBullet(GameObject bullet)
    {
        ApplySkinTintToWindArrowBullet(bullet);
    }

    /// <summary>按所选皮肤索引取风箭染色；琪露诺保留 prefab 原色（返回 false 表示不染）。</summary>
    private static bool TryGetWindArrowTintForSkin(int skin, out Color tint)
    {
        switch (skin)
        {
            case SKIN_NANXIAOFENG:
                // 青绿色（teal / 翡翠绿）—— 与南筱风「风」的清新意象呼应
                tint = new Color(0.20f, 0.85f, 0.65f, 1f);
                return true;
            case SKIN_XIAWU:
                // 红色（偏红橙）—— 沿用原本夏无 UR 加成的染色
                tint = new Color(1f, 0.25f, 0.18f, 1f);
                return true;
            case SKIN_TOMB:
                // 紫黑色 —— 与无罪/亡者领域的暗紫意象呼应；比 SkillWindArrow.TombDomainCircleColor 更深
                tint = new Color(0.22f, 0.06f, 0.30f, 1f);
                return true;
            case SKIN_CIRNO:
            default:
                // 琪露诺：保留 prefab 原色，不染色
                tint = Color.white;
                return false;
        }
    }

    /// <summary>
    /// 给 ParticleSystemRenderer 套一份按角色染色的材质实例。
    /// 用静态字典缓存「原 sharedMaterial + 染色 → 染色版材质」，避免每发风箭都 new Material 造成 GC。
    /// key 同时包含色值，防止「无罪 / 夏无 共用同一份 prefab 材质」时互相覆盖。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<(Material src, int colorKey), Material>
        _skinTintMatCache = new System.Collections.Generic.Dictionary<(Material, int), Material>();

    private static void ApplySkinTintParticleMaterial(ParticleSystemRenderer psr, Color tint)
    {
        Material src = psr.sharedMaterial;
        if (src == null) return;

        // 把颜色压成 int 当 key（精度足够区分三种身份色，避免浮点 hash 抖动）
        int colorKey = (Mathf.RoundToInt(tint.r * 255) << 24)
                     | (Mathf.RoundToInt(tint.g * 255) << 16)
                     | (Mathf.RoundToInt(tint.b * 255) << 8)
                     | Mathf.RoundToInt(tint.a * 255);

        var k = (src, colorKey);
        if (!_skinTintMatCache.TryGetValue(k, out Material tinted) || tinted == null)
        {
            tinted = new Material(src);
            tinted.name = src.name + "_WindArrowSkinTint_" + colorKey.ToString("X");
            if (tinted.HasProperty("_Color"))    tinted.color = tint;
            if (tinted.HasProperty("_TintColor")) tinted.SetColor("_TintColor", tint);
            _skinTintMatCache[k] = tinted;
        }
        psr.sharedMaterial = tinted;
    }

    /// <summary>子弹 SpriteRenderer 兜底材质（共享一份 Sprites/Default，避免每发子弹都 new Material 泄漏）。</summary>
    private static Material _bulletSpriteDefault;
    private static Material GetSharedSpritesDefaultForBullet()
    {
        if (_bulletSpriteDefault == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _bulletSpriteDefault = new Material(sh) { name = "WindArrowTintSpritesDefault" };
        }
        return _bulletSpriteDefault;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor 时自动按文件名搜索 fireballSkill.prefab 并绑定到 _autoFireballPrefab。
    /// 这样脚本一被挂上（或 prefab 被 Inspector 打开）就会自动找到火球术 prefab，
    /// 打包时该资产作为依赖被一起打进游戏，运行时无须 Resources/ 也无须手动拖拽。
    /// </summary>
    private void OnValidate()
    {
        if (_autoFireballPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("fireballSkill t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "fireballSkill") continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    _autoFireballPrefab = go;
                    Debug.Log($"[UR加成] OnValidate 自动绑定火球术 prefab: {path}");
                    break;
                }
            }
        }

        if (_autoHurricanePrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("hurricaneSkill t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "hurricaneSkill") continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    _autoHurricanePrefab = go;
                    Debug.Log($"[UR加成] OnValidate 自动绑定飓风 prefab: {path}");
                    break;
                }
            }
        }

        if (_autoSporeFieldPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("SporeFieldSkill t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "SporeFieldSkill") continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    _autoSporeFieldPrefab = go;
                    Debug.Log($"[UR加成] OnValidate 自动绑定孢子领域 prefab: {path}");
                    break;
                }
            }
        }

        if (_autoWindArrowPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("WindArrowskill t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "WindArrowskill") continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    _autoWindArrowPrefab = go;
                    Debug.Log($"[UR加成] OnValidate 自动绑定风箭 prefab: {path}");
                    break;
                }
            }
        }

        if (_autoTombDomainPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("SkillTombDomain t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "SkillTombDomain") continue;
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null)
                {
                    _autoTombDomainPrefab = go;
                    Debug.Log($"[UR加成·测试] OnValidate 自动绑定亡者领域 prefab: {path}");
                    break;
                }
            }
        }
    }
#endif
}
