using System.Collections;
using UnityEngine;

public class Camp : enemy
{
    public int bonusExpCount;
    public Sprite capturedSprite;
    public CampHealthBar healthBar;
    private bool isCaptured = false;

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
