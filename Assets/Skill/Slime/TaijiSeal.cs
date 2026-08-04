using TMPro;
using UnityEngine;

/// <summary>
/// 太极印 —— 太极史莱姆的第一种攻击方式。
///
/// 规格：「对最近的敌人连续释放 3 次太极印，类似威压从敌人头顶压制，
///正在被攻击的敌人无法移动」。数量升级会增加太极印的数量。
///
/// 表现分三段（总时长 = fallTime + holdTime + fadeTime，约 0.55s）：
///   1. 蓄势：印记在目标头顶高空出现，半透明、放大到 1.3 倍并快速旋转；
///   2. 下压：急速下落砸到目标身上，同时缩到 1.0 倍、旋转减速（有"砸实"的顿感）；
///   3. 定身+结算：落地瞬间造成伤害并对目标施加禁止移动，随后印记渐隐。
///
/// 禁止移动的时长刻意比印记动画略长（immobilizeDuration 默认 0.45s），
/// 这样连续 3 次太极印之间不会出现"松一下又被压住"的抖动，
/// 玩家观感是整段连击期间敌人一直被按在原地。
/// </summary>
public class TaijiSeal : MonoBehaviour
{
    [Header("运行时由控制器注入")]
    public int   damage = 5;
    public Transform target;
    public Attribute playerAttr;
    public string trackingSkillName = SlimeFactionAssets.SKILL_SHARED_DISPLAY;

    [Header("时序")]
    public float fallTime = 0.18f;   // 从高空砸下
    public float holdTime = 0.12f;   //砸实停顿
    public float fadeTime = 0.25f;   // 渐隐
    public float startHeight = 4.5f; // 起始高度（世界单位）

    [Header("表现")]
    [Tooltip("印记落地时的世界尺寸（米）。与源图分辨率无关，由 sprite.bounds 反算。")]
    public float sealWorldSize = 2.4f;
    [Tooltip("命中后对目标施加的禁止移动时长（秒）。")]
    public float immobilizeDuration = 0.45f;
    [Tooltip("范围伤害半径。0 = 只打目标本体。")]
    public float splashRadius = 1.6f;

    private SpriteRenderer _sr;
    private float _t;
    private bool _damageApplied;
    private Vector3 _groundPos;
    private Transform _enemyLayer;
    /// <summary>换算到 sealWorldSize 米的基准缩放；所有动画倍率叠在它之上。</summary>
    private float _baseScale = 1f;

    /// <summary>由TaijiSlimeController 调用完成初始化与自动播放。</summary>
    public void Fire(Transform tgt, int dmg, Attribute attr, Transform enemyLayer)
    {
        target= tgt;
        damage      = dmg;
        playerAttr  = attr;
        _enemyLayer = enemyLayer;

        // 落点在施放瞬间锁定：如果每帧跟随目标，敌人跑动时印记会"贴着脸滑行"，
        // 失去"从天而降砸下"的压迫感；锁定落点也让闪避走位有意义。
        _groundPos = tgt != null ? tgt.position : transform.position;
        _groundPos.y = 0.05f;

        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr != null)
        {
            _sr.sprite = SlimeFactionAssets.Seal;
            _sr.sortingOrder = 95;
        }

        //绝对尺寸换算。注意：这里把系数写在**本对象**的 localScale 上（而不是 sprite 子物体），
        // 因为下面的下压/扩散动画直接改 transform.localScale。
        _baseScale = _sr != null
            ? SlimeFactionAssets.WorldSizeScale(_sr.sprite, sealWorldSize) : 1f;

        transform.position = _groundPos + new Vector3(0f, startHeight, 0f);
        // 太极印是"从上往下拍"的平面印记→ 完全水平铺开（X=90 才是躺在地面上）
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        transform.localScale = Vector3.one * (_baseScale * 1.3f);
    }

    private void Update()
    {
        _t += Time.deltaTime;

        float total = fallTime + holdTime + fadeTime;

        if (_t <= fallTime)
        {
            // 段1→2：下压。用 t² 缓入，越接近地面越快，砸感更强
            float k = Mathf.Clamp01(_t / fallTime);
            float ease = k * k;
            transform.position = Vector3.Lerp(
                _groundPos + new Vector3(0f, startHeight, 0f), _groundPos, ease);
            transform.localScale = Vector3.one * Mathf.Lerp(_baseScale * 1.3f, _baseScale, ease);
            // 旋转从快到慢
            transform.Rotate(0f, 0f, Mathf.Lerp(720f, 90f, ease) * Time.deltaTime, Space.Self);
            SetAlpha(Mathf.Lerp(0.35f, 1f, ease));
        }
        else if (_t <= fallTime + holdTime)
        {
            // 段 3：砸实 —— 伤害与定身只在这一刻结算一次
            transform.position = _groundPos;
            if (!_damageApplied)
            {
                _damageApplied = true;
                ApplyImpact();
            }
            SetAlpha(1f);
        }
        else
        {
            // 渐隐 + 略微扩散，像冲击波扩开
            float k = Mathf.Clamp01((_t - fallTime - holdTime) / fadeTime);
            SetAlpha(1f - k);
            transform.localScale = Vector3.one * Mathf.Lerp(_baseScale, _baseScale * 1.45f, k);
            transform.Rotate(0f, 0f, 60f * Time.deltaTime, Space.Self);
        }

        if (_t >= total) Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        if (_sr == null) return;
        Color c = _sr.color;
        c.a = Mathf.Clamp01(a);
        _sr.color = c;
    }

    /// <summary>
    /// 砸实瞬间：对落点半径内所有敌人造成伤害 + 施加禁止移动。
    /// 走 enemylayer 子物体遍历而非 FindObjectsOfType（后者在 600+ 敌人时非常慢）。
    /// </summary>
    private void ApplyImpact()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Hit);

        if (_enemyLayer == null)
        {
            // 兜底：只打锁定目标
            enemy solo = target != null ? target.GetComponent<enemy>() : null;
            if (solo != null) HitOne(solo);
            return;
        }

        float rSq = splashRadius * splashRadius;
        int cnt = _enemyLayer.childCount;
        for (int i = 0; i < cnt; i++)
        {
            Transform t = _enemyLayer.GetChild(i);
            if (t == null) continue;

            // 先判距离再 GetComponent（同 BulletTadpole.TryHit 的性能考量）
            Vector3 d = t.position - _groundPos; d.y = 0f;
            if (splashRadius > 0f && d.sqrMagnitude > rSq) continue;

            enemy en = t.GetComponent<enemy>();
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;
            if (en._mindControlledFlag) continue;
            if (en is DragonBoss db && db.IsInvincible) continue;

            HitOne(en);
        }
    }

    private void HitOne(enemy en)
    {
        // 定身：即使这一下被闪避，压制效果依然生效（"威压"是场地效果，不是打击效果）
        en.ApplyImmobilize(immobilizeDuration);

        // 闪避判定
        if (en.EVA > Random.value * 100f)
        {
            MissNumber.Show(en.atknumber, en.transform.position);
            return;
        }

        float atk = playerAttr != null ? playerAttr.atk : 0f;
        float final = damage * (1f + atk * 0.1f);

        bool isCrit = false;
        if (playerAttr != null && playerAttr.CR > Random.value * 100f)
        {
            final *= playerAttr.CD / 100f;
            isCrit = true;
        }

        final -= en.def;

        if (en is GateChallengeEnemy && EquipmentSystem.Instance != null &&
            EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 1))
            final *= 1.2f;

        if (final < 1f) final = 1f;
        int dealt = (int)final;

        GameSessionTracker.Instance?.RecordDamage(trackingSkillName, dealt);
        en.health -= dealt;

        if (DamageNumberSettings.Visible && en.atknumber != null)
        {
            GameObject num = Instantiate(en.atknumber, en.transform.position, default);
            num.transform.localScale *= DamageNumberSettings.SizeScale;
            var txt = num.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = dealt.ToString();
                txt.color = isCrit ? new Color32(255, 215, 0, 255)
                                   : new Color32(150, 235, 255, 255); // 太极印用青白色区分
            }
        }

        en.startturnred();
        EquipmentInitializer.TryAllSourceLifesteal(dealt, en.atknumber, en.transform.position);
        if (en.health <= 0) en.Destroy1();
    }
}
