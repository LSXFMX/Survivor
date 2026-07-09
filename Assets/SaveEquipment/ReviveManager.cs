using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 复活管理器（R_2 读档币 / 抽卡 R 装备）。
///
/// ── 设计目标 ────────────────────────────────────────────────────────────────
/// 1) 每局游戏只能复活一次（通过 _usedThisRun 标记，进入战斗场景时由 EquipmentInitializer 重置）。
/// 2) 玩家死亡时，先弹出"是否使用读档币复活"的选择面板，等待玩家点击后再决定后续。
/// 3) 严格时序保证：
///    - <see cref="TryConsumeReviveAndRecover"/> 由 Player.death() 在确认主体死亡且 _isDead 置位后调用；
///    - 若返回 true：本类已接管全部死亡 / 复活后续流程，Player.death() 必须立即 return；
///      • 玩家选「复活」：扣 1 张读档币 + health 拉回满 + _isDead 反射清零 + 0.5~1.5s grace 防瞬死 +
///                       恢复 timeScale；分身保留（复活也能继承）；不再启动 ReturnToMain。
///      • 玩家选「放弃」：清场所有分身 + 恢复 timeScale + 由本类自己 StartCoroutine
///                       battleUI.ReturnToMainPublic(false)，等同原死亡流程。
///    - 若返回 false：表示玩家没有读档币 / 本局已用过 / 不是主玩家，
///      Player.death() 按原逻辑继续走「清场分身 → ReturnToMain」。
///
/// ── 时序约定（关键不能反！）─────────────────────────────────────────────
///   Player.death()：
///     1. 分身判定 / grace 拦截 / _isDead 防重入
///     2. _isDead = true
///     3. if (ReviveManager.TryConsumeReviveAndRecover(this)) return;   ← 复活流程被接管
///     4. 清场所有分身                                                  ← 否则才执行
///     5. battleUI.StartCoroutine(ReturnToMainPublic(false))            ← 否则才执行
///
///   注意：本接口是「同步返回 + 异步完成」的——
///     • 同步部分：检查能否复活 → 弹窗 → Time.timeScale = 0 → StartCoroutine 协程 → return true。
///     • 异步部分：协程内 while 等待玩家点击；玩家点完后才执行复活 / 放弃的实际逻辑。
///   Player.death() 调用方一律按"返回 true 就 return"处理；剩余生命周期完全由本类协程负责。
///   不能改成"协程驱动 Player.death()"——Player.death() 不是协程，且 _isDead 防重入要求它必须同步返回。
///
/// ── 读档币（R_2）规格 ─────────────────────────────────────────────────────
///   稀有度: GachaRarity.R, rarityId = 2
///   存档 key: GachaCount_R_2（与其他 R/SR 抽卡装备一致）
///   消耗规则: 每次复活消耗 1 张；用完不能再复活。
///   局内限制: 即使存档币 ≥ 2，每局只能复活 1 次（由 _usedThisRun 控制）。
/// </summary>
public class ReviveManager : MonoBehaviour
{
    public static ReviveManager Instance { get; private set; }

    /// <summary>读档币在抽卡系统中的稀有度内编号（R_2）。</summary>
    public const int RARITY_ID = 2;

    /// <summary>本局是否已经用过复活（每局仅 1 次硬上限）。</summary>
    private bool _usedThisRun = false;

    // 玩家选择结果：0=未选择 / 1=确认复活 / -1=放弃
    private int _decision = 0;
    private bool _waiting = false;

    // 动态生成的 UI（只在需要时才 Lazy 创建）
    private GameObject _panel;
    private TextMeshProUGUI _msgText;

    // 缓存暂停前的 timeScale，复活后复原
    private float _savedTimeScale = 1f;

    // 中文字体（heiti SDF）。Lazy 解析：首次创建面板时从 ToastManager / 场景里现有 TMP 文本拿。
    // 不加这个，TextMeshProUGUI 会用 LiberationSans，全部中文字符渲染成豆腐方块（□）。
    private TMPro.TMP_FontAsset _cnFont;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>战斗场景开局时由 EquipmentInitializer.Start 调用，重置每局状态。</summary>
    public void ResetForNewRun()
    {
        _usedThisRun = false;
        _decision = 0;
        _waiting = false;
        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>
    /// 玩家是否还能复活（已学到/有读档币 + 本局未用过）。供 UI 提示判断。
    /// </summary>
    public bool CanReviveNow()
    {
        if (_usedThisRun) return false;
        if (PlayerPrefs.GetInt($"GachaCount_R_{RARITY_ID}", 0) <= 0) return false;
        return true;
    }

    /// <summary>
    /// Player.death() 在主体死亡判定 + _isDead = true 之后调用此方法。
    ///
    /// 返回值：
    ///   true  ─ 表示已成功接管死亡 / 复活流程，调用方 **必须立即 return**：
    ///           不要再清场分身、不要再启动 ReturnToMain。后续生命周期完全由本类协程负责
    ///           （复活成功路径 OR 放弃路径都已就地处理）。
    ///   false ─ 表示玩家不满足复活条件（无读档币 / 本局已用过 / player 为空），
    ///           调用方按原死亡流程继续：清场分身 → ReturnToMain。
    ///
    /// 实现：返回 true 之前，本方法已：
    ///   1) 把 timeScale 设为 0（暂停游戏，但 UI 事件依然派发）；
    ///   2) 弹出"是否复活"对话框；
    ///   3) StartCoroutine 自旋等待玩家点击。
    /// 玩家点击后由 WaitDecisionAndApply 协程根据决定执行复活 OR 放弃路径，
    /// 主线程上的 Player.death() 此时早已 return 完成。
    /// </summary>
    public bool TryConsumeReviveAndRecover(Player player)
    {
        if (player == null) return false;
        if (!CanReviveNow()) return false;

        // 弹窗 + 等待
        EnsurePanel();
        ShowPanel(true);
        _decision = 0;
        _waiting = true;

        // 暂停游戏（用 unscaled time 等待玩家点击）
        _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        // 由协程负责后续：等待玩家选择 → 执行复活 / 放弃路径。
        // Time.timeScale=0 不会冻结协程的 yield return null（基于真实帧），
        // 也不会冻结 UGUI 的 OnClick 派发，所以协程能正常推进。
        StartCoroutine(WaitDecisionAndApply(player));

        // 返回 true 表示「我接管了死亡流程，不要再走 ReturnToMain」。
        // 不论玩家最终选「复活」还是「放弃」，都由协程内部完成善后；
        // Player.death() 调用方一律按"true → return"处理。
        return true;
    }

    private IEnumerator WaitDecisionAndApply(Player player)
    {
        // 等待玩家点击（unscaledTime 驱动，不受 timeScale=0 影响）
        while (_waiting && _decision == 0)
            yield return null;

        bool revived = (_decision == 1);
        _waiting = false;
        ShowPanel(false);

        if (revived)
        {
            // 1) 扣 1 张读档币
            string key = $"GachaCount_R_{RARITY_ID}";
            int cur = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.SetInt(key, Mathf.Max(0, cur - 1));
            PlayerPrefs.Save();

            // 2) 标记本局已使用，杜绝再次弹窗
            _usedThisRun = true;

            // 3) 取消玩家死亡状态 + 满血复活
            //    注意：Player._isDead 字段是 private，没有公开 setter；
            //    我们用反射兜底设回 false，否则下次受伤又会因为防重入直接 return 不死，
            //    但奇遇/Boss 死亡判定会卡住。这里反射一次代价可接受。
            ResetPlayerDeadFlag(player);
            player.health = player.healthmax;

            // 4) 给玩家一个短暂的死亡免疫窗口，避免敌人下一帧再次撞死
            Player.MainPlayerDeathGraceUntilUnscaled = Time.unscaledTime + 1.5f;

            // 5) 恢复 timeScale
            Time.timeScale = _savedTimeScale;

            ToastManager.Show("[读档币] 原地复活！本局复活机会已用完");
            Debug.Log($"[ReviveManager] 复活成功：消耗读档币 1 张（剩余 {PlayerPrefs.GetInt(key, 0)}），玩家 hp 已恢复满");

            // 6) 通知存档界面 / 战斗 UI 刷新读档币计数显示（如果在场景里就刷新）
            //    EquipmentIcon.UpdateDisplay 会自动重新读 GachaCount_R_2，因此这里只要广播 OnEquipmentUnlocked 即可，
            //    但读档币不是 EquipmentSystem 的解锁项（R/SR 用 PlayerPrefs 计数），无现成事件。
            //    最简方案：让所有可见的 EquipmentIcon 立即刷新一次。
            RefreshAllReviveIconsInScene();
        }
        else
        {
            // 玩家放弃复活：恢复 timeScale，按原死亡流程返回主菜单。
            // 注意：必须把"清场分身"也搬过来——因为 Player.death() 在调用本方法后就直接 return 了，
            // 它原本的分身清场代码不会被执行，否则放弃复活后分身仍在场上跟随光标乱跑。
            CleanupAllClones(player);

            Time.timeScale = _savedTimeScale;
            var bui = FindObjectOfType<battleUI>();
            if (bui != null)
            {
                bui.StartCoroutine(bui.ReturnToMainPublic(false));
            }
            else
            {
                Debug.LogWarning("[ReviveManager] 玩家放弃复活，但未找到 battleUI；请检查场景。");
            }
        }
    }

    /// <summary>
    /// 与 Player.death() 中原本的"清场分身"逻辑等价，搬到这里方便"玩家放弃复活"分支复用。
    /// </summary>
    private static void CleanupAllClones(Player mainPlayer)
    {
        if (mainPlayer == null) return;
        Transform layer = mainPlayer.transform.parent;
        if (layer == null) return;

        var toKill = new System.Collections.Generic.List<GameObject>();
        foreach (Transform t in layer)
        {
            if (t == null || t == mainPlayer.transform) continue;
            if (t.GetComponent<Player>() != null) toKill.Add(t.gameObject);
        }
        for (int i = 0; i < toKill.Count; i++)
        {
            if (toKill[i] == null) continue;
            toKill[i].SetActive(false);
            Object.Destroy(toKill[i]);
        }
    }

    private static void ResetPlayerDeadFlag(Player player)
    {
        // 直接调用公开方法，替代反射（反射设私有字段在 IL2CPP 代码剥离下可能失败，
        // 导致复活后 _isDead 仍为 true → 玩家无法再次触发死亡/复活流程）。
        if (player != null) player.ResetDeadFlag();
    }

    private static void RefreshAllReviveIconsInScene()
    {
        var icons = FindObjectsOfType<EquipmentIcon>();
        foreach (var ic in icons)
        {
            if (ic == null) continue;
            // 只刷新 R 类抽卡图标，避免不必要的全场刷新
            if (ic.equipmentType == EquipmentType.GachaEquipment && ic.gachaRarity == GachaRarity.R)
                ic.ManualUpdateDisplay();
        }
    }

    // ── UI 玩家点击回调 ───────────────────────────────────────────────────
    public void OnClickConfirm()
    {
        if (!_waiting) return;
        _decision = 1;
    }

    public void OnClickCancel()
    {
        if (!_waiting) return;
        _decision = -1;
    }

    // ── 动态构建一个简单的 Canvas+Panel UI（避免依赖手工拖 prefab）─────────
    private void EnsurePanel()
    {
        if (_panel != null) return;

        // 先解析中文字体，下面创建的所有 TMP 文本都要赋这个 font，否则全是 □ 乱码
        _cnFont = ResolveChineseFont();

        // 顶层 Canvas（独立，避免被战斗 UI 暂停隐藏）
        var canvasGo = new GameObject("ReviveCanvas");
        DontDestroyOnLoad(canvasGo);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // 顶层
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        // 整屏遮罩
        _panel = new GameObject("RevivePanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var prt = _panel.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
        prt.offsetMin = Vector2.zero; prt.offsetMax = Vector2.zero;
        var pImg = _panel.AddComponent<Image>();
        pImg.color = new Color(0f, 0f, 0f, 0.78f);
        pImg.raycastTarget = true; // 阻挡背景点击

        // 中央内容容器
        var content = new GameObject("Content");
        content.transform.SetParent(_panel.transform, false);
        var crt = content.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(720f, 360f);
        crt.anchoredPosition = Vector2.zero;
        var cImg = content.AddComponent<Image>();
        cImg.color = new Color(0.10f, 0.10f, 0.14f, 0.96f);

        // 标题文字
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(content.transform, false);
        var trt = titleGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0, 80f);
        trt.anchoredPosition = new Vector2(0f, -20f);
        var titleTxt = titleGo.AddComponent<TextMeshProUGUI>();
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.fontSize = 48;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.color = new Color(1f, 0.85f, 0.4f);
        if (_cnFont != null) titleTxt.font = _cnFont;
        // 注意：标题里不要用「」（heiti SDF 字体没收录这两个全角符号，会渲染成 □）。
        // 改用富文本 <color> 给"读档币"上色突出，效果等价且兼容性更好。
        titleTxt.richText = true;
        titleTxt.text = "你死了 — 是否使用 <color=#FFD24A>读档币</color> 复活？";

        // 副文字（动态显示剩余数量）
        var msgGo = new GameObject("Msg");
        msgGo.transform.SetParent(content.transform, false);
        var mrt = msgGo.AddComponent<RectTransform>();
        mrt.anchorMin = new Vector2(0f, 0.5f);
        mrt.anchorMax = new Vector2(1f, 0.5f);
        mrt.pivot = new Vector2(0.5f, 0.5f);
        mrt.sizeDelta = new Vector2(0, 90f);
        mrt.anchoredPosition = new Vector2(0f, 10f);
        _msgText = msgGo.AddComponent<TextMeshProUGUI>();
        _msgText.alignment = TextAlignmentOptions.Center;
        _msgText.fontSize = 28;
        _msgText.color = Color.white;
        if (_cnFont != null) _msgText.font = _cnFont;

        // 按钮：是
        var yesGo = MakeButton(content.transform, "Yes", "复活（消耗 1 张）",
            new Vector2(-160f, -110f), new Color(0.18f, 0.52f, 0.20f, 1f), _cnFont);
        yesGo.GetComponent<Button>().onClick.AddListener(OnClickConfirm);

        // 按钮：否
        var noGo = MakeButton(content.transform, "No", "放弃 / 返回主菜单",
            new Vector2(160f, -110f), new Color(0.55f, 0.18f, 0.18f, 1f), _cnFont);
        noGo.GetComponent<Button>().onClick.AddListener(OnClickCancel);

        _panel.SetActive(false);
    }

    private static GameObject MakeButton(Transform parent, string name, string label, Vector2 anchored, Color bg, TMPro.TMP_FontAsset cnFont)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280f, 80f);
        rt.anchoredPosition = anchored;
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var txtGo = new GameObject("Text");
        txtGo.transform.SetParent(go.transform, false);
        var trt = txtGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
        var t = txtGo.AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 30;
        t.fontStyle = FontStyles.Bold;
        t.color = Color.white;
        if (cnFont != null) t.font = cnFont;
        t.text = label;
        t.raycastTarget = false;
        return go;
    }

    /// <summary>
    /// 解析中文字体（heiti SDF）。
    /// 优先级：
    ///   1) ToastManager.Instance.font（场景里已有 Inspector 拖好）；
    ///   2) 场景里任意 TextMeshProUGUI.font（拿一个能显示中文的就行）；
    ///   3) Resources.Load&lt;TMP_FontAsset&gt;("heiti SDF") 兜底（heiti SDF 不在 Resources 里，
    ///      正常返回 null，最终回落到 LiberationSans —— 这种情况下中文仍会乱码，
    ///      但只要场景中至少有一个 TMP 文本（几乎一定有），第 2 步就能拿到正确字体）。
    /// </summary>
    private static TMPro.TMP_FontAsset ResolveChineseFont()
    {
        if (ToastManager.Instance != null && ToastManager.Instance.font != null)
            return ToastManager.Instance.font;

        var any = FindObjectsOfType<TextMeshProUGUI>(true);
        foreach (var t in any)
        {
            if (t != null && t.font != null) return t.font;
        }
        // 兜底：仅当 heiti SDF 被放进 Resources/ 时才会命中
        return Resources.Load<TMPro.TMP_FontAsset>("heiti SDF");
    }

    private void ShowPanel(bool visible)
    {
        if (_panel == null) return;
        _panel.SetActive(visible);
        if (visible && _msgText != null)
        {
            int remain = PlayerPrefs.GetInt($"GachaCount_R_{RARITY_ID}", 0);
            _msgText.text =
                $"当前剩余读档币：<color=#FFD24A>{remain}</color> 张\n" +
                $"复活将原地满血起身，并继续本局游戏\n" +
                $"<size=22><color=#999999>每局只能使用 1 次</color></size>";
        }
    }
}
