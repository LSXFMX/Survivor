using UnityEngine;

/// <summary>
/// 存档槽自动落盘守卫。
///
/// <see cref="SaveSlotManager"/> 只在**切槽那一刻**把 PlayerPrefs 导出成槽文件。
/// 如果玩家玩了很久却一直没切档，槽文件就会停留在上次切档时的旧内容 ——
/// 一旦之后切到别的槽再切回来，中间这段进度就都丢了。
///
/// 所以这里挂一个常驻守卫，在三个时机把当前进度同步到槽文件：
///   ① 每 <see cref="AUTO_SAVE_INTERVAL"/> 秒一次（真实时间，不受暂停/倍速影响）；
///   ② 应用暂停 / 失去焦点（切后台、Alt-Tab）；
///   ③ 退出游戏。
///
/// 导出本身只是几百次 HasKey + 一次小文件写入，开销可忽略。
/// 用 RuntimeInitializeOnLoadMethod 自动挂载，场景里不需要拖任何东西。
/// </summary>
public class SaveSlotAutoSaver : MonoBehaviour
{
    private const float AUTO_SAVE_INTERVAL = 60f;

    private float _timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<SaveSlotAutoSaver>() != null) return;
        var go = new GameObject("[SaveSlotAutoSaver]");
        DontDestroyOnLoad(go);
        go.AddComponent<SaveSlotAutoSaver>();
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < AUTO_SAVE_INTERVAL) return;
        _timer = 0f;
        SaveSlotManager.SaveCurrentSlot();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveSlotManager.SaveCurrentSlot();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) SaveSlotManager.SaveCurrentSlot();
    }

    private void OnApplicationQuit()
    {
        SaveSlotManager.SaveCurrentSlot();
    }
}
