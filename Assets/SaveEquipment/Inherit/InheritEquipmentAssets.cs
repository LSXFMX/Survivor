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
    /// </summary>
    private static Sprite SafeLoad(string path)
    {
        try
        {
            var sp = BulletParasite.LoadSpriteFallback(path, conservative: true);
            if (sp == null) Debug.LogWarning($"[Inherit] 素材加载失败: Resources/{path}");
            return sp;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Inherit] 素材加载异常已忽略: Resources/{path} → " +
                             $"{ex.GetType().Name}: {ex.Message}");
            return null;
        }
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
