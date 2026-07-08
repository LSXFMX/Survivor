using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>龙王 Boss 用的轻量工具：共享白色 Sprite（做纯色特效矩形）。</summary>
public static class DragonFx
{
    private static Sprite _white;
    public static Sprite WhiteSprite()
    {
        if (_white != null) return _white;
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        var px = new Color[4]; for (int i = 0; i < 4; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        _white = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
        _white.name = "DragonFx_White";
        return _white;
    }
}

/// <summary>
/// 龙王投射物：追踪玩家的元素弹（火球/蓝焰标/龙卷/史莱姆凝弹/金龙鳞）。
/// 命中结算伤害，可附带：灼烧 DoT / 减速 / 龙王吸血。使用真实技能精灵图。
/// </summary>
public class DragonProjectile : MonoBehaviour
{
    private DragonBoss _owner;
    private Transform  _target;
    private int   _dmg;
    private float _speed;
    private float _slowSec;
    private bool  _lifesteal;
    private bool  _homing;             // 是否在初始窗口内追踪修正
    private bool  _burn;
    private Vector3 _dir;              // 3D 方向（含 Y，使弹体从 Boss 高处俯冲到玩家）
    private float _life;
    private float _worldSize;
    private float _hitRadius;
    private SpriteRenderer _sr;
    private bool  _orientToVel = true;
    private float _spinDegPerSec = 0f;
    private float _spin;
    private float _baseScaleX = 1f, _baseScaleY = 1f;

    // 追踪修正窗口：发射后前若干秒持续朝玩家身体中心修正（会俯冲下压），之后锁定方向直线飞。
    private bool  _continuousHoming = false; // 龙卷风：全程追踪
    private const float HOMING_WINDOW = 0.65f;
    private float _homingTimer;
    private static readonly Vector3 BODY_OFFSET = new Vector3(0f, 1.0f, 0f); // 玩家身体中心（脚底 pivot 上抬）

    public void SetOrientToVelocity(bool v) { _orientToVel = v; }
    public void SetSpin(float degPerSec) { _spinDegPerSec = degPerSec; }
    /// <summary>龙卷风：全程缓慢追踪玩家（不锁方向）。</summary>
    public void SetContinuousHoming(bool v) { _continuousHoming = v; }

    public void Init(DragonBoss owner, Transform target, Sprite sprite, Color tint,
                     int dmg, float speed, float slowSec, bool lifesteal, bool homing, bool burn, float worldSize = 1.3f)
    {
        _owner = owner; _target = target; _dmg = dmg; _speed = speed;
        _slowSec = slowSec; _lifesteal = lifesteal; _homing = homing; _burn = burn;
        _worldSize = worldSize; _life = 6f;

        _sr = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite = sprite != null ? sprite : DragonFx.WhiteSprite();
        _sr.color = tint;
        _sr.sortingOrder = 18;
        transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        FitScale();
        _hitRadius = Mathf.Clamp(_worldSize * 0.6f, 1.2f, 3.5f);

        // 初始方向：朝玩家身体中心（含 Y）→ 弹体会从 Boss 高处斜向下冲，最终能落到玩家身上
        if (_target != null)
        {
            Vector3 d = TargetPoint() - transform.position;
            _dir = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.right;
        }
        else _dir = Vector3.right;

        ApplyRotation();
    }

    private Vector3 TargetPoint()
        => _target != null ? _target.position + BODY_OFFSET : transform.position + _dir;

    private void FitScale()
    {
        float baseSize = 1f;
        if (_sr.sprite != null) baseSize = Mathf.Max(0.01f, _sr.sprite.bounds.size.x);
        float s = _worldSize / baseSize;
        _baseScaleX = s; _baseScaleY = s;
        transform.localScale = new Vector3(s, s, s);
    }

    private void ApplyRotation()
    {
        if (_orientToVel)
        {
            float ang = Mathf.Atan2(_dir.z, _dir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(45f, 0f, ang);
        }
        else
        {
            transform.rotation = Quaternion.Euler(45f, 0f, 0f); // 竖直
            if (_spinDegPerSec != 0f)
            {
                float wob = 1f + Mathf.Sin(_spin * Mathf.Deg2Rad) * 0.18f; // 漏斗左右churn
                transform.localScale = new Vector3(_baseScaleX * wob, _baseScaleY, _baseScaleX);
            }
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _life -= dt;

        // 追踪修正：全程（龙卷风）或初始窗口内，朝玩家身体中心（3D，含 Y）转向 → 会俯冲下压
        bool doHome = false;
        if (_target != null)
        {
            var plc = _target.GetComponent<Player>();
            if (plc != null && plc.health > 0)
            {
                if (_continuousHoming) doHome = true;
                else if (_homing && _homingTimer < HOMING_WINDOW) { doHome = true; _homingTimer += dt; }
            }
        }
        if (doHome)
        {
            Vector3 d = TargetPoint() - transform.position;
            if (d.sqrMagnitude > 0.001f)
            {
                float rate = _continuousHoming ? 3f : 9f; // 窗口内强修正，保证下压命中
                _dir = Vector3.Slerp(_dir, d.normalized, rate * dt).normalized;
            }
        }

        transform.position += _dir * _speed * dt;

        if (_spinDegPerSec != 0f) _spin += _spinDegPerSec * dt;
        ApplyRotation();

        if (_target != null)
        {
            var pl = _target.GetComponent<Player>();
            if (pl != null && pl.health > 0 &&
                Vector3.Distance(transform.position, TargetPoint()) <= _hitRadius)
            {
                if (_owner != null)
                {
                    _owner.ApplyDamageToPlayer(pl, _target, _dmg, _slowSec, _burn);
                    if (_lifesteal) _owner.LifestealHeal(_dmg);
                }
                Destroy(gameObject);
                return;
            }
        }
        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>淡出并销毁的纯色特效精灵（吐息光束 / 冲击环 复用）。</summary>
public class DragonFadeSprite : MonoBehaviour
{
    private float _life, _max;
    private SpriteRenderer _sr;
    public void Init(float life) { _life = life; _max = Mathf.Max(0.01f, life); _sr = GetComponent<SpriteRenderer>(); }
    private void Update()
    {
        _life -= Time.deltaTime;
        if (_sr != null) { var c = _sr.color; c.a = Mathf.Clamp01(_life / _max) * 0.85f; _sr.color = c; }
        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>由内向外扩张并淡出的冲击波环（着陆/砸地/变身/巨爪斩）。</summary>
public class DragonRingFx : MonoBehaviour
{
    private float _target, _life, _max;
    private SpriteRenderer _sr;
    public void Init(float targetDiameter, float life)
    {
        _target = targetDiameter; _life = life; _max = Mathf.Max(0.01f, life);
        _sr = GetComponent<SpriteRenderer>();
    }
    private void Update()
    {
        _life -= Time.deltaTime;
        float k = 1f - Mathf.Clamp01(_life / _max);
        float s = Mathf.Lerp(0.5f, _target, k);
        transform.localScale = new Vector3(s, s, 1f);
        if (_sr != null) { var c = _sr.color; c.a = (1f - k) * 0.5f; _sr.color = c; }
        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>全屏冲击动画：ScreenSpaceOverlay 满屏色块淡入淡出（蝙蝠抓取撕咬 / 黄金龙强控）。</summary>
public class DragonScreenFx : MonoBehaviour
{
    private float _life, _max;
    private Image _img;

    public static void Flash(Color col, float dur)
    {
        var go = new GameObject("DragonScreenFx");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29000;
        var gr = go.AddComponent<GraphicRaycaster>(); gr.enabled = false;

        var imgGo = new GameObject("flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        var img = imgGo.GetComponent<Image>(); img.raycastTarget = false; img.color = col;

        var fx = go.AddComponent<DragonScreenFx>();
        fx._img = img; fx._life = dur; fx._max = Mathf.Max(0.01f, dur);
    }

    private void Update()
    {
        _life -= Time.unscaledDeltaTime;
        if (_img != null)
        {
            float k = Mathf.Clamp01(_life / _max);
            var c = _img.color; c.a = Mathf.Sin(k * Mathf.PI) * 0.55f; _img.color = c; // 淡入淡出
        }
        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>
/// 全屏 AI 特效叠层：把一张特效精灵铺满屏幕（ScreenSpaceOverlay），随时间缩放/旋转/淡入淡出。
/// 用于黄金龙死亡演出（金色大爆炸 + 旋转光芒），不受 Time.timeScale 影响。
/// </summary>
public class DragonFullScreenFx : MonoBehaviour
{
    private Image _img; private RectTransform _rt;
    private float _life, _max, _fromScale, _toScale, _rotSpeed, _maxAlpha;

    public static void Show(Sprite sprite, float dur, float fromScale, float toScale, float rotSpeed, float maxAlpha)
    {
        var go = new GameObject("DragonFullScreenFx");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29500;
        var gr = go.AddComponent<GraphicRaycaster>(); gr.enabled = false;

        var imgGo = new GameObject("fx", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imgGo.transform.SetParent(go.transform, false);
        var rt = imgGo.GetComponent<RectTransform>();
        float size = Mathf.Max(Screen.width, Screen.height) * 1.5f; // 取大边×1.5 铺满
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;

        var img = imgGo.GetComponent<Image>(); img.raycastTarget = false;
        if (sprite != null) { img.sprite = sprite; img.color = new Color(1f, 1f, 1f, 0f); }
        else img.color = new Color(1f, 0.85f, 0.2f, 0f);

        var fx = go.AddComponent<DragonFullScreenFx>();
        fx._img = img; fx._rt = rt; fx._life = dur; fx._max = Mathf.Max(0.01f, dur);
        fx._fromScale = fromScale; fx._toScale = toScale; fx._rotSpeed = rotSpeed; fx._maxAlpha = maxAlpha;
        rt.localScale = new Vector3(fromScale, fromScale, 1f);
    }

    private void Update()
    {
        _life -= Time.unscaledDeltaTime;
        float k = 1f - Mathf.Clamp01(_life / _max); // 0→1 进度
        if (_rt != null)
        {
            float s = Mathf.Lerp(_fromScale, _toScale, k);
            _rt.localScale = new Vector3(s, s, 1f);
            if (_rotSpeed != 0f)
                _rt.localRotation = Quaternion.Euler(0f, 0f, _rt.localEulerAngles.z + _rotSpeed * Time.unscaledDeltaTime);
        }
        if (_img != null)
        {
            var c = _img.color; c.a = Mathf.Sin(k * Mathf.PI) * _maxAlpha; _img.color = c; // 淡入淡出
        }
        if (_life <= 0f) Destroy(gameObject);
    }
}

/// <summary>玩家减速 debuff：缓存原速，降速一段时间后恢复；重复施加只刷新时长，绝不永久降速。</summary>
public class DragonSlowDebuff : MonoBehaviour
{
    private Player _pl; private int _originalSpeed; private float _timer; private bool _applied;
    public static void Apply(Player pl, float factor, float duration)
    {
        if (pl == null) return;
        var d = pl.GetComponent<DragonSlowDebuff>() ?? pl.gameObject.AddComponent<DragonSlowDebuff>();
        d.Begin(pl, factor, duration);
    }
    private void Begin(Player pl, float factor, float duration)
    {
        _pl = pl;
        if (!_applied) { _originalSpeed = pl.speed; pl.speed = Mathf.Max(1, Mathf.RoundToInt(_originalSpeed * Mathf.Clamp01(factor))); _applied = true; }
        _timer = Mathf.Max(_timer, duration);
    }
    private void Update() { if (!_applied) return; _timer -= Time.deltaTime; if (_timer <= 0f) Restore(); }
    private void Restore() { if (_applied && _pl != null) _pl.speed = _originalSpeed; _applied = false; Destroy(this); }
    private void OnDisable() { if (_applied && _pl != null) _pl.speed = _originalSpeed; _applied = false; }
}

/// <summary>
/// 玩家灼烧 debuff：一段时间内每 0.5s 造成固定真实伤害（无视防御），弹红橙飘字，
/// 并在玩家身上叠加一团跳动的红色火焰视觉（随 debuff 存续，到期/禁用时清除）。
/// </summary>
public class DragonBurnDebuff : MonoBehaviour
{
    private Player _pl; private int _dmgPerTick; private float _duration; private float _tick; private GameObject _atknumber;
    private GameObject _flameVfx; private SpriteRenderer _flameSr; private Sprite _flameSprite;

    public static void Apply(Player pl, DragonBoss owner, int dmgPerTick, float duration)
    {
        if (pl == null) return;
        var d = pl.GetComponent<DragonBurnDebuff>() ?? pl.gameObject.AddComponent<DragonBurnDebuff>();
        d._pl = pl; d._dmgPerTick = Mathf.Max(1, dmgPerTick);
        d._duration = Mathf.Max(d._duration, duration);
        d._atknumber = owner != null ? owner.AtkNumberPrefab : null;
        if (owner != null && owner.burnFlameSprite != null) d._flameSprite = owner.burnFlameSprite;
        d.EnsureFlame();
    }

    private void EnsureFlame()
    {
        if (_flameVfx != null || _pl == null) return;
        _flameVfx = new GameObject("BurnFlameVfx");
        _flameVfx.transform.SetParent(_pl.transform, false);
        _flameVfx.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        _flameVfx.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        _flameSr = _flameVfx.AddComponent<SpriteRenderer>();
        _flameSr.sprite = _flameSprite != null ? _flameSprite : DragonFx.WhiteSprite();
        _flameSr.color  = _flameSprite != null ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.3f, 0.08f, 0.6f);
        _flameSr.sortingOrder = 30;
        float baseH = _flameSr.sprite != null ? _flameSr.sprite.bounds.size.y : 1f;
        if (baseH < 0.01f) baseH = 1f;
        float s = 2.4f / baseH;                 // 火焰视觉高度约 2.4 世界单位
        _flameVfx.transform.localScale = new Vector3(s, s, s);
    }

    private void Update()
    {
        if (_pl == null) { Cleanup(); Destroy(this); return; }
        float dt = Time.deltaTime; _duration -= dt; _tick -= dt;

        // 火焰跳动（透明度 + 轻微缩放脉动）
        if (_flameSr != null)
        {
            float pulse = Mathf.Sin(Time.time * 20f);
            var c = _flameSr.color; c.a = 0.7f + pulse * 0.22f; _flameSr.color = c;
            if (_flameVfx != null)
            {
                float baseS = _flameVfx.transform.localScale.x;
                float k = 1f + pulse * 0.06f;
                _flameVfx.transform.localScale = new Vector3(baseS, Mathf.Abs(baseS) * k / 1f, baseS);
            }
        }

        if (_tick <= 0f && _pl.health > 0)
        {
            _tick = 0.5f; _pl.health -= _dmgPerTick;
            SpawnNum(_dmgPerTick, new Color(1f, 0.35f, 0.08f, 1f));
            AudioManager.PlaySfx(AudioManager.SfxKey.Burn);
            if (_pl.health <= 0) _pl.death();
        }
        if (_duration <= 0f) { Cleanup(); Destroy(this); }
    }

    private void Cleanup() { if (_flameVfx != null) Destroy(_flameVfx); _flameVfx = null; _flameSr = null; }
    private void OnDisable() { Cleanup(); }

    private void SpawnNum(int d, Color col)
    {
        if (!DamageNumberSettings.Visible || _atknumber == null || _pl == null) return;
        GameObject n = Instantiate(_atknumber, _pl.transform.position, default);
        n.transform.localScale *= DamageNumberSettings.SizeScale;
        var tmp = n.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = d.ToString(); tmp.color = col; }
    }
}

/// <summary>蝙蝠龙「吸血反噬」debuff：5s 内每 0.5s 抽取玩家生命并回复给龙王，弹紫色飘字。</summary>
public class DragonDrainDebuff : MonoBehaviour
{
    private Player _pl; private DragonBoss _owner; private int _dmgPerTick; private float _duration; private float _tick; private GameObject _atknumber;
    public static void Apply(Player pl, DragonBoss owner, int dmgPerTick, float duration)
    {
        if (pl == null) return;
        var d = pl.GetComponent<DragonDrainDebuff>() ?? pl.gameObject.AddComponent<DragonDrainDebuff>();
        d._pl = pl; d._owner = owner; d._dmgPerTick = Mathf.Max(1, dmgPerTick);
        d._duration = Mathf.Max(d._duration, duration);
        d._atknumber = owner != null ? owner.AtkNumberPrefab : null;
    }
    private void Update()
    {
        if (_pl == null) { Destroy(this); return; }
        float dt = Time.deltaTime; _duration -= dt; _tick -= dt;
        if (_tick <= 0f && _pl.health > 0)
        {
            _tick = 0.5f; _pl.health -= _dmgPerTick;
            if (_owner != null) _owner.HealBoss(_dmgPerTick);
            SpawnNum(_dmgPerTick, new Color(0.7f, 0.2f, 0.95f, 1f));
            if (_pl.health <= 0) _pl.death();
        }
        if (_duration <= 0f) Destroy(this);
    }
    private void SpawnNum(int d, Color col)
    {
        if (!DamageNumberSettings.Visible || _atknumber == null || _pl == null) return;
        GameObject n = Instantiate(_atknumber, _pl.transform.position, default);
        n.transform.localScale *= DamageNumberSettings.SizeScale;
        var tmp = n.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
        if (tmp != null) { tmp.text = d.ToString(); tmp.color = col; }
    }
}
