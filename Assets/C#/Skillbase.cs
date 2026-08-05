using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Skillbase : MonoBehaviour
{
    public string Skillname;//技能名
    public float CDtime;//CD时间,固定参数
    public float CDkey;//CD间隔，冷却键，每秒恢复的动态量，二者概念相同
    public int damage;//技能伤害
    public int level;//技能等级
    public float lifetime;//生命周期
    public int pass;//穿透
    public float speed;//子弹速度
    public int number;//子弹数量
    public GameObject bullet;//子弹物体
    public float size;//子弹大小
    public float interval;//间隔时间
    public GameObject player;
    public float angel;//旋转角度
    public bool isfaceenemy;//是否朝向最近敌人
    public Sprite icon; 

    // ============================================================
    //  攻击范围（2026-08 新增）
    // ------------------------------------------------------------
    //  语义：范围内没有存活敌人时**不释放技能、也不消耗冷却**。
    //
    //  为什么做在 Skillbase 而不是新建一个 SkillFireball 子类：
    //    火球术在项目里没有专属脚本类，它直接挂的就是 Skillbase
    //    （参见 ChoiceUI.IsFireballSizeOrSpeedUpgrade 的注释：靠 Skillname 识别）。
    //    要改成子类就得动场景/预制体上的组件类型，风险远大于收益。
    //    因此把「攻击范围」做成基类的通用能力，默认 0 = 不限制，
    //    对现有所有技能（冰锥/暗齿轮/…）零行为变化。
    //
//  为什么字段名叫 castRange 而不是 attackRadius：
    //    风箭 / 孢子领域 / 血族血统 / 命途:寄生 / 地狱火 / 阴阳史莱姆 这 6 个子类
    //    **各自都已声明了 public float attackRadius**。若基类再加一个同名字段，
    // 会在这 6 个类里造成字段隐藏（CS0108）：子类代码读的是自己的，
    //    基类逻辑读的是基类的，两个值互不相干；Unity Inspector 还会出现重名字段、
    //    序列化归属含混。改名可以彻底避开这一整类问题，且不必改动任何子类。
    // ============================================================

    [Header("攻击范围（0 = 不限制）")]
    [Tooltip("范围内没有敌人时不释放技能。0 表示不做限制（全图索敌）。\n" +
      "注意：已自带 attackRadius 的技能（风箭/孢子/血族/寄生/地狱火/阴阳史莱姆）走各自的字段，与此无关。")]
    public float castRange = 0f;

    /// <summary>火球术的技能名（与 getnewskill_Hellfire.FireballSkillName 保持一致）。</summary>
    public const string FireballSkillName = "火球术";

    /// <summary>
    /// 火球术的默认攻击范围。与地狱火 <see cref="SkillHellfire.attackRadius"/> 取值相同（40），
    /// 使"火球术 → 地狱火"这条进化路线的索敌范围保持连续，玩家不会在进化前后感到突变。
    /// </summary>
    public const float FireballDefaultAttackRadius = 40f;

    /// <summary>
    /// 实际生效的施法范围。
    /// Inspector 显式配了 <see cref="castRange"/> 就用它；没配（0）时，火球术回退到
    /// <see cref="FireballDefaultAttackRadius"/>，其余技能保持 0（不限制）。
    /// 这样无需改动任何场景/预制体即可让火球术获得范围限制。
    /// </summary>
    protected virtual float EffectiveCastRange
    {
    get
  {
   if (castRange > 0f) return castRange;
         if (Skillname == FireballSkillName) return FireballDefaultAttackRadius;
            return 0f;
        }
    }

private static Transform s_enemyLayerCacheSB;

    /// <summary>
    /// 施法范围内是否存在可打击的敌人。范围为 0（不限制）时恒为 true。
    ///
  /// 排除项与风箭/地狱火的索敌口径一致：死亡、亡者领域友军、已占领营地。
    /// 性能上先算平方距离再 GetComponent，避免在几百敌人时每次施法都做上百次
    /// GetComponent（该方法在无目标时会被反复调用）。
    /// </summary>
    protected bool HasEnemyInCastRange()
    {
        float r = EffectiveCastRange;
     if (r <= 0f) return true;   // 不限制
        if (player == null) return true;       // 拿不到玩家就不拦，避免技能被误禁

        if (s_enemyLayerCacheSB == null)
            s_enemyLayerCacheSB = GameObject.Find("enemylayer")?.transform;
        Transform layer = s_enemyLayerCacheSB;
        if (layer == null) return true;        // 场景还没就绪，同样不拦

        Vector3 center = player.transform.position;
        float rSq = r * r;
        int cnt = layer.childCount;

        for (int i = 0; i < cnt; i++)
        {
            Transform t = layer.GetChild(i);
 if (t == null) continue;

            Vector3 d = t.position - center; d.y = 0f;
      if (d.sqrMagnitude > rSq) continue;

       enemy en = t.GetComponent<enemy>();
    if (en == null) continue;
     if (en.health <= 0 || en.rolestate == enemy.state.dead) continue;
            if (en._mindControlledFlag) continue;          // 亡者领域友军
            Camp camp = en as Camp;
       if (camp != null && camp.IsCaptured) continue; // 已占领的友方营地

          return true;
        }
   return false;
    }

    /// <summary>场景重载时清空 enemylayer 静态缓存（由 enemy.ResetSceneCaches 统一调用）。</summary>
    public static void ResetEnemyLayerCache() { s_enemyLayerCacheSB = null; }

  void FixedUpdate()
    {
        CDkey += Time.fixedDeltaTime;
        if (CDkey > CDtime )
        {
            CDkey = CDtime;
        }
    }

    public virtual IEnumerator Useskill()//使用技能
    {
        // 攻击范围判定（火球术等配置了 castRange 的技能）：
        // 范围内没有敌人时**不释放、也不消耗冷却**，而是把 CDkey 回退一点点做重试节流。
   // 直接 return 且保持 CDkey 满值会导致 Player 每帧都来调一次本方法
        // （每次都要全场搜敌），敌人多时是可观的无谓开销。
        if (!HasEnemyInCastRange())
        {
            CDkey = Mathf.Max(0f, CDtime - RangeRetryDelay);
   yield break;
        }

        CDkey = 0;
        // 发射音效：火球/冰类播放专属音，其他技能默认不播放
        PlayCastSfx();
        for ( int i = 0; i < number; i++ )
        { 
   GameObject newbullet = Instantiate( bullet ,player.transform.position,Quaternion.Euler(new Vector3(0,0,angel)));//创建子弹
       Bulletbase n =newbullet.GetComponent<Bulletbase>();
            n.fatherskill = this;
            n.GetFather();
            n.getrole();
            n.cango = true;
            yield return new WaitForSeconds(interval);
     }
    }

    /// <summary>范围内无敌人时的重试间隔（秒）。</summary>
    protected const float RangeRetryDelay = 0.12f;

    // ── 命中音"每轮施法只响一次"的节流状态 ──
    /// <summary>施法轮次序号：每调用一次 PlayCastSfx（= 释放一次技能）自增。</summary>
    private int _castSerial;
    /// <summary>上一次播命中音所属的施法轮次。</summary>
    private int _hitSfxSerial = -1;
    private float _lastHitSfxTime = -99f;
    /// <summary>同一轮施法内命中音的兜底冷却：持续型 AoE 超过这个时长可以再响一次。</summary>
    private const float HitSfxSameCastCooldown = 0.5f;

    /// <summary>
    /// 子弹命中时调用的命中音效；按技能名派发，子类可 override 自定义。
    ///
    /// 【2026-08修复】命中音与"命中数量"彻底解绑：
    ///   Bulletbase 是**每颗子弹命中每个敌人**都调一次本方法。地狱火（多枚三叉戟
    ///   连续落地 + 每枚 AoE 多目标）和火球（多弹 + 穿透）会在一帧里调用几十次，
    ///   而 FireballHit 的限流只有 0.10s，于是命中音几乎不间断地响 —— 就是玩家
    ///   反馈的"不知道是地狱火还是火球术的音效会一直放"。
    ///   现在改为**一次施法只播一次命中音**（与弹数/命中数无关）；
    ///   持续型 AoE 若同一轮超过 HitSfxSameCastCooldown 仍会补一声，避免长技能全程静音。
    /// </summary>
    public virtual void PlayHitSfx()
    {
        if (string.IsNullOrEmpty(Skillname)) return;

        // 本轮已经响过 → 直接静音（这正是"和数量无关"的关键）
        if (_hitSfxSerial == _castSerial &&
            Time.time - _lastHitSfxTime < HitSfxSameCastCooldown) return;
        _hitSfxSerial   = _castSerial;
        _lastHitSfxTime = Time.time;

        if (Skillname.Contains("火球") || Skillname.Contains("地狱火"))
            AudioManager.PlaySfx(AudioManager.SfxKey.FireballHit);
        else if (Skillname.Contains("冰"))
            AudioManager.PlaySfx(AudioManager.SfxKey.IceHit);
        else if (Skillname.Contains("血族") || Skillname.Contains("血统"))
            AudioManager.PlaySfx(AudioManager.SfxKey.BloodHit);
        else if (Skillname.Contains("风箭") || Skillname.Contains("风之形") || Skillname.Contains("飓风"))
            AudioManager.PlaySfx(AudioManager.SfxKey.WindHit);
        // 其他技能让 Bulletbase 已经触发的通用 Hit 覆盖
    }

    /// <summary>
    /// 发射音效；按技能名派发到贴合各自元素主题的音效，子类可 override 自定义。
    ///
    /// 注意：只有走 Skillbase.Useskill() 的技能会自动调用本方法；
    /// 所有 override 了 Useskill() 的子类需要在自己的发射时机显式调用 PlayCastSfx()。
    /// 各子类都是在"整轮发射前"调用一次（循环外），所以发射音天然与弹数无关。
    ///
    /// 音效的最小播放间隔由 AudioManager.SfxIntervalOverride 按 key 统一管理，
    /// 短 CD / 多弹连发技能不会因为一帧内多次调用而叠成噪音。
    /// </summary>
    public virtual void PlayCastSfx()
    {
        // 新一轮施法：解锁本轮的命中音配额（放在最前面，
        // 即使技能名为空或没有匹配的发射音，轮次也要照常推进）
        _castSerial++;

        if (string.IsNullOrEmpty(Skillname)) return;

        // 火系
        if (Skillname.Contains("地狱火"))
            AudioManager.PlaySfx(AudioManager.SfxKey.HellfireCast);
        else if (Skillname.Contains("火球") || Skillname.Contains("火"))
            AudioManager.PlaySfx(AudioManager.SfxKey.FireballCast);
        // 冰系
        else if (Skillname.Contains("冰") || Skillname.Contains("霜"))
            AudioManager.PlaySfx(AudioManager.SfxKey.IceCast);
        // 风系（飓风 / 风之形 / 风箭各有专属音，区分开）
        else if (Skillname.Contains("飓风"))
            AudioManager.PlaySfx(AudioManager.SfxKey.Hurricane);
        else if (Skillname.Contains("风之形"))
            AudioManager.PlaySfx(AudioManager.SfxKey.WindBlade);
        else if (Skillname.Contains("风"))
            AudioManager.PlaySfx(AudioManager.SfxKey.WindCast);
        // 暗影系
        else if (Skillname.Contains("黑暗") || Skillname.Contains("暗") || Skillname.Contains("齿轮"))
            AudioManager.PlaySfx(AudioManager.SfxKey.DarkCast);
        // 血族
        else if (Skillname.Contains("血族") || Skillname.Contains("血统") || Skillname.Contains("血"))
            AudioManager.PlaySfx(AudioManager.SfxKey.BloodCast);
        // 寄生
        else if (Skillname.Contains("寄生"))
            AudioManager.PlaySfx(AudioManager.SfxKey.ParasiteCast);
        // 亡者领域（进化形态，优先于孢子领域判断）
        else if (Skillname.Contains("亡者"))
            AudioManager.PlaySfx(AudioManager.SfxKey.TombCast);
        // 史莱姆社群（阴/阳/太极史莱姆）：借用孢子的粘稠涌动音，
        // 与"史莱姆"的黏液质感契合，且避免为新社群单独引入音源文件。
        // 注意必须放在"孢子"判断之前——技能名里没有"孢子"二字，
        // 但也不能落到最后被别的关键词误命中。
        else if (Skillname.Contains("史莱姆"))
            AudioManager.PlaySfx(AudioManager.SfxKey.SporeCast);
        // 自然 / 孢子
        else if (Skillname.Contains("孢子"))
            AudioManager.PlaySfx(AudioManager.SfxKey.SporeCast);
    }
}
