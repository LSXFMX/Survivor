using UnityEngine;

/// <summary>
/// 奇遇8：愚弄
/// 效果：门挑战的进度重置（本局内回到第1层）
/// </summary>
public class AdventureOption8_Fool : AdventureOptionBase
{
    public override bool IsAvailableInCurrentDifficulty()
    {
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
        effectDescription = "门挑战的进度重置";
    }

    public override void Execute()
    {
        if (GateChallengeManager.Instance != null)
        {
            // 重置本局门挑战进度到第1层
            var field = typeof(GateChallengeManager).GetField("_currentFloor",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(GateChallengeManager.Instance, 1);
            ToastManager.Show("门挑战进度已重置！");
        }
        base.Execute();
    }
}
