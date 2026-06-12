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

    // N1~N8 难度配置（在 Inspector 中可继续微调）
    //
    // 数值依据：
    //   伤害公式： damage * (1 + atk * 0.1)
    //   玩家 atk 累计：初始2 + 通关装备(N1~N8 全开)63 + 升级满级20 + 攻击灵果5 ≈ 90 (10x 倍率)
    //   奇遇加成：无敌30秒、无尽源木、源木投掷者、风险抹杀等会显著加快玩家成长
    //              使玩家平均输出再 +30%~+80%，因此 HP 倍率必须再上一档。
    //   每个难度对应的"装备解锁阶段"和"玩家伤害倍率"大致如下：
    //     N1: 仅初始     atk≈2-5    倍率1.2-1.5x
    //     N2: + N1 装备  atk≈4-10   倍率1.4-2.0x
    //     N3: + N3 装备  atk≈10-18  倍率2.0-2.8x
    //     N4: + N4 装备  atk≈20-30  倍率3.0-4.0x
    //     N5: + N5 装备  atk≈35-50  倍率4.5-6.0x
    //     N6: + N6 装备  atk≈55-65  倍率6.5-7.5x
    //     N7: + N7 装备  atk≈75-85  倍率8.5-9.5x
    //     N8: + N8 装备  atk≈85-95  倍率9.5-10.5x
    //
    //   设计目标：每个难度普通杂兵需 1-2 发清(技能)，BOSS 需玩家持续 6-12 秒输出。
    //   HP 缩放比 ATK 更激进 —— 玩家伤害膨胀很快，但玩家防御几乎不长。
    //   ATK 倍率克制（避免一击秒玩家），主要靠 HP 来拉关卡时长 / 难度。
    // 对局时长调整（2026-06）：之前 8~15 分整体偏长，前期推进节奏太慢。
    // 新规则——N8 锚定 13 分钟，N2~N8 反向递减 1 分（13/12/11/10/9/8/7），
    // N1 特殊为 1 分钟（教学局，让新人快速跑完一局拿到初心者装备）。
    // 这样总体节奏：N1 极快、N2~N4 中短局、N5~N8 仍是较长的硬核挑战，
    // 但相比原来每个难度都少 2 分钟，整体对局时间缩短 25% 左右。
    public DifficultyConfig[] configs = new DifficultyConfig[]
    {
        // N1：教学局，特殊处理为 1 分钟——让新玩家几乎一定能"通关"领到首套通关装备
        new DifficultyConfig { label = "N1", hpMultiplier = 1.0f,  atkMultiplier = 0.9f,  minutes = 1  },
        // N2：装上 N1 装备后，体验提升一档难度（短局 7 分钟）
        new DifficultyConfig { label = "N2", hpMultiplier = 1.8f,  atkMultiplier = 1.1f,  minutes = 7  },
        // N3：标准难度，玩家 atk≈10~15，怪 HP×3 才能撑住
        new DifficultyConfig { label = "N3", hpMultiplier = 3.0f,  atkMultiplier = 1.4f,  minutes = 8  },
        // N4：玩家 atk≈25，伤害 3.5x，怪 HP×5
        new DifficultyConfig { label = "N4", hpMultiplier = 5.0f,  atkMultiplier = 1.7f,  minutes = 9  },
        // N5：玩家伤害 ~5x，怪 HP×8；ATK 提升克制（玩家 def 跟不上）
        new DifficultyConfig { label = "N5", hpMultiplier = 8.0f,  atkMultiplier = 2.0f,  minutes = 10 },
        // N6：解锁更多技能升级，HP×13 才有压力
        new DifficultyConfig { label = "N6", hpMultiplier = 13.0f, atkMultiplier = 2.5f,  minutes = 11 },
        // N7：玩家伤害 ~9x，怪 HP×20
        new DifficultyConfig { label = "N7", hpMultiplier = 20.0f, atkMultiplier = 3.0f,  minutes = 12 },
        // N8：终极挑战，玩家满配 ~10x 倍率叠奇遇加成。
        // 时长锚定 13 分钟——配合 HP×25、BOSS 击杀节奏 6~12 秒，
        // 普通杂兵 1~2 发清，整体强度仍是最高一档但不至于劝退。
        new DifficultyConfig { label = "N8", hpMultiplier = 25.0f, atkMultiplier = 3.5f,  minutes = 13 },
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
