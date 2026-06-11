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

    // 自然回血计时
    private float _regenTimer = 0f;
    // 死亡防重入：避免多个敌人同帧打死时重复触发返回主菜单协程
    private bool _isDead = false;

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
        rb.mass = 8.0f;
        Physics.gravity = new Vector3(0, -30f, 0);

        // ===== 分身（tag="Clone"）物理处理 =====
        // 背景 / 历史 bug：
        //   分身和主玩家是同一份 Player prefab 克隆，Update() 里走同一段
        //   `rb.velocity = new Vector3(hmove,0,vmove).normalized * speed;` —— 读全局
        //   Input.GetAxis，于是分身和主玩家被同一套输入同时驱动。但因为分身的初始位置
        //   与主玩家有 (1.5,0,0) 偏移，两者并不会刚好重叠：一旦主玩家被某些情景挤
        //   到分身位置（拐弯、被敌人推、奇遇刷怪等），两份 **非 kinematic** 的
        //   Rigidbody 互相碰撞 → 物理引擎给互相施加反作用力 → 又因为下一帧两者各自
        //   都用 `rb.velocity = ...` 直接覆盖速度，碰撞抖动会被速度赋值"冻结"成
        //   净位移 → 分身像被卡在主玩家身上一起被推着乱跑，玩家观感就是
        //   "分身一直推着主角胡乱移动"。
        //
        // 修复：分身的 Rigidbody 改 kinematic，并把 velocity 清零；这样分身完全不
        //       参与物理推挤，主玩家可以自由穿过它。Update() 里也会对分身跳过输入
        //       与移动逻辑（见 Update 中的 isClone 早返回），分身就稳定地站在出生
        //       点继续运转 SkillList（"技能塔"行为），符合策划"克隆玩家保留技能"的语义。
        //
        // 例外：SSR9「三清化一」分身由 ShadowCloneInvisibility 接管，它在 Awake 里
        //       也会把自己 rb.isKinematic = true，这里再写一次幂等无副作用。
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

        // ===== 分身（tag="Clone"）跳过输入 / 移动 / 朝向 / 冲刺 =====
        // 历史 bug：分身和主玩家走同一份 Update —— `Input.GetAxis` 是全局的，分身会
        // 跟主玩家同方向移动；又因为分身和主玩家的 Rigidbody 互相碰撞，会产生
        // "分身一直推着玩家胡乱移动"的怪异表现。
        // 修复：分身上的 Player.Update 只保留 SkillList 运转（"技能塔"），不再读
        // 输入、不再改 velocity / scale。物理推挤已经在 Start() 里把分身 rb 设为
        // kinematic 阻止；这里再把它的 velocity 钳零兜底（防御 ResetRunCounter 之类
        // 外部把 rb 重新设为非 kinematic 的极端情况）。
        // 注意：SkillList 段必须保留 —— 策划「人格解离」明文："克隆体...继承...玩家
        // 技能列表中随机一半技能"，SSR6 还会逐帧拉满，分身的技能必须照常发射。
        if (gameObject.CompareTag("Clone"))
        {
            if (rb != null && !rb.isKinematic) rb.velocity = Vector3.zero;
            // ani 状态保持上一帧；分身不会动，ismove 自然保持 false（出生即 false）
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
            transform.localScale = new Vector3(1, 1, 1);
        if (hmove < 0)
            transform.localScale = new Vector3(-1, 1, 1);

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
