using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class title : MonoBehaviour
{
    public Transform enemylayer;
    public GameObject fightscene;
    public GameObject savescene;
    public GameObject choiceUI;
    public GameObject campPrefab;           // 营地 prefab
    public Transform playerlayer;           // 用于获取玩家位置
    [SerializeField] private float campSpawnRadius = 20f;
    [SerializeField] private float campMinDistance = 8f;
    public GameObject difficultySelectUI;   // 难度选择面板
    public battleUI battleUI;               // 战斗UI引用

    private void OnEnable()
    {
        savescene.SetActive(false);
        fightscene.SetActive(false);
        if (choiceUI != null) choiceUI.SetActive(false);
        closegacha(); // 进入游戏/返回标题时关闭抽卡界面
        closeskin();  // 同时也关闭换装面板
        SetDeleteArchiveButtonVisible(true);
        Time.timeScale = 0f;
        // 主菜单 BGM
        AudioManager.PlayBgm(AudioManager.BgmKey.Main);

        // 自动给主菜单子面板挂右键关闭
        SetupRightClickClose();

        // 运行时确保换装系统已挂载
        EnsureSkinChangerMounted();

        // 测试模式按钮（左上角，玩家点击切换 god-mode 状态）
        EnsureTestModeButton();

        // 内测福利按钮（左上角，测试模式按钮下方）
        EnsureBetaRewardButton();

        // 测试按钮：一键把所有副本时间设为 1 分钟
        EnsureOneMinuteButton();
    }

    private void EnsureSkinChangerMounted()
    {
        // 优先挂在 gachaPanel 所在的 Canvas 上（与抽卡界面同 Canvas）
        GameObject mountTarget = null;
        if (gachaPanel != null && gachaPanel.transform.parent != null)
            mountTarget = gachaPanel.transform.parent.gameObject;
        else if (skinShowroomPanel != null && skinShowroomPanel.transform.parent != null)
            mountTarget = skinShowroomPanel.transform.parent.gameObject;

        if (mountTarget == null)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas != null) mountTarget = canvas.gameObject;
        }

        if (mountTarget != null && mountTarget.GetComponent<SkinChanger>() == null)
        {
            mountTarget.AddComponent<SkinChanger>();
            Debug.Log($"[换装] 已自动为 {mountTarget.name} 挂载 SkinChanger 系统！");
        }
    }

    private void SetupRightClickClose()
    {
        if (savescene != null)
        {
            var p = RightClickClosePanel.EnsureOn(savescene);
            p.onRightClickClose = new UnityEngine.Events.UnityEvent();
            p.onRightClickClose.AddListener(closesave);
        }
        if (gachaPanel != null)
        {
            var p = RightClickClosePanel.EnsureOn(gachaPanel);
            p.onRightClickClose = new UnityEngine.Events.UnityEvent();
            p.onRightClickClose.AddListener(closegacha);
        }
        if (skinShowroomPanel != null)
        {
            var p = RightClickClosePanel.EnsureOn(skinShowroomPanel);
            p.onRightClickClose = new UnityEngine.Events.UnityEvent();
            p.onRightClickClose.AddListener(closeskin);
        }
        // difficultySelectUI 已自带 Update 监听右键，无需重复
    }

    /// <summary>点击"开始游戏"按钮 → 弹出难度选择面板</summary>
    public void click_start_button()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
        if (difficultySelectUI != null)
        {
            difficultySelectUI.SetActive(true);
        }
        else
        {
            // 没有配置难度面板时直接开始
            click_start();
        }
    }

    /// <summary>由 DifficultySelectUI 在确认后调用</summary>
    public void click_start()
    {
        // 战斗 BGM
        AudioManager.PlayBgm(AudioManager.BgmKey.Battle);
        // 重置每局静态状态，避免上一局残留
        getexp.triggerMultiplier = 1;
        if (enemylayer.childCount > 0)
        {
            foreach (Transform enemy in enemylayer)
                Destroy(enemy.gameObject);
        }
        if (choiceUI != null) choiceUI.SetActive(false);
        Time.timeScale = 1.0f;
        fightscene.SetActive(true);
        gameObject.SetActive(false);

        // 生成5个营地（N1/N2不生成）
        string diffLabel = DifficultyManager.Instance != null ? DifficultyManager.Instance.Current.label : "N3";
        Debug.Log($"[title] click_start 难度={diffLabel}，生成营地={diffLabel != "N1" && diffLabel != "N2"}");
        bool spawnCamp = diffLabel != "N1" && diffLabel != "N2";

        if (campPrefab != null && spawnCamp)
        {
            Vector3 playerPos = playerlayer != null && playerlayer.childCount > 0
                ? playerlayer.GetChild(0).position
                : Vector3.zero;

            // 基础 5 座；SSR「便携营地」：每局开局额外 +2 座可攻占营地
            int campTarget = 5;
            if (EquipmentSystem.Instance != null &&
                EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.GachaEquipment, 0))
                campTarget += 2;

            float minDistance = campMinDistance;
            float spawnRadius = campSpawnRadius;
            if (EquipmentSystem.Instance != null &&
                EquipmentSystem.Instance.IsEquipmentUnlocked(EquipmentType.AchievementEquipment, 5))
            {
                minDistance = 6f;
                spawnRadius = 15f;
            }

            int spawned = 0;
            int attempts = 0;
            while (spawned < campTarget && attempts < 100)
            {
                attempts++;
                float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float dist  = UnityEngine.Random.Range(minDistance, spawnRadius);
                float x = playerPos.x + Mathf.Cos(angle) * dist;
                float z = playerPos.z + Mathf.Sin(angle) * dist;

                // 从高处向下 Raycast，贴合地表
                Vector3 rayOrigin = new Vector3(x, 50f, z);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 200f))
                {
                    Vector3 pos = new Vector3(x, hit.point.y + 1.5f, z);
                    Instantiate(campPrefab, pos, Quaternion.Euler(45, 0, 0), enemylayer);
                    spawned++;
                }
            }

            if (campTarget > 5)
                ToastManager.Show($"[抽卡·SSR] 便携营地：本局共 {campTarget} 座可攻占营地（基础5+额外2）");
        }
    }

    public GameObject gachaPanel;   // 抽奖面板
    public GameObject deleteArchiveButton; // 主页面固定显示的删除存档按钮
    public GameObject skinShowroomPanel;   // 外观橱窗面板

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gachaPanel != null && gachaPanel.transform.parent != null)
        {
            var canvasGo = gachaPanel.transform.parent.gameObject;
            if (canvasGo != null && canvasGo.GetComponent<SkinChanger>() == null)
            {
                canvasGo.AddComponent<SkinChanger>();
                Debug.Log($"[换装] 已自动为 {canvasGo.name} 挂载 SkinChanger 系统！");
            }
        }
    }
#endif

    private void SetDeleteArchiveButtonVisible(bool visible)
    {
        if (deleteArchiveButton != null)
            deleteArchiveButton.SetActive(visible);
    }

    public void openskin()
    {
        if (skinShowroomPanel != null) skinShowroomPanel.SetActive(true);
        SetDeleteArchiveButtonVisible(false);
    }

    public void closeskin()
    {
        if (skinShowroomPanel != null) skinShowroomPanel.SetActive(false);
        if ((gachaPanel == null || !gachaPanel.activeSelf) && (savescene == null || !savescene.activeSelf))
            SetDeleteArchiveButtonVisible(true);
    }

    public void opensave()
    {
        savescene.SetActive(true);
        SetDeleteArchiveButtonVisible(false);
        closeskin();
    }

    public void closesave()
    {
        savescene?.SetActive(false);
        if ((gachaPanel == null || !gachaPanel.activeSelf) && (skinShowroomPanel == null || !skinShowroomPanel.activeSelf))
            SetDeleteArchiveButtonVisible(true);
    }

    public void opengacha()
    {
        if (gachaPanel != null) gachaPanel.SetActive(true);
        SetDeleteArchiveButtonVisible(false);
        closeskin();
    }

    public void closegacha()
    {
        if (gachaPanel != null) gachaPanel.SetActive(false);
        if ((savescene == null || !savescene.activeSelf) && (skinShowroomPanel == null || !skinShowroomPanel.activeSelf))
            SetDeleteArchiveButtonVisible(true);
    }

    public void exitgame()
    {
        Application.Quit();
    }

    // ───────────────────────── 测试模式按钮（god-mode）─────────────────────────
    //   2026-06 新增：主菜单左上角的"测试模式"按钮——点击切换 TestModeManager.Enabled，
    //   开启后下一次进战斗时 EquipmentInitializer.Start 末尾会把玩家 hp/atk 强制拉到 99999。
    //   不动 SampleScene.unity（25MB YAML，编辑风险大），按钮在 OnEnable 用代码动态创建
    //   到主菜单 Canvas 上，状态变化时立即刷新文字。
    private GameObject _testModeBtnGo;
    private Text       _testModeBtnLabel;

    private void EnsureTestModeButton()
    {
        // 1) 兜底创建 TestModeManager 单例（场景没挂组件也能跑）
        TestModeManager.EnsureInstance();

        // 2) 已经创建过按钮 → 仅刷新文字
        if (_testModeBtnGo != null)
        {
            RefreshTestModeButtonLabel();
            _testModeBtnGo.SetActive(true);
            return;
        }

        // 3) 找一个挂载 Canvas（优先用主菜单页面已有的 Canvas，与其他按钮同层）
        Canvas hostCanvas = FindMainMenuCanvas();
        if (hostCanvas == null)
        {
            Debug.LogWarning("[TestMode] 找不到主菜单 Canvas，按钮无法创建");
            return;
        }

        // 4) 创建 Button GameObject
        var btnGo = new GameObject("__TestModeButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(hostCanvas.transform, false);
        var rt = btnGo.GetComponent<RectTransform>();
        // 锚定左上角，距离边缘 20px，尺寸 220×60
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -20f);
        rt.sizeDelta = new Vector2(220f, 60f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.12f, 0.12f, 0.18f, 0.85f); // 半透明深色背景

        var btn = btnGo.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(0.85f, 0.85f, 1f, 1f);
        colors.pressedColor     = new Color(0.7f, 0.7f, 1f, 1f);
        btn.colors = colors;

        // 5) Label（用普通 Text，避免依赖 TMP 字体资源；字体使用内置 Arial 兜底）
        var labelGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(btnGo.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize  = 22;
        label.color     = Color.white;
        // 字体：优先用 Unity 内置 LegacyRuntime（Unity 2022+），不行再退回 Arial
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) label.font = font;

        _testModeBtnGo    = btnGo;
        _testModeBtnLabel = label;

        // 6) 点击事件
        btn.onClick.AddListener(OnTestModeButtonClick);

        RefreshTestModeButtonLabel();
    }

    private void OnTestModeButtonClick()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
        var mgr = TestModeManager.EnsureInstance();
        mgr.Toggle();
        RefreshTestModeButtonLabel();

        // 测试模式开启时，同时解锁所有难度（通过 ClearRecordManager 正规途径写入）
        if (mgr.Enabled)
        {
            // 将 N1~N12 的通关次数都设为 1，使 N2~N13 全部解锁
            if (ClearRecordManager.Instance != null)
            {
                for (int i = 1; i <= 12; i++)
                    ClearRecordManager.Instance.SetClearCount("N" + i, 1);
                PlayerPrefs.Save();
                Debug.Log("[测试模式] 已通过 ClearRecordManager 解锁 N1~N12");
            }
            else
            {
                // 兜底：直接写 PlayerPrefs
                for (int i = 1; i <= 12; i++)
                    PlayerPrefs.SetInt("ClearCount_N" + i, 1);
                PlayerPrefs.Save();
            }

            // 如果难度选择面板已打开，强制刷新（重新执行 OnEnable）
            if (difficultySelectUI != null && difficultySelectUI.activeInHierarchy)
            {
                difficultySelectUI.SetActive(false);
                difficultySelectUI.SetActive(true);
            }

            ToastManager.Show("[测试模式] 已开启：玩家 HP/ATK=99999，所有难度已解锁");
        }
        else
        {
            ToastManager.Show("[测试模式] 已关闭：玩家数值恢复正常（难度解锁状态保留）");
        }
    }

    private void RefreshTestModeButtonLabel()
    {
        if (_testModeBtnLabel == null) return;
        bool on = TestModeManager.Instance != null && TestModeManager.Instance.Enabled;
        _testModeBtnLabel.text  = on ? "测试模式：开" : "测试模式：关";
        _testModeBtnLabel.color = on ? new Color(1f, 0.85f, 0.3f) : Color.white;
    }

    // ───────────────────────── 内测福利按钮 ─────────────────────────
    //   点击一次给 10 源，可以无限点。
    //   每连续点击 10 次弹出一次"魔了？"的提示。
    private GameObject _betaRewardBtnGo;
    private int        _betaRewardClickCount;

    private void EnsureBetaRewardButton()
    {
        if (_betaRewardBtnGo != null)
        {
            _betaRewardBtnGo.SetActive(true);
            return;
        }

        Canvas hostCanvas = FindMainMenuCanvas();
        if (hostCanvas == null) return;

        // 创建按钮（位于测试模式按钮正下方：y = -20 - 60 - 10 = -90）
        var btnGo = new GameObject("__BetaRewardButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(hostCanvas.transform, false);
        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -90f); // 测试模式按钮下方
        rt.sizeDelta = new Vector2(220f, 60f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.18f, 0.08f, 0.28f, 0.9f); // 紫色调，区分测试模式按钮

        var btn = btnGo.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.85f, 1f, 1f);
        colors.pressedColor     = new Color(0.9f, 0.7f, 1f, 1f);
        btn.colors = colors;

        // Label
        var labelGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(btnGo.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<Text>();
        label.text      = "内测福利";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize  = 24;
        label.color     = new Color(1f, 0.9f, 0.5f); // 金色文字
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) label.font = font;

        btn.onClick.AddListener(OnBetaRewardButtonClick);
        _betaRewardBtnGo = btnGo;
        _betaRewardClickCount = 0;
    }

    private void OnBetaRewardButtonClick()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);

        // 给 10 源
        GachaManager.Instance?.AddYuan(10);
        _betaRewardClickCount++;

        // 每 10 次弹出"魔了？"提示
        if (_betaRewardClickCount % 10 == 0)
        {
            ToastManager.Show("魔了？");
        }
        else
        {
            ToastManager.Show($"获得 10 源！（已领 {_betaRewardClickCount} 次）");
        }
    }

    // ───────────────────────── 测试：副本时间设为 1 分钟 ─────────────────────────
    private GameObject _oneMinBtnGo;

    private void EnsureOneMinuteButton()
    {
        if (_oneMinBtnGo != null) { _oneMinBtnGo.SetActive(true); return; }

        Canvas hostCanvas = FindMainMenuCanvas();
        if (hostCanvas == null) return;

        // 位于内测福利按钮下方：y = -90 - 60 - 10 = -160
        var btnGo = new GameObject("__OneMinuteButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(hostCanvas.transform, false);
        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(20f, -160f);
        rt.sizeDelta = new Vector2(220f, 60f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.28f, 0.08f, 0.10f, 0.9f); // 深红调，区分其它测试按钮

        var btn = btnGo.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor      = new Color(1f, 1f, 1f, 1f);
        colors.highlightedColor = new Color(1f, 0.8f, 0.8f, 1f);
        colors.pressedColor     = new Color(1f, 0.6f, 0.6f, 1f);
        btn.colors = colors;

        var labelGo = new GameObject("Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(btnGo.transform, false);
        var lrt = labelGo.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
        var label = labelGo.GetComponent<Text>();
        label.text      = "副本时间=1分钟";
        label.alignment = TextAnchor.MiddleCenter;
        label.fontSize  = 20;
        label.color     = new Color(1f, 0.85f, 0.5f);
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font != null) label.font = font;

        btn.onClick.AddListener(OnOneMinuteButtonClick);
        _oneMinBtnGo = btnGo;
    }

    private void OnOneMinuteButtonClick()
    {
        AudioManager.PlaySfx(AudioManager.SfxKey.Click);
        if (DifficultyManager.Instance != null && DifficultyManager.Instance.configs != null)
        {
            var cfgs = DifficultyManager.Instance.configs;
            for (int i = 0; i < cfgs.Length; i++)
                cfgs[i].minutes = 1;
            ToastManager.Show("[测试] 所有副本时间已设为 1 分钟");
        }
        else
        {
            ToastManager.Show("[测试] 未找到 DifficultyManager");
        }
    }

    /// <summary>找一个挂主菜单按钮的 Canvas。优先用现有按钮的父级（保持视觉一致性）。</summary>
    private Canvas FindMainMenuCanvas()
    {
        // 优先用 deleteArchiveButton 的父 Canvas（它在主菜单上一直显示，最稳）
        if (deleteArchiveButton != null)
        {
            var c = deleteArchiveButton.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }
        // 退回到场景里第一个启用且不在 fightscene/savescene/gachaPanel 下的 Canvas
        var all = FindObjectsOfType<Canvas>();
        foreach (var c in all)
        {
            if (c == null || !c.isActiveAndEnabled) continue;
            // 跳过明显不属于主菜单的 Canvas
            if (fightscene != null && c.transform.IsChildOf(fightscene.transform)) continue;
            if (savescene != null && c.transform.IsChildOf(savescene.transform)) continue;
            if (gachaPanel != null && c.transform.IsChildOf(gachaPanel.transform)) continue;
            return c;
        }
        return null;
    }
}
