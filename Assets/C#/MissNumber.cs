using TMPro;
using UnityEngine;

/// <summary>
/// 闪避飘字工具：统一显示青蓝色的 "Miss"。
///
/// 复用现有 atknumber 预制体（子物体带 TextMeshProUGUI），
/// 只把文本改为 "Miss"、颜色改为青蓝色即可。
///
/// 敌我闪避均调用此方法：
///   - 玩家闪避敌人：在玩家位置显示（用敌人的 atknumber 预制体即可，两侧使用同一份）
///   - 敌人闪避玩家子弹：在敌人位置显示（用敌人的 atknumber 预制体）
/// </summary>
public static class MissNumber
{
    // 青蓝色（cyan-blue）；与暴击金黄、普通白色/红色都区分明显
    private static readonly Color32 MissColor = new Color32(0, 200, 255, 255);

    /// <summary>
    /// 在世界坐标 pos 处实例化一个 Miss 飘字。
    /// atknumberPrefab 为空 / DamageNumberSettings.Visible 为 false 时不做任何事。
    /// </summary>
    public static void Show(GameObject atknumberPrefab, Vector3 pos)
    {
        if (!DamageNumberSettings.Visible) return;
        if (atknumberPrefab == null) return;

        GameObject number = Object.Instantiate(atknumberPrefab, pos, Quaternion.identity);
        number.transform.localScale *= DamageNumberSettings.SizeScale;
        // 兼容既有伤害数字预制体结构：TextMeshProUGUI 挂在第 0 个子物体
        if (number.transform.childCount > 0)
        {
            var txt = number.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = "Miss";
                txt.color = MissColor;
            }
        }
    }
}
