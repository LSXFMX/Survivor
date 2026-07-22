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

    // ── 鼠标点击移动 ────────────────────────────────────────
    [Header("鼠标移动")]
    [Tooltip("鼠标点击移动到达判定的距离阈值（世界单位）。")]
    public float clickMoveReachThreshold = 0.4f;
    public Color   clickMarkerColor      = new Color(1f, 0.85f, 0.4f, 0.9f); // 金色半透明
    public float   clickMarkerRadius     = 0.55f;   // 标记圆的世界半径
    private Vector3?  _clickTarget;
    private GameObject _clickMarker;
    private static Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

    // 自然回血计时
    private float _regenTimer = 0f;
    // 死亡防重入：避免多个敌人同帧打死时重复触发返回主菜单协程
    private bool _isDead = false;

    /// <summary>复活时重置死亡防重入标志。公开方法替代反射（反射在 IL2CPP 剥离下会失败）。</summary>
    public void ResetDeadFlag() { _isDead = false; }

    // ===== 分身跟随主体 =====
    /// <summary>分身的主体引用（由 AdventurePersonalityDissolve 在 clone 激活后设定）。</summary>
    [HideInInspector] public Player cloneOwner;
    /// <summary>跟随速度倍率（相对自身 speed）。</summary>
    private const float CLONE_FOLLOW_SPEED_MULT = 0.95f;
    /// <summary>分身跟随偏移距离（单位）。</summary>
    private const float CLONE_FOLLOW_DISTANCE = 1.5f;
    /// <summary>分身相对于玩家的 X 偏移符号：-1=左侧，+1=右侧。</summary>
    [HideInInspector] public int cloneFollowSide = -1;
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
        rb.mass = 500f; // 与Boss同级质量，确保不被小怪(10)和友军(5)推动
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
            // 分身与主体之间彻底不发生物理碰撞（防止分身 collider 推动主玩家）
            IgnoreCollisionWithMainPlayer();
        }

        // 经验石拾取范围圆圈（仅主玩家；分身不画，避免视觉混乱）
        if (!gameObject.CompareTag("Clone") && GetComponent<ExpPickupRangeCircle>() == null)
            gameObject.AddComponent<ExpPickupRangeCircle>();

        // 记录主玩家出生点（供"脱离卡死"使用）
        if (!gameObject.CompareTag("Clone") && !_mainSpawnSet)
        {
            _mainSpawnPosition = transform.position;
            _mainSpawnSet = true;
        }

        // 鼠标点击标记物（仅主玩家，用程序生成的环形圆纹理）
        if (!gameObject.CompareTag("Clone"))
            CreateClickMarker();
    }

    /// <summary>生成一个程序化圆形标记（SpriteRenderer + 动态环形纹理），初始隐藏。</summary>
    private void CreateClickMarker()
    {
        _clickMarker = new GameObject("ClickMarker");
        _clickMarker.transform.SetParent(null); // 世界空间，不跟玩家移动
        _clickMarker.SetActive(false);

        var sr = _clickMarker.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite(clickMarkerRadius, clickMarkerColor);
        sr.sortingOrder = 200; // 始终在最上层

        // 匹配 45° 俯视视角
        _clickMarker.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    /// <summary>生成一个环形 sprite（空心圆环，中心透明，边缘带颜色）。</summary>
    private static Sprite CreateRingSprite(float worldRadius, Color color)
    {
        const int TEX_SIZE = 64;
        Texture2D tex = new Texture2D(TEX_SIZE, TEX_SIZE, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode   = TextureWrapMode.Clamp;

        float cx = TEX_SIZE * 0.5f, cy = TEX_SIZE * 0.5f;
        float outerR = TEX_SIZE * 0.48f;  // 外半径（像素）
        float innerR = TEX_SIZE * 0.38f;  // 内半径

        Color32[] pixels = new Color32[TEX_SIZE * TEX_SIZE];
        for (int y = 0; y < TEX_SIZE; y++)
        {
            for (int x = 0; x < TEX_SIZE; x++)
            {
                float dx = x - cx, dy = y - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= innerR && d <= outerR)
                {
                    // 环内部：按径向渐变
                    float t = (d - innerR) / (outerR - innerR); // 0=内缘, 1=外缘
                    float alpha = color.a * (0.4f + 0.6f * (1f - Mathf.Abs(t - 0.5f) * 2f)); // 环中间最亮
                    pixels[y * TEX_SIZE + x] = new Color32(
                        (byte)(color.r * 255), (byte)(color.g * 255), (byte)(color.b * 255),
                        (byte)Mathf.Clamp(alpha * 255, 0, 255));
                }
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        // PPU 按世界半径折算：worldRadius 米 = 像素环外半径 / PPU
        float ppu = (outerR - 1f) / worldRadius; // 留 1px 余量
        ppu = Mathf.Max(1f, ppu);
        return Sprite.Create(tex, new Rect(0, 0, TEX_SIZE, TEX_SIZE),
            new Vector2(0.5f, 0.5f), ppu);
    }

    /// <summary>分身启动时忽略与主玩家的所有碰撞（防止"分身推着主角跑"）。</summary>
    private void IgnoreCollisionWithMainPlayer()
    {
        var myCols = GetComponentsInChildren<Collider>();
        if (myCols == null) return;
        // 向上查找 playerlayer → 遍历找 tag="Player"（主玩家）
        Transform layer = transform.parent;
        if (layer == null) return;
        foreach (Transform t in layer)
        {
            if (t == transform || !t.CompareTag("Player")) continue;
            var mainCols = t.GetComponentsInChildren<Collider>();
            if (mainCols == null) continue;
            foreach (var mc in mainCols)
            {
                if (mc == null) continue;
                foreach (var cc in myCols)
                    if (cc != null) Physics.IgnoreCollision(cc, mc, true);
            }
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
                // 分身跟随：cloneFollowSide=-1 → 玩家左侧，+1 → 右侧（世界空间，不随朝向翻转）
                Vector3 followOffset = new Vector3(cloneFollowSide * CLONE_FOLLOW_DISTANCE, 0f, 0f);
                Vector3 targetPos = cloneOwner.transform.position + followOffset;
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

        // ── 鼠标点击移动：优先级低于键盘（WASD 接管时清空鼠标目标）──────
        bool keyboardMoving = false;
        float hmove = Input.GetAxis("Horizontal");
        float vmove = Input.GetAxis("Vertical");
        if (Mathf.Abs(hmove) > 0.01f || Mathf.Abs(vmove) > 0.01f)
        {
            keyboardMoving = true;
            _clickTarget = null; // 键盘移动接管，清除鼠标目标
            HideMarker();
        }
        else
        {
            HandleMouseClickMove();
        }

        // ── 计算移动方向 & 速度 ──────────────────────────────────
        Vector3 moveDir;
        if (_clickTarget.HasValue && !keyboardMoving)
        {
            // 鼠标目标移动
            Vector3 toTarget = _clickTarget.Value - transform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist <= clickMoveReachThreshold)
            {
                _clickTarget = null; // 到达，停止
                HideMarker();
                rb.velocity = Vector3.zero;
            }
            else
            {
                moveDir = toTarget.normalized;
                rb.velocity = moveDir * speed;
                hmove = moveDir.x;
                vmove = moveDir.z;
            }
        }
        else if (keyboardMoving)
        {
            // 键盘移动
            moveDir = new Vector3(hmove, 0, vmove).normalized;
            rb.velocity = moveDir * speed;
        }
        else
        {
            rb.velocity = Vector3.zero;
        }

        // 每帧兜底钳制地图边界（冲刺后已有额外钳制，此处为常规移动兜底）
        ClampToMapBounds();

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

        // ★ 修复"穿山丘卡出地图"：不再用 detectCollisions=false 全局关碰撞，
        //   改用逐敌人 IgnoreCollision 穿怪（仅限敌方单位），地形/山丘/边界绝不穿透。
        float yBeforeDash = transform.position.y;
        bool disableCollision = dashPhaseUnlocked;
        if (disableCollision && rb != null)
            DashIgnoreEnemyCollisions(true);

        float dashSpeed = dashDistance / dashDuration;
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rb.velocity = dir * dashSpeed;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (disableCollision && rb != null)
            DashIgnoreEnemyCollisions(false);
        rb.velocity = Vector3.zero;

        // 冲刺后安全钳：如果 Y 掉落过多（被挤出地图），立刻弹回冲刺前 Y。
        const float SAFE_Y_FLOOR = -50f;
        if (transform.position.y < SAFE_Y_FLOOR || transform.position.y < yBeforeDash - 10f)
        {
            var p = transform.position; p.y = yBeforeDash;
            transform.position = p;
        }
        // 地图边界钳制（防卡出 ±85 范围）
        ClampToMapBounds();

        _isDashing = false;
    }

    private const float MAP_BOUND = 85f;

    /// <summary>冲刺期间仅忽略与敌人单位碰撞，地形/山丘/边界保持不变。</summary>
    private void DashIgnoreEnemyCollisions(bool ignore)
    {
        var myCols = GetComponentsInChildren<Collider>();
        if (myCols == null || myCols.Length == 0) return;
        Transform el = transform.parent; // playerlayer
        if (el == null) return;
        // 遍历 playerlayer 下所有子节点 ≠ 所有敌人；敌人在 enemylayer。这里通过
        // Collider 的位置附近 OverlapSphere 更准，但为了性能直接用固定距离遍历兄弟节点不可行。
        // 更好的做法：直接搜场景中所有 enemy 组件，忽略其 Collider。
        var allEnemies = FindObjectsOfType<enemy>();
        if (allEnemies == null) return;
        foreach (var en in allEnemies)
        {
            if (en == null) continue;
            var eCols = en.GetComponentsInChildren<Collider>();
            if (eCols == null) continue;
            foreach (var mc in eCols)
            {
                if (mc == null) continue;
                foreach (var cc in myCols)
                {
                    if (cc == null) continue;
                    if (ignore) Physics.IgnoreCollision(cc, mc, true);
                    else { /* 冲刺结束不恢复——Ignored 状态已处理碰撞分离，强行恢复反而会重新陷入穿透 */ }
                }
            }
        }
        // 保持 detectCollisions=true，不让地形穿透
    }

    /// <summary>
    /// 鼠标左键点击/长按地面移动：先用 Plane 求交 (Y=0)；若失败则回退到 Physics.Raycast。
    ///   - 按下瞬间 (GetMouseButtonDown)：立即设置目标
    ///   - 长按状态 (GetMouseButton) ：持续更新目标，让玩家可以"按住拖动"实时修正路径
    /// 若点击到 UI 元素上则完全跳过。
    /// </summary>
    private void HandleMouseClickMove()
    {
        // 三选一/奇遇/暂停/总结面板等打开时，Time.timeScale=0，此期间禁用鼠标点击移动
        if (Time.timeScale <= 0f) { _clickTarget = null; HideMarker(); return; }

        bool clickedDown = Input.GetMouseButtonDown(0);
        bool held        = Input.GetMouseButton(0);
        if (!clickedDown && !held) return;

        // 避免点击 HUD 按钮（倍速/暂停/源木等）时触发移动：
        // 仅当鼠标悬停在实际 UI 按钮/面板上才拦截，不拦截空白区域。
        // battleUI 的 Canvas 是全屏的，不能直接用 IsPointerOverGameObject。
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null && IsPointerOverInteractiveUI(es))
            return;

        Camera cam = Camera.main;
        // 兜底：若场景中找不到 MainCamera tag，取第一个活跃摄像机
        if (cam == null)
        {
            var all = Camera.allCameras;
            if (all.Length > 0) cam = all[0];
        }
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Vector3? hitPoint = null;

        // 方式 A：数学平面 (Y=0) 求交 —— 最快且不依赖碰撞体
        if (_groundPlane.Raycast(ray, out float planeDist) && planeDist > 0f)
            hitPoint = ray.GetPoint(planeDist);

        // 方式 B：Physics.Raycast 兜底（打场景碰撞体）
        if (!hitPoint.HasValue &&
            Physics.Raycast(ray, out RaycastHit phit, 500f))
            hitPoint = phit.point;

        if (!hitPoint.HasValue) return;

        Vector3 hit = hitPoint.Value;
        hit.x = Mathf.Clamp(hit.x, -MAP_BOUND, MAP_BOUND);
        hit.z = Mathf.Clamp(hit.z, -MAP_BOUND, MAP_BOUND);
        hit.y = transform.position.y;
        _clickTarget = hit;
        ShowMarkerAt(hit);
    }

    private void ShowMarkerAt(Vector3 worldPos)
    {
        if (_clickMarker == null) return;
        _clickMarker.transform.position = worldPos + Vector3.up * 0.05f; // 略微抬高避免 Z-fight
        _clickMarker.SetActive(true);
    }

    private void HideMarker()
    {
        if (_clickMarker != null) _clickMarker.SetActive(false);
    }

    /// <summary>
    /// 判断鼠标是否悬停在可交互 UI（按钮/面板）上，但不过滤全屏 Canvas 背景。
    /// EventSystem.IsPointerOverGameObject 在全屏 Canvas 下永远返回 true，
    /// 需要用 RaycastAll 精确判断鼠标下方是否有 Button/Selectable 等组件。
    /// </summary>
    private static bool IsPointerOverInteractiveUI(UnityEngine.EventSystems.EventSystem es)
    {
        var ped = new UnityEngine.EventSystems.PointerEventData(es)
        {
            position = Input.mousePosition
        };
        var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
        es.RaycastAll(ped, hits);

        foreach (var hit in hits)
        {
            var go = hit.gameObject;
            if (go == null) continue;
            // 只拦截有实际交互组件的 UI（按钮/开关/滑条/下拉框等）
            if (go.GetComponent<UnityEngine.UI.Button>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Toggle>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Slider>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Dropdown>() != null) return true;
            if (go.GetComponent<UnityEngine.UI.Selectable>() != null) return true;
            if (go.GetComponent<TMPro.TMP_InputField>() != null) return true;
        }
        return false;
    }

    /// <summary>把玩家位置钳制在地图边界内（防卡出地图）。每帧 LateUpdate 也会调一次。</summary>
    private void ClampToMapBounds()
    {
        var p = transform.position;
        bool clamped = false;
        if (p.x < -MAP_BOUND) { p.x = -MAP_BOUND; clamped = true; }
        if (p.x >  MAP_BOUND) { p.x =  MAP_BOUND; clamped = true; }
        if (p.z < -MAP_BOUND) { p.z = -MAP_BOUND; clamped = true; }
        if (p.z >  MAP_BOUND) { p.z =  MAP_BOUND; clamped = true; }
        if (clamped) transform.position = p;
    }

    /// <summary>主玩家的出生点（设置为脱离卡死使用）。</summary>
    private static Vector3 _mainSpawnPosition = Vector3.zero;
    private static bool    _mainSpawnSet = false;
    public  static Vector3 MainSpawnPosition => _mainSpawnPosition;
    public  static bool    HasSpawnPoint => _mainSpawnSet;

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
