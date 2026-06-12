using System.Collections;
using UnityEngine;

public class Camp : enemy
{
    public int bonusExpCount;
    public Sprite capturedSprite;
    public CampHealthBar healthBar;
    private bool isCaptured = false;

    /// <summary>
    /// 营地是否已被玩家占领（已变为友军建筑）。
    /// 暴露为只读属性，供索敌系统（如风箭 SkillWindArrow / BulletWindArrow）跳过友方营地。
    /// 写入仍只在内部 Capture() 中进行，外部无法误改。
    /// 
    /// 历史背景（2026-06）：营地占领与"亡者领域魅惑（_mindControlledFlag）"是两套独立机制，
    /// 占领后 tag/layer/_mindControlledFlag 均不变，外部系统过去无法识别"已占营地"，
    /// 导致风箭等远程技能仍把它当成敌人攻击。此属性是统一的"已占领"判定信号。
    /// </summary>
    public bool IsCaptured => isCaptured;

    private Vector3 fixedPosition;

    protected new void OnEnable()
    {
        // 手动重复 enemy.OnEnable 的初始化，因为父类方法是 private
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
        }
        fixedPosition = transform.position;

        // 应用难度倍率到营地血量
        if (DifficultyManager.Instance != null)
        {
            var cfg = DifficultyManager.Instance.Current;
            healthmax = Mathf.RoundToInt(healthmax * cfg.hpMultiplier);
            health    = healthmax;
        }
    }

    protected override void FixedUpdate()
    {
        // 强制保持位置不变
        transform.position = fixedPosition;
        rolestate = state.idle;
    }

    protected override void OnCollisionEnter(Collision collision)
    {
        // 空方法：禁用对玩家的碰撞伤害
    }

    public override void Destroy1()
    {
        if (isCaptured) return;
        Capture();
    }

    private void Capture()
    {
        isCaptured = true;

        // 一劳永逸：将已占领营地移出 enemylayer，这样所有遍历 enemylayer 的索敌逻辑
        // （风箭、火球、地狱火、暗齿轮、血族血统蝙蝠、亡者领域友军……）
        // 自动跳过已占营地，无需每个技能单独写 IsCaptured 判断。
        // 移到场景根节点下一个名为 "capturedCampLayer" 的容器中（自动创建）。
        Transform capturedLayer = GetOrCreateCapturedCampLayer();
        transform.SetParent(capturedLayer);

        // 同时禁用所有 Collider，防止子弹 OnTriggerEnter 仍然能碰到已占营地
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (capturedSprite != null)
            GetComponent<SpriteRenderer>().sprite = capturedSprite;

        for (int i = 0; i < bonusExpCount; i++)
            Instantiate(expstone, transform.position, Quaternion.Euler(45, 0, 0));

        if (healthBar != null)
            healthBar.Hide();

        int campCount = PlayerPrefs.GetInt("CampCapturedCount", 0) + 1;
        PlayerPrefs.SetInt("CampCapturedCount", campCount);
        PlayerPrefs.Save();

        ToastManager.Show($"已攻占营地，每秒源木 +1 (累计攻占: {campCount}/100)");

        if (campCount >= 100 && EquipmentSystem.Instance != null && !EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 5))
        {
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 5);
            ToastManager.Show("成就装备5「扎营大师」已解锁！");
        }

        StartCoroutine(YuanMuCoroutine());
    }

    private static Transform _capturedCampLayerCache;

    private static Transform GetOrCreateCapturedCampLayer()
    {
        if (_capturedCampLayerCache != null) return _capturedCampLayerCache;
        GameObject go = GameObject.Find("capturedCampLayer");
        if (go == null)
        {
            go = new GameObject("capturedCampLayer");
            // 保持与 enemylayer 同级（场景根），仅做逻辑容器
        }
        _capturedCampLayerCache = go.transform;
        return _capturedCampLayerCache;
    }

    private IEnumerator YuanMuCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            // 基础每秒+1，加上奇遇4的额外加成
            int bonus = YuanMuManager.Instance != null ? YuanMuManager.Instance.perSecond : 0;
            YuanMuManager.Instance?.Add(1 + bonus);
        }
    }
}
