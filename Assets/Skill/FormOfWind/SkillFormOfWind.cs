using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 风之形（风箭进化）：风箭命中敌人时有概率在命中处生成直线穿透冲击波；朝向在生成瞬间指向优先目标（其他敌人，否则当前敌人）。
/// </summary>
public class SkillFormOfWind : Skillbase
{
    /// <summary>调试：风刃不生成时先设为 true，看 Console / 屏幕 Toast 停在哪一步；查完改回 false。</summary>
    public static bool DebugTrace = false;
    public static bool DebugTraceToast = false;

    [Header("风之形")]
    [Range(0f, 1f)] public float procChance = 1f;
    [Tooltip("相对当前风箭面板伤害的比例")]
    [Range(0.05f, 3f)] public float damageFactorFromWindArrow = 0.55f;

    void Awake()
    {
        CDtime = 1e30f;
        CDkey = 0f;
    }

    public override IEnumerator Useskill()
    {
        yield break;
    }

    /// <param name="preferredFo">风箭发射时注入，优先使用，避免解析失败</param>
    public static void TryProcOnWindArrowHit(Player player, SkillWindArrow windArrow, Transform hitEnemy, Vector3 hitPos, SkillFormOfWind preferredFo = null)
    {
        FormOfWindDebug.Err("TryProc", "进入 TryProcOnWindArrowHit");
        if (windArrow == null || hitEnemy == null)
        {
            Dbg("A", "windArrow 或 hitEnemy 为空");
            return;
        }

        SkillFormOfWind fo = preferredFo != null ? preferredFo : ResolveFormOfWind(windArrow, player);
        if (fo == null)
        {
            Dbg("B", $"未找到风之形组件。风箭父节点={(windArrow.transform.parent != null ? windArrow.transform.parent.name : "null")} 子物体数={(windArrow.transform.parent != null ? windArrow.transform.parent.childCount : 0)} player.SkillList={(player != null && player.SkillList != null ? player.SkillList.childCount.ToString() : "null")}");
            WarnFoMissingOnce("[风之形] 未在 SkillList 下找到 SkillFormOfWind（是否未解锁 UR/未学习？）");
            return;
        }
        if (fo.bullet == null)
        {
            Dbg("C", "FormOfWindskill 未指定 bullet 预制体");
            WarnBulletMissingOnce("[风之形] FormOfWind 未指定 bullet 预制体，无法生成冲击波。");
            return;
        }
        float roll = UnityEngine.Random.value;
        if (roll > fo.procChance)
        {
            if (DebugTrace)
                Debug.Log($"[风之形·D] 概率未中 roll={roll:F2} procChance={fo.procChance}（仅 Console，避免 Toast 刷屏）");
            return;
        }

        Transform aimAt = fo.PickAimTarget(hitEnemy, hitPos);
        if (aimAt == null)
        {
            Dbg("E", "PickAimTarget 返回 null");
            return;
        }

        // 飞行方向只用「命中点 → 瞄准目标」的水平向量，避免 spawn 前移越过目标后 fireDir 反向或乱飘。
        Vector3 toAim = aimAt.position - hitPos;
        toAim.y = 0f;
        float distSq = toAim.sqrMagnitude;
        Vector3 dirNorm;
        float push;
        if (distSq < 1e-6f)
        {
            dirNorm = Vector3.right;
            push = 0f;
        }
        else
        {
            float dist = Mathf.Sqrt(distSq);
            dirNorm = toAim / dist;
            push = Mathf.Min(0.35f, dist * 0.45f);
        }

        Vector3 spawn = hitPos + dirNorm * push;
        Vector3 fireDir = dirNorm;

        int dmg = Mathf.Max(1, Mathf.RoundToInt(windArrow.damage * fo.damageFactorFromWindArrow));

        GameObject go = Instantiate(fo.bullet, spawn, Quaternion.identity);
        BulletWindBlade blade = go.GetComponent<BulletWindBlade>();
        if (blade == null)
        {
            Dbg("F", "bullet 预制体上没有 BulletWindBlade 脚本");
            Destroy(go);
            return;
        }

        blade.fatherskill = fo;
        blade.SetDamageOverride(dmg);
        blade.SetInitialDirection(fireDir);
        blade.GetFather();
        blade.cango = true;
        DbgOk($"已生成冲击波 dmg={dmg} pass={fo.pass}");
    }

    static void Dbg(string step, string msg)
    {
        if (!DebugTrace) return;
        string line = $"[风之形·{step}] {msg}";
        Debug.LogWarning(line);
        if (DebugTraceToast && ToastManager.Instance != null)
            ToastManager.Show(line);
    }

    static void DbgOk(string msg)
    {
        if (!DebugTrace) return;
        string line = "[风之形·OK] " + msg;
        Debug.Log(line);
        if (DebugTraceToast && ToastManager.Instance != null)
            ToastManager.Show(line);
    }

    static int _warnFoMissingCount;
    static int _warnBulletMissingCount;

    static void WarnFoMissingOnce(string msg)
    {
        if (_warnFoMissingCount >= 6) return;
        _warnFoMissingCount++;
        Debug.LogWarning(msg);
    }

    static void WarnBulletMissingOnce(string msg)
    {
        if (_warnBulletMissingCount >= 6) return;
        _warnBulletMissingCount++;
        Debug.LogWarning(msg);
    }

    /// <summary>先整棵 SkillList 递归查找，再退回与风箭技能同父节点的兄弟（嵌套文件夹时更稳）。</summary>
    static SkillFormOfWind ResolveFormOfWind(SkillWindArrow windArrow, Player player)
    {
        if (player != null && player.SkillList != null)
        {
            var fo = player.SkillList.GetComponentInChildren<SkillFormOfWind>(true);
            if (fo != null) return fo;
        }
        if (windArrow != null && windArrow.transform.parent != null)
        {
            foreach (Transform t in windArrow.transform.parent)
            {
                if (t == null) continue;
                var fo = t.GetComponent<SkillFormOfWind>();
                if (fo != null) return fo;
                fo = t.GetComponentInChildren<SkillFormOfWind>(true);
                if (fo != null) return fo;
            }
        }
        return null;
    }

    /// <summary>优先选最近的其他敌人作为「瞄准方向」；没有则瞄准当前命中的敌人。</summary>
    Transform PickAimTarget(Transform hitEnemy, Vector3 fromPos)
    {
        Transform layer = GameObject.Find("enemylayer")?.transform;
        if (layer == null) return hitEnemy;

        Transform bestOther = null;
        float bestSq = float.MaxValue;

        foreach (Transform e in layer)
        {
            if (e == hitEnemy) continue;
            enemy en = e.GetComponent<enemy>();
            if (en != null && (en.health <= 0 || en.rolestate == global::enemy.state.dead)) continue;
            // 亡者领域：风之形挑下个目标时跳过友军，避免风箭弹射到自己复活出来的盟友
            if (en != null && en._mindControlledFlag) continue;

            Vector3 d = e.position - fromPos;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                bestOther = e;
            }
        }

        if (bestOther != null) return bestOther;

        // 仅命中体时：仍用其 Transform 定方向（调用方应在扣血前调用，故通常仍存活；此处不验 health，避免边界情况返回 null）
        if (hitEnemy != null)
            return hitEnemy;

        return null;
    }
}
