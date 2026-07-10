using System.Collections;
using UnityEngine;

/// <summary>
/// 奇遇5：Something for nothing
/// 效果：无敌30秒（玩家不受伤害）
/// </summary>
public class AdventureOption5_SomethingForNothing : AdventureOptionBase
{
    /// <summary>防止短时间内重复触发导致 EVA 恢复逻辑错乱（第二次的 originalEVA=100 → 恢复后永久无敌）。</summary>
    private static bool _invincibleActive = false;

    private void Reset()
    {
        optionName        = "Something for nothing";
        optionDescription = "无敌30秒";
        effectDescription = "这个选项不该出现在这里的...";
    }

    /// <summary>无尽模式下不出现该奇遇。</summary>
    public override bool IsAvailableInCurrentDifficulty()
    {
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.IsEndless)
            return false;
        return base.IsAvailableInCurrentDifficulty();
    }

    public override void Execute()
    {
        base.Execute();
        Player player = null;
        var playerLayer = GameObject.Find("playerlayer")?.transform;
        if (playerLayer != null)
        {
            foreach (Transform t in playerLayer)
            {
                if (t != null && t.CompareTag("Player"))
                {
                    player = t.GetComponent<Player>();
                    break;
                }
            }
            if (player == null && playerLayer.childCount > 0)
                player = playerLayer.GetChild(0).GetComponent<Player>();
        }
        if (player != null)
        {
            // 若无敌已在生效中，只延长持续时间，不复用已变形的 EVA 快照
            if (_invincibleActive)
            {
                Debug.Log("[无敌奇遇] 已在无敌中，跳过额外协程（防止 EVA 恢复错乱）");
                return;
            }
            player.StartCoroutine(InvincibleRoutine(player));
        }
    }

    private IEnumerator InvincibleRoutine(Player player)
    {
        _invincibleActive = true;
        int originalEVA = player.EVA; // 在改为 100 之前快照
        player.EVA = 100;
        ToastManager.Show("无敌30秒！");
        yield return new WaitForSeconds(30f);
        player.EVA = originalEVA;
        _invincibleActive = false;
        ToastManager.Show("无敌状态结束");
    }
}
