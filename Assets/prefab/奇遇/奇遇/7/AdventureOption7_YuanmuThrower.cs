using UnityEngine;

/// <summary>
/// 奇遇7：源木投掷者
/// 效果：清空源木，每扣除一点源木增加一点攻击
/// </summary>
public class AdventureOption7_YuanmuThrower : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "源木投掷者";
        optionDescription = "你把源木扔出去了";
        effectDescription = "清空源木，每扣除五十点源木增加一点攻击";
    }

    public override void Execute()
    {
        var player = GameObject.Find("playerlayer")?.transform.GetChild(0)?.GetComponent<Player>();
        if (player != null && YuanMuManager.Instance != null)
        {
            int yuanmu = YuanMuManager.Instance.Current;
            player.atk += (yuanmu/50);
            YuanMuManager.Instance.Spend(yuanmu);
            ToastManager.Show($"消耗{yuanmu}源木，攻击力+{(yuanmu/50)}");
        }
        base.Execute();
    }
}
