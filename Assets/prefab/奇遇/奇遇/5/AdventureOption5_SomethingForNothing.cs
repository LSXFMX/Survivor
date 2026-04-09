using System.Collections;
using UnityEngine;

/// <summary>
/// 奇遇5：Something for nothing
/// 效果：无敌30秒（玩家不受伤害）
/// </summary>
public class AdventureOption5_SomethingForNothing : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "Something for nothing";
        optionDescription = "无敌30秒";
        effectDescription = "这个选项不该出现在这里的...";
    }

    public override void Execute()
    {
        base.Execute();
        var player = GameObject.Find("playerlayer")?.transform.GetChild(0)?.GetComponent<Player>();
        if (player != null)
            player.StartCoroutine(InvincibleRoutine(player));
    }

    private IEnumerator InvincibleRoutine(Player player)
    {
        // 临时将 EVA 设为 100（100%闪避=无敌）
        int originalEVA = player.EVA;
        player.EVA = 100;
        ToastManager.Show("无敌30秒！");
        yield return new WaitForSeconds(30f);
        player.EVA = originalEVA;
        ToastManager.Show("无敌状态结束");
    }
}
