using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 奇遇9：风险抹杀
/// 效果：怪物血量翻倍，伤害减半
/// </summary>
public class AdventureOption9_RiskErasure : AdventureOptionBase
{
    private void Reset()
    {
        optionName        = "风险抹杀";
        optionDescription = "我说割草不如刮痧有没有懂的";
        effectDescription = "怪物血量翻倍，伤害减半";
        BindIconInEditor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        optionName        = "风险抹杀";
        optionDescription = "我说割草不如刮痧有没有懂的";
        effectDescription = "怪物血量翻倍，伤害减半";
        BindIconInEditor();
    }
#endif

    private void BindIconInEditor()
    {
#if UNITY_EDITOR
        const string iconPath = "Assets/prefab/奇遇/奇遇/9/9.风险抹杀.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        if (sprite != null && icon != sprite)
        {
            icon = sprite;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    public override void Execute()
    {
        // 1. 设置全局倍率以便后续生成的怪物
        enemy.adventureHpMultiplier = 2.0f;
        enemy.adventureAtkMultiplier = 0.5f;

        // 2. 将场上现有的所有怪物生命值翻倍，攻击力减半
        enemy[] activeEnemies = FindObjectsOfType<enemy>();
        foreach (enemy e in activeEnemies)
        {
            if (e != null && e.rolestate != enemy.state.dead)
            {
                e.healthmax = Mathf.RoundToInt(e.healthmax * 2.0f);
                e.health = Mathf.RoundToInt(e.health * 2.0f);
                e.atk = Mathf.RoundToInt(e.atk * 0.5f);
            }
        }

        ToastManager.Show("风险抹杀已生效！怪物生命值翻倍，伤害减半！");
        base.Execute();
    }
}
