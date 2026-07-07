using UnityEngine;

/// <summary>
/// 「亡者领域」小怪死亡回血特效（青绿光电飞向玩家 → 融入玩家 → 回 0.5% 最大生命值）。
///
/// 触发时机：被亡者领域复活的「小怪」（非世界 Boss）死亡时，由 <see cref="MindControlled.OnDestroy"/>
/// 调用 <see cref="Spawn"/>。世界 Boss 不触发——它们设计上"永久存在"，即便被打回去也属于
/// "敌方再次杀回"的语义，不应回血给玩家。
///
/// 数值历史（HealRatio）：
///   • v1：1%（healthmax × 0.01）—— 早期版本测试期数值。
///   • v2（当前）：0.5%（healthmax × 0.005）—— 策划调整，降低被动回血强度，让"亡者领域"的
///     续航更依赖玩家主动控场击杀友军链路而非自动回满。
///   注：至少 +1 的下限保留 —— 即使 healthmax × 0.005 < 0.5 取整为 0，仍能保证每颗光电至少 +1 HP，
///       防止在低血量上限角色（如琪露诺）+ 仅由特殊路径学到亡者领域时"球飞回来一点血都不加"。
///
/// 视觉表现：
///   - 在小怪死亡位置生成一个青绿色发光的"光电球"（用 Resources/Effects/ReviveEnergy_*  序列帧实现），
///     色调染成清亮的青绿（Cyan-Green），与亡者领域紫色友军 overlay 形成色彩对比，玩家一眼能看出
///     "灵魂回流"的视觉语义；
///   - 光电球平滑、快速地朝玩家位置飞行（Ease-In 加速），到达玩家时缩小 + 淡出，象征"融入玩家体内"；
///   - 全程脉动呼吸 + 微旋转，避免视觉单调；
///   - 抵达玩家瞬间给玩家加血（healthmax 的 0.5%，至少 +1，钳到 healthmax）。
///
/// 设计要点：
///   - 玩家位置在飞行过程中持续追踪（不是 spawn 时锁定一次）——这样玩家移动时光电仍然能精确"喂到嘴里"；
///   - 玩家中途死亡或销毁 → 自我销毁，光电消失，不产生悬空特效；
///   - 不依赖 PlayerSkin / 皮肤等任何外部状态，只要存在 Player（任意 tag："Player" 或 "Clone"）就工作；
///   - 兜底：如果序列帧资源缺失，仍能用纯色 Sprite + 衰减 alpha 完成"飞向玩家"的视觉（保证回血逻辑不被阻断）。
/// </summary>
public class TombMinionDeathOrb : MonoBehaviour
{
    // ============================================================
    //  常量：飞行 / 视觉参数
    // ============================================================

    /// <summary>飞行总时长（秒）。玩家在该时间内移动时会持续追踪。</summary>
    private const float FlightDuration = 0.65f;

    /// <summary>光电球世界尺寸（unit），大致与小蘑菇头顶徽记一致。</summary>
    private const float OrbWorldSize = 0.55f;

    /// <summary>序列帧切换间隔（秒）。</summary>
    private const float FrameInterval = 0.06f;

    /// <summary>抵达玩家时（最后 20% 时间）开始缩小 + 淡出。</summary>
    private const float FadeOutStartRatio = 0.80f;

    /// <summary>每次回血 = healthmax × HealRatio（v2 策划调整：1% → 0.5%）。
    /// 配合 TryHealOnce 的 `Mathf.Max(1, …)` 下限，保证就算 healthmax 很小也至少 +1 HP。</summary>
    private const float HealRatio = 0.005f;

    /// <summary>青绿主色（清亮的湖水色 / 灵能青）。</summary>
    private static readonly Color OrbCoreColor = new Color(0.30f, 1.00f, 0.85f, 1f);

    // ============================================================
    //  运行时
    // ============================================================

    private Transform _playerTarget;     // 飞向的玩家 transform（每帧追踪 position）
    private Player    _playerComp;       // 抵达时给它回血
    private Vector3   _startWorldPos;    // 出发坐标
    private float     _t0;               // 出发时间
    private SpriteRenderer _sr;
    private bool _healed;                // 防止重复加血

    // 序列帧资源（共享缓存）
    private static Sprite[] _energyFrames;
    private static bool     _energyFramesProbed;

    private int   _frameIdx;
    private float _frameTimer;

    /// <summary>
    /// 由 MindControlled.OnDestroy 调用：在小怪原位置生成一个回血光电，飞向玩家并回 0.5% HP。
    /// 调用方应仅在"被亡者领域复活的小怪（非世界 Boss）"死亡时调用。
    /// </summary>
    public static void Spawn(Vector3 worldPos, Player target)
    {
        if (target == null || target.health <= 0) return; // 玩家不存在或已死 → 跳过
        GameObject go = new GameObject("__TombMinionDeathOrb");
        go.transform.position = worldPos;
        var orb = go.AddComponent<TombMinionDeathOrb>();
        orb._playerTarget = target.transform;
        orb._playerComp = target;
        orb._startWorldPos = worldPos;
        orb._t0 = Time.time;
    }

    void Awake()
    {
        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.color = OrbCoreColor;
        // 用通用 Sprites/Default shader，确保不挑管线、可参与合批
        _sr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        _sr.sortingOrder = 32766; // 仅次于 MindControlled 头顶徽记（32767）

        EnsureEnergyFrames();
        if (_energyFrames != null && _energyFrames.Length > 0)
        {
            _sr.sprite = _energyFrames[0];
        }

        // 缩放到目标世界尺寸（依据 sprite 实际尺寸推算 localScale）
        ApplyWorldSize(OrbWorldSize);
        // 让特效"朝相机"——本项目其它特效都用 X=45 视角倾斜
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
    }

    void Update()
    {
        // 玩家中途消失 → 自我销毁（不强行回血）
        if (_playerTarget == null || _playerComp == null || _playerComp.health <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // ===== 进度计算 =====
        float elapsed = Time.time - _t0;
        float p = Mathf.Clamp01(elapsed / FlightDuration);
        // Ease-In Cubic：刚出来慢、越接近玩家越快——视觉上像"被玩家吸入"
        float eased = p * p * p;

        // ===== 位置插值（追踪玩家当前位置）=====
        Vector3 cur = Vector3.LerpUnclamped(_startWorldPos, _playerTarget.position, eased);
        // 微旋绕：让光电球路径不是死板的直线，加一个 0.4 unit 振幅的横向正弦扰动（在飞行中段最明显）
        float wobble = Mathf.Sin(elapsed * 18f) * 0.4f * (1f - p); // 接近终点振幅归零，确保精确命中玩家
        Vector3 perp = Vector3.Cross(_playerTarget.position - _startWorldPos, Vector3.up).normalized;
        cur += perp * wobble;
        transform.position = cur;

        // ===== 序列帧 =====
        AdvanceEnergyFrame();

        // ===== 收尾：缩小 + 淡出 =====
        float fadeT = (p - FadeOutStartRatio) / (1f - FadeOutStartRatio);
        if (fadeT > 0f)
        {
            fadeT = Mathf.Clamp01(fadeT);
            // 0.55→0.15 缩小，模拟"被吸入"
            ApplyWorldSize(Mathf.Lerp(OrbWorldSize, OrbWorldSize * 0.27f, fadeT));
            Color c = OrbCoreColor;
            c.a = Mathf.Lerp(1f, 0f, fadeT);
            _sr.color = c;
        }
        else
        {
            // 飞行中：颜色保持，alpha 用呼吸做出"灵能脉动"
            float pulse = 0.85f + 0.15f * Mathf.Sin(elapsed * 10f);
            Color c = OrbCoreColor;
            c.a = pulse;
            _sr.color = c;
        }

        // ===== 抵达 → 加血 + 销毁 =====
        if (p >= 1f)
        {
            TryHealOnce();
            Destroy(gameObject);
        }
    }

    /// <summary>真正给玩家加血（healthmax × HealRatio，至少 +1，钳到 healthmax）。仅执行一次。
    /// 当前 HealRatio = 0.005（0.5%）。落地瞬间同步播放"亡者回血.wav"的清亮上行琶音，
    /// 给玩家明确的"回血到账"听觉反馈（音效由 _tools/gen_audio_sfx.py 程序化合成）。</summary>
    private void TryHealOnce()
    {
        if (_healed) return;
        _healed = true;
        if (_playerComp == null || _playerComp.health <= 0) return;
        int heal = Mathf.Max(1, Mathf.RoundToInt(_playerComp.healthmax * HealRatio));
        _playerComp.health = Mathf.Min(_playerComp.healthmax, _playerComp.health + heal);
        // 回血音效已移除（TombHeal 过于突兀）
    }

    private void AdvanceEnergyFrame()
    {
        if (_energyFrames == null || _energyFrames.Length == 0) return;
        _frameTimer += Time.deltaTime;
        if (_frameTimer >= FrameInterval)
        {
            _frameTimer -= FrameInterval;
            _frameIdx = (_frameIdx + 1) % _energyFrames.Length;
            _sr.sprite = _energyFrames[_frameIdx];
        }
    }

    /// <summary>按 sprite 实际几何尺寸推算 localScale，让世界 size = `worldSize` unit。</summary>
    private void ApplyWorldSize(float worldSize)
    {
        if (_sr == null || _sr.sprite == null)
        {
            transform.localScale = new Vector3(worldSize, worldSize, worldSize);
            return;
        }
        // sprite.bounds.size 是无 transform.scale 时的世界尺寸；除以它得到使其等于 worldSize 的缩放
        Vector2 sp = _sr.sprite.bounds.size;
        float bs = Mathf.Max(sp.x, sp.y);
        if (bs < 1e-4f) bs = 1f;
        float s = worldSize / bs;
        transform.localScale = new Vector3(s, s, s);
    }

    private static void EnsureEnergyFrames()
    {
        if (_energyFramesProbed) return;
        _energyFramesProbed = true;
        // ReviveEnergy_0 ~ ReviveEnergy_11，共 12 帧（与 ReviveBossEffect 使用同套素材）
        var list = new System.Collections.Generic.List<Sprite>(12);
        for (int i = 0; i < 12; i++)
        {
            Sprite s = Resources.Load<Sprite>($"Effects/ReviveEnergy_{i}");
            if (s != null) list.Add(s);
        }
        _energyFrames = list.Count > 0 ? list.ToArray() : null;
        if (_energyFrames == null)
        {
            Debug.LogWarning("[亡者领域·回血光电] Resources/Effects/ReviveEnergy_* 序列帧未找到，光电将使用纯色占位。");
        }
    }
}
