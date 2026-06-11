using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「亡者领域」Boss 复活全屏视觉表演（v7，三阶段串行编排）。
///
/// 三阶段串行（用户 v7 反馈："龙眼出现 → 缓缓变小变暗消失 → 然后才反向死亡 → 然后走路"）：
///
///   Stage 1 [龙眼演出 + 粒子能量场]（EYE_DURATION = 1.6s）
///     ┌── 0.00 ~ 0.05s ：龙眼 alpha 从 0 瞬升到 1.0（立刻出现，不渐入）
///     ┌── 0.05 ~ 0.40s ：龙眼定格凝视（alpha=1, scale=1）+ 能量场粒子爬升/汇聚
///     ┌── 0.40 ~ 1.30s ：龙眼缓缓变小（1.0 → 0.55）+ 渐暗（1.0 → 0.0）；
///                       同时粒子能量场播放高潮（爆炸/扩散）
///     └── 1.30 ~ 1.60s ：龙眼完全消失，粒子残辉收尾
///     此阶段 Boss 保持死亡 pose 不动，MindControlled 被 SetReviveFreeze(true) 冻结。
///
///   Stage 2 [反向死亡动画]（clipLength，按 Boss dead clip 自适应）
///     从 normalizedTime=1 反推到 0，让 Boss "死而复活"，碎片从地面拼回身体。
///
///   Stage 3 [切换到行走动画 + 解冻 MindControlled]
///     按 Animator 实际 state 名兜底切换：
///       蝙蝠王 batboss.controller : "move", "idle"
///       蘑菇王 Shoom.controller   : "move", "move 0", "Idel"(原作拼写错误)
///       通用兜底                  : "move", "move 0", "Idel", "idle"
///     同步重置 trigger / SetBool("ismove", false)，
///     然后 SetReviveFreeze(false) 把动画驱动权交还 MindControlled。
///
/// === 资源 ===
/// • Resources/Effects/ReviveDragonEye.png   AI 生成扁平双眼（金瞳竖瞳、紫色眼眶）。
/// • Resources/Effects/ReviveEnergy_0..11.png  v2 精修粒子能量场（384×384，密度+50%）。
///
/// === UI 层级 ===
/// • Canvas: ScreenSpaceOverlay, sortOrder=32766。
/// • [底] 暗紫遮罩 RawImage 铺满 Screen，alpha 跟 MASK_ALPHA。
/// • [中] 粒子能量场 Image 铺满 Screen（对角线 × 1.05），自旋 + 缩放呼吸 + 12 帧切换。
/// • [上] 龙眼 Image 居中偏上 10%（pivot=0.5,0.5），按 12 帧 alpha/scale 曲线播放。
/// </summary>
public class ReviveBossEffect : MonoBehaviour
{
    private const int   ENERGY_FRAME_COUNT = 12;
    private const int   CANVAS_SORT_ORDER  = 32766;
    private const float DEFAULT_DEAD_LEN   = 1.2f;
    private const float EYE_DURATION      = 1.6f;    // Stage 1 总时长

    // —— 能量场 12 帧的相对节拍权重（归一到 EYE_DURATION）——
    private static readonly float[] ENERGY_FRAME_WEIGHT = {
        0.50f, 0.50f, 0.60f, 0.60f, 0.70f, 0.80f,
        0.85f, 0.90f, 0.85f, 1.00f, 1.10f, 0.80f,
    };
    private const float ENERGY_FRAME_WEIGHT_SUM = 9.20f;

    // 能量场 alpha 呼吸曲线（汇聚 → 爆炸全开 → 残辉）
    private static readonly float[] ENERGY_ALPHA = {
        0f,    0.30f, 0.55f, 0.75f, 0.90f, 1.00f,
        1.00f, 1.00f, 1.00f, 1.00f, 0.95f, 0.55f,
    };

    // 能量场缩放（高潮放大 → 收尾回弹）
    private static readonly float[] ENERGY_SCALE = {
        1.00f, 1.00f, 1.00f, 1.02f, 1.04f, 1.06f,
        1.07f, 1.08f, 1.10f, 1.12f, 1.10f, 1.05f,
    };

    // 暗紫遮罩 alpha（贯穿整个 Stage 1，演出结束随龙眼一起淡出）
    private static readonly float[] MASK_ALPHA = {
        0.05f, 0.18f, 0.30f, 0.38f, 0.45f, 0.50f,
        0.52f, 0.55f, 0.50f, 0.40f, 0.25f, 0.05f,
    };

    private const float ENERGY_ROTATE_SPEED = -10f;  // 能量场缓慢逆时针自旋

    // 共享资源
    private static Sprite[] _sharedEnergyFrames;
    private static Sprite   _sharedEyeSprite;
    private static Texture2D _sharedMaskTex;
    private static bool      _resourceMissingLogged;

    // 实例引用
    private Canvas        _canvas;
    private RawImage      _maskImg;
    private Image         _energyImg;
    private RectTransform _energyRT;
    private Image         _eyeImg;
    private RectTransform _eyeRT;
    private Vector2       _eyeBaseSize;
    private float         _energyRotation;

    // 帧推进（仅控制能量场 12 帧）
    private int   _energyFrameIdx;
    private float _energyFrameTimer;
    private float[] _energyFrameDuration;

    // 全局阶段计时
    private float _stageTimer;       // Stage 1 已运行秒数（用于龙眼连续曲线）
    private bool  _stage1Done;

    /// <summary>
    /// 在 Boss 复活成功的瞬间调起特效。三阶段串行：
    ///   1) 全屏龙眼演出 + 粒子能量场（EYE_DURATION 秒）
    ///   2) 反向死亡动画（自适应 Boss dead clip 长度）
    ///   3) 切换行走动画 + 解冻 MindControlled
    /// </summary>
    public static GameObject Spawn(Transform targetTransform, bool isWorldBoss)
    {
        if (targetTransform == null) return null;
        EnsureResources();
        if (_sharedEyeSprite == null) return null;  // 龙眼资源缺失则放弃整套演出

        // —— 压迫感音效（与 Stage 1 起拍同步）——
        // 程序化合成的 "亡者复活.wav"（3.2s）：sub-bass 暴击 + 死亡心跳 ×3 + 金属轰鸣 drone
        // + 龙吼共振扫频 + 玻璃裂高频 burst + 低频残辉。节拍设计与本特效三阶段对齐：
        //   0.00 ~ 1.60s 龙眼演出（音效前段：boom + 心跳 + drone 起来）
        //   1.50 ~ 2.00s 玻璃裂 + 砰（正好落在龙眼消失瞬间，给玩家"封印破碎"的听觉钩子）
        //   2.00 ~ 3.20s 低频残辉（贯穿 Stage 2 反向死亡演出）
        // 音效缺失时 AudioManager 内部自带 no-op 兜底，不会报错。
        AudioManager.PlaySfx(AudioManager.SfxKey.TombRevive);

        // —— Stage 0：冻结 MindControlled（如果已经挂上），让 Boss 死亡 pose 不被 ismove 改写 ——
        MindControlled mc = targetTransform.GetComponent<MindControlled>();
        if (mc != null) mc.SetReviveFreeze(true);

        // —— 解析 dead clip 真实长度（蝙蝠 0.83s / 蘑菇 1.68s）——
        // 同时拿到 AnimationClip 引用：v10 Stage 2 改用 `AnimationClip.SampleAnimation`
        // 手动反向逐帧采样（Unity 的 Animator.speed=-1 在 PPtr Sprite 动画上不可靠）。
        AnimationClip deadClip = ResolveDeadClip(targetTransform);
        float deadLen = (deadClip != null) ? deadClip.length : DEFAULT_DEAD_LEN;
        if (deadLen <= 0f) deadLen = DEFAULT_DEAD_LEN;

        // —— 创建全屏 UI Canvas ——
        GameObject go = new GameObject($"ReviveBossEffect_{targetTransform.name}");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CANVAS_SORT_ORDER;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        go.AddComponent<GraphicRaycaster>().enabled = false;

        ReviveBossEffect fx = go.AddComponent<ReviveBossEffect>();
        fx._canvas = canvas;
        fx._targetTransform = targetTransform;
        fx._deadClipLen = deadLen;
        fx._deadClip    = deadClip;

        float sw = Screen.width;
        float sh = Screen.height;
        float shortSide = Mathf.Min(sw, sh);
        float longSide  = Mathf.Max(sw, sh);

        // [底] 暗紫遮罩
        GameObject maskGo = new GameObject("Mask", typeof(RectTransform));
        maskGo.transform.SetParent(go.transform, false);
        RectTransform maskRT = (RectTransform)maskGo.transform;
        maskRT.anchorMin = Vector2.zero;
        maskRT.anchorMax = Vector2.one;
        maskRT.offsetMin = Vector2.zero;
        maskRT.offsetMax = Vector2.zero;
        fx._maskImg = maskGo.AddComponent<RawImage>();
        fx._maskImg.texture = _sharedMaskTex;
        fx._maskImg.color = new Color(0.08f, 0.02f, 0.18f, 0f);
        fx._maskImg.raycastTarget = false;

        // [中] 能量场（铺满对角线 × 1.05，自旋时也不会露角）
        if (_sharedEnergyFrames != null && _sharedEnergyFrames.Length >= ENERGY_FRAME_COUNT)
        {
            GameObject energyGo = new GameObject("EnergyField", typeof(RectTransform));
            energyGo.transform.SetParent(go.transform, false);
            RectTransform energyRT = (RectTransform)energyGo.transform;
            energyRT.anchorMin = new Vector2(0.5f, 0.5f);
            energyRT.anchorMax = new Vector2(0.5f, 0.5f);
            energyRT.pivot     = new Vector2(0.5f, 0.5f);
            float energySize = Mathf.Sqrt(sw * sw + sh * sh) * 1.05f;
            energyRT.sizeDelta = new Vector2(energySize, energySize);
            energyRT.anchoredPosition = Vector2.zero;
            fx._energyRT = energyRT;
            fx._energyImg = energyGo.AddComponent<Image>();
            fx._energyImg.sprite = _sharedEnergyFrames[0];
            fx._energyImg.color = new Color(1f, 1f, 1f, 0f);
            fx._energyImg.raycastTarget = false;
            fx._energyImg.preserveAspect = false;
        }

        // [上] 龙眼：参考用户 v7 扁平双眼图（宽:高 ≈ 1212:319 ≈ 3.8:1），按短边的 1.6 撑宽
        GameObject eyeGo = new GameObject("DragonEye", typeof(RectTransform));
        eyeGo.transform.SetParent(go.transform, false);
        RectTransform eyeRT = (RectTransform)eyeGo.transform;
        eyeRT.anchorMin = new Vector2(0.5f, 0.5f);
        eyeRT.anchorMax = new Vector2(0.5f, 0.5f);
        eyeRT.pivot     = new Vector2(0.5f, 0.5f);
        Sprite eyeSp = _sharedEyeSprite;
        float eyeAspect = (eyeSp != null && eyeSp.rect.height > 0f) ? eyeSp.rect.width / eyeSp.rect.height : 3.8f;
        float eyeWidth  = shortSide * 1.4f;
        if (eyeWidth > longSide * 0.95f) eyeWidth = longSide * 0.95f;
        float eyeHeight = eyeWidth / eyeAspect;
        fx._eyeBaseSize = new Vector2(eyeWidth, eyeHeight);
        eyeRT.sizeDelta = fx._eyeBaseSize;
        // 居中略偏上 5%，眼睛更有"高位俯视"感
        eyeRT.anchoredPosition = new Vector2(0f, sh * 0.05f);
        fx._eyeRT = eyeRT;
        fx._eyeImg = eyeGo.AddComponent<Image>();
        fx._eyeImg.sprite = eyeSp;
        fx._eyeImg.color = new Color(1f, 1f, 1f, 0f);   // 起始透明，第一帧 Update 立刻刷成 1.0
        fx._eyeImg.raycastTarget = false;
        fx._eyeImg.preserveAspect = true;

        // 能量场帧时长归一到 EYE_DURATION
        fx._energyFrameDuration = new float[ENERGY_FRAME_COUNT];
        for (int i = 0; i < ENERGY_FRAME_COUNT; i++)
        {
            fx._energyFrameDuration[i] = EYE_DURATION * (ENERGY_FRAME_WEIGHT[i] / ENERGY_FRAME_WEIGHT_SUM);
        }
        fx.ApplyEnergyFrame(0);

        Debug.Log($"[亡者领域·复活特效v10] {targetTransform.name} (isWorldBoss={isWorldBoss}) " +
                  $"eye={EYE_DURATION:F2}s, reverseDead={deadLen:F2}s, screen={sw}x{sh}");

        // —— v10 新增 Stage 1 子协程：正向播放一次完整 dead 动画 ——
        // 用户反馈"没有播放死亡动画"：因为 TombDomainHook.TryReviveAsAlly 在 Boss.Destroy1 最开头
        // 就拦截 return 了，Boss 自己 SetTrigger("dead") 那行根本走不到。所以这里在龙眼演出开始时，
        // 同步在 Boss 身上手动 SampleAnimation 正向播一次 dead 动画，让玩家看到"Boss 倒下 → 龙眼凝视
        //  → 反向复活"的完整叙事。Animator.enabled 在期间被关闭，避免 idle/move 动画与采样争抢 sprite。
        if (deadClip != null && deadLen > 0f)
        {
            fx.StartCoroutine(fx.PlayForwardDeathDuringStage1(deadClip, deadLen));
        }

        return go;
    }

    /// <summary>
    /// v10：与 Stage 1 龙眼演出同步，正向逐帧 SampleAnimation 播一次 dead 动画。
    /// 因为 TombDomainHook 在 Destroy1 开头就拦截了 return，Boss 自己没机会 SetTrigger("dead")。
    /// 用户期望先看到 Boss 倒下，再看到反向复活——这里弥补缺失的"正向死亡"叙事。
    /// </summary>
    private IEnumerator PlayForwardDeathDuringStage1(AnimationClip clip, float clipLen)
    {
        Transform tr = _targetTransform;
        if (tr == null || clip == null || clipLen <= 0f) yield break;
        Animator ani = tr.GetComponent<Animator>();
        if (ani == null) yield break;
        MindControlled mc = tr.GetComponent<MindControlled>();
        GameObject go = tr.gameObject;

        ani.enabled = false; // 让 SampleAnimation 的写入不被 Animator 覆盖
        // 立即采样起点（活着的第一帧）
        clip.SampleAnimation(go, 0f);
        if (mc != null) mc.ForceSyncOverlayNow();

        float wallClock = 0f;
        // 正向播完整个 dead clip 长度；如果 dead clip 比 EYE_DURATION 长（蘑菇 1.68s > 1.6s 眼演出），
        // Stage 1 一结束就提前 break，避免与 Stage 2 反向协程对同一 clip 同时 SampleAnimation 冲突。
        while (wallClock < clipLen && tr != null && !_stage1Done)
        {
            yield return null;
            if (tr == null) yield break;
            wallClock += Time.deltaTime;
            float t = Mathf.Min(wallClock, clipLen);
            clip.SampleAnimation(go, t);
            if (mc != null) mc.ForceSyncOverlayNow();
        }
        // 播完后**保持** Animator.enabled=false + sprite 停在最后一帧，
        // 等 Stage 2 协程进来接管反向采样；Stage 2 末尾会重新 ani.enabled = true。
    }

    // —— 实例字段（Stage 2/3 用）——
    private Transform     _targetTransform;
    private float         _deadClipLen;
    private AnimationClip _deadClip;   // v10 新增：用于手动反向 SampleAnimation

    // —— Stage 1: 龙眼连续曲线（与帧节奏解耦，更细腻）——
    // x = stageTimer / EYE_DURATION ∈ [0, 1]
    //   0.00 ~ 0.03 ：alpha 0 → 1（立现）
    //   0.03 ~ 0.25 ：定格 alpha=1, scale=1
    //   0.25 ~ 0.85 ：缓缓变小（scale 1.0 → 0.55）+ 缓缓变暗（alpha 1.0 → 0.0）
    //   0.85 ~ 1.00 ：完全消失（alpha=0）
    private static void ComputeEyeCurve(float x, out float alpha, out float scale)
    {
        if (x <= 0.03f)
        {
            alpha = Mathf.Clamp01(x / 0.03f);
            scale = 1.0f;
        }
        else if (x <= 0.25f)
        {
            alpha = 1.0f;
            scale = 1.0f;
        }
        else if (x <= 0.85f)
        {
            float t = (x - 0.25f) / 0.60f;     // 0..1
            // 用 smoothstep 让变化更自然
            float s = t * t * (3f - 2f * t);
            alpha = Mathf.Lerp(1.0f, 0.0f, s);
            scale = Mathf.Lerp(1.0f, 0.55f, s);
        }
        else
        {
            alpha = 0f;
            scale = 0.55f;
        }
    }

    void Update()
    {
        if (_stage1Done) return;  // Stage 2/3 由协程驱动，Update 不再做事

        _stageTimer += Time.deltaTime;

        // —— 能量场帧推进 ——
        if (_energyFrameDuration != null && _energyFrameIdx < ENERGY_FRAME_COUNT)
        {
            _energyFrameTimer += Time.deltaTime;
            float dur = _energyFrameDuration[_energyFrameIdx];
            if (_energyFrameTimer >= dur)
            {
                _energyFrameTimer -= dur;
                _energyFrameIdx++;
                if (_energyFrameIdx < ENERGY_FRAME_COUNT)
                {
                    ApplyEnergyFrame(_energyFrameIdx);
                }
            }
        }

        // —— 能量场自旋（实时）——
        if (_energyRT != null)
        {
            _energyRotation += ENERGY_ROTATE_SPEED * Time.deltaTime;
            _energyRT.localRotation = Quaternion.Euler(0f, 0f, _energyRotation);
        }

        // —— 龙眼曲线（连续，不按帧）——
        float x = Mathf.Clamp01(_stageTimer / EYE_DURATION);
        ComputeEyeCurve(x, out float eyeAlpha, out float eyeScale);
        if (_eyeImg != null)
            _eyeImg.color = new Color(1f, 1f, 1f, eyeAlpha);
        if (_eyeRT != null)
            _eyeRT.localScale = new Vector3(eyeScale, eyeScale, 1f);

        // —— Stage 1 结束 ——
        if (_stageTimer >= EYE_DURATION)
        {
            _stage1Done = true;
            StartCoroutine(Stage2_ReverseDeath_ThenStage3());
        }
    }

    private void ApplyEnergyFrame(int idx)
    {
        if (idx < 0 || idx >= ENERGY_FRAME_COUNT) return;

        if (_maskImg != null)
        {
            var c = _maskImg.color;
            c.a = MASK_ALPHA[idx];
            _maskImg.color = c;
        }
        if (_energyImg != null && _sharedEnergyFrames != null
            && idx < _sharedEnergyFrames.Length && _sharedEnergyFrames[idx] != null)
        {
            _energyImg.sprite = _sharedEnergyFrames[idx];
            _energyImg.color = new Color(1f, 1f, 1f, ENERGY_ALPHA[idx]);
        }
        if (_energyRT != null)
        {
            float s = ENERGY_SCALE[idx];
            _energyRT.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>
    /// Stage 2: 反向死亡动画 → Stage 3: 切换行走 + 解冻 MindControlled。
    /// 先销毁全屏 UI（Stage 1 已结束），然后在 Boss 自己身上驱动 Animator。
    ///
    /// === v10 关键修复（用户：v9 之后\"既无死亡动画、又无反向、直接死亡 pose 飘移\"）===
    /// 三个独立但叠加的根因：
    ///
    /// 1) TombDomainHook.TryReviveAsAlly 在 Boss.Destroy1 的最开头就拦截 return，**SetTrigger("dead")
    ///    根本没机会被调用** → Animator 自始至终停在 idle/move state，没播过死亡动画。
    ///    用户期望"先死、再反向复活"，所以 v10 在 Stage 1 同步**手动**播一次完整 dead 动画。
    ///
    /// 2) Unity 的 `Animator.speed = -1` + `Play(deadHash, 0, 1f)` 在 **PPtr Sprite 关键帧动画**
    ///    上反向采样**不可靠**：PPtr 曲线插值规则是"取左侧 keyframe"，反向时 sprite 字段经常根本
    ///    不更新，导致玩家看到的就是死亡最后一帧静止。dead.anim 都是 PPtr：
    ///    bat dead 10 帧/0.83s（m_SampleRate=12），shoom dead 8 帧/1.67s（m_SampleRate=60）。
    ///    v10 解决：**绕开 Animator**，直接 `AnimationClip.SampleAnimation(go, t)` 手动逐帧反向
    ///    采样。SampleAnimation 是 Unity 公开 API，会直接读 clip 的 m_PPtrCurves 在 time=t 的离散
    ///    值并写入 SpriteRenderer.sprite，反向播放绝对可靠。
    ///
    /// 3) MindControlled.Setup 把 base SpriteRenderer.enabled=false，玩家看到的是 overlay 子物体
    ///    sprite。所以反向采样 base sprite 后必须每帧 ForceSyncOverlayNow 把 overlay sprite 同步过去。
    ///
    /// 流程：
    ///   Stage 1（已在 Update 中跑完龙眼+正向死亡动画——见 Spawn 内 PlayForwardDeathOnce 协程）
    ///   Stage 2 这里：从 deadLen → 0 手动 SampleAnimation 反向（×0.5 速度，总耗时 2×deadLen）
    ///   Stage 3：Animator.Play(move/idle) + 多帧 sync + 解冻 MindControlled
    /// </summary>
    private IEnumerator Stage2_ReverseDeath_ThenStage3()
    {
        Transform tr = _targetTransform;
        float deadLen = _deadClipLen;
        AnimationClip clip = _deadClip;

        // === v10.2 致命 BUG 修复 ===
        // 用户反馈："播放完死亡动画就不动了，没有后续的逆死亡动画和切换为行走动画"。
        // 根因：Stage 1 末尾这里**曾经**写 `Destroy(_canvas.gameObject)`，而 _canvas.gameObject **就是
        //       this.gameObject**（ReviveBossEffect 这个 MonoBehaviour 挂载的 GO）。一旦销毁宿主 GO，
        //       Unity 协程调度器在下一次 yield 返回时会发现宿主已销毁 → **立刻终止当前协程**，
        //       Stage 2 反向采样 + Stage 3 切 move/idle + 解冻 MindControlled 全部不会执行。
        //       结果就是用户看到的：Boss 停在 dead 最后一帧（Animator.enabled=false 保留 sprite），
        //       MindControlled.FixedUpdate 因 _frozenForRevive=true 短路 → 永久死亡 pose。
        //
        // 修复：**只销毁 UI 子对象（Mask/EnergyField/DragonEye），保留宿主 GO 与本协程**。
        //       等 Stage 3 全部完成（解冻 MindControlled 之后）再 Destroy(gameObject) 自我清理。
        if (_canvas != null)
        {
            // 销毁所有 UI 子对象，但保留宿主 GO（带 Canvas 组件也无所谓，没有子节点不会绘制任何东西）
            for (int i = _canvas.transform.childCount - 1; i >= 0; i--)
            {
                Destroy(_canvas.transform.GetChild(i).gameObject);
            }
        }

        if (tr == null) { Destroy(gameObject); yield break; }
        Animator ani = tr.GetComponent<Animator>();
        if (ani == null || ani.runtimeAnimatorController == null)
        {
            UnfreezeMindControl(tr);
            Destroy(gameObject);
            yield break;
        }

        // 缓存 MindControlled 引用，便于每帧 ForceSyncOverlayNow
        MindControlled mc = tr.GetComponent<MindControlled>();
        GameObject go = tr.gameObject;

        // —— Stage 2：反向播 dead clip（**手动 SampleAnimation 逐帧反向**）——
        // v10 关键：**完全绕开 Animator**，避免 `speed=-1` 在 PPtr 动画上不更新 sprite 的 Unity 已知问题。
        const float REVERSE_TOTAL_FACTOR = 2f;  // 总耗时 = deadLen × 2（视觉慢一倍，让玩家看清反向）

        if (clip != null && deadLen > 0f)
        {
            // 关闭 Animator 避免它在我们 SampleAnimation 之后再覆写 sprite
            // （Animator.enabled=false 时 Update 不跑，SpriteRenderer.sprite 保留我们的写入值）
            ani.enabled = false;

            float reverseDuration = deadLen * REVERSE_TOTAL_FACTOR;
            float wallClock = 0f;
            // 先采样一次起点（最后一帧）确保起始 sprite 正确
            clip.SampleAnimation(go, deadLen);
            if (mc != null) mc.ForceSyncOverlayNow();

            while (wallClock < reverseDuration && tr != null)
            {
                yield return null;
                if (tr == null) { Destroy(gameObject); yield break; }
                wallClock += Time.deltaTime;
                // t 从 deadLen 线性递减到 0
                float t = Mathf.Lerp(deadLen, 0f, Mathf.Clamp01(wallClock / reverseDuration));
                clip.SampleAnimation(go, t);
                if (mc != null) mc.ForceSyncOverlayNow();
            }
            // 重新启用 Animator，让后续 Play(move) 生效
            ani.enabled = true;
        }
        if (ani == null) { Destroy(gameObject); yield break; }
        // 恢复正向播放速度（v9 残留：手动反向后 speed 没动过，这里只做兜底）
        ani.speed = 1f;
        int deadHash = Animator.StringToHash("dead");
        ani.ResetTrigger(deadHash);

        // —— Stage 3：切到行走/待机 state，并清理 Animator 参数 ——
        // 兼容两个 Boss 的实际 state 名（来自 controller 文件分析）：
        //   batboss.controller: "move", "idle"  （参数 ismove，默认 entry 已经是 idle）
        //   Shoom.controller  : "move", "move 0", "Idel" (拼写错误)  （参数 ismove）
        int moveHash   = Animator.StringToHash("move");
        int move0Hash  = Animator.StringToHash("move 0");
        int idelHash   = Animator.StringToHash("Idel");
        int idleHash   = Animator.StringToHash("idle");

        // 先把所有可能让 dead 重新被触发的输入参数清掉
        TrySetBool(ani, "ismove", false);
        TryResetTrigger(ani, "isattack");
        TryResetTrigger(ani, "issummon");

        bool played = false;
        if (ani.HasState(0, moveHash))       { ani.Play(moveHash,  0, 0f); played = true; }
        else if (ani.HasState(0, move0Hash)) { ani.Play(move0Hash, 0, 0f); played = true; }
        else if (ani.HasState(0, idelHash))  { ani.Play(idelHash,  0, 0f); played = true; }
        else if (ani.HasState(0, idleHash))  { ani.Play(idleHash,  0, 0f); played = true; }
        ani.Update(0f);
        if (mc != null) mc.ForceSyncOverlayNow();

        // 再多 sync 3 帧，确保 Animator 把 move 的首帧 sprite 真正写入 base SpriteRenderer
        // 后 overlay 已经追上。这一段都在 _frozenForRevive=true 下进行，MindControlled 不会干扰。
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            if (ani == null) { Destroy(gameObject); yield break; }
            if (mc != null) mc.ForceSyncOverlayNow();
        }

        if (!played)
        {
            Debug.LogWarning($"[ReviveBossEffect] {tr.name} 找不到 move/Idel/idle state，Animator 可能停在 dead pose");
        }

        // —— 解冻 MindControlled ——
        // 解冻后 MindControlled.FixedUpdate 接管 SetBool("ismove", ...) + SyncOverlayFrameIfChanged，
        // 此时 Animator 已经稳定播 move/idle，overlay 也已被强同步到 move 首帧，视觉无缝衔接。
        UnfreezeMindControl(tr);

        // v10.2：Stage 3 全部完成后，自我销毁宿主 GO（之前在 Stage 2 开头销毁会杀死本协程）。
        Destroy(gameObject);
    }

    private static void UnfreezeMindControl(Transform tr)
    {
        if (tr == null) return;
        MindControlled mc = tr.GetComponent<MindControlled>();
        if (mc != null) mc.SetReviveFreeze(false);
    }

    private static void TrySetBool(Animator ani, string param, bool value)
    {
        if (ani == null || ani.parameters == null) return;
        foreach (var p in ani.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == param)
            {
                ani.SetBool(param, value);
                return;
            }
        }
    }
    private static void TryResetTrigger(Animator ani, string param)
    {
        if (ani == null || ani.parameters == null) return;
        foreach (var p in ani.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == param)
            {
                ani.ResetTrigger(param);
                return;
            }
        }
    }

    /// <summary>解析 Boss dead clip（找名字含 "dead" 的 AnimationClip）。v10 起返回引用，
    /// 用于 Stage 2 手动 `AnimationClip.SampleAnimation(go, t)` 反向逐帧采样。</summary>
    private static AnimationClip ResolveDeadClip(Transform tr)
    {
        Animator ani = tr.GetComponent<Animator>();
        if (ani == null || ani.runtimeAnimatorController == null) return null;
        var all = ani.runtimeAnimatorController.animationClips;
        if (all == null) return null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name.ToLower().Contains("dead"))
                return all[i];
        }
        return null;
    }

    private static void EnsureResources()
    {
        if (_sharedEnergyFrames != null && _sharedEyeSprite != null && _sharedMaskTex != null) return;

        if (_sharedMaskTex == null)
        {
            _sharedMaskTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _sharedMaskTex.SetPixel(0, 0, Color.white);
            _sharedMaskTex.Apply();
            _sharedMaskTex.name = "ReviveMaskTex_White1x1";
        }

        if (_sharedEnergyFrames == null)
        {
            _sharedEnergyFrames = new Sprite[ENERGY_FRAME_COUNT];
            int loaded = 0;
            for (int i = 0; i < ENERGY_FRAME_COUNT; i++)
            {
                Sprite s = Resources.Load<Sprite>($"Effects/ReviveEnergy_{i}");
                if (s != null) { _sharedEnergyFrames[i] = s; loaded++; }
            }
            if (loaded < ENERGY_FRAME_COUNT && !_resourceMissingLogged)
            {
                Debug.LogWarning($"[ReviveBossEffect] Resources/Effects/ReviveEnergy_0..11 仅找到 {loaded}/{ENERGY_FRAME_COUNT} 帧。");
                _resourceMissingLogged = true;
                if (loaded == 0) _sharedEnergyFrames = null;
            }
        }

        if (_sharedEyeSprite == null)
        {
            _sharedEyeSprite = Resources.Load<Sprite>("Effects/ReviveDragonEye");
            if (_sharedEyeSprite == null && !_resourceMissingLogged)
            {
                Debug.LogWarning("[ReviveBossEffect] Resources/Effects/ReviveDragonEye 未找到。");
                _resourceMissingLogged = true;
            }
        }
    }
}
