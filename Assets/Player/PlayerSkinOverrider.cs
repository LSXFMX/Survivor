using System.IO;
using UnityEngine;

/// <summary>
/// 主角换装系统（运行时通过 AnimatorOverrideController 替换 AnimationClip）
/// 关键思路：
///  1. 局内角色由 Animator 控制器 (Idel/move/dead) 驱动，每帧 keyframe 都会写 SpriteRenderer.sprite。
///  2. 单纯在 LateUpdate 覆盖 sprite 经常被下一帧的 keyframe 抢回去 → 不可靠。
///  3. 因此当切换皮肤时，我们运行时构造 *新的* AnimationClip（含新皮肤的 sprite keyframes），
///     再用 AnimatorOverrideController 把原 controller 中的 Idel/move/dead clip 整体替换。
///  4. 这样 Animator 的状态机、参数、过渡条件全部沿用，仅 *动画帧序列* 被偷换 → 局内换装真正生效。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSkinOverrider : MonoBehaviour
{
    [Header("外观选择")]
    [Tooltip("0: 默认琪露诺, 1: 南筱风(UR_0 风之形), 2: 夏无(UR_1 地狱火), 3: 无罪(UR_2 亡者领域)")]
    [Range(0, 3)]
    public int skinIndex = 0;

    [Header("动画时长（秒）")]
    public float idleClipLength = 0.5f;   // 4 帧 idle 总时长（默认）
    public float moveClipLength = 0.6f;   // 4 帧 move 总时长（一组完整走路循环）

    [Header("UR 无罪（skinId=3, 亡者领域皮肤）特殊参数")]
    [Tooltip("无罪 4 帧 idle 总时长（秒）。亡灵气质偏慢，建议 0.8~1.2 秒（≈ 3.3~5fps）。")]
    public float tombIdleClipLength = 1.0f;
    [Tooltip("无罪 4 帧 walk 总时长（秒）。取走路第 3、4、6、7 帧，左右脚各 2 帧；建议 0.6~0.8 秒（≈ 5~6.7fps）。")]
    public float tombMoveClipLength = 0.7f;

    [Header("UR 南筱风（skinId=1, 风之形皮肤）特殊参数")]
    [Tooltip("南筱风待机动画总时长（秒）。设大数让动画显著放慢。")]
    public float windIdleClipLength = 1.6f;
    [Tooltip("南筱风待机是否删掉第 0 帧（正面图）。")]
    public bool windIdleDropFirstFrame = true;
    [Tooltip("南筱风走路总时长（秒）。7 帧建议 0.7~0.85；4 帧建议 0.45~0.55；8 帧建议 0.8~1.0。")]
    public float windMoveClipLength = 0.85f;
    [Tooltip("南筱风走路播放方案：\n  0 = 7 帧顺序（默认，按图集自然顺序）：取走路 8 帧的前 7 帧（去掉最后一帧）——\n      [中行 4 帧 + 底行 3 帧]，对应 PNG 中视觉上 idle 之后的前 7 张。\n  1 = Row1 钟摆（4 帧）：仅用中行的 [0,1,2,1]，走路幅度大但只有 4 帧节奏快。\n  2 = Row0 钟摆（4 帧）：仅用底行的 [0,1,2,1]，幅度小看着稳。\n  3 = 8 帧交错钟摆（旧默认）：从两行 4 帧里挑左右脚交替的 8 帧组成钟摆型步态，观感对称但末尾有顿挫。")]
    [Range(0, 3)]
    public int windWalkSequenceMode = 0;

    [Header("UR 皮肤缩放参数（按高度对齐，避免陷地/浮空）")]
    [Tooltip("琪露诺单帧约 79x58 像素，世界高度 0.58 单位。UR 每帧约 256x341，按高度对齐后世界尺寸约 0.43x0.58。")]
    public float urReferenceFrameHeightUnits = 0.58f;
    [Tooltip("UR sprite 缩放系数（在自动计算 PPU 后再放大/缩小）。1=与琪露诺等高；0.9=略小一点。")]
    [Range(0.3f, 2f)]
    public float urScale = 1.0f;
    [Tooltip("UR sprite 创建时使用的 PixelsPerUnit（手动覆盖）。设为 0 则按 cellH/urReferenceFrameHeightUnits 自动计算。")]
    public float urPixelsPerUnit = 0f;
    [Tooltip("启用自动 Pivot：扫描 idle 列 1（标准站立帧）的最低不透明像素行，把 pivot Y 对齐到该位置（=脚位置），从根本上消除浮空/陷地。建议保持开。")]
    public bool urAutoPivotByFootScan = true;
    [Tooltip("脚位置微调（世界单位）。正值=角色整体上移（更浮）、负值=下移（更陷）。仅在自动 Pivot 模式下使用。")]
    [Range(-0.3f, 0.3f)]
    public float urFootOffsetUnits = 0f;
    [Tooltip("手动 Pivot Y（仅当『自动 Pivot』关闭时生效）。0=帧底, 1=帧顶。脚像素若在 cell 底部 ~25/341 处，应填 ~0.075。")]
    [Range(0f, 1f)]
    public float urPivotY = 0.08f;

    [Tooltip("每帧独立扫描角色重心 X / 脚 Y，让每帧 sprite 各自对齐自己的角色中心，消除帧间左右抖动。\n关闭后所有帧共用 idle 列 1 的 pivot（旧行为），可能在不同帧角色横向位置不同时左右抖。")]
    public bool urAutoPivotPerFrame = true;
    [Tooltip("帧重心 X 扫描时，只用脚部窄带（cell 底 0~脚高度+扫描带）来求 X 中位数，避免风元素飘散像素拉偏重心。建议保持开。")]
    public bool urCenterXFromFootBand = true;
    [Tooltip("脚部窄带高度（占 cell 高的比例）。从扫到的脚像素行往上扩展这么高的范围，在该范围内求 X 中位数。0.15 = 脚以上约 15% cell 高。")]
    [Range(0.05f, 0.5f)]
    public float urCenterXBandRatio = 0.18f;

    [Header("白边/白雾剔除")]
    [Tooltip("是否把贴图当作线性数据上传（实验性）。一般保持 false：在 Linear 色彩空间项目中按 sRGB 上传才会得到正确视觉颜色，false 时角色不会泛白。")]
    public bool urTreatTextureAsLinear = false;
    [Tooltip("启用色键剔除：把 RGB 总和高于阈值的近白色像素 alpha 强制清零，去除轮廓白边。")]
    public bool urCullWhiteEdges = true;
    [Tooltip("RGB 总和阈值（0-1，每通道平均）。超过此值的像素会被清成完全透明。0.85 ≈ 217/255 三通道平均，可较彻底地去描边。")]
    [Range(0.5f, 1f)]
    public float urWhiteCullThreshold = 0.85f;
    [Tooltip("Alpha 二值化阈值。低于此 alpha 的像素直接置 0，避免半透明边缘和不该有的白雾。")]
    [Range(0f, 1f)]
    public float urAlphaCutoff = 0.5f;

    [Header("身体中心校准（关键：让 UR 皮肤的脚底/中心和琪露诺对齐）")]
    [Tooltip("【强烈推荐保持开启】完全模仿琪露诺 sprite 的 pivot 规则——把所有 UR 帧的 pivot 固定为 (0.5, 0.5)，\n" +
             "即 cell 的几何中心。琪露诺的 sprite meta 设的就是 (0.5, 0.5)，所以：\n" +
             "  • transform.position = sprite cell 几何中心\n" +
             "  • 视觉脚像素出现在 transform.position 下方约 0.29 单位 (= cellH/(2*ppu))\n" +
             "  • UR 帧高世界 = 0.58 单位（按 urReferenceFrameHeightUnits 对齐）→ 与琪露诺脚到 transform 的距离完全一致\n" +
             "  • 环绕物绕 transform → 自动落到 sprite 几何中心（接近身体中段）\n" +
             "  • 不再扫像素 → 帧间 pivot 完全一致 → 不再左右/上下抖\n" +
             "若 PNG 里的角色画得明显偏离 cell 中心（比如脚画在 cell 底部很靠下、头距 cell 顶部留白多），\n" +
             "可用下面两个 Fine 字段微调。\n关闭后退回旧的\"扫像素算脚/身体中心\"行为（已不推荐）。")]
    public bool urMimicCirnoPivot = true;
    [Tooltip("pivot.x 微调（叠加在 0.5 之上）。正值=角色整体右移（视觉看上去左偏修正）。0=不调整。一般保持 0。")]
    [Range(-0.3f, 0.3f)]
    public float urPivotXFine = 0f;
    [Tooltip("pivot.y 微调（叠加在 0.5 之上）。正值=pivot 上移→角色整体下沉（脚更踩地）；负值=pivot 下移→角色上浮。\n  若 UR 角色看上去比琪露诺浮，试 +0.05~+0.10；陷地则负方向调。一般保持 0。")]
    [Range(-0.3f, 0.3f)]
    public float urPivotYFine = 0f;

    [Header("（旧）身体中心校准 - 仅在 urMimicCirnoPivot 关闭时生效")]
    [Tooltip("启用后，pivot Y 不再设到\"脚像素位置\"，而是设到\"脚 + 头之间的身体几何中心\"。\n仅当 urMimicCirnoPivot=false 时才参与计算。")]
    public bool urCenterPivotOnBody = true;
    [Tooltip("身体中心 Y 微调（归一化, -0.5~0.5）。仅当 urMimicCirnoPivot=false 时生效。")]
    [Range(-0.5f, 0.5f)]
    public float urBodyCenterPivotOffset = 0f;

    private SpriteRenderer _renderer;
    private Animator _animator;

    private RuntimeAnimatorController _originalController;
    private AnimatorOverrideController _overrideController;

    private int _appliedSkinIndex = -1;

    private const string Ur0Path = "像素幸存者资源包/玩家/ur0_wind_skin.png";
    private const string Ur1Path = "像素幸存者资源包/玩家/ur1_fire_skin.png";
    private const string Ur2Path = "像素幸存者资源包/玩家/ur2_tomb_skin.png";

    void Awake()
    {
        CacheRefs();
        if (Application.isPlaying)
        {
            int saved = PlayerPrefs.GetInt("SelectedSkin", 0);
            skinIndex = Mathf.Clamp(saved, 0, 3);
            ApplySkinChoice();
        }
    }

    void OnEnable()
    {
        CacheRefs();
        if (Application.isPlaying)
        {
            int saved = PlayerPrefs.GetInt("SelectedSkin", 0);
            skinIndex = Mathf.Clamp(saved, 0, 3);
        }
        _appliedSkinIndex = -1;
        ApplySkinChoice();
    }

    void Update()
    {
        if (!Application.isPlaying)
        {
            // 编辑器内 Inspector 实时预览（仅刷新 sprite，不构造 clip）
            if (skinIndex != _appliedSkinIndex)
            {
                ApplySkinChoice();
            }
            UpdateEditorPreview();
            return;
        }

        // 运行时实时检测玩家在橱窗里切换皮肤
        int saved = PlayerPrefs.GetInt("SelectedSkin", 0);
        if (saved != _appliedSkinIndex)
        {
            skinIndex = Mathf.Clamp(saved, 0, 3);
            ApplySkinChoice();
        }
    }

#if !UNITY_EDITOR
    // 运行时 Build 中 AnimationUtility 不可用，新 clip 没有 keyframe，
    // 此时通过 LateUpdate 强制覆盖 sprite 兜底。
    private float _runtimeAnimTimer = 0f;
    private Sprite[] _runtimeIdleSprites;
    private Sprite[] _runtimeMoveSprites;
#endif

    // 持有一份共享的 Sprites/Default 材质（只创建一次，所有皮肤共用，避免每次重切产生材质泄漏）
    private static Material _sharedSpritesDefault;
    private static Material GetSharedSpritesDefault()
    {
        if (_sharedSpritesDefault == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) _sharedSpritesDefault = new Material(sh) { name = "RuntimeSpritesDefault" };
        }
        return _sharedSpritesDefault;
    }

    // 守护 SpriteRenderer 的渲染状态，避免 Player.turnred() 把材质切到 URP Lit 后导致光照变化（"一会儿黑一会儿白"）
    // 同时把"红色受击反馈"无损迁移为 color tint，保留视觉反馈。
    private bool _wasRedMaterial;
    private float _redTintTimer;
    private void GuardMaterialAndColor()
    {
        if (_renderer == null || skinIndex == 0) return;

        var spriteDefault = GetSharedSpritesDefault();
        if (spriteDefault == null) return;

        var curMat = _renderer.sharedMaterial;
        bool isSpriteDefault = curMat != null && curMat.shader != null && curMat.shader.name == "Sprites/Default";

        if (!isSpriteDefault)
        {
            // 非 Sprites/Default（Player.turnred 切到 red 或 prefab 用了 URP Lit 材质都会走这里）
            // 先识别是不是受击红光（材质名含 "red" 或材质本身就是 Lit 红）→ 用 color 模拟
            string n = curMat != null && curMat.name != null ? curMat.name.ToLower() : "";
            if (n.Contains("red"))
            {
                _wasRedMaterial = true;
                _redTintTimer = 0.3f;
            }
            // 一律恢复成共享的 Sprites/Default（unlit），消除"一会儿黑一会儿白"
            _renderer.sharedMaterial = spriteDefault;
        }

        // 受击红色 tint（取代之前 red 材质的视觉反馈）
        if (_redTintTimer > 0f)
        {
            _redTintTimer -= Time.deltaTime;
            _renderer.color = new Color(1f, 0.35f, 0.35f, 1f);
            if (_redTintTimer <= 0f)
            {
                _wasRedMaterial = false;
                _renderer.color = Color.white;
            }
        }
        else if (_renderer.color != Color.white && !_wasRedMaterial)
        {
            _renderer.color = Color.white;
        }
    }

#if !UNITY_EDITOR
    void LateUpdate()
    {
        if (!Application.isPlaying || skinIndex == 0) return;
        if (_renderer == null) return;

        // 1) 持续守护材质 + 颜色（首要：消除黑影/白光的根因）
        GuardMaterialAndColor();

        if (_runtimeIdleSprites == null) return;

        bool isMoving = _animator != null && _animator.GetBool("ismove");
        Sprite[] frames = isMoving ? _runtimeMoveSprites : _runtimeIdleSprites;
        if (frames == null || frames.Length == 0) return;
        _runtimeAnimTimer += Time.deltaTime;
        // idle fps：风之形 = 帧数/windIdleClipLength；无罪 = 帧数/tombIdleClipLength；其它 = 6fps（旧默认）
        float idleFps;
        if (skinIndex == 1)
            idleFps = frames.Length / Mathf.Max(0.01f, windIdleClipLength);
        else if (skinIndex == 3)
            idleFps = frames.Length / Mathf.Max(0.01f, tombIdleClipLength);
        else
            idleFps = 6f;
        // move：南筱风用 windMoveClipLength；无罪用 tombMoveClipLength；其它用 moveClipLength（彼此独立）
        float thisMoveLen = (skinIndex == 1) ? windMoveClipLength
                          : (skinIndex == 3) ? tombMoveClipLength
                          : moveClipLength;
        float moveFps = frames.Length / Mathf.Max(0.05f, thisMoveLen);
        float fps = isMoving ? moveFps : idleFps;
        int idx = (int)(_runtimeAnimTimer * fps) % frames.Length;
        if (frames[idx] != null) _renderer.sprite = frames[idx];
    }
#else
    // Editor 下的 PlayMode 也要守护材质，否则受击/特效切材质后画面闪烁
    void LateUpdate()
    {
        if (!Application.isPlaying || skinIndex == 0) return;
        if (_renderer == null) return;
        GuardMaterialAndColor();
    }
#endif

    private void CacheRefs()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_animator == null) _animator = GetComponentInParent<Animator>();
        if (_animator != null && _originalController == null)
        {
            // 仅记录原始 controller（首次进入运行时）
            _originalController = _animator.runtimeAnimatorController is AnimatorOverrideController aoc
                ? aoc.runtimeAnimatorController
                : _animator.runtimeAnimatorController;
        }
    }

    private void ApplySkinChoice()
    {
        CacheRefs();
        _appliedSkinIndex = skinIndex;

        // 默认皮肤（琪露诺）：恢复原始 controller，让原动画照常播放
        if (skinIndex == 0)
        {
            if (_animator != null && _originalController != null)
            {
                _animator.runtimeAnimatorController = _originalController;
            }
            return;
        }

        if (_animator == null || _originalController == null)
        {
            Debug.LogWarning("[换装] Animator 或原 controller 未就绪，无法替换 clip。");
            return;
        }

        // 加载并切片皮肤精灵
        Sprite[] idleSprites, moveSprites;
        if (!BuildSkinSprites(skinIndex, out idleSprites, out moveSprites))
        {
            Debug.LogWarning("[换装] 无法加载皮肤贴图，回退到默认皮肤。");
            return;
        }

        // 构造新的 AnimationClip
        // idle 时长：南筱风用 windIdleClipLength；无罪用 tombIdleClipLength；其他用 idleClipLength
        float thisIdleLen = (skinIndex == 1) ? windIdleClipLength
                          : (skinIndex == 3) ? tombIdleClipLength
                          : idleClipLength;
        // move 时长：南筱风用 windMoveClipLength；无罪用 tombMoveClipLength；其他用 moveClipLength
        // 三者彼此独立 → 调任意一个皮肤的速度都不会影响其它皮肤。
        float thisMoveLen = (skinIndex == 1) ? windMoveClipLength
                          : (skinIndex == 3) ? tombMoveClipLength
                          : moveClipLength;
        AnimationClip idleClip = BuildSpriteClip("Idel", idleSprites, thisIdleLen, true);
        // 无罪（skin==3）的 walk 已剔除"中性站立重复帧"，关闭 sentinel 让循环无缝衔接，
        // 否则末尾会多停 1 step 再跳回头帧 → 视觉表现就是"走一会、等一会、再走"。
        bool moveAddSentinel = (skinIndex != 3);
        AnimationClip moveClip = BuildSpriteClip("move", moveSprites, thisMoveLen, true, moveAddSentinel);
        AnimationClip deadClip = BuildSpriteClip("dead", new Sprite[] { idleSprites[0] }, 0.5f, false);

        // 创建 AnimatorOverrideController 替换原 controller 中的 clip
        _overrideController = new AnimatorOverrideController(_originalController);

        var overrides = new System.Collections.Generic.List<
            System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
        _overrideController.GetOverrides(overrides);

        for (int i = 0; i < overrides.Count; i++)
        {
            var key = overrides[i].Key;
            if (key == null) continue;
            string nm = key.name;
            AnimationClip target = null;
            if (nm == "Idel" || nm.ToLower() == "idle") target = idleClip;
            else if (nm == "move") target = moveClip;
            else if (nm == "dead") target = deadClip;

            if (target != null)
            {
                overrides[i] = new System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>(key, target);
            }
        }
        _overrideController.ApplyOverrides(overrides);
        _animator.runtimeAnimatorController = _overrideController;

        // 立即给 SpriteRenderer 设置首帧，避免切换瞬间出现一帧空白
        if (_renderer != null && idleSprites.Length > 0 && idleSprites[0] != null)
        {
            _renderer.sprite = idleSprites[0];
            // 重置颜色为纯白，避免 Player.cs 把 SpriteRenderer.material 切成 red 后残留 tint
            _renderer.color = Color.white;
            // 用共享 Sprites/Default 标准材质，避免局内换装受 Player.material（URP Lit/hd2d 等）影响
            // 这一步直接消除"全身白雾"以及"一会儿黑一会儿白的光照波动"——这些都来自 URP Lit 材质对 sprite 的额外光照
            // LateUpdate 中 GuardMaterialAndColor() 会持续守护，把 Player.turnred() 切回的材质重新换成 Sprites/Default
            var spriteDefault = GetSharedSpritesDefault();
            if (spriteDefault != null) _renderer.sharedMaterial = spriteDefault;
        }

        // 强制 Animator 重新评估到新 clip 的当前状态
        _animator.Update(0f);

#if !UNITY_EDITOR
        // 运行时 Build 中 LateUpdate 兜底覆盖 sprite 用
        _runtimeIdleSprites = idleSprites;
        _runtimeMoveSprites = moveSprites;
#endif
    }

    /// <summary>从精灵图加载并切割成 idle + move（每行一组完整循环）</summary>
    private bool BuildSkinSprites(int skin, out Sprite[] idle, out Sprite[] move)
    {
        idle = null;
        move = null;

        string relativePath;
        if (skin == 1) relativePath = Ur0Path;
        else if (skin == 2) relativePath = Ur1Path;
        else relativePath = Ur2Path;
        string fullPath = Path.Combine(Application.dataPath, relativePath);
        if (!File.Exists(fullPath)) return false;

        byte[] bytes = File.ReadAllBytes(fullPath);
        // linear=true：在 Linear 色彩空间下把贴图当作线性数据上传，
        // 避免 sRGB→Linear 解码导致 alpha 抗锯齿边缘产生白色光晕（白边/白雾）
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: urTreatTextureAsLinear);
        if (!tex.LoadImage(bytes)) return false;
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        // 白边/半透明像素剔除：扫一遍像素，把"近白+非完全不透明"的像素清零
        if (urCullWhiteEdges || urAlphaCutoff > 0f)
        {
            Color32[] pixels = tex.GetPixels32();
            float sumThreshold = urWhiteCullThreshold * 3f * 255f; // 三通道总和阈值
            byte alphaMin = (byte)Mathf.Clamp(urAlphaCutoff * 255f, 0f, 255f);
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                // 1) Alpha cutoff：低于阈值直接清零（去掉半透明白雾）
                if (c.a < alphaMin)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                // 2) 近白色 + 不是完全不透明 → 清零（去掉抗锯齿白边）
                if (urCullWhiteEdges && c.a < 255)
                {
                    int sum = c.r + c.g + c.b;
                    if (sum >= sumThreshold)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                }
                // 3) 完全不透明的近白色像素也剔（轮廓描边其实是 alpha=255 的白）
                if (urCullWhiteEdges && c.a == 255)
                {
                    int sum = c.r + c.g + c.b;
                    if (sum >= sumThreshold)
                    {
                        pixels[i] = new Color32(0, 0, 0, 0);
                    }
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }

        int cellW = tex.width / 4;
        int cellH = tex.height / 3;

        // 让 UR 每帧的世界高度 ≈ 琪露诺单帧（58px@PPU100 = 0.58 单位）—— 按高度对齐避免陷地
        float autoPpu = Mathf.Max(1f, cellH / Mathf.Max(0.05f, urReferenceFrameHeightUnits * urScale));
        float ppu = urPixelsPerUnit > 0f ? urPixelsPerUnit : autoPpu;

        // === 全局 fallback pivot Y（来自 idle 列 1 的脚位置） ===
        // 关闭 PerFrame 时所有帧共用，开启 PerFrame 时也作为某帧扫不到内容时的兜底
        float fallbackPivotYNorm;
        float fallbackPivotXNorm = 0.5f;
        bool mimicCirno = urMimicCirnoPivot;
        if (mimicCirno)
        {
            // ★ 关键：完全模仿琪露诺 sprite 的 pivot=(0.5, 0.5) 规则。
            // 琪露诺的 sprite meta 里 spritePivot={x:0.5, y:0.5}，
            // 即 cell 几何中心；transform.position 对应 sprite 中心，脚像素出现在 transform 下方约 cellH/(2*ppu) 单位。
            // 让 UR 帧高对齐到 0.58 单位（urReferenceFrameHeightUnits）后，UR 脚到 transform 的距离 = 0.29 单位 = 与琪露诺一致 → 不浮空。
            // 所有帧使用同一个 pivot → 帧间不再左右/上下抖。
            fallbackPivotXNorm = 0.5f + urPivotXFine;
            fallbackPivotYNorm = 0.5f + urPivotYFine;
        }
        else if (urAutoPivotByFootScan)
        {
            // idle 行（Row 2，最顶上）的列 1 是中性站立姿势——双脚并拢，腿最完整
            int scanCol = 1;
            int scanRowBase = cellH * 2;
            int footPxFromCellBottom = ScanLowestOpaquePixelY(tex, scanCol * cellW, scanRowBase, cellW, cellH);
            fallbackPivotYNorm = Mathf.Clamp01(footPxFromCellBottom / (float)cellH);
            fallbackPivotYNorm += urFootOffsetUnits * ppu / cellH;
            fallbackPivotYNorm = Mathf.Clamp01(fallbackPivotYNorm);
        }
        else
        {
            fallbackPivotYNorm = Mathf.Clamp01(urPivotY);
        }
        Vector2 fallbackPivot = new Vector2(fallbackPivotXNorm, fallbackPivotYNorm);

        // 整张纹理像素一次取出，给 PerFrame 扫描使用（避免每帧重复 GetPixels32）。
        // mimic 模式下不需要扫像素，allPixels 留 null → ComputePivotForCell 直接返回 fallback（即琪露诺规则）。
        Color32[] allPixels = (!mimicCirno && urAutoPivotPerFrame) ? tex.GetPixels32() : null;
        int texW = tex.width;

        // 是否把 pivot Y 设到身体中心（仅在 mimic 关闭、且南筱风时启用）。
        // mimic 模式下统一用琪露诺规则，不再 per-skin 区分。
        bool centerOnBody = !mimicCirno && (skin == 1) && urCenterPivotOnBody;

        // ── idle：风之形(skin==1)删第 0 帧，仅取列 1/2/3 共 3 帧；其它皮肤取 4 帧 ──
        bool dropFirst = (skin == 1 && windIdleDropFirstFrame);
        int idleFrameCount = dropFirst ? 3 : 4;
        idle = new Sprite[idleFrameCount];
        int yIdle = cellH * 2;
        int idleStart = dropFirst ? 1 : 0;
        // 无罪（skin==3）：4 帧 idle 之间角色姿态/披风轻微飘动，逐帧扫描 pivot 会导致脚/重心位置帧间漂移 →
        // 视觉上"中心不稳、画面在抖"。这里强制使用 fallback pivot（即琪露诺中心规则或全局脚位置），所有帧共用同一 pivot。
        bool forceSharedPivot = (skin == 3);
        for (int i = 0; i < idleFrameCount; i++)
        {
            int col = idleStart + i;
            Vector2 framePivot = forceSharedPivot
                ? fallbackPivot
                : ComputePivotForCell(allPixels, texW, col * cellW, yIdle, cellW, cellH, ppu, fallbackPivot, centerOnBody);
            idle[i] = Sprite.Create(tex, new Rect(col * cellW, yIdle, cellW, cellH),
                                    framePivot, ppu);
        }

        // ── move 帧切片 ──
        // 贴图布局（Unity 纹理 Y 轴左下原点）：
        //   Row 2 (y=cellH*2) = idle 待机（最顶行）
        //   Row 1 (y=cellH)   = 走路动作组 B（身体晃动幅度大）
        //   Row 0 (y=0)       = 走路动作组 A（身体晃动幅度小）
        //
        // 经过逐帧分析（dump 出来肉眼标注），8 帧的腿部姿态如下：
        //   R0_C0 = 左脚前(小)    R0_C1 = 过渡(中)    R0_C2 = 过渡(中)    R0_C3 = 右脚前(中)
        //   R1_C0 = 左脚抬(大)    R1_C1 = 中性        R1_C2 = 右脚前(中)  R1_C3 = 右脚前(大)
        //
        // 朴素地按 R0_C0..R0_C3, R1_C0..R1_C3 顺序播 → 视觉上像一条腿走路，因为：
        //   左脚迈出(R0_C0)→收→收→右脚迈(R0_C3)→左脚抬(R1_C0)→中→右脚迈(R1_C2)→右脚迈(R1_C3)
        //   → 末尾连续 3 帧右脚在前，视觉重心严重偏向"右脚一直在动"。
        //
        // 解决：按"左脚组 / 中性组 / 右脚组"重新交错排列，做成对称钟摆：
        //   左小 → 左大 → 过渡 → 右小 → 右大 → 右回 → 过渡 → 中性 → 循环
        // 即 [R0_C0, R1_C0, R0_C1, R1_C2, R1_C3, R0_C3, R0_C2, R1_C1]，左右脚交替出现。
        if (skin == 1)
        {
            // 用 (col, row) 二元组表达每帧位置，row 是 Unity 坐标（左下原点 0=底/1=中）
            (int col, int row)[] sequence;
            switch (windWalkSequenceMode)
            {
                case 1:
                    // Row 1 钟摆 4 帧（旧 default）
                    sequence = new (int, int)[] { (0,1), (1,1), (2,1), (1,1) };
                    break;
                case 2:
                    // Row 0 钟摆 4 帧（幅度小一点）
                    sequence = new (int, int)[] { (0,0), (1,0), (3,0), (2,0) };
                    break;
                case 3:
                    // 8 帧交错钟摆（旧 default，左右脚对称但末尾的中性帧观感像"后撤一步"）
                    sequence = new (int, int)[]
                    {
                        (0, 0), (0, 1), (1, 0), (2, 1),
                        (3, 1), (3, 0), (2, 0), (1, 1),
                    };
                    break;
                default:
                    // ★ 推荐：按精灵图自然顺序取走路前 7 帧（丢掉最后一帧）
                    // 用户原话：精灵图共 12 个，前 4 个是待机不用管，移动用剩下的 7 个（最后一帧不要）。
                    // 图集 PNG 视觉布局（自上而下）：行 idle / 行 move-B / 行 move-A
                    // Unity 纹理 Y 轴左下原点 → 视觉顶行=row 2 (idle), 视觉中行=row 1 (move-B), 视觉底行=row 0 (move-A)
                    // 按 PNG 视觉顺序（idle 后第 1~8 帧）= 中行 4 帧 + 底行 4 帧。
                    // 去掉最后一帧 → 中行 4 帧 + 底行前 3 帧 = 7 帧。
                    sequence = new (int, int)[]
                    {
                        (0, 1), (1, 1), (2, 1), (3, 1),  // 视觉中行：走路 4 帧
                        (0, 0), (1, 0), (2, 0),          // 视觉底行：走路 3 帧（最后一帧 (3,0) 按要求丢弃）
                    };
                    break;
            }
            move = new Sprite[sequence.Length];
            for (int i = 0; i < sequence.Length; i++)
            {
                int col = sequence[i].col;
                int row = sequence[i].row;
                int cellX = col * cellW;
                int cellY = row * cellH;
                Vector2 framePivot = ComputePivotForCell(allPixels, texW, cellX, cellY, cellW, cellH, ppu, fallbackPivot, centerOnBody);
                move[i] = Sprite.Create(tex,
                                        new Rect(cellX, cellY, cellW, cellH),
                                        framePivot, ppu);
            }
            return true;
        }

        // 非南筱风（夏无 skin==2 / 无罪 skin==3）走以下分支
        if (skin == 3)
        {
            // 无罪（skin==3）：使用第 3、4、6、7 帧（共 4 帧）走路循环。
            // 之前用 3 关键帧 (2,1)(0,0)(2,0) 只剩 2 个迈步姿态 + 1 个中间过渡 →
            //   节奏上像"原地跺脚"，缺少左右脚交替的连贯感。
            // 之前的 5 帧（第 3~7 帧）相邻姿态又太接近 → "小短腿倒腾"。
            //
            // 折中方案：取第 3、4、6、7 共 4 帧，跳过第 5 帧（中间衔接的中性过渡）。
            //   这样左右脚各保留 2 帧（迈出 + 收回），节奏对称、姿态对比明显，
            //   既不像跺脚也不像倒腾。
            //
            // 帧映射（沿用之前的"中行→底行"顺序）：
            //   第 3 帧 = (2,1) 左脚迈出
            //   第 4 帧 = (3,1) 左脚收
            //   第 5 帧 = (0,0) 中间过渡    ← 跳过
            //   第 6 帧 = (1,0) 右脚迈出
            //   第 7 帧 = (2,0) 右脚收
            //
            // Unity 纹理 Y 轴左下原点 → 视觉中行 = row 1，视觉底行 = row 0
            (int col, int row)[] walk4 = new (int, int)[]
            {
                (2, 1),  // 第 3 帧：左脚迈出
                (3, 1),  // 第 4 帧：左脚收
                (1, 0),  // 第 6 帧：右脚迈出
                (2, 0),  // 第 7 帧：右脚收
            };
            move = new Sprite[walk4.Length];
            for (int i = 0; i < walk4.Length; i++)
            {
                int col = walk4[i].col;
                int row = walk4[i].row;
                int cellX = col * cellW;
                int cellY = row * cellH;
                // 同样为无罪强制使用 fallback pivot，保证帧之间不左右抖
                Vector2 framePivot = fallbackPivot;
                move[i] = Sprite.Create(tex, new Rect(cellX, cellY, cellW, cellH),
                                        framePivot, ppu);
            }
            return true;
        }

        // 夏无（skin==2）：使用 Row 1 的前 3 帧（列 0、1、2）循环。
        // pivot 在脚，不做身体中心校准 → 与之前的视觉位置完全一致，仅减少最后一帧。
        int[] moveCols = new int[] { 0, 1, 2 };
        move = new Sprite[moveCols.Length];
        for (int i = 0; i < moveCols.Length; i++)
        {
            int col = moveCols[i];
            int cellX = col * cellW;
            int cellY = cellH;
            Vector2 framePivot = ComputePivotForCell(allPixels, texW, cellX, cellY, cellW, cellH, ppu, fallbackPivot, false);
            move[i] = Sprite.Create(tex, new Rect(cellX, cellY, cellW, cellH),
                                    framePivot, ppu);
        }
        return true;
    }

    /// <summary>
    /// 在 (cellX, cellY)（左下角原点）大小 cellW × cellH 的区域内扫描，
    /// 返回从 cell 底部（cellY）向上数、第一个出现"不透明像素"的 y 偏移（像素）。
    /// 用于自动定位人物脚像素位置 → 设置 sprite pivot Y。
    /// </summary>
    private static int ScanLowestOpaquePixelY(Texture2D tex, int cellX, int cellY, int cellW, int cellH)
    {
        // 注意 Unity 的 GetPixel(x, y) 是左下角原点；Texture2D 内部行优先存储。
        // 为减少 GetPixels 复制开销，整张取一次后局部访问。
        Color32[] all = tex.GetPixels32();
        int texW = tex.width;
        const byte ALPHA_THRESHOLD = 32; // 太低可能是飘散的风元素半透明像素，跳过
        for (int dy = 0; dy < cellH; dy++)
        {
            int y = cellY + dy;
            int rowStart = y * texW;
            for (int dx = 0; dx < cellW; dx++)
            {
                int x = cellX + dx;
                if (all[rowStart + x].a >= ALPHA_THRESHOLD)
                {
                    return dy; // 找到第一行有像素的位置，即脚的高度
                }
            }
        }
        // 未扫到（贴图全透明）：退回 cellH * 0.08（默认假设）
        return Mathf.RoundToInt(cellH * 0.08f);
    }

    /// <summary>
    /// 为单个 cell 计算独立的 pivot（X = 角色重心, Y = 脚位置 或 身体中心），消除帧间因角色横向位置不一致导致的左右抖动。
    /// 若 urAutoPivotPerFrame=false，则直接返回 fallback。
    /// 算法：
    ///   Y：扫描 cell 内最低不透明像素行（脚），归一化到 [0..1]。
    ///      若 centerOnBody=true，则继续扫最高不透明像素行（头），pivot Y 设为 (脚+头)/2 → 身体几何中心。
    ///      这样 transform.position 直接对应"角色身体中心"，环绕物绕身体而不是脚下。
    ///   X：在脚部上方一条窄带内（避免风元素飘散像素干扰），求所有不透明像素 X 坐标的中位数
    /// </summary>
    private Vector2 ComputePivotForCell(Color32[] all, int texW, int cellX, int cellY,
                                        int cellW, int cellH, float ppu, Vector2 fallback,
                                        bool centerOnBody)
    {
        if (!urAutoPivotPerFrame || all == null) return fallback;

        const byte ALPHA_THRESHOLD = 32;

        // 1) 脚位置（Y）：从 cell 底向上扫，第一行有不透明像素的 dy
        int footDy = -1;
        for (int dy = 0; dy < cellH; dy++)
        {
            int y = cellY + dy;
            int rowStart = y * texW;
            for (int dx = 0; dx < cellW; dx++)
            {
                if (all[rowStart + cellX + dx].a >= ALPHA_THRESHOLD)
                {
                    footDy = dy;
                    break;
                }
            }
            if (footDy >= 0) break;
        }
        if (footDy < 0)
        {
            return fallback; // 全透明，退回兜底
        }

        float pivotYNorm;
        if (centerOnBody)
        {
            // 1b) 头位置（Y）：从 cell 顶向下扫，第一行有不透明像素的 dy
            int headDy = -1;
            for (int dy = cellH - 1; dy >= 0; dy--)
            {
                int y = cellY + dy;
                int rowStart = y * texW;
                for (int dx = 0; dx < cellW; dx++)
                {
                    if (all[rowStart + cellX + dx].a >= ALPHA_THRESHOLD)
                    {
                        headDy = dy;
                        break;
                    }
                }
                if (headDy >= 0) break;
            }
            if (headDy < 0) headDy = footDy; // 退化保护

            // pivot 设到身体几何中心 → transform.position 对应身体中心，环绕物绕身体
            int centerDy = (footDy + headDy) / 2;
            pivotYNorm = Mathf.Clamp01(centerDy / (float)cellH + urBodyCenterPivotOffset);
        }
        else
        {
            // 旧行为：pivot 设到脚像素位置（夏无走这条路径）
            pivotYNorm = Mathf.Clamp01(footDy / (float)cellH);
            if (urAutoPivotByFootScan)
            {
                pivotYNorm += urFootOffsetUnits * ppu / cellH;
                pivotYNorm = Mathf.Clamp01(pivotYNorm);
            }
            else
            {
                pivotYNorm = Mathf.Clamp01(urPivotY);
            }
        }

        // 2) X 重心：在脚部窄带内取所有不透明像素 X 的中位数
        // 用脚部窄带（脚 ~ 脚+bandH）能保持稳定：脚位置代表角色站立点，
        // 用整个角色（含飘散风元素、向外伸展的手臂）求重心容易抖。
        int bandH = Mathf.Max(4, Mathf.RoundToInt(cellH * urCenterXBandRatio));
        int dyStart, dyEnd;
        if (urCenterXFromFootBand)
        {
            dyStart = footDy;
            dyEnd = Mathf.Min(cellH, footDy + bandH);
        }
        else
        {
            dyStart = 0;
            dyEnd = cellH;
        }

        // 收集 X 坐标到列表后取中位数（中位数比均值抗离群值，更稳）
        // 用 buffer 数组避免 GC：cell 像素数已知上限
        int maxCount = (dyEnd - dyStart) * cellW;
        if (maxCount <= 0) return new Vector2(0.5f, pivotYNorm);

        // 直方图法求中位数：cellW 通常 ≤ 1024，开个数组够用且零分配
        int[] hist = new int[cellW];
        int total = 0;
        for (int dy = dyStart; dy < dyEnd; dy++)
        {
            int y = cellY + dy;
            int rowStart = y * texW;
            for (int dx = 0; dx < cellW; dx++)
            {
                if (all[rowStart + cellX + dx].a >= ALPHA_THRESHOLD)
                {
                    hist[dx]++;
                    total++;
                }
            }
        }
        if (total == 0)
        {
            return new Vector2(0.5f, pivotYNorm);
        }

        // 累计直方图找中位数
        int half = total / 2;
        int acc = 0;
        int medianDx = cellW / 2;
        for (int dx = 0; dx < cellW; dx++)
        {
            acc += hist[dx];
            if (acc >= half)
            {
                medianDx = dx;
                break;
            }
        }
        float pivotXNorm = Mathf.Clamp01(medianDx / (float)cellW);

        return new Vector2(pivotXNorm, pivotYNorm);
    }

    /// <summary>构造一个 sprite-keyframe AnimationClip。
    /// addSentinel=true：末尾追加一个 time=length 的关键帧，把 clip 总长撑到 length，
    /// 让最后一帧完整占满 step 秒（适合一次播完不循环、或末尾帧本身有意义的场景）。
    /// addSentinel=false：clip 长度 = (n-1)*step + step = length，但最后一个关键帧时间 = (n-1)*step；
    /// 在 loop 模式下播放器到达 (n-1)*step 后立刻回到 0，循环更紧凑、无"末尾停顿"——
    /// 适合需要无缝循环的走路 / 跑步动画。
    /// </summary>
    private AnimationClip BuildSpriteClip(string name, Sprite[] frames, float length, bool loop, bool addSentinel = true)
    {
        AnimationClip clip = new AnimationClip { name = name, frameRate = 60f };

        var settings = UnityEditorBridge.GetClipSettings(clip);
        settings.loopTime = loop;
        UnityEditorBridge.SetClipSettings(clip, settings);

        EditorCurveBindingProxy binding = new EditorCurveBindingProxy
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        // ObjectReferenceCurve 的 clip 长度 = 最后一个关键帧的 time。
        int n = frames.Length;
        float step = n > 1 ? length / n : length;
        if (addSentinel)
        {
            // 末尾追加 sentinel 关键帧 time=length, value=最后一帧 → 最后一帧完整播放一个 step
            ObjectReferenceKeyframeProxy[] keyframes = new ObjectReferenceKeyframeProxy[n + 1];
            for (int i = 0; i < n; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframeProxy { time = i * step, value = frames[i] };
            }
            keyframes[n] = new ObjectReferenceKeyframeProxy { time = length, value = frames[n - 1] };
            UnityEditorBridge.SetObjectReferenceCurve(clip, binding, keyframes);
        }
        else
        {
            // 不加 sentinel：clip 长度 = (n-1)*step。
            // loop 模式下播放器在 (n-1)*step 时刻回到 0 → 最后一帧只显示 1 个采样瞬间，
            // 但因循环紧凑，整体观感是连续无停顿的步态。
            ObjectReferenceKeyframeProxy[] keyframes = new ObjectReferenceKeyframeProxy[n];
            for (int i = 0; i < n; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframeProxy { time = i * step, value = frames[i] };
            }
            UnityEditorBridge.SetObjectReferenceCurve(clip, binding, keyframes);
        }
        return clip;
    }

    private void UpdateEditorPreview()
    {
        if (skinIndex == 0 || _renderer == null) return;
#if UNITY_EDITOR
        Sprite[] idle, move;
        if (BuildSkinSprites(skinIndex, out idle, out move))
        {
            if (idle == null || idle.Length == 0) return;
            float t = (float)UnityEditor.EditorApplication.timeSinceStartup;
            float fps;
            if (skinIndex == 1)
                fps = idle.Length / Mathf.Max(0.01f, windIdleClipLength);
            else if (skinIndex == 3)
                fps = idle.Length / Mathf.Max(0.01f, tombIdleClipLength);
            else
                fps = 6f;
            int idx = (int)(t * fps) % idle.Length;
            if (idle[idx] != null) _renderer.sprite = idle[idx];
        }
#endif
    }

    private void OnDisable()
    {
        // 不去主动还原，因为玩家可能只是禁用对象
    }
}

// ===== 跨域桥（避免 #if UNITY_EDITOR 在主类内部破坏序列化结构） =====
internal struct EditorCurveBindingProxy
{
    public System.Type type;
    public string path;
    public string propertyName;
}

internal struct ObjectReferenceKeyframeProxy
{
    public float time;
    public Object value;
}

internal static class UnityEditorBridge
{
    public static AnimationClipSettingsProxy GetClipSettings(AnimationClip clip)
    {
#if UNITY_EDITOR
        var s = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
        return new AnimationClipSettingsProxy { loopTime = s.loopTime };
#else
        return new AnimationClipSettingsProxy { loopTime = clip.isLooping };
#endif
    }

    public static void SetClipSettings(AnimationClip clip, AnimationClipSettingsProxy proxy)
    {
#if UNITY_EDITOR
        var s = UnityEditor.AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = proxy.loopTime;
        UnityEditor.AnimationUtility.SetAnimationClipSettings(clip, s);
#else
        // 运行时 build 中 AnimationClip.wrapMode 控制循环
        clip.wrapMode = proxy.loopTime ? WrapMode.Loop : WrapMode.Once;
#endif
    }

    public static void SetObjectReferenceCurve(AnimationClip clip,
                                               EditorCurveBindingProxy bindingProxy,
                                               ObjectReferenceKeyframeProxy[] frames)
    {
#if UNITY_EDITOR
        var binding = new UnityEditor.EditorCurveBinding
        {
            type = bindingProxy.type,
            path = bindingProxy.path,
            propertyName = bindingProxy.propertyName
        };
        var keys = new UnityEditor.ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new UnityEditor.ObjectReferenceKeyframe { time = frames[i].time, value = frames[i].value };
        }
        UnityEditor.AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
#else
        // 运行时 Build：使用 AnimationClip.SetCurve 不支持 PPtr。
        // 兜底：把 sprite 走 SpriteAnimationFallback 处理。
        clip.wrapMode = WrapMode.Loop;
        // 在 Build 中无法直接构造 sprite keyframe curve，这里留空，
        // 由 PlayerSkinOverrider 在 LateUpdate 自行覆盖 sprite。
#endif
    }
}

internal struct AnimationClipSettingsProxy
{
    public bool loopTime;
}
