using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 运行时贴图加载统一入口。
///
/// 历史背景：
/// 项目里多个脚本（PlayerSkinOverrider / SkinChanger / battleUI / EquipmentIcon）此前都用
///   Path.Combine(Application.dataPath, "像素幸存者资源包/...")
///     + File.ReadAllBytes + Texture2D.LoadImage
/// 在运行时把工程内的 PNG 当文件读出来。该方案在 Unity 编辑器 PlayMode 中能跑（dataPath 指向 Assets/），
/// 但 **Build 后必然失败**：
///   • Standalone：Application.dataPath 指向 *_Data/，里面没有原始 PNG；
///   • Android：dataPath 指向 apk 内部，File.IO 完全失效，且中文路径会进一步因 ZIP 编码乱码；
///   • iOS：dataPath 指向 Data/Raw，PNG 也没被打进去。
/// 这就是"切换存档后角色仍是琪露诺、UR 行走图加载不出来"的根因。
///
/// 本工具类做三件事：
/// 1. 提供 LoadTexture(directRef, resourcesPath, editorRelativePath) 三层兜底：
///    a) 优先使用调用方在 Inspector 上拖好的 Texture2D 直接引用——
///       这是 **打包后唯一可靠**的资源持有方式（场景/Prefab 直接引用 → 打包系统自动包含）。
///    b) 退一步走 Resources.Load<Texture2D>(resourcesPath)——
///       要求资源放在 Assets/Resources/ 下，跨平台无视中文路径，二进制打入 Resources.assets。
///    c) 最后才退到旧的 Application.dataPath 文件读取——
///       仅 UNITY_EDITOR 下生效，打包后直接跳过避免 IOException。
/// 2. 提供 LoadSpriteFullRect(...)：取整张 Texture2D 作为 Sprite，调用方常见用法。
/// 3. 全部加载结果带轻量缓存（Texture2D 在内存里，下次直接命中），避免反复 LoadImage。
///
/// 调用方推荐写法：
///   var tex = RuntimeAssetLoader.LoadTexture(
///       direct: serializedTexField,                                 // Inspector 拖入的引用
///       resourcesRelativePath: "Players/ur0_wind_skin",             // 若拷贝到了 Resources 下
///       editorAssetsRelativePath: "像素幸存者资源包/玩家/ur0_wind_skin.png" // 编辑器兜底
///   );
/// </summary>
public static class RuntimeAssetLoader
{
    // 缓存 Texture2D（按 editor 相对路径 / Resources 路径 key）。
    // 注意：通过 Inspector 直接引用的 Texture 不进缓存，由 GC 自然管理。
    private static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();

    /// <summary>
    /// 三层兜底加载 Texture2D。
    /// 任意一层成功即返回，全部失败返回 null。永不抛异常。
    /// </summary>
    /// <param name="direct">Inspector 上序列化的 Texture2D 引用（最可靠，打包后唯一保证）。</param>
    /// <param name="resourcesRelativePath">Resources/ 下的相对路径（不含扩展名）。如 "Players/ur0_wind_skin"。</param>
    /// <param name="editorAssetsRelativePath">Assets/ 下的相对路径（含扩展名）。仅编辑器有效兜底。</param>
    public static Texture2D LoadTexture(Texture2D direct,
                                        string resourcesRelativePath = null,
                                        string editorAssetsRelativePath = null)
    {
        // 第 1 层：直接引用最可靠
        if (direct != null) return direct;

        // 第 2 层：Resources（跨平台，编码安全，二进制打入 Resources.assets）
        if (!string.IsNullOrEmpty(resourcesRelativePath))
        {
            if (_texCache.TryGetValue("R::" + resourcesRelativePath, out var cached) && cached != null)
                return cached;
            var loaded = Resources.Load<Texture2D>(resourcesRelativePath);
            if (loaded != null)
            {
                _texCache["R::" + resourcesRelativePath] = loaded;
                return loaded;
            }
        }

        // 第 3 层：仅编辑器内的 dataPath 文件兜底
#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(editorAssetsRelativePath))
        {
            if (_texCache.TryGetValue("F::" + editorAssetsRelativePath, out var cached) && cached != null)
                return cached;

            // 统一斜杠避免 Windows 反斜杠混用
            string normalized = editorAssetsRelativePath.Replace('\\', '/');
            string fullPath = Path.Combine(Application.dataPath, normalized);
            if (File.Exists(fullPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.filterMode = FilterMode.Point;
                        tex.wrapMode = TextureWrapMode.Clamp;
                        _texCache["F::" + editorAssetsRelativePath] = tex;
                        return tex;
                    }
                }
                catch
                {
                    // 静默失败，返回 null 让上层走兜底逻辑
                }
            }
        }
#endif
        return null;
    }

    /// <summary>
    /// 加载 Texture 并按指定参数创建 Sprite。失败返回 null。
    /// rect 为 null 时取整张图。
    /// </summary>
    public static Sprite LoadSprite(Texture2D direct,
                                    string resourcesRelativePath = null,
                                    string editorAssetsRelativePath = null,
                                    Rect? rect = null,
                                    Vector2? pivot = null,
                                    float pixelsPerUnit = 100f)
    {
        var tex = LoadTexture(direct, resourcesRelativePath, editorAssetsRelativePath);
        if (tex == null) return null;
        Rect r = rect ?? new Rect(0, 0, tex.width, tex.height);
        Vector2 p = pivot ?? new Vector2(0.5f, 0.5f);
        try
        {
            return Sprite.Create(tex, r, p, pixelsPerUnit);
        }
        catch
        {
            return null;
        }
    }
}
