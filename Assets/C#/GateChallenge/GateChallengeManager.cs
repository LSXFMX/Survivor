using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GateChallengeManager : MonoBehaviour
{
    public static GateChallengeManager Instance { get; private set; }

    [Header("UI")]
    public Button gateChallengeButton;
    public GameObject challengePanel;
    public TextMeshProUGUI floorText;
    public TextMeshProUGUI remainText;

    [Header("按钮彩虹颜色（挑战中）")]
    public float colorSpeed = 0.5f; // H 值每秒变化速度（0~1）

    private float _hue = 0f; // 当前色相

    [Header("引用")]
    public GameObject enemyPrefab;
    public GameObject healthBarPrefab;
    public Transform  enemylayer;
    public Player     player;
    public GateChallengeConfig config;

    private const int MAX_FLOOR = 13;

    private bool _inChallenge     = false;
    private bool _unlockedThisRun = false;
    private int  _currentFloor    = 1;
    private int  _remainCount     = 0;
    private List<GateChallengeEnemy> _spawnedEnemies = new List<GateChallengeEnemy>();

    // 按钮组件缓存
    private Image            _btnImage;
    private TextMeshProUGUI  _btnText;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        EnsureRuntimeRefs();

        if (gateChallengeButton != null)
        {
            gateChallengeButton.gameObject.SetActive(false);
            gateChallengeButton.onClick.AddListener(OnChallengeButtonClick);
            _btnImage = gateChallengeButton.GetComponent<Image>();
            _btnText  = gateChallengeButton.GetComponentInChildren<TextMeshProUGUI>();
        }
        if (challengePanel != null) challengePanel.SetActive(false);
    }

    void Update()
    {
        if (_inChallenge && _btnImage != null)
        {
            _hue = (_hue + colorSpeed * Time.deltaTime) % 1f;
            _btnImage.color = Color.HSVToRGB(_hue, 1f, 1f);
        }
    }

    public void Unlock()
    {
        if (_unlockedThisRun) return;

        // 仅 N5 及以上难度才开放门挑战
        if (DifficultyManager.Instance != null)
        {
            string label = DifficultyManager.Instance.Current.label;
            if (label != "N5" && label != "N6" && label != "N7" && label != "N8") return;
        }

        _unlockedThisRun = true;
        if (gateChallengeButton != null) gateChallengeButton.gameObject.SetActive(true);
        ToastManager.Show("门挑战已解锁！");
    }

    public int CurrentFloor => _currentFloor;

    private void OnChallengeButtonClick()
    {
        if (_inChallenge)
        {
            CancelChallenge();
            return;
        }
        if (_currentFloor > MAX_FLOOR) { ToastManager.Show("已通关全部13层！"); return; }
        StartChallenge(_currentFloor);
    }

    private void StartChallenge(int floor)
    {
        EnsureRuntimeRefs();
        if (config == null || config.floors == null || floor - 1 >= config.floors.Length)
        {
            ToastManager.Show("门挑战配置缺失，无法开始挑战");
            Debug.LogError("[GateChallenge] 配置缺失：config/floors 未正确设置");
            return;
        }
        if (enemyPrefab == null)
        {
            ToastManager.Show("门挑战敌人未配置，无法开始挑战");
            Debug.LogError("[GateChallenge] enemyPrefab 未设置");
            return;
        }
        if (enemylayer == null)
        {
            enemylayer = GameObject.Find("enemylayer")?.transform;
            if (enemylayer == null)
            {
                ToastManager.Show("门挑战层级未找到，无法开始挑战");
                Debug.LogError("[GateChallenge] enemylayer 未设置且场景中不存在名为 enemylayer 的对象");
                return;
            }
        }

        // 初次进行门挑战 → 解锁成就装备3（钥匙剑）
        if (floor == 1 && EquipmentSystem.Instance != null)
        {
            bool alreadyUnlocked = EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 3);
            EquipmentSystem.Instance.UnlockEquipment(EquipmentType.AchievementEquipment, 3);
            if (!alreadyUnlocked) ToastManager.Show("成就装备3「钥匙剑」已解锁！");
        }

        _inChallenge = true;
        _spawnedEnemies.Clear();

        GateFloorData data = config.floors[floor - 1];
        _remainCount = data.enemyCount;

        if (challengePanel != null) challengePanel.SetActive(true);
        UpdateUI(floor);

        // 按钮改为"挑战中"
        if (_btnText != null) _btnText.text = "挑战中";

        for (int i = 0; i < data.enemyCount; i++)
        {
            Vector3 spawnPos = GetSpawnPos(i, data.enemyCount);
            GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.Euler(45, 0, 0), enemylayer);
            GateChallengeEnemy e = obj.GetComponent<GateChallengeEnemy>();
            if (e == null) continue;

            e.health    = data.enemyHealth;
            e.healthmax = data.enemyHealth;
            e.atk       = data.enemyAtk;
            e.def       = data.enemyDef;
            e.speed     = data.enemySpeed > 0 ? data.enemySpeed : e.speed;
            e.EVA       = data.enemyEVA;

            _spawnedEnemies.Add(e);

            if (healthBarPrefab != null)
                Instantiate(healthBarPrefab, obj.transform);
        }

        ToastManager.Show($"门挑战第{floor}层开始！击败所有敌人！");
    }

    private void EnsureRuntimeRefs()
    {
        // 打包后若 Inspector 丢引用，尝试运行时补齐
        if (config == null)
        {
            config = Resources.Load<GateChallengeConfig>("GateChallengeConfig");
            if (config == null)
            {
                // 兜底：生成一份默认配置，避免点击无响应
                config = ScriptableObject.CreateInstance<GateChallengeConfig>();
                Debug.LogWarning("[GateChallenge] 未找到 GateChallengeConfig 资源，已使用运行时默认配置");
            }
        }

        if (player == null)
            player = FindObjectOfType<Player>();
    }

    /// <summary>取消挑战，删除所有挑战怪</summary>
    private void CancelChallenge()
    {
        foreach (var e in _spawnedEnemies)
            if (e != null) Destroy(e.gameObject);
        _spawnedEnemies.Clear();

        _inChallenge = false;
        if (challengePanel != null) challengePanel.SetActive(false);
        ResetButton();
        ToastManager.Show("已取消门挑战");
    }

    public void OnEnemyKilled()
    {
        if (!_inChallenge) return;
        _remainCount--;
        UpdateUI(CurrentFloor);
        if (_remainCount <= 0)
            StartCoroutine(CompleteFloor());
    }

    private IEnumerator CompleteFloor()
    {
        yield return new WaitForSeconds(0.5f);

        int floor = CurrentFloor;
        if (ChoiceUI.Instance != null)
            ChoiceUI.Instance.IncreaseAllMaxUpgrades();
        ToastManager.Show($"第{floor}层通关！所有技能升级上限 +1");

        _currentFloor = Mathf.Min(_currentFloor + 1, MAX_FLOOR + 1);
        _inChallenge  = false;
        _spawnedEnemies.Clear();

        if (challengePanel != null) challengePanel.SetActive(false);
        ResetButton();

        if (_currentFloor > MAX_FLOOR)
            ToastManager.Show("恭喜！已通关全部13层门挑战！");
    }

    /// <summary>恢复按钮默认状态</summary>
    private void ResetButton()
    {
        if (_btnText != null) _btnText.text = "门挑战";
        // 颜色固定在当前随机色，不恢复默认
        if (_currentFloor <= MAX_FLOOR && gateChallengeButton != null)
            gateChallengeButton.gameObject.SetActive(true);
    }

    private void UpdateUI(int floor)
    {
        if (floorText != null) floorText.text = $"门挑战 第{floor}层";
        if (remainText == null || config == null || floor - 1 >= config.floors.Length) return;

        GateFloorData data = config.floors[floor - 1];
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"剩余敌人：{Mathf.Max(0, _remainCount)}");
        if (data.enemyDef   > 0) sb.AppendLine($"防御力：{data.enemyDef}");
        if (data.enemySpeed > 0) sb.AppendLine($"移动速度：{data.enemySpeed}");
        if (data.enemyEVA   > 0) sb.AppendLine($"闪避率：{data.enemyEVA}%");
        remainText.text = sb.ToString().TrimEnd();
    }

    private Vector3 GetSpawnPos(int index, int total)
    {
        if (player == null) return Vector3.zero;
        float angle  = (360f / Mathf.Max(total, 1)) * index * Mathf.Deg2Rad;
        float radius = 5f;
        return new Vector3(
            player.transform.position.x + Mathf.Cos(angle) * radius,
            player.transform.position.y,
            player.transform.position.z + Mathf.Sin(angle) * radius);
    }
}
