using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音效/BGM 管理器。
/// 
/// 使用方式（零手动配置）：
/// 1. 把 9 个 mp3 文件从 像素幸存者资源包/音效/ 复制（或移动）到 Assets/Resources/Audio/，并保持原文件名：
///    - 主BGM.mp3、战斗BGM.mp3、点击.mp3、收集.mp3
///    - 火球发射.mp3、火球击中.mp3、冰击中.mp3、燃烧.mp3、被击中.mp3
/// 2. 不需要在任何 GameObject 上手动挂载，本类通过 RuntimeInitializeOnLoadMethod 自动建管理器。
/// 3. 进入主菜单场景自动播主BGM，进入战斗场景自动切战斗BGM；技能/碰撞/按钮等处通过 AudioManager.PlaySfx(...) 触发。
/// 
/// 如果 Resources/Audio 里没有对应文件，所有调用都安全 no-op，不会报错。
///
/// === 程序化合成音效（_tools/gen_audio_sfx.py）===
/// 现支持以下 6 个 wav，由脚本程序化合成后落到 Resources/Audio/：
///   • 亡者复活.wav    —— 无罪复活 Boss 演出压迫感音效（3.2s）
///   • 亡者回血.wav    —— 光电融入回血（0.4s 清亮上行琶音）
///   • 升级.wav        —— 玩家升级（0.6s 明亮琶音）
///   • 经验拾取.wav    —— 经验石拾取（轻量替代品，可选）
///   • 按键悬停.wav    —— UI 悬停（极轻）
///   • Boss出现.wav    —— 普通 Boss 入场预警（1.2s）
/// 这些 wav 与原 mp3 加载逻辑完全一致（Resources.Load<AudioClip> 自动识别扩展名）。
/// </summary>
[DefaultExecutionOrder(-9000)]
public class AudioManager : MonoBehaviour
{
    public enum SfxKey
    {
        Click,         // 点击.mp3
        Pickup,        // 收集.mp3
        Hit,           // 被击中.mp3（玩家/敌人通用命中）
        FireballCast,  // 火球发射.mp3
        FireballHit,   // 火球击中.mp3
        IceHit,        // 冰击中.mp3
        Burn,          // 燃烧.mp3

        // ── 程序化合成新增 ───────────────────────────────────────────
        TombRevive,    // 亡者复活.wav —— 无罪复活 Boss 压迫感演出音效
        TombHeal,      // 亡者回血.wav —— 光电融入回 0.5% 血
        LevelUp,       // 升级.wav     —— 玩家升级
        XpPickup,      // 经验拾取.wav —— 经验石（轻量替代 Pickup，可按需切换）
        UiHover,       // 按键悬停.wav —— UI 悬停
        BossAppear,    // Boss出现.wav —— 普通 Boss 入场预警

        DragonFlap,    // 龙翼扇动.wav —— 龙王进场翅膀扇动 whoosh
        DragonRoar,    // 龙吼.wav —— 龙王咆哮

        // ── 玩家技能音效（_tools/gen_skill_sfx.py 程序化合成）─────────
        WindCast,      // 风箭发射.wav   —— 风系：轻盈破空 whoosh
        WindHit,       // 风箭命中.wav   —— 风系：干脆切割
        Hurricane,     // 飓风.wav       —— 风系：环形气旋爆发
        WindBlade,     // 风刃.wav       —— 风系：锐利挥砍
        IceCast,       // 冰霜发射.wav   —— 冰系：结晶凝聚 + 霜冻嘶声
        DarkCast,      // 暗影发射.wav   —— 暗影：低频压迫 + 齿轮摩擦
        BloodCast,     // 血族发射.wav   —— 血族：液体涌动 + 心跳脉动
        BloodHit,      // 血族命中.wav   —— 血族：湿润 splat
        ParasiteCast,  // 寄生发射.wav   —— 寄生：有机蠕动触手
        SporeCast,     // 孢子扩散.wav   —— 自然：柔和孢子噗散
        TombCast,      // 亡者领域.wav   —— 幽冥：阴森低语共鸣
        HellfireCast,  // 地狱火发射.wav —— 火系：三叉戟下坠 + 烈焰轰鸣

        // ── Boss 技能音效 ────────────────────────────────────────────
        BossSlash,     // Boss挥砍.wav   —— 物理：厚重挥砍 + 金属锐鸣
        BossQuake,     // Boss震地.wav   —— 物理：极低频地鸣 + 碎石
        BossBreath,    // Boss吐息.wav   —— 吐息：持续气流喷射
        BossSpit,      // Boss吐射.wav   —— 黏腻弹丸喷吐
        BossDive,      // Boss俯冲.wav   —— 俯冲下压 whoosh
        BossSummon,    // Boss召唤.wav   —— 诡异召唤仪式共鸣
        BossCharge,    // Boss蓄力.wav   —— 技能前摇紧张上扬
    }

    public enum BgmKey
    {
        Main,        // 主BGM.mp3
        Battle,      // 战斗BGM.mp3
        DragonBattle,// 龙王战.wav —— 最终龙王战斗曲（程序化合成，激昂）
    }

    private const string ResAudioRoot = "Audio/";

    // SfxKey -> Resources/Audio/ 下的资源名（不含后缀）
    private static readonly Dictionary<SfxKey, string> SfxFile = new Dictionary<SfxKey, string>
    {
        { SfxKey.Click,        "点击" },
        { SfxKey.Pickup,       "收集" },
        { SfxKey.Hit,          "被击中" },
        { SfxKey.FireballCast, "火球发射" },
        { SfxKey.FireballHit,  "火球击中" },
        { SfxKey.IceHit,       "冰击中" },
        { SfxKey.Burn,         "燃烧" },

        // 程序化合成 wav（_tools/gen_audio_sfx.py）
        { SfxKey.TombRevive,   "亡者复活" },
        { SfxKey.TombHeal,     "亡者回血" },
        { SfxKey.LevelUp,      "升级" },
        { SfxKey.XpPickup,     "经验拾取" },
        { SfxKey.UiHover,      "按键悬停" },
        { SfxKey.BossAppear,   "Boss出现" },
        { SfxKey.DragonFlap,   "龙翼扇动" },
        { SfxKey.DragonRoar,   "龙吼" },

        // 玩家技能（_tools/gen_skill_sfx.py）
        { SfxKey.WindCast,     "风箭发射" },
        { SfxKey.WindHit,      "风箭命中" },
        { SfxKey.Hurricane,    "飓风" },
        { SfxKey.WindBlade,    "风刃" },
        { SfxKey.IceCast,      "冰霜发射" },
        { SfxKey.DarkCast,     "暗影发射" },
        { SfxKey.BloodCast,    "血族发射" },
        { SfxKey.BloodHit,     "血族命中" },
        { SfxKey.ParasiteCast, "寄生发射" },
        { SfxKey.SporeCast,    "孢子扩散" },
        { SfxKey.TombCast,     "亡者领域" },
        { SfxKey.HellfireCast, "地狱火发射" },

        // Boss 技能
        { SfxKey.BossSlash,    "Boss挥砍" },
        { SfxKey.BossQuake,    "Boss震地" },
        { SfxKey.BossBreath,   "Boss吐息" },
        { SfxKey.BossSpit,     "Boss吐射" },
        { SfxKey.BossDive,     "Boss俯冲" },
        { SfxKey.BossSummon,   "Boss召唤" },
        { SfxKey.BossCharge,   "Boss蓄力" },
    };

    private static readonly Dictionary<BgmKey, string> BgmFile = new Dictionary<BgmKey, string>
    {
        { BgmKey.Main,         "主BGM" },
        { BgmKey.Battle,       "战斗BGM" },
        { BgmKey.DragonBattle, "龙王战" },
    };

    public static AudioManager Instance { get; private set; }

    [Header("音量")]
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.8f;

    private const string PpKeyBgm = "AudioManager.bgmVolume";
    private const string PpKeySfx = "AudioManager.sfxVolume";

    [Header("调试")]
    public bool logMissingClip = false;

    private AudioSource _bgmSource;
    private AudioSource _sfxSource;

    private readonly Dictionary<SfxKey, AudioClip> _sfxCache = new Dictionary<SfxKey, AudioClip>();
    private readonly Dictionary<BgmKey, AudioClip> _bgmCache = new Dictionary<BgmKey, AudioClip>();

    private BgmKey _currentBgm;
    private bool   _bgmPlaying;

    // 简易 SFX 限流，避免同一帧多次同音效叠加（如经验石批量拾取）
    private readonly Dictionary<SfxKey, float> _lastPlayTime = new Dictionary<SfxKey, float>();
    private const float SfxMinInterval = 0.04f; // 40ms（默认值）

    /// <summary>
    /// 按音效单独配置的最小播放间隔（秒）。用于防止短 CD 技能的音效重叠成噪音。
    ///
    /// 取值原则：
    ///   · 间隔 ≈ 音效自身时长，让上一声基本放完再触发下一声，听感清晰不糊；
    ///   · 短 CD / 多弹连发技能（风箭 CD 0.3s × 5 连发、血族 CD 1.0s）取略大于单发音长；
    ///   · 长 CD 技能（飓风 4.5s、地狱火、亡者领域）间隔无需太大，按音长设即可；
    ///   · 未在此表中的 key 回落到 SfxMinInterval(40ms)，保持原有行为不变。
    /// </summary>
    private static readonly Dictionary<SfxKey, float> SfxIntervalOverride = new Dictionary<SfxKey, float>
    {
        // ── 玩家技能：发射音 ──
        // 风箭：CDtime 0.3（可升级到 0.1），且 number 5 一帧连发 → 取 0.28s 使每轮只响一声
        { SfxKey.WindCast,     0.28f },
        // 风箭命中：多目标同时命中很常见，命中音 0.16s → 0.12s 保留密集感但不糊
        { SfxKey.WindHit,      0.12f },
        // 飓风：音效 0.85s，CD 4.5s，不会撞车，按音长留够
        { SfxKey.Hurricane,    0.80f },
        // 风之形风刃：被动触发（10% 概率），可能同时爆多个 → 0.20s
        { SfxKey.WindBlade,    0.20f },
        { SfxKey.IceCast,      0.30f },
        // 黑暗齿轮：环绕型持续技能，发射音 0.4s
        { SfxKey.DarkCast,     0.35f },
        // 血族血统：CD 1.0s（好感 40 后 0.8s），音效 0.45s
        { SfxKey.BloodCast,    0.45f },
        // 血族吸血命中：蝙蝠 3 只高频命中 → 0.15s 稀释
        { SfxKey.BloodHit,     0.15f },
        // 命途·寄生：CD 下限 2.5s，音效 0.5s
        { SfxKey.ParasiteCast, 0.45f },
        // 孢子领域：持续 AoE，触发频繁 → 按音长 0.4s 限流
        { SfxKey.SporeCast,    0.38f },
        { SfxKey.TombCast,     0.55f },
        // 地狱火：音效 0.72s，多枚三叉戟 0.04s 间隔连续落地 → 必须限流到整段只响一次
        { SfxKey.HellfireCast, 0.70f },
        // 火球：原有音效，多弹连发时限流
        { SfxKey.FireballCast, 0.20f },
        { SfxKey.FireballHit,  0.10f },
        { SfxKey.IceHit,       0.10f },

        // ── Boss 技能 ──
        { SfxKey.BossSlash,    0.30f },
        { SfxKey.BossQuake,    0.85f },
        { SfxKey.BossBreath,   0.90f },
        // 史莱姆吐息 volley 会连发多颗弹丸 → 0.25s
        { SfxKey.BossSpit,     0.25f },
        { SfxKey.BossDive,     0.35f },
        { SfxKey.BossSummon,   0.65f },
        { SfxKey.BossCharge,   0.55f },
    };

    /// <summary>取某音效的最小播放间隔；未配置则用默认 40ms。</summary>
    private static float GetMinInterval(SfxKey key)
    {
        return SfxIntervalOverride.TryGetValue(key, out float v) ? v : SfxMinInterval;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AudioManager]");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 读取保存的音量
        if (PlayerPrefs.HasKey(PpKeyBgm)) bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PpKeyBgm));
        if (PlayerPrefs.HasKey(PpKeySfx)) sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(PpKeySfx));

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;
        _bgmSource.volume = bgmVolume;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
        _sfxSource.volume = sfxVolume;

        SceneManager.sceneLoaded += OnSceneLoaded;

        // 启动时立即播一次（首启场景已加载完）
        PickAndPlayBgmForCurrentScene();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PickAndPlayBgmForCurrentScene();
    }

    /// <summary>根据当前场景名挑选 BGM。可在 battleUI / title 中显式调用以覆盖。</summary>
    public void PickAndPlayBgmForCurrentScene()
    {
        // 这个项目大多是同场景切 UI，没多个 Scene，所以默认播主BGM。
        // battleUI.starttime / title.click_start 中会显式调 PlayBgm(BgmKey.Battle/Main) 覆盖。
        if (!_bgmPlaying)
            PlayBgm(BgmKey.Main);
    }

    // ── 公共播放 API（静态包装，便于在各处零样板调用）───────────────────
    public static void PlaySfx(SfxKey key)
    {
        Instance?.PlaySfxInternal(key);
    }

    public static void PlayBgm(BgmKey key)
    {
        Instance?.PlayBgmInternal(key);
    }

    public static void StopBgm()
    {
        if (Instance == null) return;
        Instance._bgmSource.Stop();
        Instance._bgmPlaying = false;
    }

    public static void StopAll()
    {
        if (Instance == null) return;
        if (Instance._bgmSource != null) Instance._bgmSource.Stop();
        if (Instance._sfxSource != null) Instance._sfxSource.Stop();
        Instance._bgmPlaying = false;
    }

    /// <summary>设置 BGM 音量 [0,1]，并持久化</summary>
    public static void SetBgmVolume(float v)
    {
        if (Instance == null) return;
        v = Mathf.Clamp01(v);
        Instance.bgmVolume = v;
        if (Instance._bgmSource != null) Instance._bgmSource.volume = v;
        PlayerPrefs.SetFloat(PpKeyBgm, v);
    }

    /// <summary>设置 SFX 音量 [0,1]，并持久化</summary>
    public static void SetSfxVolume(float v)
    {
        if (Instance == null) return;
        v = Mathf.Clamp01(v);
        Instance.sfxVolume = v;
        if (Instance._sfxSource != null) Instance._sfxSource.volume = v;
        PlayerPrefs.SetFloat(PpKeySfx, v);
    }

    public static float GetBgmVolume() => Instance != null ? Instance.bgmVolume : 0.6f;
    public static float GetSfxVolume() => Instance != null ? Instance.sfxVolume : 0.8f;

    // ── 内部实现 ──────────────────────────────────────────────────────
    private void PlaySfxInternal(SfxKey key)
    {
        // 限流：按 key 独立配置的最小间隔（防止短 CD 技能音效重叠成噪音）
        if (_lastPlayTime.TryGetValue(key, out float last)
            && Time.unscaledTime - last < GetMinInterval(key))
            return;
        _lastPlayTime[key] = Time.unscaledTime;

        AudioClip clip = LoadSfx(key);
        if (clip == null) return;
        _sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private void PlayBgmInternal(BgmKey key)
    {
        if (_bgmPlaying && _currentBgm == key && _bgmSource.isPlaying) return;
        AudioClip clip = LoadBgm(key);
        if (clip == null) { _bgmPlaying = false; return; }

        _bgmSource.clip = clip;
        _bgmSource.volume = bgmVolume;
        _bgmSource.Play();
        _currentBgm = key;
        _bgmPlaying = true;
    }

    private AudioClip LoadSfx(SfxKey key)
    {
        if (_sfxCache.TryGetValue(key, out var cached)) return cached;
        if (!SfxFile.TryGetValue(key, out var fname)) return null;
        AudioClip clip = Resources.Load<AudioClip>(ResAudioRoot + fname);
        if (clip == null && logMissingClip)
            Debug.LogWarning($"[AudioManager] Resources/Audio/{fname} 未找到，请把 mp3 复制到该目录");
        _sfxCache[key] = clip; // 缓存即使 null 也存，避免重复 IO
        return clip;
    }

    private AudioClip LoadBgm(BgmKey key)
    {
        if (_bgmCache.TryGetValue(key, out var cached)) return cached;
        if (!BgmFile.TryGetValue(key, out var fname)) return null;
        AudioClip clip = Resources.Load<AudioClip>(ResAudioRoot + fname);
        if (clip == null && logMissingClip)
            Debug.LogWarning($"[AudioManager] Resources/Audio/{fname} 未找到，请把 mp3 复制到该目录");
        _bgmCache[key] = clip;
        return clip;
    }
}
