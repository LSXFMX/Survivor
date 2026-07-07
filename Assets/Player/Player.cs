using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Player : Attribute
{
    public Material material;
    public Material red;
    private Rigidbody rb;
    public Animator ani;
    public battleUI battleUI;
    public Transform SkillList;
    /// <summary>
    /// SSR9「三清化一」：分身技能被合并到本体后，存放在这个独立容器中。
    /// 容器内技能保持分身的数值比例，不受本体升级影响，由本体 Update 统一释放。
    /// </summary>
    [HideInInspector] public Transform SkillListClone;
    public float PickupRadius;

    [Header("UR 角色加成依赖资源")]
    [Tooltip("夏无 UR 加成：开局自动获得的火球术 prefab。\n" +
             "Editor 会通过 OnValidate 自动按文件名 fireballSkill.prefab 在项目内搜索并绑定，\n" +
             "打包后该 prefab 作为资产依赖被一起打进游戏。无须 Resources/ 也无须手动拖拽。")]
    public GameObject fireballSkillPrefab;

    [Tooltip("南筱风 UR 加成：开局自动获得的飓风 prefab。\n" +
             "Editor 会通过 OnValidate 自动按文件名 hurricaneSkill.prefab 在项目内搜索并绑定，\n" +
             "打包后该 prefab 作为资产依赖被一起打进游戏。无须 Resources/ 也无须手动拖拽。")]
    public GameObject hurricaneSkillPrefab;

    // 冲刺（成就装备2解锁）
    [HideInInspector] public bool dashUnlocked = false;
    public float dashDistance = 5f;   // 冲刺距离
    public float dashDuration = 0.15f; // 冲刺持续时间
    public float dashCooldown = 2f;   // 冲刺CD（局内可被好感度装备等修正）
    /// <summary>Prefab 上的初始冲刺 CD，供装备按倍率重算，避免每局重复叠乘</summary>
    public float DashCooldownBase { get; private set; }
    private float _dashCDTimer = 0f;
    private bool  _isDashing   = false;
    [HideInInspector] public bool dashInvincibleUnlocked = false;
    [HideInInspector] public bool dashPhaseUnlocked = false;

    /// <summary>被抓取/定身时置 true，屏蔽玩家的移动与冲刺（供 WolfBoss 撕咬处决等定身演出使用）。</summary>
    [HideInInspector] public bool movementLocked = false;

    // 自然回血计时
    private float _regenTimer = 0f;
    // 死亡防重入：避免多个敌人同帧打死时重复触发返回主菜单协程
    private bool _isDead = false;

    // ===== 分身跟随主体 =====
    /// <summary>分身的主体引用（由 AdventurePersonalityDissolve 在 clone 激活后设定）。</summary>
    [HideInInspector] public Player cloneOwner;
    /// <summary>跟随速度倍率（相对自身 speed）。</summary>
    private const float CLONE_FOLLOW_SPEED_MULT = 0.95f;
    /// <summary>跟随偏移（出生时与主体的相对偏移方向）。</summary>
    private Vector3 _cloneFollowOffset = new Vector3(1.5f, 0f, 0f);
    /// <summary>跟随死区——距目标点小于该值时停止移动，避免抖动。</summary>
    private const float CLONE_FOLLOW_DEADZONE = 0.3f;
    /// <summary>分身模型缩放比例：1=原大小，0.7=70%（一化三清专属）。</summary>
    [System.NonSerialized] public float modelScale = 1f;
    /// <summary>缓存 SSR9 组件检测结果，避免每帧 GetComponent。</summary>
    private bool _cloneSsr9Checked = false;
    private bool _cloneSsr9Active = false;

    /// <summary>
    /// 主玩家死亡判定的「免疫截止时间戳」（Time.unscaledTime 基准）。
    /// 由对主玩家进行高风险物理改造（SetActive 翻转 / 大批 Instantiate / blink 等）的脚本设置，
    /// 用于跨越"物理重激活那一帧多个敌人同时 OnCollisionEnter 把残血主玩家秒杀"的窗口。
    ///
    /// 例如：奇遇「人格解离」(AdventurePersonalityDissolve) 会瞬间把主玩家血量减半，
    /// 同帧 Instantiate 一个分身——历史上曾因 SetActive(false)→Instantiate→SetActive(true)
    /// 触发主玩家 collider 重启用、被周围敌人重复扣血至 hp≤0 → Player.death() 误判失败。
    /// 该 bug 的根因已在 AdventurePersonalityDissolve.Execute 内用「inactive holder」模式修复，
    /// 但本字段作为第二道防御保留：即使将来其它奇遇 / 装备 / 测试代码触发类似时序，
    /// 也能保证主玩家在事件落地后短暂(默认 0.5s)免死亡判定，并把 hp 钳回 ≥1。
    ///
    /// 设置方式：
    ///   <code>Player.MainPlayerDeathGraceUntilUnscaled = Time.unscaledTime + 0.5f;</code>
    /// 默认值 0 表示「无免疫窗口」（不影响正常死亡判定）。
    /// </summary>
    public static float MainPlayerDeathGraceUntilUnscaled = 0f;

    void Awake()
    {
        DashCooldownBase = dashCooldown;

        // 运行时动态确保渲染子物体挂载了换装系统，使局内换装真正生效
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.GetComponent<PlayerSkinOverrider>() == null)
        {
            sr.gameObject.AddComponent<PlayerSkinOverrider>();
        }

        // ===== UR 角色技能加成注入器 =====
        // 不论选哪个皮肤都挂上：组件 Start() 内部按 SelectedSkin 派发，
        // 琪露诺(0)走空分支无副作用，南筱风(1)/夏无(2)各自应用专属加成。
        // 只挂在原始 Player 上，不挂分身（Clone）以免重复授予技能。
        if (!gameObject.CompareTag("Clone") && GetComponent<PlayerSkinSkillBuff>() == null)
        {
            var buff = gameObject.AddComponent<PlayerSkinSkillBuff>();
            // 把 Player 在 OnValidate 自动找到的火球术 / 飓风 prefab 注入给 buff
            if (fireballSkillPrefab != null)
                buff.fireballSkillPrefabFallback = fireballSkillPrefab;
            if (hurricaneSkillPrefab != null)
                buff.hurricaneSkillPrefabFallback = hurricaneSkillPrefab;
            // AddComponent 不会触发 buff.Awake 之外的逻辑，但我们立即调用 GrantInitialSkillNow，
            // 把"开局自带技能"的 Instantiate 提前到 Player.Awake 阶段，
            // 这样后续 ChoiceUI.OnEnable / refresh 在第一次执行时就能扫到自带技能，
            // 不会把同名学习卡塞进卡池。
            buff.GrantInitialSkillNow();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.GetComponent<PlayerSkinOverrider>() == null)
        {
            sr.gameObject.AddComponent<PlayerSkinOverrider>();
            Debug.Log($"[换装] 已自动为 {sr.gameObject.name} 挂载 PlayerSkinOverrider 换装系统！");
        }

        // 自动按文件名搜索 fireballSkill.prefab 并绑定到 fireballSkillPrefab 字段
        if (fireballSkillPrefab == null)
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
                    fireballSkillPrefab = go;
                    Debug.Log($"[UR加成] Player.OnValidate 自动绑定火球术 prefab: {path}");
                    break;
                }
            }
        }

        // 自动按文件名搜索 hurricaneSkill.prefab 并绑定到 hurricaneSkillPrefab 字段
        if (hurricaneSkillPrefab == null)
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
                    hurricaneSkillPrefab = go;
                    Debug.Log($"[UR加成] Player.OnValidate 自动绑定飓风 prefab: {path}");
                    break;
                }
            }
        }
    }
#endif

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 100f; // 高质量防推：避免被普通小怪（mass=1）和友军小怪（mass=0.1）推着走
        Physics.gravity = new Vector3(0, -30f, 0);

        // ===== 分身（tag="Clone"）物理处理 =====
        // 分身不通过物理引擎移动（避免与主玩家产生碰撞推挤），改为 kinematic 模式。
        // 分身的位移由 Update 中的 AI 跟随逻辑（transform.position = MoveTowards）驱动。
        // SSR9「三清化一」分身由 ShadowCloneInvisibility 额外接管位置，幂等无副作用。
        if (gameObject.CompareTag("Clone") && rb != null)
        {
            rb.isKinematic = true;
            rb.velocity        = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void levelup()
    {
        level += 1;
        int bestLevel = PlayerPrefs.GetInt("BestSingleRunLevel", 1);
        if (level > bestLevel)
        {
            PlayerPrefs.SetInt("BestSingleRunLevel", level);
            PlayerPrefs.Save();
        }
        if (level >= 50 && PlayerPrefs.GetInt("ReachedLevel50Once", 0) == 0)
        {
            PlayerPrefs.SetInt("ReachedLevel50Once", 1);
            PlayerPrefs.Save();
            if (EquipmentSystem.Instance != null && !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 7))
            {
                EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 7);
                ToastManager.Show("成就装备7「万象天引」已解锁！");
            }
        }

        healthmax += 20;
        exp = 0;
        expmax += 20;
        battleUI.openchoice();
    }

    void Update()
    {
        // 自然回血：每秒恢复 regen 点血量
        if (regen > 0)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1f)
            {
                _regenTimer = 0f;
                health = Mathf.Min(health + regen, healthmax);
            }
        }

        // 冲刺 CD 计时
        if (_dashCDTimer > 0f) _dashCDTimer -= Time.deltaTime;

        if (_isDashing) return; // 冲刺中不处理普通移动

        // 被抓取/定身：清零移动，屏蔽移动与冲刺（撕咬处决等定身演出）
        if (movementLocked)
        {
            if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero;
            if (ani != null) ani.SetBool("ismove", false);
            return;
        }

        // ===== 分身（tag="Clone"）：跟随主体 + 释放技能 =====
        // 分身不读 Input，改为 AI 跟随主体移动。Rigidbody 保持 kinematic 避免物理推挤，
        // 位移通过 transform.position = MoveTowards 实现。
        if (gameObject.CompareTag("Clone"))
        {
            if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero;

            // —— 跟随主体移动 ——
            // SSR9「三清化一」分身由 ShadowCloneInvisibility 通过父子 transform 管理位置，
            // 此处跳过 AI 跟随，避免与 LateUpdate 的位置锁定互相冲突。
            if (!_cloneSsr9Checked)
            {
                _cloneSsr9Active = GetComponent<ShadowCloneInvisibility>() != null;
                _cloneSsr9Checked = true;
            }
            bool ssr9Active = _cloneSsr9Active;
            if (!ssr9Active && cloneOwner != null)
            {
                Vector3 targetPos = cloneOwner.transform.position + _cloneFollowOffset;
                targetPos.y = transform.position.y; // 保持同一水平面
                float dist = Vector3.Distance(transform.position, targetPos);
                if (dist > CLONE_FOLLOW_DEADZONE)
                {
                    float moveSpeed = Mathf.Max(speed, cloneOwner.speed) * CLONE_FOLLOW_SPEED_MULT;
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, moveSpeed * Time.deltaTime);
                    if (ani != null) ani.SetBool("ismove", true);
                    // 朝向：根据移动方向翻转
                    float dx = targetPos.x - transform.position.x;
                    if (dx > 0.01f) transform.localScale = new Vector3(modelScale, modelScale, modelScale);
                    else if (dx < -0.01f) transform.localScale = new Vector3(-modelScale, modelScale, modelScale);
                }
                else
                {
                    if (ani != null) ani.SetBool("ismove", false);
                }
            }
            else if (!ssr9Active && cloneOwner == null)
            {
                // cloneOwner 未设定时尝试自动查找主玩家
                Transform playerlayer = transform.parent;
                if (playerlayer != null)
                {
                    foreach (Transform t in playerlayer)
                    {
                        if (t == null || t == transform) continue;
                        if (t.CompareTag("Player"))
                        {
                            cloneOwner = t.GetComponent<Player>();
                            break;
                        }
                    }
                }
                if (ani != null) ani.SetBool("ismove", false);
            }

            // —— 技能释放（保持不变）——
            if (SkillList != null && SkillList.childCount > 0)
            {
                foreach (Transform Skill in SkillList)
                {
                    Skillbase s = Skill.GetComponent<Skillbase>();
                    if (s == null) continue;
                    s.player = gameObject;
                    if (s.CDkey >= s.CDtime)
                        StartCoroutine(s.Useskill());
                }
            }
            return;
        }

        float hmove = Input.GetAxis("Horizontal");
        float vmove = Input.GetAxis("Vertical");
        rb.velocity = new Vector3(hmove, 0, vmove).normalized * speed;

        if (hmove != 0 || vmove != 0)
            ani.SetBool("ismove", true);
        if (hmove == 0 && vmove == 0)
            ani.SetBool("ismove", false);
        if (hmove > 0)
            transform.localScale = new Vector3(modelScale, modelScale, modelScale);
        if (hmove < 0)
            transform.localScale = new Vector3(-modelScale, modelScale, modelScale);

        // 冲刺触发：已解锁 + 有移动输入 + 按空格 + CD 结束
        if (dashUnlocked && _dashCDTimer <= 0f &&
            Input.GetKeyDown(KeyCode.Space) &&
            (hmove != 0 || vmove != 0))
        {
            Vector3 dir = new Vector3(hmove, 0, vmove).normalized;
            StartCoroutine(DashRoutine(dir));
        }

        if (SkillList.childCount > 0)
        {
            foreach (Transform Skill in SkillList)
            {
                Skillbase s = Skill.GetComponent<Skillbase>();
                s.player = gameObject;
                if (s.CDkey >= s.CDtime)
                    StartCoroutine(s.Useskill());
            }
        }

        // SSR9「三清化一」：释放合并过来的分身技能
        if (SkillListClone != null && SkillListClone.childCount > 0)
        {
            foreach (Transform Skill in SkillListClone)
            {
                Skillbase s = Skill.GetComponent<Skillbase>();
                if (s == null) continue;
                s.player = gameObject;
                if (s.CDkey >= s.CDtime)
                    StartCoroutine(s.Useskill());
            }
        }
    }

    public bool IsDashing => _isDashing;
    public bool IsDashInvincibleActive => dashInvincibleUnlocked && _isDashing;

    private IEnumerator DashRoutine(Vector3 dir)
    {
        _isDashing = true;
        _dashCDTimer = dashCooldown;

        bool disableCollision = dashPhaseUnlocked;
        bool prevDetectCollisions = rb != null && rb.detectCollisions;
        if (disableCollision && rb != null) rb.detectCollisions = false;

        float dashSpeed = dashDistance / dashDuration;
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rb.velocity = dir * dashSpeed;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (disableCollision && rb != null) rb.detectCollisions = prevDetectCollisions;
        rb.velocity = Vector3.zero;
        _isDashing = false;
    }

    public void startturnred()
    {
        // 亡者领域：玩家每次受伤时治疗所有被控制的世界 Boss
        TombDomainHook.OnPlayerTookDamage();
        StartCoroutine(turnred());
    }

    public IEnumerator turnred()
    {
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = red;
        yield return new WaitForSeconds(0.3f);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = material;
    }

    public void death()
{
    // 分身判定：只要 tag != "Player"（一般是 "Clone"），或者 playerlayer 里还有别的
    // 标签为 "Player" 的对象在自己之前，都视为分身——只销毁自身，不触发失败。
    // 这里同时用 tag 和"主玩家是否还在"双重保险，避免 tag 因极端流程被错改导致失败误触。
    bool isClone = !gameObject.CompareTag("Player");
    if (!isClone)
    {
        Transform playerlayer = transform.parent;
        if (playerlayer != null)
        {
            foreach (Transform t in playerlayer)
            {
                if (t == null || t == transform) continue;
                if (t.CompareTag("Player"))
                {
                    // 还有别的"Player"——本对象其实是漏标的分身，按分身处理
                    isClone = true;
                    break;
                }
            }
        }
    }

    if (isClone)
    {
        Destroy(gameObject);
        return;
    }

    // ============== 主玩家 grace period 兜底 ==============
    // 仅作用于主玩家（已通过上面的分身判定）。若当前时间仍在 MainPlayerDeathGraceUntilUnscaled
    // 之内（由奇遇「人格解离」等高风险操作设置），跳过本次死亡判定，并把 health 钳回 1，
    // 避免敌人下一帧再次撞到时又走进死亡判定。
    // 详细背景见 MainPlayerDeathGraceUntilUnscaled 字段注释。
    if (Time.unscaledTime < MainPlayerDeathGraceUntilUnscaled)
    {
        if (health < 1) health = 1;
        Debug.LogWarning($"[Player.death] 主玩家在 grace period 内触发死亡判定（剩余 {MainPlayerDeathGraceUntilUnscaled - Time.unscaledTime:F2}s），已拦截并把 health 钳回 1");
        return;
    }

    // 防重入：同一局只允许触发一次
    if (_isDead) return;
    _isDead = true;

    // ============== 复活拦截（R_2 读档币 / ReviveManager）==============
    // 时序契约（必须严格保留）：
    //   1) 主体死亡判定确认 + 防重入标志置位 后才询问复活；
    //   2) ReviveManager 弹窗时把 timeScale 设为 0 暂停游戏，等玩家点击；
    //   3) 玩家点「复活」：扣 1 张读档币、health 拉满、_isDead 重置为 false（反射）、
    //      给 0.5s grace 防瞬死、恢复 timeScale —— 后续 Update 玩家继续操控。
    //      此时本方法必须直接 return，不再走分身清场 / ReturnToMain；
    //   4) 玩家点「放弃」：ReviveManager 协程自己调用 battleUI.ReturnToMainPublic(false)
    //      负责返回主菜单；本方法依然要 return（避免再次启动 ReturnToMain 重复触发）。
    //   5) 不满足复活条件（无读档币 / 本局已用过 / 不是主玩家）→ 返回 false，按原死亡流程继续。
    //
    // 关键：本检查必须在 _isDead = true 之后、清场分身之前执行——
    //   - 在 _isDead 之前会被多次重入；
    //   - 在清场分身之后再复活就来不及了（分身已被销毁，复活后场上只剩本体没问题，
    //     但"分身被先杀"是死亡结算的一部分，应该等玩家做出最终决定再执行）。
    if (ReviveManager.Instance != null && ReviveManager.Instance.TryConsumeReviveAndRecover(this))
    {
        // 复活流程已被 ReviveManager 接管：不再清场分身，不再启动 ReturnToMain。
        // 注意：是否复活成功是 ReviveManager 协程内部异步决定的；这里只要它返回 true，
        // 就视为"由它接管整个死亡/复活后续逻辑"，本方法立即 return。
        return;
    }

    // === Bug 修复：主体死亡 → 立刻清场分身 ===
    // 旧逻辑：主体 health<=0 时只启动 ReturnToMain 协程（先 slowMo 再暂停 Time.timeScale=0）。
    // 期间 Time.timeScale=slowMoScale（非 0）→ 分身仍在 Update 中移动；且分身的 health 不一定
    // 同步到 0（仅 MushroomShadowCloneSync 才会同步），它们要等敌人撞到才会触发各自 death() → Destroy。
    // 玩家观感：主体倒下后，分身还能跟着光标到处乱走，"游戏好像没结束"。
    //
    // 修复：在主体死亡判定确认后，立即遍历 playerlayer 把所有非自身的 Player 子节点全销毁
    // （它们都是分身：要么 tag=="Clone"，要么是漏标分身——但既然主体 death 已确认为本节点，
    // 同 playerlayer 下其它任何 Player 都视为分身，全部销毁）。这样 ReturnToMain 协程慢动作
    // 阶段画面里只剩主体死亡 pose，符合"主体死立刻结束"的直觉。
    Transform layer = transform.parent;
    if (layer != null)
    {
        // 先把所有要销毁的 Player 收集到临时列表（直接在 foreach 中 Destroy 不会立刻把子节点从 layer 移除，
        // 但用 SetActive(false) 立即生效——双保险：SetActive(false) 立即停 Update，Destroy 在帧末清理）。
        var toKill = new System.Collections.Generic.List<GameObject>();
        foreach (Transform t in layer)
        {
            if (t == null || t == transform) continue;
            if (t.GetComponent<Player>() != null) toKill.Add(t.gameObject);
        }
        for (int i = 0; i < toKill.Count; i++)
        {
            if (toKill[i] == null) continue;
            toKill[i].SetActive(false);  // 立即停 Update / 物理
            Destroy(toKill[i]);
        }
    }

    // 只有原始玩家死亡才触发游戏结束
    if (battleUI != null)
        battleUI.StartCoroutine(battleUI.ReturnToMainPublic(false));
}
}
