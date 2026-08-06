using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 主菜单「切换存档」面板（运行时构建）。
///
/// 三个槽位竖排，每个槽显示摘要（最高通关 / 时长 / 装备数 / 塔层 / 源），
/// 当前槽标「使用中」。点击其它槽 → 二次确认 → 切换 → 重载主菜单场景刷新全部 UI。
/// 每个槽还带一个「清空」按钮，用于在**不影响其它槽**的前提下重新开荒。
///
/// 与项目里其它运行时 UI 一致：不动 SampleScene（25MB YAML，编辑风险大），
/// 全部用代码建在 UIOverlayLayer 上；文字用内置 LegacyRuntime.ttf，中文不会乱码。
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    private static SaveSlotUI _instance;

    private GameObject _root;
    private Font       _font;
    private readonly Text[] _slotLabels = new Text[SaveSlotManager.SLOT_COUNT + 1];

    /// <summary>确认弹窗（切换 / 清空共用）。</summary>
    private GameObject _confirmPanel;
    private Text       _confirmText;
    private System.Action _confirmAction;

    public static void Open()
    {
        if (_instance == null)
        {
            var go = new GameObject("SaveSlotUI");
            _instance = go.AddComponent<SaveSlotUI>();
        }
        _instance.Show();
    }

    private void Show()
    {
        EnsureBuilt();
        if (_root == null) return;
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        RefreshAll();
    }

    private void Hide()
    {
        if (_root != null) _root.SetActive(false);
        if (_confirmPanel != null) _confirmPanel.SetActive(false);
    }

    // ══════════════════════ 构建 ══════════════════════

    private void EnsureBuilt()
    {
        if (_root != null) return;

        Transform host = UIOverlayLayer.Get();
        if (host == null) return;

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ── 全屏遮罩（挡住背后按钮，点空白不关闭以免误触）──
        _root = new GameObject("SaveSlotPanel", typeof(RectTransform));
        _root.transform.SetParent(host, false);
        var rrt = (RectTransform)_root.transform;
        rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
        rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
        var mask = _root.AddComponent<Image>();
        mask.color = new Color(0f, 0f, 0f, 0.75f);

        // ── 主面板──
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_root.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(660f, 560f);
        prt.anchoredPosition = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.07f, 0.08f, 0.13f, 0.98f);

        MakeLabel(panel.transform, "切换 存 档", new Vector2(0f, -16f),
                  new Vector2(0f, 52f), 30, new Color(1f, 0.85f, 0.4f), true);
        MakeLabel(panel.transform, "每个存档互相独立，切换后原存档完整保留",
                  new Vector2(0f, -66f), new Vector2(0f, 28f), 16,
                  new Color(0.78f, 0.83f, 0.95f), true);

        // ── 三个槽 ──
        for (int slot = 1; slot <= SaveSlotManager.SLOT_COUNT; slot++)
        {
            float top = -104f - (slot - 1) * 124f;
            BuildSlotRow(panel.transform, slot, top);
        }

        // ── 关闭 ──
        var closeBtn = MakeButton(panel.transform, "关闭", new Vector2(0f, 20f),
                                  new Vector2(200f, 48f), new Color(0.2f, 0.2f, 0.25f), false);
        closeBtn.onClick.AddListener(() =>
        {
            AudioManager.PlaySfx(AudioManager.SfxKey.Click);
            Hide();
        });

        BuildConfirmPanel();
    }

    private void BuildSlotRow(Transform parent, int slot, float top)
    {
        // 行底板
        var row = new GameObject($"Slot{slot}", typeof(RectTransform), typeof(Image));
        row.transform.SetParent(parent, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(24f, 0f);
        rt.offsetMax = new Vector2(-24f, 0f);
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, 112f);
        rt.anchoredPosition = new Vector2(0f, top);
        row.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.22f, 1f);

        // 摘要文字（左侧，占大部分宽度）
        var label = MakeLabel(row.transform, "", Vector2.zero, Vector2.zero, 17, Color.white, false);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(0.66f, 1f);
        lrt.offsetMin = new Vector2(16f, 6f);
        lrt.offsetMax = new Vector2(0f, -6f);
        label.alignment = TextAnchor.MiddleLeft;
        _slotLabels[slot] = label;

        int captured = slot;

        // 使用此存档
        var useBtn = MakeButton(row.transform, "使用", Vector2.zero,
                                Vector2.zero, new Color(0.16f, 0.32f, 0.20f), false);
        var urt = (RectTransform)useBtn.transform;
        urt.anchorMin = new Vector2(0.68f, 0.5f);
        urt.anchorMax = new Vector2(0.68f, 0.5f);
        urt.pivot = new Vector2(0f, 0.5f);
        urt.sizeDelta = new Vector2(120f, 48f);
        urt.anchoredPosition = Vector2.zero;
        useBtn.onClick.AddListener(() => OnUseSlot(captured));

        // 清空此存档
        var eraseBtn = MakeButton(row.transform, "清空", Vector2.zero,
                                  Vector2.zero, new Color(0.34f, 0.15f, 0.17f), false);
        var ert = (RectTransform)eraseBtn.transform;
        ert.anchorMin = new Vector2(0.68f, 0.5f);
        ert.anchorMax = new Vector2(0.68f, 0.5f);
        ert.pivot = new Vector2(0f, 0.5f);
        ert.sizeDelta = new Vector2(96f, 48f);
        ert.anchoredPosition = new Vector2(132f, 0f);
        eraseBtn.onClick.AddListener(() => OnEraseSlot(captured));
    }

    private void BuildConfirmPanel()
    {
        _confirmPanel = new GameObject("Confirm", typeof(RectTransform), typeof(Image));
        _confirmPanel.transform.SetParent(_root.transform, false);
        var rt = (RectTransform)_confirmPanel.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _confirmPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(_confirmPanel.transform, false);
        var brt = (RectTransform)box.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(520f, 240f);
        brt.anchoredPosition = Vector2.zero;
        box.GetComponent<Image>().color = new Color(0.09f, 0.10f, 0.16f, 0.99f);

        _confirmText = MakeLabel(box.transform, "", new Vector2(0f, -24f),
                                 new Vector2(0f, 120f), 19, Color.white, true);

        var ok = MakeButton(box.transform, "确定", Vector2.zero, Vector2.zero,
                            new Color(0.16f, 0.32f, 0.20f), false);
        var okRt = (RectTransform)ok.transform;
        okRt.anchorMin = okRt.anchorMax = new Vector2(0.5f, 0f);
        okRt.pivot = new Vector2(1f, 0f);
        okRt.sizeDelta = new Vector2(160f, 48f);
        okRt.anchoredPosition = new Vector2(-12f, 24f);
        ok.onClick.AddListener(() =>
        {
            AudioManager.PlaySfx(AudioManager.SfxKey.Click);
            _confirmPanel.SetActive(false);
            _confirmAction?.Invoke();
            _confirmAction = null;
        });

        var no = MakeButton(box.transform, "取消", Vector2.zero, Vector2.zero,
                            new Color(0.22f, 0.22f, 0.26f), false);
        var noRt = (RectTransform)no.transform;
        noRt.anchorMin = noRt.anchorMax = new Vector2(0.5f, 0f);
        noRt.pivot = new Vector2(0f, 0f);
        noRt.sizeDelta = new Vector2(160f, 48f);
        noRt.anchoredPosition = new Vector2(12f, 24f);
        no.onClick.AddListener(() =>
        {
            AudioManager.PlaySfx(AudioManager.SfxKey.Click);
            _confirmPanel.SetActive(false);
            _confirmAction = null;
        });

        _confirmPanel.SetActive(false);
    }

    // ══════════════════════ 交互 ══════════════════════

    private void OnUseSlot(int slot)
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);

        if (slot == SaveSlotManager.CurrentSlot)
        {
            ToastManager.Show("<color=#9BE8FF>已经在使用这个存档了</color>");
            return;
        }

        var sm = SaveSlotManager.GetSummary(slot);
        string desc = sm.exists ? "该存档已有进度，将从上次的位置继续。" : "该存档是空的，将从零开始开荒。";
        AskConfirm($"切换到存档 {slot}？\n\n{desc}\n当前存档 {SaveSlotManager.CurrentSlot} 会被完整保留。",
            () =>
            {
                SaveSlotManager.SwitchTo(slot);
                ReloadTitleScene(slot);
            });
    }

    private void OnEraseSlot(int slot)
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);

        var sm = SaveSlotManager.GetSummary(slot);
        if (!sm.exists)
        {
            ToastManager.Show("<color=#999999>这个存档本来就是空的</color>");
            return;
        }

        AskConfirm($"清空存档 {slot}？\n\n该存档的全部进度（装备 / 通关 / 好感度 /\n继承装备 / 抽卡）都会被删除，无法恢复。\n其它存档不受影响。",
            () =>
            {
                bool isCurrent = slot == SaveSlotManager.CurrentSlot;
                SaveSlotManager.EraseSlot(slot);
                ToastManager.Show($"<color=#FF8080>存档 {slot} 已清空</color>");
                if (isCurrent) ReloadTitleScene(slot);
                else RefreshAll();
            });
    }

    private void AskConfirm(string msg, System.Action onOk)
    {
        if (_confirmPanel == null) { onOk?.Invoke(); return; }
        _confirmAction = onOk;
        if (_confirmText != null) _confirmText.text = msg;
        _confirmPanel.SetActive(true);
        _confirmPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// 切档后重载场景。
    /// 项目的主菜单与战斗在**同一个场景**（title.click_start 只是 SetActive 战斗根节点），
    /// 所以重载当前场景就等于回到干净的主菜单，顺带让所有读过 PlayerPrefs 的 UI
    /// （存档界面图标、皮肤、聚宝盆…）按新档重建 —— 比逐个手动刷新可靠得多。
    /// </summary>
    private void ReloadTitleScene(int slot)
    {
        ToastManager.Show($"<color=#80FF80>已切换到存档 {slot}</color>");
        Time.timeScale = 1f;
        Hide();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void RefreshAll()
    {
        for (int slot = 1; slot <= SaveSlotManager.SLOT_COUNT; slot++)
        {
            if (_slotLabels[slot] == null) continue;

            var sm = SaveSlotManager.GetSummary(slot);
            bool cur = slot == SaveSlotManager.CurrentSlot;
            string head = cur
                ? $"<color=#80FF80>存档 {slot}（使用中）</color>"
                : $"存档 {slot}";

            _slotLabels[slot].text = sm.exists
                ? $"{head}\n" +
                  $"<size=15>最高通关 N{sm.maxClearN}   已解锁装备 {sm.unlockedCount}   " +
                  $"塔 {sm.towerFloor} 层\n游戏时长 {sm.playMinutes} 分钟   源 {sm.yuan}</size>"
                : $"{head}\n<size=15><color=#999999>空存档 —— 可从零开始开荒</color></size>";
        }
    }

    // ══════════════════════ 小工具 ══════════════════════

    private Text MakeLabel(Transform parent, string text, Vector2 pos, Vector2 size,
                           int fontSize, Color color, bool topAnchored)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (topAnchored)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(16f, 0f);
            rt.offsetMax = new Vector2(-16f, 0f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, size.y);
            rt.anchoredPosition = pos;
        }

        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = _font;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.supportRichText = true;
        return t;
    }

    private Button MakeButton(Transform parent, string text, Vector2 pos, Vector2 size,
                              Color tint, bool topAnchored)
    {
        var go = new GameObject("Btn_" + text,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        if (!topAnchored && size != Vector2.zero)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }
        go.GetComponent<Image>().color = tint;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        var t = labelGo.GetComponent<Text>();
        t.text = text;
        t.font = _font;
        t.fontSize = 20;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        t.raycastTarget = false;

        return go.GetComponent<Button>();
    }
}
