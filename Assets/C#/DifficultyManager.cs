using UnityEngine;

/// <summary>
/// 难度管理器单例，存储当前难度及各项倍率。
/// N1 最简单，N5 最难。
/// </summary>
public class DifficultyManager : MonoBehaviour
{
    public static DifficultyManager Instance { get; private set; }

    [System.Serializable]
    public struct DifficultyConfig
    {
        public string label;        // 显示名称，如 "N1"
        public float hpMultiplier;  // 敌人血量倍率
        public float atkMultiplier; // 敌人攻击倍率
        public int   minutes;       // 对局时长（分钟）
    }

    // N1~N8 默认配置，可在 Inspector 中调整
    public DifficultyConfig[] configs = new DifficultyConfig[]
    {
        new DifficultyConfig { label = "N1", hpMultiplier = 0.6f,  atkMultiplier = 0.6f,  minutes = 8  },
        new DifficultyConfig { label = "N2", hpMultiplier = 0.8f,  atkMultiplier = 0.8f,  minutes = 9  },
        new DifficultyConfig { label = "N3", hpMultiplier = 1.0f,  atkMultiplier = 1.0f,  minutes = 10 },
        new DifficultyConfig { label = "N4", hpMultiplier = 1.3f,  atkMultiplier = 1.3f,  minutes = 10 },
        new DifficultyConfig { label = "N5", hpMultiplier = 1.7f,  atkMultiplier = 1.7f,  minutes = 10 },
        new DifficultyConfig { label = "N6", hpMultiplier = 2.5f,  atkMultiplier = 2.5f,  minutes = 12 },
        new DifficultyConfig { label = "N7", hpMultiplier = 3.0f,  atkMultiplier = 3.0f,  minutes = 13 },
        new DifficultyConfig { label = "N8", hpMultiplier = 3.0f,  atkMultiplier = 3.0f,  minutes = 15 },
    };

    // 当前选中的难度索引（0=N1 … 4=N5），默认 N3
    public int CurrentIndex { get; private set; } = 2;

    public DifficultyConfig Current => configs[CurrentIndex];

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetDifficulty(int index)
    {
        CurrentIndex = Mathf.Clamp(index, 0, configs.Length - 1);
        Debug.Log($"[难度] 已选择 {Current.label}  HP×{Current.hpMultiplier}  ATK×{Current.atkMultiplier}  {Current.minutes}min");
    }
}
