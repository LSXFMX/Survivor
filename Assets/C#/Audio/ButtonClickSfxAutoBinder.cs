using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全局按钮点击音效自动绑定器：
/// 场景加载或新 UI 激活时，扫描所有 <see cref="Button"/>，在它们的 onClick 上追加一次"点击.mp3"播放。
/// 
/// 设计要点：
/// - 完全零手动配置：通过 RuntimeInitializeOnLoadMethod 自动启动，挂在 [AudioManager] 之外的独立 DontDestroyOnLoad GO 上。
/// - 幂等：每个 Button 只会绑定一次（用 HashSet 记录已绑定的 Button 实例 ID）。
/// - 覆盖运行时新生成 UI：每隔 0.5s 重扫一次激活的 Button；并监听场景加载事件立刻扫一遍。
/// - 不破坏原有 onClick 行为：追加 listener，不 Remove 已有。
/// - 黑名单：如果 GameObject 名字里包含 "NoClickSfx"，则跳过；如果 GameObject 上挂了组件名为 NoClickSfx 的脚本（marker），也跳过。
/// - 已在代码里手动调过 AudioManager.PlaySfx(Click) 的 Button 也会被本类再绑一次——为防重复，需要那些位置移除手动调用，
///   或在该 Button 上挂 NoClickSfx marker。本次默认是接受微小重复（限流 40ms 已防同帧多次）。
/// </summary>
[DefaultExecutionOrder(-8900)]
public class ButtonClickSfxAutoBinder : MonoBehaviour
{
    public static ButtonClickSfxAutoBinder Instance { get; private set; }

    [Tooltip("每隔多少秒重扫一次场景里激活的 Button（用于覆盖运行时 Instantiate 的 UI）。设为 <=0 关闭轮询。")]
    public float rescanInterval = 0.5f;

    // 已绑定的 Button 实例 ID，避免重复挂多次回调
    private readonly HashSet<int> _bound = new HashSet<int>();
    private float _nextScanTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[ButtonClickSfxAutoBinder]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<ButtonClickSfxAutoBinder>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ScanAll();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScanAll();
    }

    private void Update()
    {
        if (rescanInterval <= 0f) return;
        if (Time.unscaledTime < _nextScanTime) return;
        _nextScanTime = Time.unscaledTime + rescanInterval;
        ScanAll();
    }

    /// <summary>立刻扫描场景里所有激活的 Button 并绑定点击音效。可在 UI 大批量 Instantiate 后手动调用以立即覆盖。</summary>
    public static void RescanNow()
    {
        Instance?.ScanAll();
    }

    private void ScanAll()
    {
        // FindObjectsByType 仅扫描激活对象更便宜；未激活的 UI（如未显示的面板）也会在激活后被定时扫描捕获。
#if UNITY_2023_1_OR_NEWER
        Button[] all = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        Button[] all = Object.FindObjectsOfType<Button>(true);
#endif
        for (int i = 0; i < all.Length; i++)
        {
            Button btn = all[i];
            if (btn == null) continue;
            int id = btn.GetInstanceID();
            if (_bound.Contains(id)) continue;

            // 黑名单检测
            if (ShouldSkip(btn))
            {
                _bound.Add(id); // 也记下，避免每次扫都重新检测
                continue;
            }

            btn.onClick.AddListener(PlayClickSfx);
            _bound.Add(id);
        }
    }

    private static bool ShouldSkip(Button btn)
    {
        if (btn == null) return true;
        // 通过 GameObject 名字快速排除（用户给特定 Button 起名加 NoClickSfx 即可静音）
        if (btn.name.IndexOf("NoClickSfx", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        // 通过 marker 组件排除（写一个空 MonoBehaviour 叫 NoClickSfx 并挂上即可）
        if (btn.GetComponent("NoClickSfx") != null) return true;
        return false;
    }

    private static void PlayClickSfx()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
    }
}
