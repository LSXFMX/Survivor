using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using TMPro;
using UnityEngine;

public class Bulletbase : MonoBehaviour
{
    public int damage;
    public int level;
    public float lifetime;
    public int pass;
    public float speed;
    public float size;
    public Skillbase fatherskill;
    public Attribute player;
    public bool cango = false;//子弹是否可以发射
    public Rigidbody rb;
    public Transform enemy;
    public GameObject role;//目标角色
    public Vector3 distance;
    private Vector3 _baseEuler;

    // 静态缓存：所有子弹共享，避免每个子弹发射时 2 次 GameObject.Find（高频战斗场景每秒几十发）
    private static Attribute      s_cachedPlayer;
    private static Transform      s_cachedEnemyLayer;
    private static bool           s_cacheReady;

    //获取子弹所属技能的参数
    public virtual void GetFather()
    {
        damage = fatherskill.damage;
        level = fatherskill.level;
        lifetime = fatherskill.lifetime;
        pass = fatherskill.pass;
        speed = fatherskill.speed;
        size = fatherskill.size;

        // 静态缓存避免每帧找 player layer（最高频调用之一）
        if (!s_cacheReady)
        {
            s_cacheReady = true;
            var pl = GameObject.Find("playerlayer");
            if (pl != null && pl.transform.childCount > 0)
                s_cachedPlayer = pl.transform.GetChild(0).GetComponent<Attribute>();
            var el = GameObject.Find("enemylayer");
            if (el != null) s_cachedEnemyLayer = el.transform;
        }
        player = s_cachedPlayer;
        enemy = s_cachedEnemyLayer;

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        if (transform.position.y < 1f)
            transform.position = new Vector3(transform.position.x, 1f, transform.position.z);
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        transform.localScale = transform.localScale * size;
        _baseEuler = transform.rotation.eulerAngles;
    }
    protected virtual void OnTriggerEnter(Collider other)
    {
        enemy enemy = other.GetComponent<enemy>();
        if (enemy == null) enemy = other.GetComponentInParent<enemy>();
        if (enemy == null) return;

        // 亡者领域：玩家子弹不打被控制的友军
        if (enemy._mindControlledFlag) return;

        if (enemy.health > 0)
        {
            // 闪避判定：EVA 为闪避概率（0~100）
            float evaRoll = UnityEngine.Random.value * 100;
            if (enemy.EVA > evaRoll)
            {
                // 敌人闪避成功：在敌人位置弹青蓝色 Miss
                MissNumber.Show(enemy.atknumber, enemy.transform.position);
                // 闪避成功，不造成伤害，但仍消耗穿透
                pass -= 1;
                if (pass < 0) Destroy();
                return;
            }

            // 伤害公式：技能基础伤害 × (1 + 攻击力 × 0.1)，再走暴击与防御
            float finaldamage = damage * (1f + player.atk * 0.1f);
            float random = UnityEngine.Random.value * 100;
            bool isCrit = false;
            if (player.CR > random)
            {
                finaldamage = finaldamage * (player.CD / 100);
                isCrit = true;
            }
            finaldamage -= enemy.def;
            // SSR「白色杀手」：对门挑战怪物增伤 20%
            if (enemy is GateChallengeEnemy && EquipmentSystem.Instance != null &&
                EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 1))
                finaldamage *= 1.2f;
            // 至少 1 点伤害，避免高防御导致负数/0 伤
            if (finaldamage < 1f) finaldamage = 1f;
            int dealt = (int)finaldamage;

            // 会话伤害追踪
            if (GameSessionTracker.Instance != null && fatherskill != null)
                GameSessionTracker.Instance.RecordDamage(fatherskill.Skillname, dealt);

            enemy.health -= dealt;
            if (DamageNumberSettings.Visible)
            {
                DamageNumberPool.EnsureInit(enemy.atknumber);
                var number = DamageNumberPool.Get(enemy.transform.position);
                if (number == null)
                {
                    number = Instantiate(enemy.atknumber, enemy.transform.position, default);
                }
                number.transform.localScale = Vector3.one * DamageNumberSettings.SizeScale;
                var txt = number.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = dealt.ToString();
                    if (isCrit) txt.color = new Color32(255, 215, 0, 255);
                }
            }
            // 命中音效：优先让所属技能派发更精确的火球/冰击音，否则播通用 Hit
            if (fatherskill != null) fatherskill.PlayHitSfx();
            else AudioManager.PlaySfx(AudioManager.SfxKey.Hit);
            enemy.startturnred();
            // SSR_10 饮血剑：全局吸血（伤害 × 1%）
            EquipmentInitializer.TryAllSourceLifesteal(dealt, enemy.atknumber, enemy.transform.position);
            if (enemy.health <= 0)
            {
                enemy.Destroy1();
            }
        }

        pass -= 1;
        if (pass < 0)
        {
            Destroy();
        }
    }
    public void Destroy()
    {
        Destroy(gameObject);
    }
    public void getrole()
    {
        float shortestdis = 999999;
        Transform shortestrole = null;
        if (enemy.childCount > 0)
        {
            foreach (Transform item in enemy)
            {
                // 亡者领域：玩家子弹不锁定被控制为友军的敌人
                if (MindControlled.IsMindControlled(item)) continue;

                Vector3 i = item.position;
                float distance = Vector3.Distance(i, transform.position);
                if (distance < shortestdis)
                {
                    shortestdis = distance;
                    shortestrole = item;
                }
            }
            if (shortestrole != null) role = shortestrole.gameObject;
        }
        if(role !=null)
        {
            Vector3 postion1 = role.transform.position;//目标坐标
            Vector3 postion2 = transform.position;//自己坐标
            distance = postion1 - postion2 + new Vector3(0, 2f, 0); // 无条件抬高2f
        }
        else
        {
            Vector3 postion1 = transform.position + new Vector3(1, 0, 0);//目标坐标
            Vector3 postion2 = transform.position;//自己坐标
            distance = postion1 - postion2;
        }
    }
    void FixedUpdate()
    {
        if (cango)
        {
            // 部分子类（如 BulletParasite 触手）使用 LineRenderer 距离判定移动，不依赖物理 Rigidbody
            // 这种情况下 prefab 上没有 Rigidbody 组件，rb 为 null，直接 return
            if (rb == null) return;
            Vector3 vect = new Vector3(distance.x, 0, distance.z).normalized * speed;
            rb.velocity = vect;
            float angle = Mathf.Atan2(distance.z, distance.x) * Mathf.Rad2Deg;
            // 保留预制体的视角倾斜（如 X=45），只更新平面朝向
            transform.rotation = Quaternion.Euler(_baseEuler.x, _baseEuler.y, angle);
            // 不再每帧重新寻找目标，方向在发射时固定
            lifetime -= Time.fixedDeltaTime;
            if (lifetime <= 0)
            {
                Destroy();
            }
        }
    }
}
