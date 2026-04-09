using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class battleUI : MonoBehaviour
{
    public GameObject SkillUI;
    public Transform Skillroom;
    public Transform Skilllist;
    public Player player;
    public Transform menu;
    public TextMeshProUGUI health;
    public TextMeshProUGUI level;
    public TextMeshProUGUI timeui;
    public Transform exp;
    public Image life;
    public int minute;
    public int second;
    public bool startcount;
    public float timer;
    public GameObject choiceUI;
    public TextMeshProUGUI yuanmuText;
    public AdventureUI adventureUI;

    [Header("Boss")]
    public GameObject bossPrefab;       // 蘑菇人Boss（N2~N6）
    public GameObject batBossPrefab;    // 蝙蝠Boss（N7~N13）
    public Transform bossSpawnPoint;
    public Transform enemylayer;
    private bool bossSpawned = false;
    private BossMushroomMan spawnedBoss = null;
    private BossBat spawnedBatBoss = null;
    private int _doubleBossRemain = 0;

    [Header("胜利/失败")]
    public GameObject victoryPanel;
    private bool bossPhase = false;
    private float bossTimer = 0f;
    private const float BOSS_TIME_LIMIT = 90f;

    [Header("难度限制对象")]
    public GameObject adventureUIRoot;  // 奇遇UI根对象（N1/N2隐藏）
    public GameObject yuanmuUIRoot;     // 源木UI根对象（N1/N2隐藏）

    [Header("通关演出")]
    public float slowMoScale = 0.2f;
    public float slowMoDuration = 1.5f;
    public float victoryDelay = 2f;

    [Header("速度按钮")]
    public TextMeshProUGUI speedButtonText;
    private bool isDoubleSpeed = false;

    void Start()
    {
        choiceUI.SetActive(false);
        RefreshSkill();
        startcount = false;
        if (timeui != null) timeui.text = "--:--";
        ApplyDifficultyRestrictions();
        StartCoroutine(DelayedStartTime());
    }

    private void ApplyDifficultyRestrictions()
    {
        if (DifficultyManager.Instance == null) return;
        string label = DifficultyManager.Instance.Current.label;
        if (label == "N1" || label == "N2")
        {
            if (adventureUIRoot != null) adventureUIRoot.SetActive(false);
            if (yuanmuUIRoot != null)    yuanmuUIRoot.SetActive(false);
        }
    }

    private IEnumerator DelayedStartTime()
    {
        yield return null;
        starttime();
    }

    public void RefreshSkill()
    {
        if (Skillroom.childCount > 0)
        {
            foreach (Transform s in Skillroom)
                Destroy(s.gameObject);
        }
        foreach (Transform playerskill in Skilllist)
        {
            GameObject skill = Instantiate(SkillUI, Skillroom);
            Skillbase sb = playerskill.GetComponent<Skillbase>();
            skill.transform.GetChild(0).GetComponent<Image>().sprite = sb.icon;
            skill.transform.GetChild(1).GetComponent<Image>().sprite = sb.icon;
            UpdateSkillCountText(skill, sb);
        }
    }

    private void UpdateSkillCountText(GameObject skillUI, Skillbase sb)
    {
        TextMeshProUGUI countText = skillUI.transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        if (countText == null || ChoiceUI.Instance == null) return;

        string group = "";
        int max = 0;
        if (ChoiceUI.Instance.skillEntries != null)
        {
            foreach (var entry in ChoiceUI.Instance.skillEntries)
            {
                if (entry.upgradeOptions == null || entry.upgradeOptions.Count == 0) continue;
                var first = entry.upgradeOptions[0].GetComponent<Upgradeoptionsbase>();
                if (first != null && !string.IsNullOrEmpty(first.upgradeGroup))
                {
                    var learn = entry.learnSkillPrefab?.GetComponent<getnewskill>();
                    if (learn != null && learn.skill != null && learn.skill.Skillname == sb.Skillname)
                    {
                        group = first.upgradeGroup;
                        max = ChoiceUI.Instance.GetEffectiveMaxUpgrades(first);
                        break;
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(group) && max > 0)
            countText.text = ChoiceUI.Instance.GetGroupCount(group) + "/" + max;
        else
            countText.text = "";
    }

    public void starttime()
    {
        minute = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.minutes : 10;
        second = 0;
        timer = 0;
        startcount = true;
    }

    void Update()
    {
        int index = 0;
        if (Skillroom.childCount > 0)
        {
            foreach (Transform skill in Skilllist)
            {
                Skillbase s = skill.GetComponent<Skillbase>();
                float ratio = s.CDkey / s.CDtime;
                Skillroom.transform.GetChild(index).GetChild(1).GetComponent<Image>().fillAmount = 1 - ratio;
                index++;
                if (index > Skillroom.childCount) index = 0;
            }
        }

        life.fillAmount = (float)player.health / (float)player.healthmax;
        exp.localScale = new Vector3((float)player.exp / (float)player.expmax, 1, 1);
        level.text = "level:" + player.level;
        health.text = player.health + "/" + player.healthmax;

        if (startcount)
        {
            timer += Time.deltaTime;
            if (timer >= 1)
            {
                timer = 0;
                second--;
                if (second < 0) { minute--; second = 59; }
                timeui.text = minute + (second < 10 ? ":0" : ":") + second;
                if (minute <= 0 && second <= 0) timeover();
            }
        }

        if (bossPhase)
        {
            bossTimer -= Time.deltaTime;
            if (timeui != null)
                timeui.text = "Boss: " + Mathf.CeilToInt(Mathf.Max(0, bossTimer)).ToString();
            if (bossTimer <= 0f)
            {
                bossPhase = false;
                StartCoroutine(ReturnToMain(false));
            }
        }

        if (YuanMuManager.Instance != null && yuanmuText != null)
            yuanmuText.text = ": " + YuanMuManager.Instance.Current;

        // ESC 快捷键开关暂停菜单
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (menu.gameObject.activeSelf)
                Click_continue();
            else
                Click_menu();
        }
    }

    public void ResumeTime()
    {
        Time.timeScale = isDoubleSpeed ? 2f : 1f;
    }

    public void openchoice()
    {
        Time.timeScale = 0;
        choiceUI.SetActive(true);
    }

    public void timeover()
    {
        startcount = false;
        // N1：不生成Boss，直接胜利
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.Current.label == "N1")
        {
            StartCoroutine(ReturnToMain(true));
            return;
        }
        if (!bossSpawned) SpawnBoss();
    }

    public void OnBossDefeated()
    {
        if (!bossPhase) return;

        // 双Boss模式：需要两只都死
        if (_doubleBossRemain > 0)
        {
            _doubleBossRemain--;
            if (_doubleBossRemain > 0) return; // 还有Boss存活
        }

        bossPhase = false;
        StartCoroutine(ReturnToMain(true));
    }

    /// <summary>供 Player.death() 调用的公开包装</summary>
    public IEnumerator ReturnToMainPublic(bool victory) => ReturnToMain(victory);

    private IEnumerator ReturnToMain(bool victory)
    {
        if (victory)
        {
            Time.timeScale = slowMoScale;
            yield return new WaitForSecondsRealtime(slowMoDuration);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(victoryDelay);
            ClearRecordManager.Instance?.RecordClear();
        }
        else
        {
            Time.timeScale = 1f;
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            var txt = victoryPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (txt != null) txt.text = victory ? "胜利！" : "失败...";
        }

        ToastManager.Show("3秒后返回主菜单...");
        yield return new WaitForSecondsRealtime(3f);
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void SpawnBoss()
    {
        string label = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.label : "N2";

        // N7/N8 生成蝙蝠Boss，N6 生成双蘑菇人Boss，其余生成单蘑菇人Boss
        bool isBatBoss    = label == "N7" || label == "N8";
        bool isDoubleBoss = label == "N6";

        bossSpawned = true;
        bossPhase   = true;
        bossTimer   = BOSS_TIME_LIMIT;
        startcount  = false;

        if (isBatBoss)
        {
            if (batBossPrefab == null) return;
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(batBossPrefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedBatBoss = obj.GetComponent<BossBat>();
            if (spawnedBatBoss != null) spawnedBatBoss.battleUI = this;
            Debug.Log("[Boss] 蝙蝠Boss已生成");
        }
        else if (isDoubleBoss)
        {
            if (bossPrefab == null) return;
            // 生成两只蘑菇人，需要两只都死才算通关
            _doubleBossRemain = 2;
            for (int i = 0; i < 2; i++)
            {
                Vector3 pos = GetBossSpawnPos(i, 2);
                GameObject obj = Instantiate(bossPrefab, pos, Quaternion.Euler(45, 0, 0),
                    enemylayer != null ? enemylayer : null);
                BossMushroomMan b = obj.GetComponent<BossMushroomMan>();
                if (b != null) b.battleUI = this;
            }
            Debug.Log("[Boss] 双蘑菇人Boss已生成");
        }
        else
        {
            if (bossPrefab == null) return;
            Vector3 pos = GetBossSpawnPos(0, 1);
            GameObject obj = Instantiate(bossPrefab, pos, Quaternion.Euler(45, 0, 0),
                enemylayer != null ? enemylayer : null);
            spawnedBoss = obj.GetComponent<BossMushroomMan>();
            if (spawnedBoss != null) spawnedBoss.battleUI = this;
            Debug.Log("[Boss] 蘑菇人Boss已生成");
        }
    }

    private Vector3 GetBossSpawnPos(int index, int total)
    {
        if (bossSpawnPoint != null) return bossSpawnPoint.position;
        const float MAP_X_MIN = -90f, MAP_X_MAX = 90f;
        const float MAP_Z_MIN = -90f, MAP_Z_MAX = 90f;
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        float angle = (Random.Range(0f, 360f) + index * (360f / Mathf.Max(total, 1))) * Mathf.Deg2Rad;
        float dist  = Random.Range(8f, 20f);
        float x = Mathf.Clamp(playerPos.x + Mathf.Cos(angle) * dist, MAP_X_MIN, MAP_X_MAX);
        float z = Mathf.Clamp(playerPos.z + Mathf.Sin(angle) * dist, MAP_Z_MIN, MAP_Z_MAX);
        return new Vector3(x, playerPos.y, z);
    }

    public void Click_menu()
    {
        menu.gameObject.SetActive(true);
        Time.timeScale = 0;
    }

    public void Click_continue()
    {
        menu.gameObject.SetActive(false);
        ResumeTime();
    }

    public void Click_toggleSpeed()
    {
        isDoubleSpeed = !isDoubleSpeed;
        Time.timeScale = isDoubleSpeed ? 2f : 1f;
        if (speedButtonText != null)
            speedButtonText.text = isDoubleSpeed ? "X2" : "X1";
    }

    public void Click_settings() { }
    public void Click_instructions() { }
    public void Click_exitgame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
