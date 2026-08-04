using UnityEngine;

/// <summary>
/// 太极图 —— 史莱姆社群好感度 100 解锁的宠物（FavorEquipment 11「太极两仪」）。
///
/// 与 <see cref="RedMoonClonePet"/> 同定位：本体是"图腾"，不主动索敌、不直接造成伤害。
/// 真正的战力由 EquipmentInitializer.ApplyFavorEquipment11_TaijiLiangYi 在开局
/// 直接赠予玩家「阴史莱姆」+「阳史莱姆」两个技能（即规格的"初始拥有太极史莱姆"）承担。
///
/// 表现：
///   • 悬浮在玩家身后偏上，缓慢呼吸起伏；
///   • 八卦环持续自转（阴阳流转）；
///   • 当太极史莱姆合体成形时，图腾会同步亮起一圈脉冲（通过 Update 检测控制器存在）。
///
/// 关联文案："我只是一个路过的化学老师罢了。"
/// </summary>
public class TaijiTuPet : MonoBehaviour
{
    [Header("引用")]
    public Player owner;

    [Header("跟随")]
    public Vector3 followOffset = new Vector3(1.5f, 0.45f, -0.3f);
    public float followLerp = 7f;
    [Tooltip("是否根据玩家朝向镜像水平偏移（保持在同一侧肩后）。")]
    public bool mirrorByFacing = true;

    [Header("尺寸")]
    [Tooltip("宠物的世界尺寸（米）。与源图分辨率无关。")]
    public float petWorldSize = 1.1f;

    [Header("呼吸悬浮")]
    public float hoverAmplitude = 0.13f;
    public float hoverFrequency = 1.2f;

    [Header("自转")]
    [Tooltip("八卦环自转速度（度/秒）。")]
    public float spinSpeed = 35f;

    private SpriteRenderer _sr;
    private float _hoverSeed;
    private float _lastHover;
    private float _spin;
    private float _pulse;

    private void Awake()
    {
        _hoverSeed = Random.Range(0f, Mathf.PI * 2f);

        _sr = GetComponentInChildren<SpriteRenderer>();
        if (_sr == null)
        {
            var go = new GameObject("PetSprite");
            go.transform.SetParent(transform, false);
            _sr = go.AddComponent<SpriteRenderer>();
        }
        if (_sr.sprite == null) _sr.sprite = SlimeFactionAssets.PetSprite;
        _sr.sortingOrder = 87;
        // 声明式绝对尺寸，避免 1024px 源图直接铺成 10 米宽
        SlimeFactionAssets.FitSpriteToWorldSize(_sr, petWorldSize);
    }

    private void Start()
    {
        if (owner == null) owner = FindObjectOfType<Player>();
        IgnoreCollisionWithOwner();
    }

    private void Update()
    {
        if (owner == null) return;

        Vector3 offset = followOffset;
        Transform ot = owner.transform;
        if (mirrorByFacing && ot.localScale.x < 0f) offset.x = -offset.x;

        Vector3 target = ot.position + offset;
        transform.position = Vector3.Lerp(transform.position, target,
            Mathf.Clamp01(followLerp * Time.deltaTime));

        // 八卦环自转（保留 45° 俯视倾斜）
        _spin = (_spin + spinSpeed * Time.deltaTime) % 360f;
        transform.rotation = Quaternion.Euler(45f, 0f, _spin);

        UpdateMergePulse();
    }

    /// <summary>
    /// 太极史莱姆成形时，图腾同步呼吸式发光，给玩家"宠物与技能同源"的反馈。
    /// 用 GetComponent 检测控制器而不是事件回调：控制器可能在任意时刻被
    /// Watcher 增删，轮询最省心且开销极低（每帧一次 GetComponent，宠物只有一个）。
    /// </summary>
    private void UpdateMergePulse()
    {
        bool merged = owner != null && owner.GetComponent<TaijiSlimeController>() != null;

        float goal = merged ? 1f : 0f;
        _pulse = Mathf.MoveTowards(_pulse, goal, Time.deltaTime * 2.5f);

        if (_sr == null) return;
        if (_pulse <= 0.01f)
        {
            _sr.color = Color.white;
            return;
        }

        // 向青白色呼吸式偏移，与太极印的青白能量呼应
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
        float k = _pulse * wave * 0.45f;
        _sr.color = Color.Lerp(Color.white, new Color(0.65f, 0.95f, 1f, 1f), k);
    }

    private void LateUpdate()
    {
        Vector3 p = transform.position;
        p.y -= _lastHover;
        float dy = Mathf.Sin(Time.time * hoverFrequency * Mathf.PI * 2f + _hoverSeed) * hoverAmplitude;
        p.y += dy;
        transform.position = p;
        _lastHover = dy;
    }

    private void IgnoreCollisionWithOwner()
    {
        if (owner == null) return;
        Collider[] mine = GetComponentsInChildren<Collider>(true);
        Collider[] hers = owner.GetComponentsInChildren<Collider>(true);
        foreach (var a in mine)
        {
            if (a == null) continue;
            foreach (var b in hers)
            {
                if (b == null) continue;
                Physics.IgnoreCollision(a, b, true);
            }
        }
    }
}
