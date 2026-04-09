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
/// 门挑战全局数据（ScriptableObject，挂在 Resources 或 Inspector 里）
/// </summary>
[CreateAssetMenu(fileName = "GateChallengeConfig", menuName = "GateChallenge/Config")]
public class GateChallengeConfig : ScriptableObject
{
    public GateFloorData[] floors = new GateFloorData[]
    {
        new GateFloorData { floor=1,  enemyCount=1, enemyHealth=50,  enemyAtk=20, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=2,  enemyCount=1, enemyHealth=70,  enemyAtk=10, enemyDef=5,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=3,  enemyCount=4, enemyHealth=20,  enemyAtk=5,  enemyDef=0,  enemySpeed=5,enemyEVA=0  },
        new GateFloorData { floor=4,  enemyCount=2, enemyHealth=50,  enemyAtk=10, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=5,  enemyCount=3, enemyHealth=100, enemyAtk=30, enemyDef=0,  enemySpeed=0, enemyEVA=10 },
        new GateFloorData { floor=6,  enemyCount=2, enemyHealth=120, enemyAtk=5,  enemyDef=15, enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=7,  enemyCount=4, enemyHealth=150, enemyAtk=30, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=8,  enemyCount=1, enemyHealth=300, enemyAtk=15, enemyDef=0,  enemySpeed=0, enemyEVA=10 },
        new GateFloorData { floor=9,  enemyCount=2, enemyHealth=300, enemyAtk=40, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=10, enemyCount=4, enemyHealth=200, enemyAtk=30, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=11, enemyCount=2, enemyHealth=350, enemyAtk=70, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=12, enemyCount=3, enemyHealth=400, enemyAtk=50, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
        new GateFloorData { floor=13, enemyCount=3, enemyHealth=600, enemyAtk=60, enemyDef=0,  enemySpeed=0, enemyEVA=0  },
    };
}
