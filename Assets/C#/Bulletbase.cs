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
    //获取子弹所属技能的参数
    public virtual void GetFather()
    {
        damage = fatherskill.damage;
        level = fatherskill.level;
        lifetime = fatherskill.lifetime;
        pass = fatherskill.pass;
        speed = fatherskill.speed;
        size = fatherskill.size;
        player = GameObject.Find("playerlayer").transform.GetChild(0).GetComponent<Attribute>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;          // 禁用重力，防止火球埋进地里
        // 火球术 / 地狱火 的子弹 Sprite 较大（size×scale 后视觉半径更大），
        // 出生时 transform 紧贴 player.position（玩家 pivot 在脚底 → y≈0），
        // 直接锁 Y=0 的话子弹下半截会扎进地面里，看起来像"陷在地里"。
        // 解决：发射前把出生 Y 抬高到 player 身体中部高度（约 1f），再锁 Y。
        // 风箭/飓风/暗齿轮等细子弹原本视觉无问题，统一抬高也不会让它们看起来"漂浮过高"，
        // 因此这里对所有子弹一致处理（保持行为一致性最重要）。
        if (transform.position.y < 1f)
        {
            transform.position = new Vector3(transform.position.x, 1f, transform.position.z);
        }
        rb.constraints = RigidbodyConstraints.FreezePositionY  // 锁定Y轴位置（在抬高后的高度）
                       | RigidbodyConstraints.FreezeRotation;  // 锁定旋转
        enemy = GameObject.Find("enemylayer").transform;
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
            enemy.health -= dealt;
            if (DamageNumberSettings.Visible)
            {
                GameObject atknumber = enemy.atknumber;
                GameObject number = Instantiate(atknumber, enemy.transform.position, default);
                var txt = number.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                txt.text = dealt.ToString();
                if (isCrit) txt.color = new Color32(255, 215, 0, 255);
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
