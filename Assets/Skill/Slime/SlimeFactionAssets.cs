using UnityEngine;

/// <summary>
/// 史莱姆社群（阴/阳/太极史莱姆）的资源与常量集中管理。
///
/// 设计意图：
///   1. 所有 Resources 路径、技能名、升级组名、装备 id 只在这里出现一次，
///      避免像早期社群那样把"Wolf/icon_parasite" 这种字符串散落在 5 个文件里，
///      一旦改名就漏改。
///   2. Sprite 一律做**静态缓存**。LoadSpriteFallback 内部含
///      RenderTexture.Blit + GetPixels32 + BFS 边缘泛洪抠图，是数万像素级CPU 操作；
///      蝌蚪射弹一次齐射就有 6~11 发，若每发都去加载会直接卡死。
///   3. 抠图统一走 conservative 模式：本社群素材是纯洋红(#FF00FF)底的黑/白史莱姆，
///      激进模式的多背景色候选会把白色阳鱼的高光一起吃掉（白与洋红在 R 通道同为 255），
///      保守模式只对"与四角同色"的连通区域下手，对高对比纯色底最安全。
/// </summary>
public static class SlimeFactionAssets
{
    // ── 技能名（skillupgrade / ChoiceUI 靠它匹配，改动会影响存档语义）──
    public const string SKILL_YIN   = "阴史莱姆";
    public const string SKILL_YANG  = "阳史莱姆";
    /// <summary>升级卡对外显示名。规格要求"升级卡共享，写作阴/阳史莱姆"。</summary>
    public const string SKILL_SHARED_DISPLAY = "阴/阳史莱姆";

    // ── 好感度装备 id ──
    // 0-2 蘑菇/ 3-5 蝙蝠 / 6-8 狼人 已被占用，史莱姆顺延到 9-11。
    public const int EQUIP_YIN    = 9;   // 阴史莱姆（好感 10）
    public const int EQUIP_YANG   = 10;  // 阳史莱姆（好感 50）
    public const int EQUIP_TAIJI  = 11;  // 太极两仪（好感 100，宠物 + 开局自带两个技能）

    // ── 好感度门槛 ──
    public const int FAVOR_YIN   = 10;
    public const int FAVOR_YANG  = 50;
    public const int FAVOR_TAIJI = 100;

    /// <summary>
    /// 阴/阳史莱姆的**最小冷却**（秒）。
    ///
    /// 为什么需要硬下限：
    ///   冷却来源有四处（升级卡 -0.3s ×5、好感度 40 档 ×0.8、奇遇、门挑战），
    ///   叠满后理论上能压到 1s 以下甚至趋近 0。那会造成两个问题：
    ///     ① 太极史莱姆的合体/分解演出根本来不及播完，画面变成不停聚散抽搐；
    ///     ②齐射是协程逐发发射（number 满配16 发 × 2 条鱼），
    ///        冷却比"一轮齐射本身的耗时"还短时会出现多轮齐射相互叠加，
    ///        射弹数量指数级堆积 → 帧率骤降。
    ///   1.5s 是实测下"演出仍清晰、射弹不堆积"的安全下限。
    ///
    /// 所有写 CDtime 的地方都必须经过这个下限（SlimeSharedUpgrade /
    /// TaijiSlimeWatcher.SyncSharedStats / WorldBossManager.ApplySlimeBonus /
    /// skillupgrade.SyncYinYangSlimePair）。
    /// </summary>
    public const float MIN_CDTIME = 1.5f;

    /// <summary>把冷却值钳到不低于 <see cref="MIN_CDTIME"/>。</summary>
    public static float ClampCD(float cd) => Mathf.Max(MIN_CDTIME, cd);

    // ── Resources 路径 ──
    private const string P_YIN_FISH= "Slime/yin_fish";
    private const string P_YANG_FISH  = "Slime/yang_fish";
    private const string P_TAIJI      = "Slime/taiji_slime";
    private const string P_TAD_BLACK  = "Slime/bullet_tadpole_black";
    private const string P_TAD_WHITE  = "Slime/bullet_tadpole_white";
    private const string P_SEAL       = "Slime/taiji_seal";
    private const string P_PET        = "Slime/TaijiTuPet_sprite";
    private const string P_ICON_YIN   = "Slime/icon_yin_slime";
    private const string P_ICON_YANG  = "Slime/icon_yang_slime";
    private const string P_ICON_TAIJI = "Slime/icon_taiji_liangyi";

    // ── 静态缓存槽 ──
    private static Sprite _yinFish, _yangFish, _taiji, _tadBlack, _tadWhite, _seal, _pet;
    private static Sprite _iconYin, _iconYang, _iconTaiji;
    private static bool _triedYinFish, _triedYangFish, _triedTaiji, _triedTadBlack, _triedTadWhite;
    private static bool _triedSeal, _triedPet, _triedIconYin, _triedIconYang, _triedIconTaiji;

    public static Sprite YinFish   => Get(ref _yinFish,   ref _triedYinFish,   P_YIN_FISH);
    public static Sprite YangFish  => Get(ref _yangFish,  ref _triedYangFish,  P_YANG_FISH);
    public static Sprite TaijiBody => Get(ref _taiji,     ref _triedTaiji,     P_TAIJI);
    public static Sprite TadpoleBlack => Get(ref _tadBlack, ref _triedTadBlack, P_TAD_BLACK);
    public static Sprite TadpoleWhite => Get(ref _tadWhite, ref _triedTadWhite, P_TAD_WHITE);
    public static Sprite Seal      => Get(ref _seal,      ref _triedSeal,      P_SEAL);
    public static Sprite PetSprite => Get(ref _pet,       ref _triedPet,       P_PET);
    public static Sprite IconYin   => Get(ref _iconYin,   ref _triedIconYin,   P_ICON_YIN);
    public static Sprite IconYang  => Get(ref _iconYang,  ref _triedIconYang,  P_ICON_YANG);
    public static Sprite IconTaiji => Get(ref _iconTaiji, ref _triedIconTaiji, P_ICON_TAIJI);

    /// <summary>按极性取伴生鱼贴图。</summary>
    public static Sprite FishOf(bool isYin) => isYin ? YinFish : YangFish;
    /// <summary>按极性取蝌蚪射弹贴图。</summary>
    public static Sprite TadpoleOf(bool isYin) => isYin ? TadpoleBlack : TadpoleWhite;
    /// <summary>按极性取技能图标。</summary>
    public static Sprite IconOf(bool isYin) => isYin ? IconYin : IconYang;

    /// <summary>阴（黑紫）/ 阳（白金）主色，用于射弹拖尾与范围圈。</summary>
    public static Color ColorOf(bool isYin) => isYin
        ? new Color(0.42f, 0.20f, 0.72f, 1f)   // 暗紫
        : new Color(1f,    0.93f, 0.70f, 1f);  // 暖白金

    /// <summary>
    /// 惰性加载 + 缓存。**保证永不抛异常**。
    ///
    /// 【2026-08修复：一张升级卡都刷不到】
    ///   LoadSpriteFallback 内部要做 RenderTexture.Blit + GetPixels32 + BFS 泛洪抠图，
    ///   在 1024×1024 上是数十万像素级操作，可能因纹理不可读 / 内存 /导入设置异常而抛。
    ///   而调用方 SlimeFactionRegistrar.BuildUpgrade 是在**注册升级卡的过程中**取图标的，
    ///   一旦这里抛出，异常会一路冒泡到 Register 的 try/catch被吞掉，
    ///   结果是 RegisterOne 根本没机会执行 → ChoiceUI.skillEntries 里
    ///   一个史莱姆 entry 都没有 → 玩家永远刷不到升级卡（但技能仍在，
    ///   因为 ApplyEquip11 是在 Register 之后独立调用的，所以现象特别具有迷惑性）。
    ///
    ///   图标只是"锦上添花"，绝不该阻断卡池注册。这里就地兜住：
    ///   失败就返回 null，卡片照样进池，只是没图标。
    /// </summary>
    private static Sprite Get(ref Sprite slot, ref bool tried, string path)
    {
        if (slot != null) return slot;
        if (tried) return null;
        tried = true;
        try
        {
            // 复用 BulletParasite 的加载器：它能兼容 TextureType=Default/Sprite 两种导入设置，
            // 并把压缩纹理 Blit 成 RGBA32 可读副本后抠背景，最后 Sprite.Create。
            slot = BulletParasite.LoadSpriteFallback(path, conservative: true);
            if (slot == null)
                Debug.LogWarning($"[SlimeFaction] 素材加载失败(返回 null): Resources/{path}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SlimeFaction] 素材加载异常，已忽略: Resources/{path} " +
                             $"→ {ex.GetType().Name}: {ex.Message}");
            slot = null;
        }
        return slot;
    }

    /// <summary>
    /// 把 SpriteRenderer 缩放到指定的**世界尺寸（米）**，返回换算出的统一缩放系数。
    ///
    /// 为什么必须这么做：
    ///   LoadSpriteFallback 内部走 Sprite.Create(..., pixelsPerUnit: 100)，
    ///   所以一张 1024×1024 的源图会变成 10.24×10.24 世界单位—— 相对玩家（约 1~2 米）
    ///   足足大了一个数量级。如果直接用"手调localScale 倍率"，一旦将来把素材
    ///   换成 512或 2048 的图，所有倍率就全废了。
    ///   改为声明"我要0.9 米宽"，再由 sprite.bounds 反算系数，就彻底与源图分辨率解耦。
    ///
    /// 这与 BulletParasite 处理 claw_tip（同样是 1024 图）的方式一致。
    /// </summary>
    public static float FitSpriteToWorldSize(SpriteRenderer sr, float worldSize)
    {
        if (sr == null || sr.sprite == null || worldSize <= 0f) return 1f;
        float w = sr.sprite.bounds.size.x;
        float k = w > 0.01f ? worldSize / w : 1f;
        sr.transform.localScale = new Vector3(k, k, k);
        return k;
    }

    /// <summary>只计算系数不写入（供需要在其上叠加动画缩放的调用方使用）。</summary>
    public static float WorldSizeScale(Sprite sprite, float worldSize)
    {
        if (sprite == null || worldSize <= 0f) return 1f;
        float w = sprite.bounds.size.x;
        return w > 0.01f ? worldSize / w : 1f;
    }

    /// <summary>
    /// 场景重载时清空缓存。Sprite.Create 出来的实例绑定在旧 Texture2D 上，
    /// 跨场景继续用虽然不会崩，但会一直占着旧纹理内存；由 enemy.ResetSceneCaches 统一调用。
    /// </summary>
    public static void ResetCaches()
    {
        _yinFish = _yangFish = _taiji = _tadBlack = _tadWhite = _seal = _pet = null;
        _iconYin = _iconYang = _iconTaiji = null;
        _triedYinFish = _triedYangFish = _triedTaiji = _triedTadBlack = _triedTadWhite = false;
        _triedSeal = _triedPet = _triedIconYin = _triedIconYang = _triedIconTaiji = false;
    }

    /// <summary>读取史莱姆社群当前好感度（FavorManager 缺失时回退 PlayerPrefs）。</summary>
    public static int CurrentFavor()
    {
        if (FavorManager.Instance != null)
            return FavorManager.Instance.GetFavor(FactionType.Slime);
        return PlayerPrefs.GetInt("Favor_" + FactionType.Slime, 0);
    }

    /// <summary>某件史莱姆好感度装备是否真正生效（装备已解锁 + 好感度门槛达成）。</summary>
    public static bool IsEquipActive(int equipId, int favorThreshold)
    {
        if (EquipmentSystem.Instance == null) return false;
        if (!EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.FavorEquipment, equipId))
            return false;
        return CurrentFavor() >= favorThreshold;
    }
}
