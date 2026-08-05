using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;

/// <summary>
/// 继承装备的素材加载与**程序化稀有度边框**。
///
/// ── 为什么边框用程序化生成而不是 AI 出图 ──
///稀有度边框是"中空的方框"：中央必须完全透明才能露出装备图标。
///   而 AI 出图没有 alpha 通道，只能靠 LoadSpriteFallback 的
///   "四角泛洪抠背景"来制造透明——泛洪只能从边缘连通区域下手，
///   被边框**包围起来的中央区域**属于独立连通域，会被完整保留下来，
///   结果就是一块挡住图标的实心色块。
///   因此边框改为运行时用 Texture2D 画：中央真透明、颜色严格按策划案、
///   任意尺寸都锐利，还能给奇点做流动星河动效。装备图标本身仍用 AI 素材。
/// </summary>
public static class InheritEquipmentAssets
{
    private const string PATH_ROOT = "Inherit/";

    // ── 装备图标缓存：[美术分组 0~2][槽位 0~5] ──
    private static readonly Sprite[,] _iconCache =
        new Sprite[3, InheritEquipmentDefs.SLOT_COUNT];
    private static readonly bool[,] _iconTried =
        new bool[3, InheritEquipmentDefs.SLOT_COUNT];

    private static Sprite _silhouette;
    private static bool   _silhouetteTried;

    // ── 边框缓存：每个稀有度一张 ──
    private static readonly Sprite[] _borderCache = new Sprite[InheritEquipmentDefs.RARITY_COUNT];

    /// <summary>槽位 → 素材文件名后缀。</summary>
    private static string SlotFile(InheritSlot s) => s switch
    {
        InheritSlot.Helmet   => "helmet",
        InheritSlot.Armor    => "armor",
        InheritSlot.Boots    => "boots",
        InheritSlot.Bracelet => "bracelet",
        InheritSlot.Necklace => "necklace",
        InheritSlot.Weapon   => "weapon",
        _                    => "weapon",
    };

    /// <summary>
    /// 取装备图标。每两档稀有度共用一套素材（策划案："每两个稀有度为一组进行设计"）：
    ///   t1 = 原子 / 质子，t2 = 中子 / 电子，t3 = 无限超弦 / 奇点
    /// </summary>
    public static Sprite Icon(InheritSlot slot, InheritRarity rarity)
    {
        int tier = Mathf.Clamp(((int)rarity) / 2, 0, 2);
        int si   = (int)slot;

        if (_iconCache[tier, si] != null) return _iconCache[tier, si];
        if (_iconTried[tier, si]) return null;
        _iconTried[tier, si] = true;

        string path = $"{PATH_ROOT}t{tier + 1}_{SlotFile(slot)}";
        _iconCache[tier, si] = SafeLoad(path);
        return _iconCache[tier, si];
    }

    /// <summary>人形轮廓（装备栏中央的纸娃娃底图）。</summary>
    public static Sprite Silhouette()
    {
        if (_silhouette != null) return _silhouette;
        if (_silhouetteTried) return null;
        _silhouetteTried = true;
        _silhouette = SafeLoad(PATH_ROOT + "silhouette");
        return _silhouette;
    }

    /// <summary>
    /// 素材加载。与史莱姆社群同样的教训：**绝不能抛异常**。
    /// LoadSpriteFallback 内部要做Blit + GetPixels32 + BFS 泛洪，
    /// 一旦抛出会冒泡到调用方（这里是 UI 构建流程），导致整个面板建不出来。
    ///
    /// 【性能】那套"Blit → GetPixels32 → BFS 泛洪抠背景"对19 张 AI 图跑一遍
    /// 要 4~5 秒，全都堆在玩家首次点开装备页面的那一刻，体感就是卡死。
    /// 因此这里加了一层**磁盘缓存**：抠好的结果编码成 PNG 存到
    /// Application.persistentDataPath，二次加载直接 LoadImage（快 1~2 个数量级）。
    /// 配合 <see cref="InheritEquipmentPrewarmer"/> 在游戏启动后分帧预热，
    /// 玩家点开面板时素材早已就绪。
    /// </summary>
    private static Sprite SafeLoad(string path)
    {
        //① 磁盘缓存命中→ 跳过泛洪抠图
        Sprite cached = TryLoadFromDiskCache(path);
        if (cached != null) return cached;

        try
        {
            var sp = BulletParasite.LoadSpriteFallback(path, conservative: true);
            if (sp == null) Debug.LogWarning($"[Inherit] 素材加载失败: Resources/{path}");
            else TrySaveToDiskCache(path, sp);   // ② 首次抠好后写缓存
            return sp;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Inherit] 素材加载异常已忽略: Resources/{path} → " +
                             $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    // ═══════════════════════抠图结果磁盘缓存 ═══════════════════════
    // 缓存版本号：抠图算法或素材更新时+1，旧缓存自然失效（不必手动清理）
    //   v2：修正 t3_necklace（奇点项链）—— 原文件误存成了人形轮廓图
    private const string CACHE_VER = "v2";

    private static string CacheDir =>
        Path.Combine(Application.persistentDataPath, "InheritIconCache");

    private static string CacheFile(string resPath) =>
        Path.Combine(CacheDir, $"{CACHE_VER}_{resPath.Replace('/', '_')}.png");

    private static Sprite TryLoadFromDiskCache(string resPath)
    {
        try
        {
            string file = CacheFile(resPath);
            if (!File.Exists(file)) return null;

            byte[] bytes = File.ReadAllBytes(file);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,      // 像素风：禁止插值
                wrapMode   = TextureWrapMode.Clamp,
            };
            if (!tex.LoadImage(bytes)) return null;

            // 参数与 LoadSpriteFallback 保持一致（全图 / 中心 pivot / 100 PPU），
            // 否则世界内掉落展示（SpriteRenderer）的尺寸会和之前不同。
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), 100f);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Inherit] 读取素材缓存失败（忽略）: {ex.Message}");
            return null;
        }
    }

    private static void TrySaveToDiskCache(string resPath, Sprite sp)
    {
        try
        {
            if (sp == null || sp.texture == null) return;
            // 只缓存"全图 sprite"，避免图集裁切导致重建后 UV 不对
            if (Mathf.RoundToInt(sp.rect.width)  != sp.texture.width ||
                Mathf.RoundToInt(sp.rect.height) != sp.texture.height) return;

            Directory.CreateDirectory(CacheDir);
            byte[] png = sp.texture.EncodeToPNG();     // texture 不可读时会抛，已被 catch
            if (png != null && png.Length > 0)
                File.WriteAllBytes(CacheFile(resPath), png);
        }
        catch (System.Exception ex)
        {
            // 写缓存失败不影响功能，只是下次仍要重新抠图
            Debug.LogWarning($"[Inherit] 写入素材缓存失败（忽略）: {ex.Message}");
        }
    }

    // ═══════════════════════ 中文字体（全模块共用）═══════════════════════

    private static TMP_FontAsset _cnFont;

    /// <summary>
    /// 解析一个**含中文字形**的 TMP 字体，给所有运行时创建的 TMP 文本用。
    ///
    /// 【为什么必须显式指定】heiti SDF 不在 Resources 目录（实际位于
    /// Assets/像素幸存者资源包/字体/），`Resources.Load` 必然返回 null；
    /// 而TMP 默认字体是 LiberationSans（**不含 CJK**），
    /// 不设置就会让所有中文变成 □□□□（局内掉落展示的乱码就是这么来的）。
    ///
    /// 四级回退：
    ///   ① ToastManager.Instance.font（场景 Inspector 已拖好，最可靠）
    ///   ② 场景里任意一个 TMP_Text（含 inactive）的 font
    ///   ③ 已加载进内存的 TMP_FontAsset 中名字带 "hei" 的
    ///   ④ 内存里任意一个 TMP_FontAsset
    /// 字体是项目资源、不随场景失效，所以这个缓存**不在 ResetCaches 里清**。
    /// </summary>
    public static TMP_FontAsset ChineseFont()
    {
        if (_cnFont != null) return _cnFont;

        if (ToastManager.Instance != null && ToastManager.Instance.font != null)
        {
            _cnFont = ToastManager.Instance.font;
            return _cnFont;
        }

        var texts = Object.FindObjectsOfType<TMP_Text>(true);
        foreach (var t in texts)
            if (t != null && t.font != null) { _cnFont = t.font; return _cnFont; }

        var allFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        foreach (var f in allFonts)
            if (f != null && f.name != null && f.name.Contains("hei"))
            { _cnFont = f; return _cnFont; }
        foreach (var f in allFonts)
            if (f != null) { _cnFont = f; return _cnFont; }

        _cnFont = Resources.Load<TMP_FontAsset>("heiti SDF");
        return _cnFont;
    }

    /// <summary>
    /// 一次性预热全部素材（18 张图标 + 轮廓 + 6 个边框）。    ///
    /// 【为什么不分帧】分帧（每帧一张）虽然把卡顿摊薄了，但玩家在主菜单只停留
    /// 两三秒就点进装备界面，预热还没跑完 → 照样卡。所以改成**游戏启动时一次做完**：
    /// 首次启动集中花 4~5 秒（发生在主菜单，玩家还在看标题），
    /// 之后由磁盘缓存兜底，二次启动几乎瞬间完成，任何时候点开装备界面都零等待。
    /// </summary>
    public static void PrewarmAll()
    {
        for (int r = 0; r < InheritEquipmentDefs.RARITY_COUNT; r++)
            Border((InheritRarity)r);

        // 图标：3 套美术分组 × 6 槽位（tier = rarity/2，所以传 tier*2）
        for (int tier = 0; tier < 3; tier++)
       for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
            Icon((InheritSlot)s, (InheritRarity)(tier * 2));

        Silhouette();
    }

  /// <summary>分帧版预热（保留给需要避免单帧长卡顿的场合，例如场景切换后的重建）。</summary>
  public static IEnumerator PrewarmRoutine()
    {
        // 边框是程序化生成，很快，先做完
 for (int r = 0; r < InheritEquipmentDefs.RARITY_COUNT; r++)
        {
       Border((InheritRarity)r);
   yield return null;
        }

  for (int tier = 0; tier < 3; tier++)
        {
            for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
            {
          Icon((InheritSlot)s, (InheritRarity)(tier * 2));
     yield return null;
            }
 }

        Silhouette();
        yield return null;
    }

    // ═══════════════════════ 程序化稀有度边框 ═══════════════════════

    private const int BORDER_TEX = 96;   // 边框贴图分辨率（正方形）
    private const int BORDER_PX  = 6;    // 边框线宽（像素）

    /// <summary>
    /// 取某稀有度的边框 Sprite（带 9-slice border，可任意拉伸不变形）。
    ///
    /// 画法：
    ///   • 外圈BORDER_PX 宽的实色描边，颜色 = 稀有度主色；
    ///   • 描边内侧再画 1px 的高光/暗边，做出金属立体感；
    ///   • 四角额外加2px 的加粗"角标"，高稀有度看起来更华丽；
    ///   • 中央完全透明（alpha = 0），保证不挡装备图标；
    ///   • 奇点（星河）在描边上叠加多彩噪点星点，配合
    ///     <see cref="InheritRarityBorder"/> 的 UV 流动实现星河感。
    /// </summary>
    public static Sprite Border(InheritRarity rarity)
    {
        int ri = (int)rarity;
        if (_borderCache[ri] != null) return _borderCache[ri];

        var tex = new Texture2D(BORDER_TEX, BORDER_TEX, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,   // 像素风：禁止插值
            wrapMode   = TextureWrapMode.Clamp,
        };

        Color baseCol = InheritEquipmentDefs.RarityColor(rarity);
        bool cosmic= InheritEquipmentDefs.IsCosmic(rarity);
        var clear     = new Color(0f, 0f, 0f, 0f);

        // 用固定种子，保证同一稀有度每次生成的星点分布一致（避免闪烁）
        var rng = new System.Random(1234 + ri);

        for (int y = 0; y < BORDER_TEX; y++)
        {
            for (int x = 0; x < BORDER_TEX; x++)
            {
                // 距最近边的距离
                int d = Mathf.Min(Mathf.Min(x, BORDER_TEX - 1 - x),
                                  Mathf.Min(y, BORDER_TEX - 1 - y));

                if (d >= BORDER_PX)
                {
                    tex.SetPixel(x, y, clear);// 中央透明
                    continue;
                }

                Color c = baseCol;

                // 立体感：最外 1px 压暗，内侧 1px 提亮
                if (d == 0)                c = baseCol * 0.55f;
                else if (d == BORDER_PX-1) c = Color.Lerp(baseCol, Color.white, 0.45f);

                // 四角加粗角标：离两条边都很近的位置整体提亮
                bool nearL = x < BORDER_PX * 3, nearR = x >= BORDER_TEX - BORDER_PX * 3;
                bool nearB = y < BORDER_PX * 3, nearT = y >= BORDER_TEX - BORDER_PX * 3;
                if ((nearL || nearR) && (nearB || nearT))
                    c = Color.Lerp(c, Color.white, 0.35f);

                // 奇点：在描边里撒多彩星点，做"宇宙星河"
                if (cosmic)
                {
                    double r = rng.NextDouble();
                    if (r < 0.10)c = Color.white;                              // 亮星
                    else if (r < 0.20)  c = new Color(1f, 0.75f, 0.95f);              // 粉星云
                    else if (r < 0.30)  c = new Color(0.65f, 0.55f, 1f);              // 紫星云
                    else if (r < 0.40)  c = new Color(0.45f, 0.95f, 1f);              // 青星云
                    else                c = Color.Lerp(c, new Color(0.10f, 0.06f, 0.25f), 0.45f); // 深空底
                }

                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
        }

        tex.Apply(false, false);

        // 9-slice：四边留 BORDER_PX*3 作为不拉伸区，保证角标不被拉扁
        int b = BORDER_PX * 3;
        var sprite = Sprite.Create(
            tex, new Rect(0, 0, BORDER_TEX, BORDER_TEX), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));

        _borderCache[ri] = sprite;
        return sprite;
    }

    /// <summary>场景重载时清缓存（Sprite 绑定的Texture2D 会随旧场景失效）。</summary>
    public static void ResetCaches()
    {
        for (int t = 0; t < 3; t++)
            for (int s = 0; s < InheritEquipmentDefs.SLOT_COUNT; s++)
            {
                _iconCache[t, s] = null;
                _iconTried[t, s] = false;
            }
        _silhouette = null;
        _silhouetteTried = false;
        for (int i = 0; i < _borderCache.Length; i++) _borderCache[i] = null;
    }
}
