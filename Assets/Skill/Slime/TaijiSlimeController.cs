using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 太极史莱姆 —— 当玩家**同时**持有「阴史莱姆」与「阳史莱姆」时自动替换两者。
///
/// 规格（逐条对应实现）：
///   1. 对最近的敌人连续释放 3 次太极印，类似威压从敌人头顶压制，
///      正在被攻击的敌人无法移动。               → <see cref="AttackSeal"/>
///   2. 第一种攻击结束后，太极史莱姆拆分为阴、阳史莱姆，
///      二者同时对周围发射射弹（一黑一白）。      → <see cref="AttackSplitVolley"/>
///   3. 上述两种攻击方式轮流切换使用，拆分和重组有动画，
///      动画速度与冷却时间绑定。→ <see cref="RunCycle"/> + AnimScale
///   4. 数量决定太极印的数量和每次发射的射弹数量。 → sealCount / volleyCount
///
/// 挂载位置：由<see cref="TaijiSlimeWatcher"/> 在检测到两个技能同时存在时，
/// 挂到玩家对象上；任一技能消失（被遗忘/替换）时自动解除并还原两个独立技能。
///
/// 「动画速度与冷却绑定」的具体做法：
///   AnimScale = CDtime / REFERENCE_CD。CD 越短 → AnimScale 越小 → 合体/分解/连击
///   的每一段耗时同比缩短，保证"整套演出必须在一个 CD 周期内演完"，
///   否则高冷却缩减下会出现上一轮还在合体、下一轮已经该开火的错帧。
///   同时用 Mathf.Clamp 限制下限，避免 CD 极短时动画快到看不清。
/// </summary>
public class TaijiSlimeController : MonoBehaviour
{
    /// <summary>参考冷却：动画时长以此为基准做等比缩放。</summary>
    private const float REFERENCE_CD = 5f;

    [Header("引用（由 Watcher 注入）")]
    public SkillYinYangSlime yinSkill;
    public SkillYinYangSlime yangSkill;
    public Transform owner;

    [Header("太极本体")]
    [Tooltip("合体后太极本体的世界尺寸（米）。与源图分辨率无关。")]
    public float taijiWorldSize = 1.5f;
    [Tooltip("太极本体的自转速度（度/秒）。")]
    public float taijiSpinSpeed = 150f;
    [Tooltip("太极本体悬浮高度。")]
    public float taijiHeight = 1.15f;

    [Header("太极印")]
    [Tooltip("单轮太极印的基础释放次数（规格为 3 次）。")]
    public int baseSealCount = 3;
    [Tooltip("每次太极印之间的间隔（会被 AnimScale 缩放）。")]
    public float sealInterval = 0.22f;
    [Tooltip("太极印伤害倍率（相对技能 damage）。印记是单点重击，倍率高于蝌蚪。")]
    public float sealDamageMul = 2.5f;

    [Header("合体 / 分解动画")]
    public float mergeDuration = 0.55f;
    public float splitDuration = 0.45f;

    private GameObject _taijiBody;
    private SpriteRenderer _taijiSr;
    private Coroutine _loop;
    private bool _mergedVisual;
    /// <summary>把源图换算到 taijiWorldSize 米的基准缩放（动画倍率叠在它之上）。</summary>
    private float _taijiBaseScale = 1f;

    /// <summary>当前动画时间缩放：与冷却绑定。</summary>
    private float AnimScale
    {
        get
        {
            float cd = yinSkill != null ? yinSkill.CDtime : REFERENCE_CD;
            if (cd <= 0.01f) cd = REFERENCE_CD;
            // 下限 0.55：形态转换动画现在只在切换时播一次（不再每次攻击都播），
            // 因此不需要为了塞进冷却而把它压得太快—— 保留足够时长让演出看得清。
            // 上限 1.6 防止长 CD 时演出拖沓。
            return Mathf.Clamp(cd / REFERENCE_CD, 0.55f, 1.6f);
        }
    }

    /// <summary>
    /// 有效冷却：取两个技能中较短的一个（玩家把任一支升了 CD 都该更快），
    /// 并强制不低于 <see cref="SlimeFactionAssets.MIN_CDTIME"/>。
    /// </summary>
    private float EffectiveCD
    {
        get
        {
            float a = yinSkill != null ? yinSkill.CDtime : REFERENCE_CD;
            float b = yangSkill != null ? yangSkill.CDtime : REFERENCE_CD;
            return SlimeFactionAssets.ClampCD(Mathf.Min(a, b));
        }
    }

    /// <summary>
    /// 有效数量：取两个技能 number 的较大值。
    /// 规格说升级卡共享，Watcher 会把升级同步到两支，正常情况下二者相等；
    /// 取Max 是为了兼容"玩家先单独升了阴、后来才学到阳"的历史存档。
    /// </summary>
    private int EffectiveNumber
    {
        get
        {
            int a = yinSkill != null ? yinSkill.number : 1;
            int b = yangSkill != null ? yangSkill.number : 1;
            return Mathf.Max(1, Mathf.Max(a, b));
        }
    }

    private int EffectiveDamage
    {
        get
        {
            int a = yinSkill != null ? yinSkill.damage : 1;
            int b = yangSkill != null ? yangSkill.damage : 1;
            return Mathf.Max(1, Mathf.Max(a, b));
        }
    }

    private void OnEnable()
    {
        _loop = StartCoroutine(RunCycle());
    }

    private void OnDisable()
    {
        if (_loop != null) { StopCoroutine(_loop); _loop = null; }
        Restore();
    }

    /// <summary>解除接管：还原两个独立技能的自主开火，销毁太极本体。</summary>
    public void Restore()
    {
        if (yinSkill != null)
        {
            yinSkill.IsSuppressedByTaiji = false;
            if (yinSkill.Fish != null)
            {
                yinSkill.Fish.SetMergeProgress(0f, Vector3.zero);
                yinSkill.Fish.SetVisible(true);
            }
        }
        if (yangSkill != null)
        {
            yangSkill.IsSuppressedByTaiji = false;
            if (yangSkill.Fish != null)
            {
                yangSkill.Fish.SetMergeProgress(0f, Vector3.zero);
                yangSkill.Fish.SetVisible(true);
            }
        }
        if (_taijiBody != null) { Destroy(_taijiBody); _taijiBody = null; _taijiSr = null; }
        _mergedVisual = false;
    }

    private void EnsureTaijiBody()
    {
        if (_taijiBody != null) return;

        // 【关键】太极本体挂在场景根，不能作为 Player 的子物体：
        //   本控制器是 AddComponent 到 Player 上的，而 Player 靠 localScale.x 取负
        //   翻转朝向。若把太极做成子物体，玩家往左走就会整体镜像 + 自转方向反转，
        //   "阴阳流转"变成来回抽搐。位置由 Update 每帧对齐 TaijiCenter。
        _taijiBody = new GameObject("TaijiSlimeBody");
        _taijiBody.transform.position = TaijiCenter;

        var sprGo = new GameObject("Sprite");
        sprGo.transform.SetParent(_taijiBody.transform, false);
        _taijiSr = sprGo.AddComponent<SpriteRenderer>();
        _taijiSr.sprite = SlimeFactionAssets.TaijiBody;
        _taijiSr.sortingOrder = 89;
        _taijiBaseScale = SlimeFactionAssets.WorldSizeScale(_taijiSr.sprite, taijiWorldSize);
        sprGo.transform.localScale = Vector3.one * _taijiBaseScale;

        SetTaijiVisible(false);
    }

    private void SetTaijiVisible(bool v)
    {
        if (_taijiSr != null) _taijiSr.enabled = v;
    }

    private Vector3 TaijiCenter =>
        (owner != null ? owner.position : transform.position) + new Vector3(0f, taijiHeight, 0f);

    private void Update()
    {
        if (_taijiBody == null) return;

        _taijiBody.transform.position = TaijiCenter;
        // 太极本体只在合体后可见，自转体现"阴阳流转"
        if (_mergedVisual)
            _taijiBody.transform.rotation =
                Quaternion.Euler(45f, 0f, Time.time * taijiSpinSpeed %360f);
    }

    /// <summary>
    /// 主循环：合体 → 太极印连击 → 分解 → 双色齐射 → （回到合体）…
    ///
    /// 之所以把"合体"放在太极印之前：太极印是太极形态的招式，
    /// 分解后的双色齐射是阴阳形态的招式。这样一个完整周期内
    /// 玩家能同时看到"合体演出 + 印记压制 + 分解演出 + 双色弹幕"，
    /// 正好对应规格里"两种攻击方式轮流切换，拆分和重组有动画"。
    /// </summary>
    /// <summary>
    /// 主循环：**每种形态连续攻击若干次后**才切换，而不是一次一换。
    ///
    /// ┌ 合体演出 ──→ 太极印 ×N ─┐
    /// └─ 双色齐射 ×N ←── 分解演出 ┘
    ///
    /// 【2026-08改动】旧版是"合体→印→分解→齐射"每轮全走一遍，
    ///   等于每次攻击都夹着一段合体/分解动画。冷却降下来之后（可低至 1.5s）
    ///   演出根本来不及看清，画面上就是太极不停地聚散抽搐。
    ///
    ///   现在改为：进入某形态时只播一次转换动画，然后在该形态下连续攻击
    ///   <see cref="AttacksPerMode"/> 次，才切到另一形态。
    ///   次数 = 目标时长 ÷ 冷却，因此"两次转换演出之间的实际间隔"恒定在
    ///   约 <see cref="TargetModeSeconds"/> 秒，无论玩家把冷却压到多低都不会再抽搐。
    /// </summary>
    private IEnumerator RunCycle()
    {
        // 接管：抑制两支技能各自开火
        if (yinSkill != null) yinSkill.IsSuppressedByTaiji = true;
        if (yangSkill != null) yangSkill.IsSuppressedByTaiji = true;

        EnsureTaijiBody();

        // 首次启动给一点前摇，避免和开局其它技能挤在同一帧
        yield return new WaitForSeconds(0.6f);

        bool sealMode = true;   // true = 太极（印）形态，false = 阴阳（齐射）形态

        while (true)
        {
            if (yinSkill == null || yangSkill == null) yield break;

            float scale = AnimScale;
            // 本轮次数在进入形态时**锁定**一次。若每次攻击都重算，
            // 玩家中途升了冷却会让当前这轮次数忽然变化，节奏感被打乱。
            int attacks = AttacksPerMode;

            if (sealMode)
            {
                // ── 进入太极形态：播一次合体演出 ──
                yield return MergeAnim(mergeDuration * scale);

                for (int i = 0; i < attacks; i++)
                {
                    if (yinSkill == null || yangSkill == null) yield break;

                    yield return AttackSeal();

                    // 最后一次攻击后不再等满冷却 —— 直接进入分解演出，
                    // 因为演出本身就占时间，再等一轮会显得"卡住不动"
                    if (i < attacks - 1)
                        yield return new WaitForSeconds(SealModeGap(scale));
                }
                sealMode = false;
            }
            else
            {
                // ── 进入阴阳形态：播一次分解演出 ──
                yield return SplitAnim(splitDuration * scale);

                for (int i = 0; i < attacks; i++)
                {
                    if (yinSkill == null || yangSkill == null) yield break;

                    AttackSplitVolley();

                    if (i < attacks - 1)
                        yield return new WaitForSeconds(EffectiveCD);
                }
                sealMode = true;
            }
        }
    }

    /// <summary>
    /// 同一形态内连续攻击的次数：由「目标形态持续时长 ÷ 冷却」反推，范围 2~10。
    ///
    /// 为什么用除法，而不是"冷却越短次数越多"的线性插值：
    ///   线性插值下 形态持续时长 = 次数 × 冷却，两个因子都在变 → 时长剧烈波动。
    ///   实测线性方案：CD 5.0s → 约 10s，CD 3.25s → 约 19.5s，CD 1.5s → 约 15s，
    ///   玩家感受到的节奏忽长忽短，反而比一次一换更乱。
    ///   改成 <c>次数 = 目标时长 ÷ 冷却</c> 后两者乘积恒等，
    ///   "看到一次合体演出的间隔"稳定在约 <see cref="TargetModeSeconds"/> 秒。
    ///
    /// 实际取值（MIN_CDTIME = 1.5、初始 CD = 5）：
    ///   CD 5.0s → 2 次 · CD 4.0s → 3 次 · CD 3.0s → 4 次
    ///   CD 2.4s → 5 次 · CD 2.0s → 6 次 · CD 1.7s → 7 次 · CD 1.5s → 8 次
    /// 上限 10 是为将来若再放宽冷却下限预留的余量。
    /// </summary>
    private int AttacksPerMode
    {
        get
        {
            float cd = EffectiveCD;
            if (cd <= 0.01f) cd = REFERENCE_CD;
            int n = Mathf.RoundToInt(TargetModeSeconds / cd);
            return Mathf.Clamp(n, MinAttacksPerMode, MaxAttacksPerMode);
        }
    }

    /// <summary>
    /// 单个形态的目标持续时长（秒），也就是"两次形态转换演出之间的间隔"。
    /// 12s 的依据：太短（&lt;6s）会退化成旧版那种不停聚散抽搐；
    /// 太长（&gt;20s）玩家几乎看不到合体/分解演出，动画就白做了。
    /// </summary>
    private const float TargetModeSeconds = 12f;

    private const int MinAttacksPerMode = 2;
    private const int MaxAttacksPerMode = 10;

    /// <summary>
    /// 太极印形态下两次攻击之间的等待。
    /// AttackSeal 本身要花 sealCount × sealInterval 秒，这里把它扣掉，
    /// 保证"每次太极印连击的起始间隔 ≈ 冷却"，DPS 不会因为演出变长而缩水。
    /// </summary>
    private float SealModeGap(float scale)
    {
        int sealCount = baseSealCount + Mathf.Max(0, (EffectiveNumber - 1) / 2);
        sealCount = Mathf.Clamp(sealCount, 1, 12);
        float consumed = sealCount * sealInterval * scale;
        return Mathf.Max(0.25f, EffectiveCD - consumed);
    }

    private IEnumerator MergeAnim(float dur)
    {
        YinYangFish fy = yinSkill != null ? yinSkill.Fish : null;
        YinYangFish fa = yangSkill != null ? yangSkill.Fish : null;

        SetTaijiVisible(false);
        _mergedVisual = false;

        if (fy != null) fy.SetVisible(true);
        if (fa != null) fa.SetVisible(true);

        AudioManager.PlaySfx(AudioManager.SfxKey.SporeCast);

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            // SmoothStep：起步慢、中段快、收尾稳，像被吸进漩涡
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Vector3 c = TaijiCenter;
            if (fy != null) fy.SetMergeProgress(k, c);
            if (fa != null) fa.SetMergeProgress(k, c);
            yield return null;
        }

        // 合体完成：隐藏两条鱼，显示太极本体
        if (fy != null) fy.SetVisible(false);
        if (fa != null) fa.SetVisible(false);
        SetTaijiVisible(true);
        _mergedVisual = true;

        // 合体成功的一个短促"实体化"缩放弹跳
        if (_taijiSr != null)
        {
            float bt = 0f, bd = 0.12f;
            while (bt < bd)
            {
                bt += Time.deltaTime;
                float k = bt / bd;
                float s = _taijiBaseScale * Mathf.Lerp(1.35f, 1f, k);
                _taijiSr.transform.localScale = Vector3.one * s;
                yield return null;
            }
            _taijiSr.transform.localScale = Vector3.one * _taijiBaseScale;
        }
    }

    private IEnumerator SplitAnim(float dur)
    {
        YinYangFish fy = yinSkill != null ? yinSkill.Fish : null;
        YinYangFish fa = yangSkill != null ? yangSkill.Fish : null;

        // 太极先"炸开"一下再消失
        if (_taijiSr != null)
        {
            float bt = 0f, bd = 0.1f;
            while (bt < bd)
            {
                bt += Time.deltaTime;
                _taijiSr.transform.localScale =
                    Vector3.one * (_taijiBaseScale * Mathf.Lerp(1f, 1.4f, bt / bd));
                yield return null;
            }
        }
        SetTaijiVisible(false);
        _mergedVisual = false;
        if (_taijiSr != null) _taijiSr.transform.localScale = Vector3.one * _taijiBaseScale;

        if (fy != null) fy.SetVisible(true);
        if (fa != null) fa.SetVisible(true);

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / dur));
            Vector3 c = TaijiCenter;
            // 从1 回到 0：合体进度反向播放= 分解
            if (fy != null) fy.SetMergeProgress(1f - k, c);
            if (fa != null) fa.SetMergeProgress(1f - k, c);
            yield return null;
        }

        if (fy != null) fy.SetMergeProgress(0f, Vector3.zero);
        if (fa != null) fa.SetMergeProgress(0f, Vector3.zero);
    }

    /// <summary>
    /// 攻击方式 1：太极印连击。
    /// 数量升级增加印记次数（baseSealCount + (number-1)/2，避免和射弹数量同步暴涨）。
    /// </summary>
    private IEnumerator AttackSeal()
    {
        if (yinSkill == null) yield break;

        // number 决定太极印数量：基础 3 次，number 每 +2 追加 1 次印
        int sealCount = baseSealCount + Mathf.Max(0, (EffectiveNumber - 1) / 2);
        sealCount = Mathf.Clamp(sealCount, 1, 12);

        Attribute attr = owner != null ? owner.GetComponent<Attribute>() : null;
        Transform enemyLayer = SkillYinYangSlime.ResolveEnemyLayer();
        int dmg = Mathf.Max(1, Mathf.RoundToInt(EffectiveDamage * sealDamageMul));
        float interval = sealInterval * AnimScale;

        for (int i = 0; i < sealCount; i++)
        {
            // 每一印都重新取最近敌人：目标死了就自动转火，不会对着空气砸
            Transform target = yinSkill.FindNearestEnemy();
            if (target == null)
            {
                // 场上没敌人就提前结束这段，别浪费整个 CD 周期
                yield break;
            }

            GameObject go = new GameObject("TaijiSeal");
            var sprGo = new GameObject("Sprite");
            sprGo.transform.SetParent(go.transform, false);
            sprGo.AddComponent<SpriteRenderer>();

            TaijiSeal seal = go.AddComponent<TaijiSeal>();
            seal.Fire(target, dmg, attr, enemyLayer);

            yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>
    /// 攻击方式 2：分解后阴阳同时对周围齐射（一黑一白）。
    /// number 决定每条鱼每轮的射弹数量。
    ///
    /// 与太极印一致：范围内没有敌人就不打空枪（targets 为空直接返回）。
    /// </summary>
    private void AttackSplitVolley()
    {
        if (yinSkill == null || yangSkill == null) return;

        var targets = yinSkill.GetEnemiesInRangeSorted();
        if (targets.Count == 0) return;

        Attribute attr = owner != null ? owner.GetComponent<Attribute>() : null;
        Transform enemyLayer = SkillYinYangSlime.ResolveEnemyLayer();

        int volleyCount = Mathf.Max(1, EffectiveNumber);
        int dmg = EffectiveDamage;
        float maxTravel = yinSkill.BulletMaxTravel;

        AudioManager.PlaySfx(AudioManager.SfxKey.SporeCast);

        if (yinSkill.Fish != null)
            yinSkill.Fish.FireVolley(volleyCount, dmg,
                yinSkill.speed > 0.1f ? yinSkill.speed : 12f,
                yinSkill.lifetime > 0.1f ? yinSkill.lifetime : 2.2f,
                yinSkill.pass, yinSkill.BulletTemplate, attr, enemyLayer,
                targets, maxTravel, yinSkill.shotInterval);

        if (yangSkill.Fish != null)
            yangSkill.Fish.FireVolley(volleyCount, dmg,
                yangSkill.speed > 0.1f ? yangSkill.speed : 12f,
                yangSkill.lifetime > 0.1f ? yangSkill.lifetime : 2.2f,
                yangSkill.pass, yangSkill.BulletTemplate, attr, enemyLayer,
                targets, maxTravel, yangSkill.shotInterval);
    }
}
