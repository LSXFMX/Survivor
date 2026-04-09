using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : Attribute
{
    public Material material;
    public Material red;
    private Rigidbody rb;
    public Animator ani;
    public battleUI battleUI;
    public Transform SkillList;
    public float PickupRadius;

    // 冲刺（成就装备2解锁）
    [HideInInspector] public bool dashUnlocked = false;
    public float dashDistance = 5f;   // 冲刺距离
    public float dashDuration = 0.15f; // 冲刺持续时间
    public float dashCooldown = 2f;   // 冲刺CD
    private float _dashCDTimer = 0f;
    private bool  _isDashing   = false;

    // 自然回血计时
    private float _regenTimer = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 8.0f;
        Physics.gravity = new Vector3(0, -30f, 0);
    }

    public void levelup()
    {
        level += 1;
        healthmax += 20;
        exp = 0;
        expmax += 20;
        battleUI.openchoice();
    }

    void Update()
    {
        // 自然回血：每秒恢复 regen 点血量
        if (regen > 0)
        {
            _regenTimer += Time.deltaTime;
            if (_regenTimer >= 1f)
            {
                _regenTimer = 0f;
                health = Mathf.Min(health + regen, healthmax);
            }
        }

        // 冲刺 CD 计时
        if (_dashCDTimer > 0f) _dashCDTimer -= Time.deltaTime;

        if (_isDashing) return; // 冲刺中不处理普通移动

        float hmove = Input.GetAxis("Horizontal");
        float vmove = Input.GetAxis("Vertical");
        rb.velocity = new Vector3(hmove, 0, vmove).normalized * speed;

        if (hmove != 0 || vmove != 0)
            ani.SetBool("ismove", true);
        if (hmove == 0 && vmove == 0)
            ani.SetBool("ismove", false);
        if (hmove > 0)
            transform.localScale = new Vector3(1, 1, 1);
        if (hmove < 0)
            transform.localScale = new Vector3(-1, 1, 1);

        // 冲刺触发：已解锁 + 有移动输入 + 按空格 + CD 结束
        if (dashUnlocked && _dashCDTimer <= 0f &&
            Input.GetKeyDown(KeyCode.Space) &&
            (hmove != 0 || vmove != 0))
        {
            Vector3 dir = new Vector3(hmove, 0, vmove).normalized;
            StartCoroutine(DashRoutine(dir));
        }

        if (SkillList.childCount > 0)
        {
            foreach (Transform Skill in SkillList)
            {
                Skillbase s = Skill.GetComponent<Skillbase>();
                s.player = gameObject;
                if (s.CDkey >= s.CDtime)
                    StartCoroutine(s.Useskill());
            }
        }
    }

    private IEnumerator DashRoutine(Vector3 dir)
    {
        _isDashing = true;
        _dashCDTimer = dashCooldown;

        float dashSpeed = dashDistance / dashDuration;
        float elapsed = 0f;
        while (elapsed < dashDuration)
        {
            rb.velocity = dir * dashSpeed;
            elapsed += Time.deltaTime;
            yield return null;
        }

        rb.velocity = Vector3.zero;
        _isDashing = false;
    }

    public void startturnred()
    {
        StartCoroutine(turnred());
    }

    public IEnumerator turnred()
    {
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = red;
        yield return new WaitForSeconds(0.3f);
        transform.GetChild(0).GetComponent<SpriteRenderer>().material = material;
    }

    public void death()
{
    // 如果是分身，直接销毁不触发游戏结束
    if (gameObject.CompareTag("Clone"))
    {
        Destroy(gameObject);
        return;
    }
    
    // 只有原始玩家死亡才触发游戏结束
    if (battleUI != null)
        battleUI.StartCoroutine(battleUI.ReturnToMainPublic(false));
}
}
