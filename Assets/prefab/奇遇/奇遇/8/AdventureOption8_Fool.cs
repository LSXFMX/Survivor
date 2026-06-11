using UnityEngine;

/// <summary>
/// 奇遇8：愚弄
/// 效果：本局门挑战进度重置到第 1 层，**但门挑战难度永久翻倍（×2 累乘）**。
/// 数值实现统一交给 <see cref="GateChallengeManager.ResetAndDouble"/>，
/// 这里不再走反射改私有字段（之前的写法只重置进度、不翻难度）。
/// </summary>
public class AdventureOption8_Fool : AdventureOptionBase
{
    public override bool IsAvailableInCurrentDifficulty()
    {
        // 先走基类的 oneShot 去重判定，否则本奇遇能在同一局内被反复抽到。
        // 基类会检查 usedOptionsThisRun，已选过就直接 false。
        if (!base.IsAvailableInCurrentDifficulty()) return false;

        if (DifficultyManager.Instance == null) return false;
        string label = DifficultyManager.Instance.Current.label;
        if (!label.StartsWith("N")) return false;
        if (!int.TryParse(label.Substring(1), out int n)) return false;
        return n >= 5;
    }

    private void Reset()
    {
        optionName        = "愚弄";
        optionDescription = "你欺骗了那扇灰白色的门，假装自己未收到赐福";
        effectDescription = "门挑战进度重置至第1层，但门挑战难度翻倍";
    }

    public override void Execute()
    {
        if (GateChallengeManager.Instance != null)
        {
            // 同时完成「重置到第1层」+「难度倍率 ×2」+「提示」。
            GateChallengeManager.Instance.ResetAndDouble();
        }
        base.Execute();
    }
}
