using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
public class enemy : Attribute
{
    public static float adventureHpMultiplier = 1.0f;
    public static float adventureAtkMultiplier = 1.0f;

    public GameObject atknumber;
    public state rolestate;
    public GameObject role;//目标角色（玩家）
    protected Transform playerlayer;//玩家层（子类可直接访问，无需反射）
    private Animator ani;
    public float Sca;//角色缩放大小，用于控制转向
    public Material material;
    public Material red;
    public GameObject expstone;

    // 全场景共享的玩家层缓存。每只怪物 OnEnable 都做 GameObject.Find 太贵（蝙蝠潮一秒几十次）。
    // 场景重载时 Unity 会把所有 static 引用置 null（domain reload）；如果没启用 reload，这里
    // 仍能在第一次 OnEnable 时通过 != null 检查重建。
    private static Transform _cachedPlayerLayer;
    public static void ResetSceneCaches()
    {
        _cachedPlayerLayer = null;
        adventureHpMultiplier = 1.0f;
        adventureAtkMultiplier = 1.0f;
    }

    private const string KEY_SPORE_MUTATION_ENABLED = "SporeMutationEnabled";
    private static readonly Color[] SporeMutationColors =
    {
        new Color(1f, 0f, 0f, 1f),       // 大红
        new Color(0.65f, 0f, 1f, 1f),    // 大紫
        new Color(0f, 0.25f, 1f, 1f),    // 大蓝
        new Color(0f, 0.9f, 0.1f, 1f),   // 亮绿
        new Color(1f, 0.85f, 0f, 1f),    // 金黄
        new Color(1f, 0.25f, 0.85f, 1f), // 品红
        new Color(0f, 0.95f, 1f, 1f),    // 青蓝
        new Color(1f, 0.45f, 0f, 1f),    // 橙色
    };

    private bool _sporeMutationRollDecided;
    private bool _sporeMutationUseColor;
    private bool _sporeMutationColorApplied;
    private Color _sporeMutationColor;
    private SpriteRenderer _sporeMutationBaseRenderer;
    private SpriteRenderer _sporeMutationOverlayRenderer;

    // ========== 性能：每帧 hot path 缓存 ==========
    // 每只敌人 FixedUpdate 50Hz × N 只 → 任何 GetComponent / 字符串 Contains 都会被放大成大头。
    // 这两个字段在 OnEnable 时一次性算好，FixedUpdate 直接读。
    private bool _isMushroomEnemyCached;
    private SpriteRenderer _cachedSpriteRenderer; // 给 startturnred 用，省两次 GetComponent
    // 由 MindControlled.Setup 设置；避免 enemy.FixedUpdate 每帧都 GetComponent<MindControlled>()
    [System.NonSerialized] public bool _mindControlledFlag;
    // startturnred 节流：用 timer 替代连续 StartCoroutine。同一只怪在 0.3s 内
    // 被多次命中只保持一份 turn-red，避免群战时协程数量爆炸。
    private float _turnRedTimer;
    private bool  _turnRedActive;
    public enum state
    {
        idle,
        move,
        dead,
    }
    void OnEnable()
    {
        _sporeMutationRollDecided = false;
        _sporeMutationUseColor = false;
        _sporeMutationColorApplied = false;

        // 性能：缓存 playerlayer Transform，避免高频生成怪时每个 OnEnable 都做 GameObject.Find（全场景搜）。
        // 蝙蝠潮 / 蘑菇潮 阶段每秒会有几十个怪 OnEnable，Find 是这一阶段卡顿的隐形大头。
        if (_cachedPlayerLayer == null)
        {
            GameObject pl = GameObject.Find("playerlayer");
            if (pl != null) _cachedPlayerLayer = pl.transform;
        }
        playerlayer = _cachedPlayerLayer;
        ani = GetComponent<Animator>();
        _cachedSpriteRenderer = GetComponent<SpriteRenderer>();
        _isMushroomEnemyCached = ComputeIsMushroomEnemy();
        _mindControlledFlag = false;
        _reviveAttempted = false; // 对象池/重生时重置：每次"活过来"都允许投一次复活骰
        _turnRedTimer = 0f;
        _turnRedActive = false;

        // 根据难度缩放基础属性
        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier * adventureHpMultiplier);
            health    = healthmax;
            atk       = Mathf.RoundToInt(atk * cfg.atkMultiplier * adventureAtkMultiplier);
        }

        ApplySporeMutationColor();
    }

    private bool ComputeIsMushroomEnemy()
    {
        // 只在 OnEnable 算一次，避免 FixedUpdate 每帧 4× string.Contains。
        return gameObject.name.Contains("Shoom") ||
               gameObject.name.Contains("Mushroom") ||
               rolename == "蘑菇人" ||
               rolename == "Shoom";
    }

    private bool IsMushroomEnemy() => _isMushroomEnemyCached;

    private void ApplySporeMutationColor()
    {
        if (!IsMushroomEnemy()) return;

        bool unlocked = EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 6);
        bool enabled = PlayerPrefs.GetInt(KEY_SPORE_MUTATION_ENABLED, 1) == 1;
        if (!unlocked || !enabled)
        {
            ClearSporeMutationColor();
            return;
        }

        if (_sporeMutationBaseRenderer == null)
            _sporeMutationBaseRenderer = GetComponent<SpriteRenderer>();
        if (_sporeMutationBaseRenderer == null) return;

        if (!_sporeMutationRollDecided)
        {
            _sporeMutationRollDecided = true;
            _sporeMutationUseColor = UnityEngine.Random.value < 0.5f;
        }
        if (!_sporeMutationUseColor)
        {
            ClearSporeMutationColor();
            return;
        }

        if (!_sporeMutationColorApplied)
        {
            _sporeMutationColor = SporeMutationColors[UnityEngine.Random.Range(0, SporeMutationColors.Length)];
            _sporeMutationColorApplied = true;
        }

        if (_sporeMutationOverlayRenderer == null)
        {
            Transform old = transform.Find("__SporeMutationRenderer");
            GameObject go = old != null ? old.gameObject : new GameObject("__SporeMutationRenderer");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _sporeMutationOverlayRenderer = go.GetComponent<SpriteRenderer>();
            if (_sporeMutationOverlayRenderer == null)
                _sporeMutationOverlayRenderer = go.AddComponent<SpriteRenderer>();
            _sporeMutationOverlayRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        _sporeMutationOverlayRenderer.sprite = _sporeMutationBaseRenderer.sprite;
        _sporeMutationOverlayRenderer.flipX = _sporeMutationBaseRenderer.flipX;
        _sporeMutationOverlayRenderer.flipY = _sporeMutationBaseRenderer.flipY;
        _sporeMutationOverlayRenderer.drawMode = _sporeMutationBaseRenderer.drawMode;
        _sporeMutationOverlayRenderer.size = _sporeMutationBaseRenderer.size;
        _sporeMutationOverlayRenderer.sortingLayerID = _sporeMutationBaseRenderer.sortingLayerID;
        _sporeMutationOverlayRenderer.sortingOrder = _sporeMutationBaseRenderer.sortingOrder + 1;
        _sporeMutationOverlayRenderer.color = _sporeMutationColor;
        _sporeMutationOverlayRenderer.enabled = true;

        // 原蘑菇材质可能不吃 SpriteRenderer.color，直接隐藏原图，显示彩色覆盖层。
        _sporeMutationBaseRenderer.enabled = false;
    }

    /// <summary>
    /// 关掉七彩蘑菇变异覆盖层，恢复原 SpriteRenderer。
    /// 公开给：
    ///   1) Destroy1 死亡前调用——避免"base SpriteRenderer 被禁用导致死亡动画看不见"的 bug；
    ///   2) MindControlled.Setup 复活为友军前调用——避免七彩 overlay 遮挡紫色友军 overlay。
    /// （之前是 private，导致彩色蘑菇死亡 / 被亡者领域复活时视觉异常。）
    /// </summary>
    public void ClearSporeMutationColor()
    {
        if (_sporeMutationBaseRenderer == null)
            _sporeMutationBaseRenderer = GetComponent<SpriteRenderer>();
        if (_sporeMutationBaseRenderer != null)
        {
            _sporeMutationBaseRenderer.enabled = true;
            _sporeMutationBaseRenderer.color = Color.white;
        }
        if (_sporeMutationOverlayRenderer != null)
            _sporeMutationOverlayRenderer.enabled = false;
    }

    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (rolestate != state.dead)
        {
            // 亡者领域：被控制为友军后，禁止对玩家造成碰撞伤害（避免友军互相伤害）
            if (_mindControlledFlag) return;

            if (collision.gameObject.CompareTag("Player"))
            {
                Player Player = collision.gameObject.GetComponent<Player>();
                if (Player.health > 0)
                {
                    // SSR「虚空斗篷」：冲刺期间无敌
                    if (Player.IsDashInvincibleActive) return;

                    // 玩家闪避判定
                    float evaRoll = UnityEngine.Random.value * 100;
                    if (Player.EVA > evaRoll) return;

                    // 按玩家防御减伤，至少 1 点
                    int dmg = Mathf.Max(1, (int)(atk - Player.def));
                    Player.health -= dmg;
                    if (DamageNumberSettings.Visible)
                    {
                        GameObject number = Instantiate(atknumber, collision.transform.position, default);
                        number.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = dmg.ToString();
                    }
                    AudioManager.PlaySfx(AudioManager.SfxKey.Hit);
                    collision.gameObject.GetComponent<Player>().startturnred();
                    if (Player.health <= 0)
                    {
                        Player.death();
                    }
                }
            }
        }
    }
    public void getrole()
    {
        float shortestdis = 999999;
        Transform shortestrole = null;
        if(playerlayer.childCount>0)
        {
            foreach(Transform item in playerlayer)
            {
                Vector3 i = item.position;
                float distance =Vector3.Distance(i,transform.position);
                if(distance < shortestdis)
                {
                    shortestdis = distance;
                    shortestrole = item;
                }
            }
            role = shortestrole.gameObject;
        }

        // 亡者领域：若身边存在「被控制的世界 Boss」，优先攻击它，不打玩家
        // （只对未被控制的敌人生效，被控制小怪自己用 MindControlled 寻敌）
        if (!_mindControlledFlag)
        {
            Transform priority = MindControlled.FindHighPriorityTargetForEnemy(transform.position);
            if (priority != null)
            {
                float dist = Vector3.Distance(priority.position, transform.position);
                // 仅在被控制 Boss 离我比玩家还近、或玩家不存在时切目标，避免被狗尾草拉到天涯海角
                if (role == null || dist < shortestdis * 1.2f)
                {
                    role = priority.gameObject;
                }
            }
        }
    }

    protected virtual void FixedUpdate()
    {
        // 性能：孢子异变颜色只在 OnEnable 时决定一次（出生即固定）；之前每帧都跑是历史遗留，
        // 是 N 只敌人 × 50Hz 的最大开销之一。FixedUpdate 不再调用。

        // 亡者领域：被控制为友军后，移动/朝向/状态切换全部交由 MindControlled 接管，
        // 跳过父类的"追玩家"逻辑（否则会和 MindControlled 同帧反向位移导致颤抖/互顶）。
        // 这里读已缓存 flag，避免每帧 GetComponent<MindControlled>()。
        if (_mindControlledFlag) return;

        if(role != null)
        {
            float chazhi = role.transform.position.x-transform.position.x;
            if(chazhi > 0)
            {
                transform.localScale = new Vector3(Sca, Sca, Sca);
            }
            else
            {
                transform.localScale = new Vector3(-1*Sca, Sca, Sca);
            }
        }

        switch(rolestate)
        {
            case state.idle:
                ani.SetBool("ismove", false);

                if (role == null)
                {
                    getrole();
                }
                else
                {
                    rolestate = state.move;
                }


                break;
            case state.move:
                ani.SetBool("ismove", true);
                if (role == null)
                {
                    rolestate = state.idle;
                }
                else
                {
                    //1.获取目标坐标和自己坐标
                    //2.设置移动向量，并赋值给刚体
                    Vector3 postion1=role.transform.position;//目标坐标
                    Vector3 postion2=transform.position;//自己坐标
                    Vector3 distance =postion1 - postion2;
                    Vector3 vect =new Vector3(distance.x, 0, distance.z).normalized*speed;
                    transform.position += vect * Time.fixedDeltaTime;
                }


                break;
            case state.dead:

                break;
        }
    }
    public void startturnred()
    {
        // 节流：群战时一只怪可能在同一帧被多发子弹命中，原版每发都 StartCoroutine + 写 material，
        // 协程数量 + GC 都会爆。改用一个 timer：第一发触发协程 turn-red，后续命中只 reset timer。
        if (_cachedSpriteRenderer == null) _cachedSpriteRenderer = GetComponent<SpriteRenderer>();
        _turnRedTimer = 0.3f;
        if (!_turnRedActive)
        {
            _turnRedActive = true;
            StartCoroutine(turnred());
        }
    }

    public IEnumerator turnred()
    {
        if (_cachedSpriteRenderer != null) _cachedSpriteRenderer.material = red;
        // 用 timer 等待，期间任何新命中都会刷新 _turnRedTimer，达到"持续变红"
        while (_turnRedTimer > 0f)
        {
            _turnRedTimer -= Time.deltaTime;
            yield return null;
        }
        if (_cachedSpriteRenderer != null) _cachedSpriteRenderer.material = material;
        _turnRedActive = false;
    }

    // 防"同一只敌人的同一次死亡里，hook 被多个 Destroy1 重写串联调用 → 多次投骰"。
    // 例如：WorldBossMushroomMan.Destroy1 → 调 hook → 失败 → base.Destroy1（BossMushroomMan）
    //   → if (rolestate==dead) return 没拦住（rolestate 还没置 dead）→ 又调一次 hook。
    // 用一个一次性 flag 锁定：本次死亡只允许 hook 投一次。
    [System.NonSerialized] public bool _reviveAttempted;

    public virtual void Destroy1()
    {
        if (rolestate != state.dead)
        {
            // 亡者领域：被孢子领域伤害过的敌人，无论最终被哪种技能击杀，
            // 都在此统一汇聚点尝试复活为友军（成功则跳过整个死亡流程）。
            // 注意：WorldBossBase/Bat/MushroomMan 重写了 Destroy1 不调 base，世界 Boss 的拦截见 WorldBossBase.Destroy1。
            // _reviveAttempted 防重入，确保 base/子类重写串联调用时不会重复投骰。
            if (!_reviveAttempted)
            {
                _reviveAttempted = true;
                if (TombDomainHook.TryReviveAsAlly(this))
                {
                    Debug.Log($"[亡者领域] {gameObject.name} 被复活为友军（普通敌人）");
                    return;
                }
            }

            rolestate =state.dead;
            // 关键修复：彩色蘑菇（孢子异变）把 base SpriteRenderer.enabled 置 false 来显示彩色覆盖层，
            //   而死亡动画切换的是 base SpriteRenderer 的 sprite——base 被禁，死亡动画就"看不见"。
            //   死亡前先恢复 base、隐藏覆盖层，让 SetTrigger("dead") 切出来的死亡帧能正常显示。
            ClearSporeMutationColor();
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));
            if (ani != null) ani.SetTrigger("dead");

            // 统计击败的蘑菇敌人
            if (IsMushroomEnemy())
            {
                int mushroomCount = PlayerPrefs.GetInt("MushroomDefeatedCount", 0) + 1;
                PlayerPrefs.SetInt("MushroomDefeatedCount", mushroomCount);
                PlayerPrefs.Save();

                if (mushroomCount >= 500 && EquipmentSystem.Instance != null && !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 6))
                {
                    EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 6);
                    ToastManager.Show("成就装备6「孢子异变」已解锁！现在会出现五颜六色的蘑菇人！");
                }
            }

            StartCoroutine(Destroy2());
        }
        
    }
    public IEnumerator Destroy2()
    {
        yield return new WaitForSeconds(2f);
        Destroy(gameObject);
    }
}
