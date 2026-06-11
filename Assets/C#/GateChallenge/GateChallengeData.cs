using UnityEngine;

/// <summary>
/// 门挑战单层数据
/// </summary>
[System.Serializable]
public class GateFloorData
{
    public int floor;           // 层数
    public int enemyCount;      // 敌人数量
    public int enemyHealth;     // 敌人血量
    public int enemyAtk;        // 敌人攻击
    public int enemyDef;        // 额外防御力
    public int enemySpeed;      // 额外移动速度
    public int enemyEVA;        // 额外闪避率（百分比）
    // 奖励：卡牌刷新次数 +1（固定）
}

/// <summary>
/// 门挑战全局数据（ScriptableObject，挂在 Resources 或 Inspector 里）。
///
/// 数值调整（2026-06）：之前的 13 层数值整体偏低（N5 玩家在前几层基本秒杀），
/// 现按"每层属性显著强于前一层"的曲线重做，并整体压低敌人数量上限、
/// 拉高血量/攻击，使中后期单兵威胁感更强（避免堆怪导致帧率掉太多，同时与
/// 奇遇·愚弄触发后 Manager 的 ×N 倍率叠乘后仍保持可读性）。
///
/// 设计基线（粗略）：
///   血量 ≈ 100 * floor^1.6
///   攻击 ≈ 25  * floor^1.1
///   敌人数量 1~5，根据"小怪潮 / 精英 / 单 boss"节奏分配。
/// </summary>
[CreateAssetMenu(fileName = "GateChallengeConfig", menuName = "GateChallenge/Config")]
public class GateChallengeConfig : ScriptableObject
{
    public GateFloorData[] floors = new GateFloorData[]
    {
        new GateFloorData { floor=1,  enemyCount=2, enemyHealth=150,   enemyAtk=30,  enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=2,  enemyCount=2, enemyHealth=250,   enemyAtk=40,  enemyDef=5,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=3,  enemyCount=5, enemyHealth=120,   enemyAtk=25,  enemyDef=0,  enemySpeed=2, enemyEVA=5  },
        new GateFloorData { floor=4,  enemyCount=3, enemyHealth=400,   enemyAtk=60,  enemyDef=5,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=5,  enemyCount=4, enemyHealth=500,   enemyAtk=80,  enemyDef=10, enemySpeed=0, enemyEVA=10 },
        new GateFloorData { floor=6,  enemyCount=3, enemyHealth=700,   enemyAtk=70,  enemyDef=20, enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=7,  enemyCount=5, enemyHealth=600,   enemyAtk=90,  enemyDef=10, enemySpeed=2, enemyEVA=0  },
        new GateFloorData { floor=8,  enemyCount=1, enemyHealth=2500,  enemyAtk=120, enemyDef=15, enemySpeed=0, enemyEVA=15 },
        new GateFloorData { floor=9,  enemyCount=3, enemyHealth=1500,  enemyAtk=140, enemyDef=20, enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=10, enemyCount=5, enemyHealth=1200,  enemyAtk=130, enemyDef=10, enemySpeed=3, enemyEVA=5  },
        new GateFloorData { floor=11, enemyCount=3, enemyHealth=2200,  enemyAtk=180, enemyDef=25, enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=12, enemyCount=4, enemyHealth=2600,  enemyAtk=200, enemyDef=20, enemySpeed=0, enemyEVA=10 },
        new GateFloorData { floor=13, enemyCount=3, enemyHealth=4000,  enemyAtk=260, enemyDef=30, enemySpeed=0, enemyEVA=10 },
    };
}
