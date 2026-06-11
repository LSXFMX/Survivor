using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 血族血统：周期性在半径内选定敌人，蝙蝠飞向目标造成伤害后返回玩家。
/// 好感度装备「血族之力」解锁后子弹伤害按比例吸血。
/// </summary>
public class SkillBloodline : Skillbase
{
    [Header("血族血统专属")]
    public float attackRadius = 8f;

    [Header("血族吸血（装备血族之力时由局外存档判定）")]
    [Tooltip("对敌人造成最终伤害的该比例转为自身回血")]
    public float lifestealRatio = 0.15f;

    [Header("范围圆圈")]
    public int circleSegments = 48;
    public Color circleColor = new Color(0.65f, 0.1f, 0.25f, 0.35f);

    private LineRenderer _circle;
    private float _lastRadius = -1f;
    private readonly List<BulletBloodlineBat> _familiars = new List<BulletBloodlineBat>();
    private Transform _ownerTransform;

    /// <summary>是否已解锁血族吸血（好感度≥50 + 存档装备）</summary>
    public bool HasLifestealFromEquipment()
    {
        if (EquipmentSystem.Instance == null ||
            !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, 4))
            return false;
        if (FavorManager.Instance == null)
            return UnityEngine.PlayerPrefs.GetInt("Favor_Bat", 0) >= 50;
        return FavorManager.Instance.GetFavor(FactionType.Bat) >= 50;
    }

    private void Start()
    {
        if (level <= 1 && CDtime > 1f)
        {
            CDtime = 1f;
            CDkey = 1f;
        }

        ResolveOwnerPlayer();

        // === Bug 修复："显示分身攻击距离" 按钮无效 ===
        // 分身由 Instantiate(主玩家) 克隆而来，prefab snapshot 里已有 Start 第一次创建的
        // "BloodlineRangeCircle" 子物体；clone 再次 Start 又新建一个，造成两个圈，旧圈未注册到
        // AttackRangeIndicatorManager → toggle 不能控制它 → 看似切换无效。详见 SkillWindArrow.Start。
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform c = transform.GetChild(i);
            if (c != null && c.name == "BloodlineRangeCircle") Destroy(c.gameObject);
        }
        GameObject circleObj = new GameObject("BloodlineRangeCircle");
        circleObj.transform.SetParent(transform);
        circleObj.transform.localPosition = Vector3.zero;

        _circle = circleObj.AddComponent<LineRenderer>();
        _circle.loop = true;
        _circle.useWorldSpace = false;
        _circle.widthMultiplier = 0.06f;
        _circle.positionCount = circleSegments;
        _circle.material = new Material(Shader.Find("Sprites/Default"));
        _circle.startColor = circleColor;
        _circle.endColor = circleColor;

        DrawCircle();
        AttackRangeIndicatorManager.Register(_circle, GetComponentInParent<Player>());
        EnsureFamiliarsCount();
    }

    private void Update()
    {
        ResolveOwnerPlayer();
        if (_ownerTransform != null)
            transform.position = _ownerTransform.position;

        if (!Mathf.Approximately(_lastRadius, attackRadius))
            DrawCircle();

        CleanupFamiliars();
        EnsureFamiliarsCount();
        SyncFamiliarSetup();
    }

    private void DrawCircle()
    {
        if (_circle == null) return;
        _lastRadius = attackRadius;
        for (int i = 0; i < circleSegments; i++)
        {
            float angle = i * 2f * Mathf.PI / circleSegments;
            _circle.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * attackRadius,
                0f,
                Mathf.Sin(angle) * attackRadius));
        }
    }

    public override IEnumerator Useskill()
    {
        CDkey = 0;
        ResolveOwnerPlayer();
        if (_ownerTransform == null) yield break;

        CleanupFamiliars();
        EnsureFamiliarsCount();
        SyncFamiliarSetup();
        if (_familiars.Count == 0) yield break;

        foreach (BulletBloodlineBat bat in _familiars)
        {
            if (bat != null) bat.BeginCooldownWindow();
        }

        List<Transform> targets = GetEnemiesInRange();
        if (targets.Count == 0) yield break;

        int targetIndex = 0;
        for (int i = 0; i < _familiars.Count && targetIndex < targets.Count; i++)
        {
            BulletBloodlineBat bat = _familiars[i];
            if (bat == null || !bat.CanAttackThisCooldown()) continue;

            bat.CommandAttack(targets[targetIndex]);
            targetIndex++;
            yield return new WaitForSeconds(interval);
        }
    }

    private void CleanupFamiliars()
    {
        for (int i = _familiars.Count - 1; i >= 0; i--)
        {
            if (_familiars[i] == null)
                _familiars.RemoveAt(i);
        }
    }

    private void EnsureFamiliarsCount()
    {
        if (_ownerTransform == null || bullet == null) return;
        int desired = Mathf.Max(0, number);

        while (_familiars.Count < desired)
        {
            GameObject go = Instantiate(bullet, _ownerTransform.position, Quaternion.identity);
            BulletBloodlineBat bat = go.GetComponent<BulletBloodlineBat>();
            if (bat == null)
            {
                // 兜底：即使传入的是普通蝙蝠预制体，也自动挂上使魔逻辑
                bat = go.AddComponent<BulletBloodlineBat>();
            }

            bat.fatherskill = this;
            bat.GetFather();
            bat.cango = true;
            bat.SetupLifesteal(HasLifestealFromEquipment(), lifestealRatio);
            _familiars.Add(bat);
        }

        while (_familiars.Count > desired)
        {
            int last = _familiars.Count - 1;
            BulletBloodlineBat bat = _familiars[last];
            _familiars.RemoveAt(last);
            if (bat != null) Destroy(bat.gameObject);
        }
    }

    private void SyncFamiliarSetup()
    {
        int total = _familiars.Count;
        bool hasLifesteal = HasLifestealFromEquipment();
        for (int i = 0; i < total; i++)
        {
            BulletBloodlineBat bat = _familiars[i];
            if (bat == null) continue;
            bat.SetupLifesteal(hasLifesteal, lifestealRatio);
            bat.ConfigureOrbit(_ownerTransform, i, total);
        }
    }

    private List<Transform> GetEnemiesInRange()
    {
        List<Transform> result = new List<Transform>();
        if (_ownerTransform == null) return result;

        enemy[] enemies = FindObjectsOfType<enemy>();
        foreach (enemy en in enemies)
        {
            if (en == null || en.rolestate == enemy.state.dead || en.health <= 0) continue;

            Transform e = en.transform;
            float dist = Vector3.Distance(_ownerTransform.position, e.position);
            if (dist <= attackRadius)
                result.Add(e);
        }

        result.Sort((a, b) =>
            Vector3.Distance(_ownerTransform.position, a.position)
                .CompareTo(Vector3.Distance(_ownerTransform.position, b.position)));
        return result;
    }

    private void ResolveOwnerPlayer()
    {
        if (player != null)
        {
            _ownerTransform = player.transform;
            return;
        }

        Transform layer = GameObject.Find("playerlayer")?.transform;
        if (layer == null || layer.childCount == 0) return;

        Transform picked = null;
        foreach (Transform t in layer)
        {
            if (t != null && t.CompareTag("Player"))
            {
                picked = t;
                break;
            }
        }
        if (picked == null) picked = layer.GetChild(0);
        if (picked == null) return;

        _ownerTransform = picked;
        player = picked.gameObject; // 回填 Skillbase.player，兼容其它逻辑
    }

    private void OnDestroy()
    {
        AttackRangeIndicatorManager.Unregister(_circle);
        for (int i = 0; i < _familiars.Count; i++)
        {
            if (_familiars[i] != null)
                Destroy(_familiars[i].gameObject);
        }
        _familiars.Clear();
    }
}
