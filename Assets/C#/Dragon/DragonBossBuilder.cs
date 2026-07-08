using UnityEngine;

/// <summary>
/// 纯代码构建最终龙王 Boss —— 无需 Prefab / 无需 Inspector 拖拽。
///
/// 装配：SpriteRenderer / Rigidbody / BoxCollider / DragonBoss；
/// 加载每形态 3 帧扑翼行走序列（dragon_X / _1 / _2）+ 5 个技能子弹精灵；
/// 从模板 Boss（蘑菇 bossPrefab）借用 atknumber / expstone / material / red 引用。
///
/// 顺序：GameObject 先 SetActive(false) → 装配组件+注入字段 → 再 SetActive(true)，
/// 确保 DragonBoss.OnEnable 时引用就绪。
/// </summary>
public static class DragonBossBuilder
{
    private const float PPU_BODY = 120f; // 龙身 1024px/120 ≈ 8.5 世界单位
    private const float PPU_PROJ = 256f; // 子弹 512px/256 ≈ 2 单位（实际由投射物 FitScale 归一化）

    private static readonly string[] PhaseBase =
    { "dragon_fire", "dragon_bat", "dragon_steel", "dragon_slime", "dragon_gold" };

    private static Sprite LoadBody(string name)
        => RuntimeAssetLoader.LoadSprite(null, "Dragon/" + name, "Resources/Dragon/" + name + ".png",
                                         null, new Vector2(0.5f, 0.5f), PPU_BODY);
    private static Sprite LoadProj(string name)
        => RuntimeAssetLoader.LoadSprite(null, "Dragon/" + name, "Resources/Dragon/" + name + ".png",
                                         null, new Vector2(0.5f, 0.5f), PPU_PROJ);

    public static DragonBoss Build(Vector3 pos, Transform parent, GameObject template, battleUI ui)
    {
        // 每形态 3 帧行走序列
        Sprite[][] frames = new Sprite[PhaseBase.Length][];
        for (int i = 0; i < PhaseBase.Length; i++)
        {
            frames[i] = new Sprite[3];
            frames[i][0] = LoadBody(PhaseBase[i]);
            frames[i][1] = LoadBody(PhaseBase[i] + "_1");
            frames[i][2] = LoadBody(PhaseBase[i] + "_2");
            if (frames[i][0] == null) Debug.LogWarning($"[DragonBoss] 形态精灵缺失：Resources/Dragon/{PhaseBase[i]}.png");
            if (frames[i][1] == null) frames[i][1] = frames[i][0]; // 帧缺失回退到基帧
            if (frames[i][2] == null) frames[i][2] = frames[i][0];
        }

        var go = new GameObject("DragonFinalBoss");
        go.SetActive(false);

        int enemyLayer = (template != null) ? template.layer : 6;
        go.layer = enemyLayer;
        try { go.tag = (template != null && !string.IsNullOrEmpty(template.tag)) ? template.tag : "enemy"; } catch { }

        var t = go.transform;
        t.position = pos;
        t.rotation = Quaternion.Euler(45f, 0f, 0f);
        if (parent != null) t.SetParent(parent, true);

        var sr = go.AddComponent<SpriteRenderer>();
        if (frames[0][0] != null) sr.sprite = frames[0][0];
        sr.sortingLayerID = 0; sr.sortingOrder = 5;
        sr.color = new Color(1f, 1f, 1f, 0f);
        // 关键：使用独立的无光照 Sprites/Default 材质，避免借用模板材质导致「发白/受光照冲淡」。
        var unlit = new Material(Shader.Find("Sprites/Default"));
        sr.material = unlit;

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation;

        var box = go.AddComponent<BoxCollider>();
        Vector2 spriteSize = frames[0][0] != null ? (Vector2)frames[0][0].bounds.size : new Vector2(8.5f, 8.5f);
        box.size = new Vector3(spriteSize.x * 0.5f, spriteSize.y * 0.62f, 2f);
        box.center = Vector3.zero; box.isTrigger = false;

        var dragon = go.AddComponent<DragonBoss>();
        dragon.phaseFrames    = frames;
        dragon.fireballSprite  = LoadProj("dragon_p_fireball");
        dragon.blueDartSprite  = LoadProj("dragon_p_bluedart");
        dragon.tornadoSprite   = LoadProj("dragon_p_tornado");
        dragon.slimeBlobSprite = LoadProj("dragon_p_slimeblob");
        dragon.goldScaleSprite = LoadProj("dragon_p_goldscale");
        // 火系补充素材：红火焰喷飞标 / 火焰吐息束 / 玩家灼烧红焰
        dragon.redDartSprite    = LoadProj("dragon_p_reddart");
        dragon.fireBreathSprite = LoadProj("dragon_p_firebreath");
        dragon.burnFlameSprite  = LoadProj("dragon_p_burnflame");
        // 蝙蝠血刃「毒镖」/ 黄金龙死亡全屏特效
        dragon.bloodBladeSprite = LoadProj("dragon_p_bloodblade");
        dragon.goldDeath1Sprite = LoadProj("dragon_gold_death1");
        dragon.goldDeath2Sprite = LoadProj("dragon_gold_death2");
        dragon.battleUI = ui;

        enemy tmpl = template != null ? template.GetComponent<enemy>() : null;
        if (tmpl != null)
        {
            dragon.atknumber = tmpl.atknumber;
            dragon.expstone  = tmpl.expstone;
        }
        else Debug.LogWarning("[DragonBoss] 模板 Boss 为空，atknumber/expstone 未注入");

        // 材质用龙王独立的无光照材质：normal=原色不受光照冲淡；red=红色 tint（命中短暂红闪后还原）。
        // 不再借用模板(蘑菇)材质——那会导致「发白」以及被持续攻击时「常驻红」。
        dragon.material = unlit;
        var redMat = new Material(Shader.Find("Sprites/Default"));
        redMat.color = new Color(1f, 0.72f, 0.72f, 1f); // 柔和暖色闪，保留龙身原色（持续受击也不会整只变纯红）
        dragon.red = redMat;

        go.SetActive(true);
        BossHealthBarUI.Register(dragon);
        Debug.Log("[Boss] 最终龙王 Boss 已构建（N13 关底）");
        return dragon;
    }
}
