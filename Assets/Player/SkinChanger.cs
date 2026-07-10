using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 主菜单换装系统（彻底重写版）。
///
/// 设计原则：**不依赖 EventSystem / Button.onClick / IPointerClickHandler**。
/// 之前所有"按钮挂监听器"的方案在该项目都因不明原因失效（按钮有视觉反馈但回调无）。
/// 这次改用最朴素的射线检测：每帧读 Input.mousePosition + 鼠标按下事件，
/// 自己用 RectTransformUtility 判断点击落在哪个矩形里，**绕开整条 UI 事件链路**。
///
/// 用法：把这个组件挂在主菜单 Canvas 上，运行时自动生成"更换角色"按钮和橱窗面板。
/// </summary>
[ExecuteAlways]
public class SkinChanger : MonoBehaviour
{
    // ============== 配置 ==============
    [Header("UI 字体")]
    public TMP_FontAsset uiFont;

    [Header("主菜单按钮")]
    public Vector2 openButtonAnchoredPosition = new Vector2(-150f, 130f);
    public Vector2 openButtonSize = new Vector2(170f, 60f);

    [Header("橱窗面板")]
    public Vector2 panelSize = new Vector2(1120f, 540f);

    [Header("外观信息")]
    // 注意：这里仅是「角色显示名」，技能名（风之形/地狱火）保持不变。
    // skinId=1 -> 南筱风（穿着风之形 UR 皮肤）
    // skinId=2 -> 夏无（穿着地狱火 UR 皮肤）
    private static readonly string[] SkinNames = { "琪露诺", "南筱风", "夏无", "无罪" };
    private const string CirnoIconPath = "像素幸存者资源包/玩家/琪诺露/闲置/1.png";
    private const string Ur0IconPath  = "像素幸存者资源包/玩家/ur0_wind_skin.png";
    private const string Ur1IconPath  = "像素幸存者资源包/玩家/ur1_fire_skin.png";
    // 之前这里指向 ur2_tomb_icon.png——它是单张匕首图标（不是 4×3 角色 sprite sheet），
    // 被 LoadSpriteForSkin 当 4×3 网格切顶行第 1 帧 → 取到图标的右上角一小块 → 显示成一把刀。
    // 修正：改用与 ur0/ur1 同布局的 4×3 角色精灵表 ur2_tomb_skin.png，
    // 顶行第 1 帧才能正确取到无罪的 idle 站姿。
    private const string Ur2IconPath  = "像素幸存者资源包/玩家/ur2_tomb_skin.png";

    // === 打包后资源加载关键修复（2026-06）===
    // 同 PlayerSkinOverrider：通过 Inspector 引用持有这 4 张 Texture，
    // 让打包系统自动把它们打入 Build——否则切换存档后人物存档面板上的卡片头像在 Build 中也加载不出来。
    [Header("人物存档卡片头像（打包必需 · 请保留 Inspector 上的引用）")]
    [Tooltip("琪露诺站立帧。对应 Assets/像素幸存者资源包/玩家/琪诺露/闲置/1.png。")]
    public Texture2D cirnoIconTexture;
    [Tooltip("UR_0 风之形 行走图（取顶行第 1 帧作为头像）。")]
    public Texture2D ur0IconTexture;
    [Tooltip("UR_1 地狱火 行走图（取顶行第 1 帧作为头像）。")]
    public Texture2D ur1IconTexture;
    [Tooltip("UR_2 亡者领域 行走图（取顶行第 1 帧作为头像）。")]
    public Texture2D ur2IconTexture;

    // ============== 运行时引用 ==============
    [HideInInspector] public GameObject openButtonGo;
    [HideInInspector] public GameObject panelGo;
    [HideInInspector] public GameObject backdropGo; // 全屏阻挡层：吞掉 panel 之外的点击，防止"点穿"到主菜单的抽奖等按钮
    [HideInInspector] public GameObject closeButtonGo;
    [HideInInspector] public GameObject[] selectButtonGos = new GameObject[4];
    [HideInInspector] public TextMeshProUGUI[] selectButtonLabels = new TextMeshProUGUI[4];
    [HideInInspector] public TextMeshProUGUI[] selectCardNameLabels = new TextMeshProUGUI[4]; // 卡片名字 Text，用于运行时刷新
    [HideInInspector] public Image[] selectButtonImages = new Image[4];

    // 信息提醒（hover tooltip，仿 GachaUI 风格）。每张皮肤卡片上都有一个右上角 "i" 按钮，
    // 鼠标悬停时弹出该皮肤的 UR 加成介绍；移开就消失。三张共用同一个 _infoTooltip 面板，
    // 切换显示哪一段文字。
    private GameObject _infoTooltip;
    private TextMeshProUGUI _infoTooltipText;
    private static readonly string[] SkinInfoTexts = new string[]
    {
        // skinId=0 琪露诺
        "<b><color=#FFE066>琪露诺</color></b>\n" +
        "基础角色，无 UR 加成。\n" +
        "适合熟悉游戏机制的玩家——所有技能均按基础数值结算。",

        // skinId=1 南筱风
        "<b><color=#7FE3FF>南筱风（UR · 风系）</color></b>\n" +
        "<color=#FFD37A>本命技能 · 风箭（风系角色基础配置）</color>\n" +
        "  • 风箭多重数量：<color=#7FFFB0>5</color>\n" +
        "  • 风箭范围：<color=#7FFFB0>15</color>\n" +
        "  • 风箭 CD：<color=#7FFFB0>0.3s</color>\n" +
        "<color=#FFD37A>专属内容</color>\n" +
        "  • <color=#7FFFB0>开局自动获得</color>「飓风」\n" +
        "<color=#A0A0A0>风箭在多重 / 范围 / CD 三项上的高基础数值是风系流派的核心，飓风提供爆发性范围伤害，二者互为表里。</color>",

        // skinId=2 夏无
        "<b><color=#FF8E70>夏无（UR · 火/血族）</color></b>\n" +
        "<color=#FFD37A>本命技能加成 · 血族血统</color>\n" +
        "  • 蝙蝠使魔数量：1 → <color=#7FFFB0>5</color>\n" +
        "  • 自带吸血 <color=#7FFFB0>10%</color>（装备[血族之力]后提升至 <color=#7FFFB0>20%</color>）\n" +
        "  • 蝙蝠每次攻击 <color=#7FFFB0>10%</color> 概率：最大生命值 +<color=#7FFFB0>1</color>\n" +
        "<color=#FFD37A>额外特性</color>\n" +
        "  • <color=#7FFFB0>开局自动获得</color>[火球术]（火球 ×3）\n" +
        "  • 风箭多重：<color=#7FFFB0>×2</color>\n" +
        "<color=#A0A0A0>持续输出 + 持续回复 + 无限成长，越打越凶。【适合在解锁血族血统后游玩】</color>",

        // skinId=3 无罪（UR · 亡者领域）
        // 注意：避免使用「」全角直角引号——TMP 默认字体对它没有 fallback，会渲染成 □。
        // 改用【】或加颜色 + 文字本身做高亮。
        "<b><color=#7FE3A0>无罪（UR · 亡者领域）</color></b>\n" +
        "<color=#FFD37A>本命技能加成</color>\n" +
        "  • 风箭 attackRadius：10 → <color=#7FFFB0>15</color>，多重：<color=#7FFFB0>×2</color>\n" +
        "  • 孢子领域 attackRadius：8 → <color=#7FFFB0>15</color>，CD：5s → <color=#7FFFB0>3s</color>\n" +
        "<color=#FFD37A>专属内容</color>\n" +
        "  • <color=#7FFFB0>开局自动获得</color><color=#FFB060>[风箭]</color>+<color=#FFB060>[孢子领域]</color>\n" +
        "  • <color=#FFB060>[亡者领域]被动</color>：被复活的友军小怪死亡时回复 <color=#7FFFB0>0.5%</color> 最大生命值\n" +
        "  • <color=#FFB060>[亡者领域·成长]</color>：每分钟攻击范围 +<color=#7FFFB0>1</color>（上限 20）\n" +
        "<color=#A0A0A0>活不过前期的乐乐角色</color>",
    };

    /// <summary>
    /// 卡片正面"特殊功能"静态短文本（rich-text）。
    /// 与 SkinInfoTexts（hover tooltip 的详尽版）不同，这里写得**精简**用以直接铺在卡片上，
    /// 让玩家一打开「人物存档」面板就直接看到每个角色的关键能力，无需 hover。
    /// </summary>
    private static readonly string[] SkinAbilityShortTexts = new string[]
    {
        // skinId=0 琪露诺
        "<color=#FFE066><b>基础角色</b></color>\n" +
        "<color=#C8C8C8>无特殊加成</color>",

        // skinId=1 南筱风
        "<color=#7FE3FF><b>风之眷顾</b></color>\n" +
        "<color=#E0E0E0>风箭多重 <color=#7FFFB0>×5</color>\n" +
        "风箭范围 <color=#7FFFB0>15</color>\n" +
        "风箭CD <color=#7FFFB0>0.3s</color>\n" +
        "开局自带<color=#FFB060>飓风</color></color>",

        // skinId=2 夏无
        "<color=#FF8E70><b>血族血脉</b></color>\n" +
        "<color=#E0E0E0>蝙蝠使魔 <color=#7FFFB0>×5</color>\n" +
        "吸血 <color=#7FFFB0>+10%</color>\n" +
        "蝙蝠攻击 10% 概率<br>最大生命值 +<color=#7FFFB0>1</color>\n" +
        "火球 <color=#7FFFB0>×3</color>\n" +
        "开局自带<color=#FFB060>火球术</color></color>",

        // skinId=3 无罪
        "<color=#7FE3A0><b>亡者契约</b></color>\n" +
        "<color=#E0E0E0>开局自带<color=#FFB060>风箭</color>+<color=#FFB060>孢子领域</color>\n" +
        "范围 <color=#7FFFB0>15</color>，孢子CD <color=#7FFFB0>3s</color>\n" +
        "亡者领域范围每分钟 +<color=#7FFFB0>1</color>（上限 <color=#7FFFB0>20</color>）\n" +
        "友军死亡回 <color=#7FFFB0>0.5%</color> 血</color>",
    };

    private title _title;
    private Canvas _canvas;
    private Camera _uiCamera;

    // ============== 生命周期 ==============
    void Awake()
    {
        _title = FindObjectOfType<title>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        _uiCamera = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? _canvas.worldCamera
            : null;

        if (uiFont == null)
        {
            var tmp = FindObjectOfType<TextMeshProUGUI>();
            if (tmp != null) uiFont = tmp.font;
        }
    }

    void OnEnable()
    {
        EnsureUI();
        RefreshButtonsState();
        if (Application.isPlaying)
        {
            Debug.Log($"[换装] SkinChanger 启动 on '{gameObject.name}'。");
        }
    }

    void Update()
    {
        // 编辑器或运行时：始终保证 UI 存在
        EnsureUI();

        if (!Application.isPlaying) return;

        // 同步 panel 引用回 title（保证右键关闭等功能能找到面板）
        SyncTitlePanelRef();

        // 让"更换角色"按钮仅在主页面（无任何子面板打开 + 标题UI 自身可见）时出现
        UpdateOpenButtonVisibility();

        // ===== 自前端检测点击（核心：绕开 EventSystem）=====
        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick(Input.mousePosition);
        }

        // 当面板可见状态变化时刷新按钮颜色
        bool nowActive = panelGo != null && panelGo.activeInHierarchy;
        if (nowActive && !_panelWasActive)
        {
            RefreshButtonsState();
            // 面板刚打开 → 同步把名字刷一遍（处理"代码改了名字但 UI 缓存了旧文本"）
            RefreshCardNames();
            // 面板刚打开 → 同步刷标题（覆盖旧"外观橱窗"）
            RefreshPanelTitle();
            // 同步把"特殊功能"文本写到每张卡片上
            RefreshCardAbilities();
        }
        // backdrop 与 panel 联动可见性（每帧兜底，防止外部代码只 SetActive(panel) 而忘了 backdrop）
        if (backdropGo != null && panelGo != null
            && backdropGo.activeSelf != panelGo.activeSelf)
        {
            backdropGo.SetActive(panelGo.activeSelf);
        }
        // tooltip 跟随 panel 一起隐藏（panel 关 → tooltip 必关，避免 hover 状态残留到下次打开）
        if (panelGo != null && !panelGo.activeSelf && _infoTooltip != null && _infoTooltip.activeSelf)
        {
            _infoTooltip.SetActive(false);
        }
        _panelWasActive = nowActive;
    }

    /// <summary>仅当主菜单可见且没有任何子面板（抽奖/存档/外观/难度）打开时，显示"人物存档"按钮。</summary>
    private void UpdateOpenButtonVisibility()
    {
        if (openButtonGo == null) return;
        if (_title == null) _title = FindObjectOfType<title>();

        bool show = true;

        // 主菜单本体未激活 → 隐藏
        if (_title == null || !_title.gameObject.activeInHierarchy)
        {
            show = false;
        }
        else
        {
            // 任意子面板打开 → 隐藏
            if (_title.gachaPanel != null && _title.gachaPanel.activeSelf) show = false;
            else if (_title.savescene != null && _title.savescene.activeSelf) show = false;
            else if (_title.skinShowroomPanel != null && _title.skinShowroomPanel.activeSelf) show = false;
            else if (_title.difficultySelectUI != null && _title.difficultySelectUI.activeSelf) show = false;
            else if (panelGo != null && panelGo.activeSelf) show = false;
        }

        if (openButtonGo.activeSelf != show)
        {
            openButtonGo.SetActive(show);
        }
    }

    private bool _panelWasActive = false;

    // ============== 自前端点击检测 ==============
    private void HandleMouseClick(Vector2 screenPos)
    {
        // 1. 主菜单"更换角色"按钮（仅当面板未打开时可点）
        if (panelGo != null && !panelGo.activeInHierarchy
            && openButtonGo != null && openButtonGo.activeInHierarchy
            && IsPointInRect(openButtonGo.transform as RectTransform, screenPos))
        {
            Debug.Log("[换装] 点击：人物存档");
            AudioManager.PlaySfx(AudioManager.SfxKey.Click);
            if (_title != null) _title.openskin();
            else if (panelGo != null) panelGo.SetActive(true);
            return;
        }

        // 2. 面板可见时，检测面板内的子按钮
        if (panelGo != null && panelGo.activeInHierarchy)
        {
            // 关闭按钮
            if (closeButtonGo != null
                && IsPointInRect(closeButtonGo.transform as RectTransform, screenPos))
            {
                Debug.Log("[换装] 点击：关闭面板");
                AudioManager.PlaySfx(AudioManager.SfxKey.Click);
                if (_title != null) _title.closeskin();
                else panelGo.SetActive(false);
                return;
            }

            // 三个使用按钮
            for (int i = 0; i < selectButtonGos.Length; i++)
            {
                var go = selectButtonGos[i];
                if (go == null || !go.activeInHierarchy) continue;
                if (IsPointInRect(go.transform as RectTransform, screenPos))
                {
                    OnSelectSkin(i);
                    return;
                }
            }
        }
    }

    private bool IsPointInRect(RectTransform rt, Vector2 screenPos)
    {
        if (rt == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, _uiCamera);
    }

    // ============== 选皮肤 ==============
    private void OnSelectSkin(int skinId)
    {
        Debug.Log($"[换装] 点击：使用皮肤 skinId={skinId}");
        if (!IsSkinUnlocked(skinId))
        {
            ToastManager.Show("该外观尚未解锁，请先在抽卡界面获取！");
            return;
        }

        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
        PlayerPrefs.SetInt("SelectedSkin", skinId);
        PlayerPrefs.Save();

        // 立即同步局内 Player（如果在对局中）
        var overrider = FindObjectOfType<PlayerSkinOverrider>();
        if (overrider != null) overrider.skinIndex = skinId;

        RefreshButtonsState();
        ToastManager.Show($"外观已替换为：{SkinNames[skinId]}！");
    }

    private bool IsSkinUnlocked(int skinId)
    {
        // 默认琪露诺始终解锁
        if (skinId == 0) return true;
        if (EquipmentSystem.Instance == null) return false;

        // 历史映射兼容：
        //   风之形：策划文档 GachaEquipment id=4，但旧场景里 urItems[0].equipmentSystemId=0
        //   地狱火：策划文档 GachaEquipment id=5，但旧场景里 urItems[1].equipmentSystemId=1
        //   亡者领域：GachaEquipment id=10（与 GachaManager.urItems[2].equipmentSystemId 保持一致）
        // 任一命中均视为已解锁；同时支持 PlayerPrefs 的存档侧写键 "SkinUnlocked_{id}"
        int[] candidates;
        if (skinId == 1)      candidates = new int[] { 4, 0 };   // 风之形
        else if (skinId == 2) candidates = new int[] { 5, 1 };   // 地狱火
        else if (skinId == 3) candidates = new int[] { 10 };     // 亡者领域
        else                  candidates = new int[0];

        foreach (int idx in candidates)
        {
            if (EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, idx))
                return true;
        }

        // 兜底键：玩家自己强制启用（用于调试/补救历史脏档）
        if (PlayerPrefs.GetInt($"SkinUnlocked_{skinId}", 0) == 1) return true;

        return false;
    }

    // ============== 刷新按钮可视化 ==============
    public void RefreshButtonsState()
    {
        EnsureArraysCapacity();
        int currentSkin = PlayerPrefs.GetInt("SelectedSkin", 0);
        for (int i = 0; i < 4; i++)
        {
            if (selectButtonImages[i] == null || selectButtonLabels[i] == null) continue;

            bool unlocked = IsSkinUnlocked(i);
            bool selected = (i == currentSkin);

            if (selected)
            {
                selectButtonImages[i].color = new Color(0.18f, 0.55f, 0.30f, 1f); // 绿
                selectButtonLabels[i].text = "使用中";
                selectButtonLabels[i].color = Color.white;
            }
            else if (unlocked)
            {
                selectButtonImages[i].color = new Color(0.20f, 0.32f, 0.55f, 1f); // 蓝
                selectButtonLabels[i].text = "使用";
                selectButtonLabels[i].color = Color.white;
            }
            else
            {
                selectButtonImages[i].color = new Color(0.30f, 0.30f, 0.30f, 1f); // 灰
                selectButtonLabels[i].text = "未解锁";
                selectButtonLabels[i].color = new Color(0.7f, 0.7f, 0.7f, 1f);
            }
        }
    }

    // ============== UI 构造 ==============
    private void SyncTitlePanelRef()
    {
        if (_title == null) _title = FindObjectOfType<title>();
        if (_title != null && panelGo != null && _title.skinShowroomPanel != panelGo)
        {
            _title.skinShowroomPanel = panelGo;
        }
    }

    private void EnsureUI()
    {
        if (_title == null) _title = FindObjectOfType<title>();
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

        // 0. 清理旧版 PlayerSkinShowroom 残留的节点（不同的命名）
        CleanupLegacyNodes();

        // 1. 主菜单"人物存档"按钮
        if (openButtonGo == null)
        {
            var found = transform.Find("SkinChangerOpenButton");
            if (found != null) openButtonGo = found.gameObject;
        }
        if (openButtonGo == null)
        {
            openButtonGo = CreateOpenButton();
        }

        // 2. 全屏阻挡背板（必须在 panel 之前创建，确保 sibling order 是 backdrop → panel）
        //    这是修复"人物选择界面没做窗口隔离，点击穿透到下层抽奖按钮"的关键：
        //    把整个 Canvas 用一张 raycastTarget=true 的全透明 Image 覆盖，
        //    所有未命中卡片/按钮/关闭按钮的点击都会被它吞掉，无法继续传到下层。
        if (backdropGo == null)
        {
            // 优先在 Canvas 根下找（这是新版的归属位置）
            Transform canvasTr = (_canvas != null) ? _canvas.transform : null;
            if (canvasTr != null)
            {
                var foundUnderCanvas = canvasTr.Find("SkinChangerBackdrop");
                if (foundUnderCanvas != null) backdropGo = foundUnderCanvas.gameObject;
            }
            // 兼容旧版本：可能挂在 SkinChanger transform 下
            if (backdropGo == null)
            {
                var foundLegacy = transform.Find("SkinChangerBackdrop");
                if (foundLegacy != null) backdropGo = foundLegacy.gameObject;
            }
        }
        if (backdropGo == null)
        {
            backdropGo = CreateBackdrop();
            backdropGo.SetActive(false);
        }
        else
        {
            // 兜底：旧场景里 backdrop 可能挂错位置（SkinChanger 自身），导致只能覆盖局部区域。
            // 强制把它移回 Canvas 根并重置全屏拉伸的 RectTransform。
            FixBackdropFullscreen(backdropGo);
        }

        // 3. 橱窗面板
        if (panelGo == null)
        {
            var found = transform.Find("SkinChangerPanel");
            if (found != null) panelGo = found.gameObject;
        }
        // 3.1 兼容旧 panel：旧场景里 panel 只有 3 张卡，缺 SkinCard_3。
        //     直接销毁旧 panel 让其重建（同时清理旧 size=3 的子按钮引用）。
        if (panelGo != null)
        {
            var card3 = panelGo.transform.Find("SkinCard_3");
            if (card3 == null)
            {
                if (Application.isPlaying)
                    Destroy(panelGo);
                else
                    DestroyImmediate(panelGo);
                panelGo = null;
                // 同时清空旧引用，强制 RecaptureChildRefs 重新捕获
                for (int i = 0; i < selectButtonGos.Length; i++) selectButtonGos[i] = null;
                for (int i = 0; i < selectButtonImages.Length; i++) selectButtonImages[i] = null;
                for (int i = 0; i < selectButtonLabels.Length; i++) selectButtonLabels[i] = null;
                for (int i = 0; i < selectCardNameLabels.Length; i++) selectCardNameLabels[i] = null;
                if (Application.isPlaying) Debug.Log("[换装] 检测到旧版 3 张卡片面板，已重建为 4 张卡片版本。");
            }
        }
        if (panelGo == null)
        {
            panelGo = CreatePanel();
            panelGo.SetActive(false);
        }

        // 4. 重新捕获面板内的子按钮引用（可能 panel 是从场景加载来的）
        RecaptureChildRefs();

        // 5. 强制刷新卡片名字文本——即使 panel 是从场景加载来的旧版本，
        //    SkinNames 里的最新名字也会被同步上去。修复"代码改了名字但 UI 显示没改"。
        RefreshCardNames();

        // 5.0 强制刷新面板标题（旧场景里被序列化保存的旧文案"外观橱窗"会被覆盖成"人物存档"）。
        RefreshPanelTitle();

        // 5.0.1 同样强制刷新主菜单按钮文本（旧场景里序列化保存的"更换角色"等旧文本会被覆盖为"人物存档"）。
        RefreshOpenButtonLabel();

        // 5.1 同样补齐"特殊功能"静态简介文本（旧场景里生成过的 panel 不会自带这个节点）。
        RefreshCardAbilities();

        // 5.2 补齐/修复每张卡片右上角的 "i" InfoButton。
        // 旧场景里 panel 是序列化保存的，可能：
        //   a) 完全没有 InfoButton 节点（旧版没这功能）
        //   b) 有 InfoButton 视觉物（Image+Text），但缺 EventTrigger 组件 → hover 无反应
        // 都需要补齐，否则玩家点 / 悬停 "i" 没有任何弹窗，看起来就是"i 没用"。
        RefreshCardInfoButtons();

        // 5.3 补齐每张卡片缩略图背后的"IconBg 浅色背板"。
        // 无罪（skinId=3）角色全身纯黑长袍，与深色卡片背景几乎融为一体，
        // 只剩头发和眼焰可见 → 玩家看到的就是一团竖直发光物（看起来像"刀"）。
        // 加一层主题色浅背板让角色立刻浮出。旧场景里没生成过这个节点 → 现场补建。
        RefreshCardIconBgs();

        // 6. backdrop 与 panel 显示状态同步（双向：panel 开 → backdrop 开）
        if (backdropGo != null && panelGo != null
            && backdropGo.activeSelf != panelGo.activeSelf)
        {
            backdropGo.SetActive(panelGo.activeSelf);
        }

        // 7. 保证层级：Canvas 下"SkinChanger 在最上、Backdrop 紧挨在它之前（即更早绘制）"。
        //    backdrop 现在挂在 Canvas 根（与 SkinChanger 平级），靠 sibling 顺序确保
        //    1) Backdrop 绘制在 SkinChanger 主菜单按钮之上、面板之下；
        //    2) 由于面板挂在 SkinChanger 子节点最后，且 SkinChanger 在 Canvas 最末位，
        //       面板会画在 backdrop 之上。
        Transform canvasTrLayer = (_canvas != null) ? _canvas.transform : null;
        if (canvasTrLayer != null && backdropGo != null && backdropGo.transform.parent == canvasTrLayer)
        {
            // 先把 SkinChanger 推到 Canvas 末位
            transform.SetAsLastSibling();
            // 再把 backdrop 放到 SkinChanger 前一位
            int myIndex = transform.GetSiblingIndex();
            backdropGo.transform.SetSiblingIndex(Mathf.Max(0, myIndex)); // 与 SkinChanger 同 index → 插到它前面
        }
        // 面板始终在 SkinChanger 子节点末位，画在 backdrop 之上
        if (panelGo != null) panelGo.transform.SetAsLastSibling();

        // 8. 关键：把 backdrop 和 panel reparent 到一个**场景根级、SortingOrder=10000** 的
        //    OverlayCanvas 下，**完全绕过原 Canvas 嵌套层级**——这是唯一能确定性盖住所有现有 UI 的方案。
        //    （之前试过给目标 GO 加 Canvas overrideSorting，但在 SubCanvas 嵌套场景里行为不稳定，
        //      表现为"半透明遮罩没生效 / 面板被红色横幅压在下面"。）
        //    reparent 第二参数传 false，保留 anchor/pivot/localPosition——
        //    backdrop 是 (0,0)~(1,1) 全屏拉伸 → 在 Overlay 下仍然全屏；
        //    panel 是 (0.5,0.5) 中心对齐 → 仍然居中。
        Transform overlay = UIOverlayLayer.Get();
        if (overlay != null)
        {
            if (backdropGo != null && backdropGo.transform.parent != overlay)
                backdropGo.transform.SetParent(overlay, false);
            if (panelGo != null && panelGo.transform.parent != overlay)
                panelGo.transform.SetParent(overlay, false);
            // panel 必须晚于 backdrop 渲染（画在它之上）→ 放最末位
            if (backdropGo != null) backdropGo.transform.SetAsLastSibling();
            if (panelGo != null)    panelGo.transform.SetAsLastSibling();
        }

        // 把面板引用回填到 title.skinShowroomPanel（这样 title.openskin/closeskin 会作用到这个面板）
        if (_title != null && _title.skinShowroomPanel != panelGo)
        {
            _title.skinShowroomPanel = panelGo;
        }
    }

    /// <summary>
    /// 强制刷新面板标题文本。处理"旧场景里 panel 是序列化保存下来的，里面 Title 还停留在'外观橱窗'"。
    /// </summary>
    private void RefreshPanelTitle()
    {
        if (panelGo == null) return;
        var titleTr = panelGo.transform.Find("Title");
        if (titleTr == null) return;
        var tmp = titleTr.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null) return;
        const string want = "人物存档";
        if (tmp.text != want) tmp.text = want;
    }

    /// <summary>
    /// 强制刷新主菜单"人物存档"按钮文本。处理"旧场景里按钮序列化保存了'更换角色'等旧文案"。
    /// 也兼容按钮被多次嵌套子节点的情形（取第一个 TextMeshProUGUI）。
    /// </summary>
    private void RefreshOpenButtonLabel()
    {
        if (openButtonGo == null) return;
        var tmp = openButtonGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp == null) return;
        const string want = "人物存档";
        if (tmp.text != want) tmp.text = want;
    }

    /// <summary>
    /// 强制把 SkinNames 写入卡片标题。修复"代码已改名字但场景里旧 panel 缓存了旧名字"。
    /// </summary>
    private void RefreshCardNames()
    {
        EnsureArraysCapacity();
        if (panelGo == null) return;
        for (int i = 0; i < 4; i++)
        {
            if (selectCardNameLabels[i] == null)
            {
                var card = panelGo.transform.Find($"SkinCard_{i}");
                if (card == null) continue;
                var nameTr = card.Find("Name");
                if (nameTr == null) continue;
                selectCardNameLabels[i] = nameTr.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (selectCardNameLabels[i] != null && selectCardNameLabels[i].text != SkinNames[i])
            {
                selectCardNameLabels[i].text = SkinNames[i];
            }
        }
    }

    /// <summary>
    /// 把"特殊功能"静态短文本写到每张卡片正面。
    /// 兼容两种情况：
    ///   1) 卡片是本次代码新建的——已有 Ability 节点，仅同步文本；
    ///   2) 卡片是旧场景里加载来的（没有 Ability 节点）——动态创建并填好。
    /// 同时也会按需调整旧卡片里 Icon 的尺寸/位置，确保新增的 Ability 文本不会与 Icon 重叠。
    /// 注意：EnsureUI 每帧调用 → 必须 ready 短路，避免每帧重写 anchor/offset/text 触发 Canvas dirty → 闪烁。
    /// </summary>
    private bool _abilitiesReady = false;
    private void RefreshCardAbilities()
    {
        if (panelGo == null) return;
        if (_abilitiesReady) return; // 已建好就不再每帧重写
        for (int i = 0; i < 4; i++)
        {
            var card = panelGo.transform.Find($"SkinCard_{i}");
            if (card == null) continue;

            // —— 兜底：旧场景里 Icon 占满中心，需要缩小+上移腾出 Ability 区——
            var iconTr = card.Find("Icon") as RectTransform;
            if (iconTr != null)
            {
                if (iconTr.sizeDelta.x > 135f || iconTr.sizeDelta.y > 135f
                    || iconTr.anchorMin != new Vector2(0.5f, 1f))
                {
                    iconTr.anchorMin = iconTr.anchorMax = new Vector2(0.5f, 1f);
                    iconTr.pivot = new Vector2(0.5f, 1f);
                    iconTr.sizeDelta = new Vector2(130f, 130f);
                    iconTr.anchoredPosition = new Vector2(0f, -50f);
                }
            }

            string text = (i >= 0 && i < SkinAbilityShortTexts.Length) ? SkinAbilityShortTexts[i] : "";

            var abilityTr = card.Find("Ability") as RectTransform;
            TextMeshProUGUI tmp;
            if (abilityTr == null)
            {
                // 旧 panel 没有 Ability 节点 —— 现场创建
                var abilityGo = new GameObject("Ability", typeof(RectTransform));
                abilityGo.transform.SetParent(card, false);
                var art = (RectTransform)abilityGo.transform;
                art.anchorMin = new Vector2(0f, 0f);
                art.anchorMax = new Vector2(1f, 1f);
                art.pivot = new Vector2(0.5f, 0.5f);
                art.offsetMin = new Vector2(8f, 75f);
                art.offsetMax = new Vector2(-8f, -185f);
                tmp = CreateText(abilityGo.transform, text, 15, TextAlignmentOptions.Top);
                tmp.richText = true;
                tmp.enableWordWrapping = true;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.lineSpacing = -6f;
                tmp.color = new Color(0.92f, 0.92f, 0.92f, 1f);
            }
            else
            {
                // 已有节点：把它的尺寸/换行设置也校准到新版（兼容旧版 Truncate 配置）
                abilityTr.anchorMin = new Vector2(0f, 0f);
                abilityTr.anchorMax = new Vector2(1f, 1f);
                abilityTr.pivot = new Vector2(0.5f, 0.5f);
                abilityTr.offsetMin = new Vector2(8f, 75f);
                abilityTr.offsetMax = new Vector2(-8f, -185f);
                tmp = abilityTr.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    if (tmp.text != text) tmp.text = text;
                    tmp.fontSize = 15;
                    tmp.richText = true;
                    tmp.enableWordWrapping = true;
                    tmp.overflowMode = TextOverflowModes.Overflow;
                    tmp.lineSpacing = -6f;
                    tmp.alignment = TextAlignmentOptions.Top;
                    tmp.color = new Color(0.92f, 0.92f, 0.92f, 1f);
                }
            }
        }
        _abilitiesReady = true; // 全部处理完毕，下一帧短路返回
    }

    /// <summary>
    /// 兜底：确保每张卡片右上角的 "i" InfoButton 存在且 hover 触发器有效。
    /// 处理两种历史脏档：
    ///   a) 旧 panel 完全没生成过 InfoButton（旧版没这功能）→ 现场补建；
    ///   b) 旧 panel 有 InfoButton 视觉节点，但 EventTrigger 组件丢失/triggers 列表为空
    ///       → 玩家鼠标悬停在 "i" 上没有任何反应，看起来"i 没用"。补建 EventTrigger 即可。
    /// 注意：EnsureUI 每帧调用 → 必须 ready 短路，否则每帧 GetComponent + 遍历 triggers 也会拖累 + 触发 dirty。
    /// </summary>
    private bool _infoButtonsReady = false;
    private void RefreshCardInfoButtons()
    {
        if (panelGo == null) return;
        if (_infoButtonsReady) return;
        for (int i = 0; i < 4; i++)
        {
            var card = panelGo.transform.Find($"SkinCard_{i}");
            if (card == null) continue;

            var infoTr = card.Find($"InfoButton_{i}");
            if (infoTr == null)
            {
                // 情况 a：完全没有 → 直接走标准创建路径
                CreateInfoButtonOnCard(card, i);
                continue;
            }

            // 情况 b：节点存在 → 检查 EventTrigger 是否有有效 PointerEnter/Exit
            var trigger = infoTr.GetComponent<EventTrigger>();
            if (trigger == null) trigger = infoTr.gameObject.AddComponent<EventTrigger>();
            bool hasEnter = false, hasExit = false;
            for (int k = 0; k < trigger.triggers.Count; k++)
            {
                var e = trigger.triggers[k];
                if (e.eventID == EventTriggerType.PointerEnter && e.callback.GetPersistentEventCount() + RuntimeListenerCount(e.callback) > 0) hasEnter = true;
                if (e.eventID == EventTriggerType.PointerExit  && e.callback.GetPersistentEventCount() + RuntimeListenerCount(e.callback) > 0) hasExit  = true;
            }
            // 若任一缺失则清空重建（避免历史残留 entry 干扰）
            if (!hasEnter || !hasExit)
            {
                trigger.triggers.Clear();
                AddInfoTrigger(trigger, EventTriggerType.PointerEnter, i, true);
                AddInfoTrigger(trigger, EventTriggerType.PointerExit,  i, false);
            }
        }
        _infoButtonsReady = true; // 全部 4 张卡片的 i 按钮校验完毕
    }

    /// <summary>
    /// 兜底：给每张卡片的 Icon 应用主题色描边/阴影（ApplyIconOutline），并清理上一版误加的 IconBg 节点。
    /// 解决：无罪（skinId=3）穿纯黑长袍，与深色卡片背景几乎融为一体 → 看起来只剩竖直发光物（"刀"的错觉）。
    ///
    /// 上一版用过"同级 IconBg 浅色背板 + 每帧 SetSiblingIndex"方案 → 因为 EnsureUI 每帧调用，
    /// 每帧都触发 SetSiblingIndex / sibling 重排 → Canvas dirty → 整个面板持续闪烁，
    /// 且 IconBg 在某些渲染顺序上会盖在 Icon 上、把角色截成一窄条。改方案：
    ///   - 不再创建 IconBg 同级节点；
    ///   - 改为给 Icon 自身添加 Outline + Shadow 描边阴影组件（同节点，不影响 layout）；
    ///   - 已存在的旧 IconBg 节点强制销毁。
    /// 通过 _iconBgsReady 字段做"已就绪短路"，避免每帧重复操作。
    /// </summary>
    private bool _iconBgsReady = false;
    private void RefreshCardIconBgs()
    {
        if (panelGo == null) return;
        if (_iconBgsReady) return;

        int builtCount = 0;
        for (int i = 0; i < 4; i++)
        {
            var card = panelGo.transform.Find($"SkinCard_{i}");
            if (card == null) continue;
            var iconTr = card.Find("Icon");
            if (iconTr == null) continue;

            // 1) 清理上一版误加的 IconBg 节点（如果存在）
            var oldBg = card.Find("IconBg");
            if (oldBg != null)
            {
                if (Application.isPlaying) Destroy(oldBg.gameObject);
                else DestroyImmediate(oldBg.gameObject);
            }

            // 2) 强制重新加载 sprite。
            //    背景：旧场景里 Icon 节点的 sprite 是按"错误的 4×3 网格切顶行第 1 帧"序列化保存的，
            //    对无罪而言取到的是一条非常窄、几乎透明的 256×341 区域 → 卡片上只剩"左上角一点点"。
            //    现在 LoadSpriteForSkin 改用了 trim 透明边距方案，必须强制重新赋值才能让旧场景受益。
            var iconImg = iconTr.GetComponent<Image>();
            if (iconImg != null)
            {
                iconImg.preserveAspect = true;
                Sprite freshSprite = LoadSpriteForSkin(i);
                if (freshSprite != null) iconImg.sprite = freshSprite;
            }

            // 3) 给 Icon 应用主题色描边
            ApplyIconOutline(iconTr.gameObject, i);

            builtCount++;
        }
        if (builtCount >= 4) _iconBgsReady = true;
    }

    /// <summary>
    /// 给指定 Icon GameObject 添加/更新 Outline 描边组件，让角色立绘从深色卡片背景里凸显出来。
    /// 不同 skinId 用不同主题色：
    ///   0 琪露诺：冰蓝；1 南筱风：草绿；2 夏无：橙红；3 无罪：浅紫灰（关键，让黑袍轮廓亮起来）
    /// 同时叠一个深色 Shadow 增强立体感。
    /// </summary>
    private static void ApplyIconOutline(GameObject iconGo, int skinId)
    {
        if (iconGo == null) return;
        Color outlineColor;
        switch (skinId)
        {
            case 0: outlineColor = new Color(0.55f, 0.85f, 1.00f, 0.95f); break;
            case 1: outlineColor = new Color(0.55f, 0.95f, 0.65f, 0.95f); break;
            case 2: outlineColor = new Color(1.00f, 0.70f, 0.40f, 0.95f); break;
            default: outlineColor = new Color(0.92f, 0.88f, 0.98f, 1.00f); break; // 无罪：偏亮的浅紫，让纯黑长袍有清晰轮廓
        }
        var outline = iconGo.GetComponent<UnityEngine.UI.Outline>();
        if (outline == null) outline = iconGo.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(skinId == 3 ? 3f : 2f, skinId == 3 ? -3f : -2f); // 无罪描边更粗
        outline.useGraphicAlpha = true;

        var shadow = iconGo.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow == null) shadow = iconGo.AddComponent<UnityEngine.UI.Shadow>();
        // 注意：同节点上 Outline + Shadow 共存时，Outline 必须先添加（Unity 文档明确要求），
        // 但因为我们已先添加 Outline，Shadow 在其后追加 → 顺序正确。
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(0f, -2f);
        shadow.useGraphicAlpha = true;
    }

    /// <summary>
    /// EventTrigger.TriggerEvent 没有公开"运行时监听器个数"，但只要 callback 非 null 且 invokable 非空就视为有效。
    /// 这里用反射兜底；失败返回 0（按"无运行时监听器"处理 → 触发重建，安全侧）。
    /// </summary>
    private static int RuntimeListenerCount(UnityEngine.Events.UnityEventBase ub)
    {
        if (ub == null) return 0;
        try
        {
            // UnityEventBase 内部维护 m_Calls.m_RuntimeCalls；不同 Unity 版本字段名差异 → 直接尝试调用 GetPersistentEventCount
            // 已经判断过；这里再多一层"反射拿运行时监听器列表"的兜底，失败则按 0 计。
            var fiCalls = typeof(UnityEngine.Events.UnityEventBase).GetField("m_Calls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fiCalls == null) return 0;
            var calls = fiCalls.GetValue(ub);
            if (calls == null) return 0;
            var fiRuntime = calls.GetType().GetField("m_RuntimeCalls",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (fiRuntime == null) return 0;
            var list = fiRuntime.GetValue(calls) as System.Collections.IList;
            return list != null ? list.Count : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 兜底：把已经存在的 backdrop 强制规整为"全屏拉伸"。
    /// （父节点不再强制设到 _canvas，第8步会统一 reparent 到 OverlayLayer。）
    /// </summary>
    private void FixBackdropFullscreen(GameObject backdrop)
    {
        if (backdrop == null) return;
        var rt = backdrop.transform as RectTransform;
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = Vector2.zero;
        }
        var img = backdrop.GetComponent<Image>();
        if (img != null)
        {
            // 颜色保持半透明黑（避免历史遗留版本是全透明的）
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = true;
        }
    }

    /// <summary>
    /// 给指定 GameObject 加一个 Canvas + GraphicRaycaster（如果还没有），并 override 一个 sortingOrder。
    /// （现已主要走 reparent 到 OverlayLayer 的方案，这个方法保留是为了向后兼容、
    ///  以及将来可能需要 sub-Canvas 隔离的场景。）
    /// </summary>
    private static void EnsureOverrideSortingCanvas(GameObject go, int sortingOrder)
    {
        if (go == null) return;
        var c = go.GetComponent<Canvas>();
        if (c == null) c = go.AddComponent<Canvas>();
        c.overrideSorting = true;
        c.sortingOrder = sortingOrder;
        if (go.GetComponent<GraphicRaycaster>() == null)
        {
            go.AddComponent<GraphicRaycaster>();
        }
    }

    /// <summary>创建全屏点击阻挡层。Image 全透明但 raycastTarget=true，吞掉所有点击。</summary>
    private GameObject CreateBackdrop()
    {
        var go = new GameObject("SkinChangerBackdrop", typeof(RectTransform));
        // 关键：挂到 Canvas 根而不是 SkinChanger 自身——
        // 如果 SkinChanger 是挂在 Canvas 的某个子节点（例如 title）上，
        // 子节点的 RectTransform 范围有限，backdrop 用 anchor=(0,0)~(1,1) 也只能填满父节点的局部矩形，
        // 表现为"半透明遮罩偏在一侧/没有覆盖整屏"。挂到 Canvas 根才能真正全屏拉伸。
        Transform parentTr = (_canvas != null) ? _canvas.transform : transform;
        go.transform.SetParent(parentTr, false);
        var rt = (RectTransform)go.transform;
        // 全屏拉伸（相对 Canvas 根 = 全屏）
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;

        var img = go.AddComponent<Image>();
        // 半透明黑色——既起视觉提示（背景变暗）又起 raycast blocker 的作用
        img.color = new Color(0f, 0f, 0f, 0.55f);
        img.raycastTarget = true;

        // 用 Button 组件作双保险——EventSystem 命中此处时直接消耗事件，
        // 防止穿透到下层的抽奖/开始游戏按钮。
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        // 不再在这里 EnsureOverrideSortingCanvas——
        // 改由 EnsureUI 第8步统一把 backdrop reparent 到 OverlayLayer，
        // 那里 sortingOrder=10000，能确定性盖在场景所有现存 Canvas 之上。

        return go;
    }

    private void CleanupLegacyNodes()
    {
        // 旧版 PlayerSkinShowroom 命名
        var legacyOpen = transform.Find("OpenSkinShowroomButton");
        if (legacyOpen != null)
        {
            if (Application.isPlaying) Destroy(legacyOpen.gameObject);
            else DestroyImmediate(legacyOpen.gameObject);
        }
        var legacyPanel = transform.Find("SkinShowroomPanel");
        if (legacyPanel != null)
        {
            if (Application.isPlaying) Destroy(legacyPanel.gameObject);
            else DestroyImmediate(legacyPanel.gameObject);
        }
    }

    // 防止 Inspector 序列化的旧 size=3 数组在运行时越界（升级到 4 张皮肤后）。
    private void EnsureArraysCapacity()
    {
        const int N = 4;
        if (selectButtonGos == null || selectButtonGos.Length < N) System.Array.Resize(ref selectButtonGos, N);
        if (selectButtonLabels == null || selectButtonLabels.Length < N) System.Array.Resize(ref selectButtonLabels, N);
        if (selectCardNameLabels == null || selectCardNameLabels.Length < N) System.Array.Resize(ref selectCardNameLabels, N);
        if (selectButtonImages == null || selectButtonImages.Length < N) System.Array.Resize(ref selectButtonImages, N);
    }

    private void RecaptureChildRefs()
    {
        EnsureArraysCapacity();
        if (panelGo == null) return;

        if (closeButtonGo == null)
        {
            var t = panelGo.transform.Find("CloseButton");
            if (t != null) closeButtonGo = t.gameObject;
        }

        for (int i = 0; i < 4; i++)
        {
            // 名字 Text 的回填（独立于 button，所以单独处理）
            if (selectCardNameLabels[i] == null)
            {
                var nameCard = panelGo.transform.Find($"SkinCard_{i}");
                if (nameCard != null)
                {
                    var nameTr = nameCard.Find("Name");
                    if (nameTr != null) selectCardNameLabels[i] = nameTr.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            if (selectButtonGos[i] != null && selectButtonImages[i] != null && selectButtonLabels[i] != null) continue;
            var card = panelGo.transform.Find($"SkinCard_{i}");
            if (card == null) continue;
            var btn = card.Find("SelectButton");
            if (btn == null) continue;
            selectButtonGos[i] = btn.gameObject;
            selectButtonImages[i] = btn.GetComponent<Image>();
            selectButtonLabels[i] = btn.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    private GameObject CreateOpenButton()
    {
        var go = new GameObject("SkinChangerOpenButton", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.sizeDelta = openButtonSize;
        rt.anchoredPosition = openButtonAnchoredPosition;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.18f, 0.32f, 0.55f, 1f);
        // raycastTarget=true：阻挡 EventSystem 把点击穿透到下面的"抽奖/开始游戏"按钮
        img.raycastTarget = true;

        // 双保险：挂 Button 让 EventSystem 在命中此处时消耗事件，
        // 即使 onClick 不绑定，也能阻止事件继续向下传递（项目现状下 Button.onClick 偶尔失效，
        // 因此真正业务逻辑仍依赖我们自己的 Input.GetMouseButtonDown + Rect 包含判断在 Update 里执行）。
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var label = CreateText(go.transform, "人物存档", 22, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;

        // 提到最前层级，确保在主菜单其它按钮上方绘制 + 优先命中
        go.transform.SetAsLastSibling();

        return go;
    }

    private GameObject CreatePanel()
    {
        var go = new GameObject("SkinChangerPanel", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = Vector2.zero;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);
        // 设为 true：阻挡 EventSystem 把点击穿透到面板下面的按钮（如开始游戏 / 抽奖等）
        bg.raycastTarget = true;

        // 标题
        var titleGo = new GameObject("Title", typeof(RectTransform));
        titleGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)titleGo.transform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, 50f);
        trt.anchoredPosition = new Vector2(0f, -10f);
        var titleTxt = CreateText(titleGo.transform, "人物存档", 32, TextAlignmentOptions.Center);
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = new Color(1f, 0.85f, 0.4f, 1f);

        // 关闭按钮
        closeButtonGo = CreateCloseButton(go.transform);

        // 四张卡片
        for (int i = 0; i < 4; i++)
        {
            CreateSkinCard(go.transform, i);
        }

        // 提到最前层级，确保面板覆盖一切（含抽奖按钮等）
        go.transform.SetAsLastSibling();

        return go;
    }

    private GameObject CreateCloseButton(Transform parent)
    {
        var go = new GameObject("CloseButton", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(50f, 50f);
        rt.anchoredPosition = new Vector2(-10f, -10f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.72f, 0.18f, 0.18f, 1f);
        img.raycastTarget = true;

        var t = CreateText(go.transform, "X", 28, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;

        return go;
    }

    private void CreateSkinCard(Transform parent, int index)
    {
        var card = new GameObject($"SkinCard_{index}", typeof(RectTransform));
        card.transform.SetParent(parent, false);
        var crt = (RectTransform)card.transform;
        float cardW = 240f, cardH = 380f;
        float spacing = 30f;
        const int CardCount = 4;
        float totalW = cardW * CardCount + spacing * (CardCount - 1);
        float startX = -totalW * 0.5f + cardW * 0.5f;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(cardW, cardH);
        crt.anchoredPosition = new Vector2(startX + index * (cardW + spacing), -20f);

        var bg = card.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.15f, 0.22f, 1f);
        bg.raycastTarget = false;

        // 名字
        var nameGo = new GameObject("Name", typeof(RectTransform));
        nameGo.transform.SetParent(card.transform, false);
        var nrt = (RectTransform)nameGo.transform;
        nrt.anchorMin = new Vector2(0f, 1f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f);
        nrt.sizeDelta = new Vector2(0f, 40f);
        nrt.anchoredPosition = new Vector2(0f, -8f);
        var nameTxt = CreateText(nameGo.transform, SkinNames[index], 24, TextAlignmentOptions.Center);
        nameTxt.fontStyle = FontStyles.Bold;
        nameTxt.color = new Color(1f, 0.92f, 0.6f, 1f);
        selectCardNameLabels[index] = nameTxt;

        // 预览图（缩小并上移，给下方"特殊功能"文本腾出空间）
        // 注意：无罪（index=3）穿纯黑长袍，与深色卡片背景容易融为一体 → 玩家看起来只剩竖直发光物（"刀"的错觉）。
        // 我们之前尝试加 IconBg 浅色背板，但带来了"每帧 SetSiblingIndex 导致 Canvas dirty 闪烁"的副作用，
        // 也可能让 IconBg 的纯色矩形遮挡到 Icon 的真实显示区域。
        // 改为：给 Icon 自身加一个 Image 描边 + 阴影组件（不增加同级 sibling 节点，不影响 layout），
        // 描边色对无罪用浅紫灰（让黑袍轮廓亮起来），其他角色用更柔和的白/黄做主题强化。
        var iconGo = new GameObject("Icon", typeof(RectTransform));
        iconGo.transform.SetParent(card.transform, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f);
        irt.pivot = new Vector2(0.5f, 1f);
        irt.sizeDelta = new Vector2(130f, 130f);
        irt.anchoredPosition = new Vector2(0f, -50f);
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.color = Color.white;
        iconImg.raycastTarget = false;
        iconImg.preserveAspect = true;
        iconImg.sprite = LoadSpriteForSkin(index);
        ApplyIconOutline(iconGo, index);

        // 特殊功能简介（静态文本，常驻显示在卡片正面）
        // 注意：这段是简短版（SkinAbilityShortTexts），目的是让玩家一眼看到关键加成；
        // 完整说明仍保留在右上角 "i" 按钮的 hover tooltip 中（SkinInfoTexts）。
        // 卡片高 380：Name(顶 50) + Icon(顶下 50→180，约) + Ability + SelectButton(底 75)
        // Ability 区: offsetMin.y=75（贴近按钮顶部），offsetMax.y=-185（紧挨 Icon 下沿）
        //            实际高度 ≈ 380 - 75 - 185 = 120px，能放下夏无的 5 行短文本。
        var abilityGo = new GameObject("Ability", typeof(RectTransform));
        abilityGo.transform.SetParent(card.transform, false);
        var art = (RectTransform)abilityGo.transform;
        art.anchorMin = new Vector2(0f, 0f);
        art.anchorMax = new Vector2(1f, 1f);
        art.pivot = new Vector2(0.5f, 0.5f);
        art.offsetMin = new Vector2(8f, 75f);
        art.offsetMax = new Vector2(-8f, -185f);
        var abilityTxt = CreateText(abilityGo.transform,
            (index >= 0 && index < SkinAbilityShortTexts.Length) ? SkinAbilityShortTexts[index] : "",
            15, TextAlignmentOptions.Top);
        abilityTxt.richText = true;
        abilityTxt.enableWordWrapping = true;
        // 用 Overflow 而不是 Truncate：宁可视觉上略溢出也不要把"开局自带火球术"这种关键信息吃掉。
        abilityTxt.overflowMode = TextOverflowModes.Overflow;
        abilityTxt.lineSpacing = -6f;
        abilityTxt.color = new Color(0.92f, 0.92f, 0.92f, 1f);

        // 使用按钮
        var btnGo = new GameObject("SelectButton", typeof(RectTransform));
        btnGo.transform.SetParent(card.transform, false);
        var brt = (RectTransform)btnGo.transform;
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(-30f, 60f);
        brt.anchoredPosition = new Vector2(0f, 15f);

        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.20f, 0.32f, 0.55f, 1f);
        btnImg.raycastTarget = true;

        var btnLabel = CreateText(btnGo.transform, "使用", 24, TextAlignmentOptions.Center);
        btnLabel.fontStyle = FontStyles.Bold;
        btnLabel.color = Color.white;

        selectButtonGos[index] = btnGo;
        selectButtonImages[index] = btnImg;
        selectButtonLabels[index] = btnLabel;

        // ===== UR 加成信息按钮（hover tooltip）=====
        // 仿照 GachaUI 的 "i" 圆按钮：鼠标悬停时弹出该角色 UR 加成说明。
        // 复用 EventTrigger PointerEnter/Exit；样式紧贴卡片右上角。
        CreateInfoButtonOnCard(card.transform, index);
    }

    private void CreateInfoButtonOnCard(Transform cardParent, int skinId)
    {
        var go = new GameObject($"InfoButton_{skinId}", typeof(RectTransform));
        go.transform.SetParent(cardParent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(34f, 34f);
        rt.anchoredPosition = new Vector2(-6f, -6f);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.10f, 0.18f, 0.92f);
        img.raycastTarget = true;

        // 让它以 Button 组件兜底吃掉点击事件，但实际信息显示用 EventTrigger 的 hover
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        var t = CreateText(go.transform, "i", 22, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        t.color = new Color(1f, 0.92f, 0.4f, 1f);
        t.raycastTarget = false;

        var trigger = go.AddComponent<EventTrigger>();
        AddInfoTrigger(trigger, EventTriggerType.PointerEnter, skinId, true);
        AddInfoTrigger(trigger, EventTriggerType.PointerExit,  skinId, false);
    }

    private void AddInfoTrigger(EventTrigger trigger, EventTriggerType type, int skinId, bool show)
    {
        var entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(_ => ShowSkinInfoTooltip(skinId, show));
        trigger.triggers.Add(entry);
    }

    /// <summary>显示/隐藏 UR 加成 tooltip（共用同一个面板，按 skinId 切换文本）。</summary>
    private void ShowSkinInfoTooltip(int skinId, bool show)
    {
        if (panelGo == null) return;

        if (show)
        {
            EnsureInfoTooltip();
            if (_infoTooltipText != null)
            {
                _infoTooltipText.text = (skinId >= 0 && skinId < SkinInfoTexts.Length)
                    ? SkinInfoTexts[skinId]
                    : "—";
            }
            if (_infoTooltip != null) _infoTooltip.SetActive(true);
        }
        else
        {
            if (_infoTooltip != null) _infoTooltip.SetActive(false);
        }
    }

    private void EnsureInfoTooltip()
    {
        if (_infoTooltip != null) return;
        if (panelGo == null) return;

        var go = new GameObject("SkinInfoTooltip", typeof(RectTransform));
        // 挂在 panel 之外（直接挂到 SkinChanger transform），保证位于面板上层，且不被卡片裁剪
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620f, 320f);
        // 显示在面板上方一点
        rt.anchoredPosition = new Vector2(0f, panelSize.y * 0.5f + 180f);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.025f, 0.04f, 0.96f);
        bg.raycastTarget = false; // 不阻挡鼠标，避免覆盖到下面的 info 按钮导致 PointerExit 抖动

        var txt = CreateText(go.transform, "", 20, TextAlignmentOptions.TopLeft);
        var tr = txt.GetComponent<RectTransform>();
        tr.offsetMin = new Vector2(20, 20);
        tr.offsetMax = new Vector2(-20, -20);
        txt.enableWordWrapping = true;
        txt.overflowMode = TextOverflowModes.Overflow;
        txt.lineSpacing = -6f;
        txt.richText = true;
        txt.color = new Color(0.95f, 0.95f, 0.95f, 1f);
        txt.name = "Content";

        _infoTooltip = go;
        _infoTooltipText = txt;
        go.transform.SetAsLastSibling();
        go.SetActive(false);
    }

    private Sprite LoadSpriteForSkin(int skinId)
    {
        // 走 RuntimeAssetLoader 三层兜底：Inspector 引用 → Resources.Load → Application.dataPath（仅编辑器）
        // 这是打包后唯一可靠的加载链路；编辑器内三条都能跑，Build 中只有第一条引用持久有效。
        Texture2D direct;
        string editorRel;
        switch (skinId)
        {
            case 0: direct = cirnoIconTexture; editorRel = CirnoIconPath; break;
            case 1: direct = ur0IconTexture;   editorRel = Ur0IconPath;   break;
            case 2: direct = ur1IconTexture;   editorRel = Ur1IconPath;   break;
            default: direct = ur2IconTexture;  editorRel = Ur2IconPath;   break;
        }
        var tex = RuntimeAssetLoader.LoadTexture(direct, null, editorRel);
        if (tex == null) return null;
        try
        {
            // === 经反复实验确认：ur0/ur1/ur2 三张 PNG 均为 1024×1024、**4 列 × 4 行的精灵网格**，
            //     每帧 256×256，左上角(PNG 坐标 0,0,256,256)就是该角色完整的"正面 idle"立绘。
            //     琪露诺保持原行为（cirno_skin.png 是单帧立绘）。
            if (skinId == 0)
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);
            }
            else
            {
                int cellW = tex.width / 4;
                int cellH = tex.height / 4;
                int yTopRow = tex.height - cellH; // 顶行（Sprite 坐标 Y 朝上）
                return Sprite.Create(tex, new Rect(0, yTopRow, cellW, cellH),
                                     new Vector2(0.5f, 0.5f), 100f);
            }
        }
        catch
        {
            return null;
        }
    }

    private TextMeshProUGUI CreateText(Transform parent, string content, float size, TextAlignmentOptions align)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        if (uiFont != null) tmp.font = uiFont;
        return tmp;
    }
}
