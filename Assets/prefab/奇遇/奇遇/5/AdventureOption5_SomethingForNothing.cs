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

    /// <summary>当前无敌协程引用（供 ResetRunCounter 强制终止）。</summary>
    private static Coroutine _invincibleCoroutine;

    /// <summary>
    /// 新一局开始时清零。
    ///
    /// 为什么必须清零：
    ///   ① <see cref="AdventureEventManager.Awake"/> 已对所有奇遇做了一次全量重置
    ///      （usedOptionsThisRun.Clear、各多选计数归零），但这个 static 字段没有被清，
    ///      上一局在无敌期间死亡/退出 → 永远卡在 true → 之后选无敌直接跳过、完全没效果。
    ///   ② 同时终止上一次残留的协程（若上一局在 WaitForSeconds 中场景已被销毁，
    ///      Coroutine 自动失效，但显式停掉是最干净的）。
    /// </summary>
    public static void ResetRunCounter()
    {
        _invincibleActive = false;
        _invincibleCoroutine = null;
    }

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
            _invincibleCoroutine = player.StartCoroutine(InvincibleRoutine(player));
        }
    }

    private IEnumerator InvincibleRoutine(Player player)
    {
        _invincibleActive = true;
        int originalEVA = player.EVA; // 在改为 100 之前快照
        player.EVA = 100;
        ToastManager.Show("无敌30秒！");
        yield return new WaitForSeconds(30f);
        // 协程在 player 已被销毁后醒来的边缘情况：静默退出，EVA 随对象一起销毁了。
        if (player == null || player.health <= 0)
        {
            _invincibleActive = false;
            _invincibleCoroutine = null;
            yield break;
        }
        player.EVA = originalEVA;
        _invincibleActive = false;
        _invincibleCoroutine = null;
        ToastManager.Show("无敌状态结束");
    }
}
