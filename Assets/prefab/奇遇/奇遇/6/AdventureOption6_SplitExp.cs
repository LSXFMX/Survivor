using UnityEngine;

/// <summary>
/// 奇遇6：自己掰开
/// 效果：经验石触发两次（每颗经验石给双倍经验）
/// </summary>
public class AdventureOption6_SplitExp : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "自己掰开";
        optionDescription = "你把经验石掰成两半用，真好用";
        effectDescription = "经验石触发两次";
    }

    public override void Execute()
    {
        getexp.triggerMultiplier = 2;
        ToastManager.Show("经验石现在触发两次！");
        base.Execute();
    }
}
