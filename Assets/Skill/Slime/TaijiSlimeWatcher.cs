using UnityEngine;

/// <summary>
/// 太极史莱姆看守者 —— 负责两件事：
///
///A. 合体检测：玩家 SkillList 中**同时**存在「阴史莱姆」与「阳史莱姆」时，
///     自动挂上 <see cref="TaijiSlimeController"/> 替换二者；任一消失时自动解除。
///
///  B. 升级卡共享同步：规格要求「技能学习卡不共享（两个独立），但升级卡共享，
///     写作『阴/阳史莱姆』」。共享升级的落地方式是——
///     升级卡只写入其中一支（skillupgrade 按 Skillname 匹配，只会命中一个），
///     本看守者每帧把两支的damage / CDtime / number / attackRadius /
///     speed / lifetime / pass 拉平为"两者中更强的那个值"。
///
///     为什么用"每帧拉平"而不是"在 skillupgrade 里同时改两支"？
///       1. 升级来源不止升级卡：世界 Boss 好感度加成（ApplySlimeBonus）、
///          奇遇事件、门挑战、SSR 效果都可能只改到一支；
///       2. 玩家可能先学阴、升了5 级，之后才学到阳 —— 新学的那支是prefab 初始值，
///          必须被拉到同一水平，否则"共享升级"名不副实；
///       3. 拉平是幂等的Max 运算，重复执行无副作用，比在N 个来源处各写一遍同步更可靠。
///
///     注意 CDtime 用 Min（越小越强），其余用 Max。
///
/// 本组件由 EquipmentInitializer 在开局挂到玩家身上，全局仅一个。
/// </summary>
public class TaijiSlimeWatcher : MonoBehaviour
{
    [Tooltip("检测间隔（秒）。技能增减是低频事件，不需要每帧全量扫 SkillList。")]
    public float checkInterval = 0.35f;

    private Player _player;
    private TaijiSlimeController _controller;
    private float _timer;

    private void Start()
    {
        _player = GetComponent<Player>();
        if (_player == null) _player = GetComponentInParent<Player>();
        if (_player == null) _player = FindObjectOfType<Player>();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = checkInterval;

        if (_player == null || _player.SkillList == null) return;

        SkillYinYangSlime yin = null, yang = null;
        foreach (Transform t in _player.SkillList)
        {
            if (t == null) continue;
            var s = t.GetComponent<SkillYinYangSlime>();
            if (s == null) continue;
            if (s.isYin) { if (yin == null) yin = s; }
            else { if (yang == null) yang = s; }
        }

        // ── B. 共享升级：先拉平数值，再决定合体 ──
        if (yin != null && yang != null)
            SyncSharedStats(yin, yang);

        // ── A. 合体 / 解除 ──
        bool shouldMerge = (yin != null && yang != null);

        if (shouldMerge && _controller == null)
        {
            _controller = gameObject.AddComponent<TaijiSlimeController>();
            _controller.yinSkill = yin;
            _controller.yangSkill = yang;
            _controller.owner = _player.transform;
            ToastManager.Show("<color=#9BE8FF>阴阳相生 —— 太极史莱姆 已成形！</color>");
            Debug.Log("[SlimeFaction] 阴 + 阳 → 太极史莱姆 已接管");
        }
        else if (!shouldMerge && _controller != null)
        {
            // 任一技能消失（被遗忘/替换）：解除接管，让剩下那支恢复独立开火
            Destroy(_controller);
            _controller = null;
            Debug.Log("[SlimeFaction] 阴/阳 之一已失去，太极史莱姆 解除");
        }
        else if (shouldMerge && _controller != null)
        {
            // 引用可能因为技能对象被重建而失效（例如三清化一克隆流程），刷新一次
            _controller.yinSkill = yin;
            _controller.yangSkill = yang;
            _controller.owner = _player.transform;
        }
    }

    /// <summary>
    /// 把阴/阳两支的成长数值拉平为"更强的那一侧"，实现规格里的"升级卡共享"。
    /// CDtime 取 Min（冷却越小越强），其余取 Max。
    /// </summary>
    private static void SyncSharedStats(SkillYinYangSlime a, SkillYinYangSlime b)
    {
        int dmg = Mathf.Max(a.damage, b.damage);
        int num = Mathf.Max(a.number, b.number);
        int ps  = Mathf.Max(a.pass, b.pass);
        float spd = Mathf.Max(a.speed, b.speed);
        float life = Mathf.Max(a.lifetime, b.lifetime);
        float radius = Mathf.Max(a.attackRadius, b.attackRadius);
        //冷却：取更短的。注意排除 0/负值（未初始化的 prefab 可能是 0）
        float cdA = a.CDtime > 0.01f ? a.CDtime : float.MaxValue;
        float cdB = b.CDtime > 0.01f ? b.CDtime : float.MaxValue;
        float cd = Mathf.Min(cdA, cdB);
        if (cd >= float.MaxValue) cd = 5f;

        a.damage = b.damage = dmg;
        a.number = b.number = num;
        a.pass = b.pass = ps;
        a.speed = b.speed = spd;
        a.lifetime = b.lifetime = life;
        a.attackRadius = b.attackRadius = radius;
        a.CDtime = b.CDtime = cd;
    }
}
