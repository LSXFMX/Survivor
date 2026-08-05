using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 继承装备素材**启动预热器**。
///
/// 问题：<c>InheritEquipmentAssets.SafeLoad</c> 依赖
/// <c>BulletParasite.LoadSpriteFallback(conservative:true)</c> 去背景，
/// 内部是 Blit → GetPixels32 → BFS 泛洪。19 张 AI 素材跑一遍要 4~5 秒，
/// 而这些全都发生在玩家**首次点开装备页面**的那一帧里，体感就是卡死。
///
/// 解决（两层）：
///   1. 本组件在游戏启动后自动创建（<see cref="Bootstrap"/>，DontDestroyOnLoad），
///      在主菜单出现后**一次性**把全部素材加载进静态缓存。
///      刻意不分帧：分帧虽然摊薄了单帧耗时，但玩家两三秒后就点进装备界面，
///      预热没跑完照样卡；集中在启动阶段做完才能保证"任何时候点开都不卡"。
///   2. <c>InheritEquipmentAssets</c> 另有一层磁盘缓存（抠好的 PNG 存到
///      persistentDataPath），二次启动连泛洪都不用跑，预热几乎瞬间完成。
///
/// 另外场景切换会调 <c>ResetCaches</c> 清空静态缓存（Sprite 绑定的 Texture2D
/// 会随旧场景失效），所以这里监听 sceneLoaded 重新预热；此时磁盘缓存已命中，
/// 重建很快，用分帧版本避免影响进场手感。
///
/// 之所以用 RuntimeInitializeOnLoadMethod 而不是往场景里拖一个对象：
/// 项目里 5 个分类容器都是空节点、继承装备整套 UI 都是运行时构建的，
/// 保持"零场景依赖"，换场景/换分支都不会漏配。
/// </summary>
public class InheritEquipmentPrewarmer : MonoBehaviour
{
    private static InheritEquipmentPrewarmer _instance;
    /// <summary>整个进程内是否已经做过那次"启动一次性预热"。</summary>
    private static bool _bootPrewarmDone;
    private Coroutine _running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
 {
        if (_instance != null) return;
  var go = new GameObject("__InheritEquipmentPrewarmer");
     _instance = go.AddComponent<InheritEquipmentPrewarmer>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
  {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Restart();
    }

  private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // 场景重载后静态缓存已被 enemy.OnAnySceneLoaded → ResetSceneCaches 清空，重新预热
        Restart();
    }

    private void Restart()
 {
      if (_running != null) StopCoroutine(_running);
        _running = StartCoroutine(Run());
    }

    private IEnumerator Run()
  {
        // 让首帧先把画面渲染出来，避免和场景初始化抢时间（否则黑屏更久）
   yield return null;
  yield return null;

        float t0 = Time.realtimeSinceStartup;

        if (!_bootPrewarmDone)
        {
            // 启动后的第一次：一次性做完，确保玩家首次点开装备界面零等待
            InheritEquipmentAssets.PrewarmAll();

            // 顺带把存档界面那40+ 个装备图标（通关/成就/好感度/抽卡）也抠好放进缓存。
            // 它们和继承装备素材是同一套 Blit+泛洪流程，且数量更多，
            // 是"点开存档界面卡四五秒 / 切换栏位卡"的主要来源。
            int n = 0;
            try { n = EquipmentIcon.PrewarmAllIconsInScene(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Inherit] 装备图标预热失败（忽略）：{ex.Message}");
            }

            _bootPrewarmDone = true;
            Debug.Log($"[Inherit] 启动预热完成（一次性）：继承素材 + {n} 个装备图标，耗时 " +
                      $"{Time.realtimeSinceStartup - t0:0.00}s；后续打开装备界面不再卡顿");
        }
        else
        {
      // 场景切换后的重建：磁盘缓存已命中，分帧做以免影响进场手感
   yield return InheritEquipmentAssets.PrewarmRoutine();
       Debug.Log($"[Inherit] 场景切换后素材重建完成，耗时 " +
      $"{Time.realtimeSinceStartup - t0:0.00}s");
        }

_running = null;
    }
}
