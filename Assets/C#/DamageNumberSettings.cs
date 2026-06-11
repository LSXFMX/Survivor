using UnityEngine;

/// <summary>
/// 伤害飘字显示开关：全局静态状态 + PlayerPrefs 持久化。
/// 关闭后所有由玩家造成的伤害与敌人造成的伤害都不会再实例化飘字 GameObject，
/// 仅保留实际伤害计算。
/// </summary>
public static class DamageNumberSettings
{
    private const string KEY = "DamageNumber.Visible";
    private static bool _visible;
    private static bool _loaded;

    public static bool Visible
    {
        get
        {
            if (!_loaded)
            {
                _visible = PlayerPrefs.GetInt(KEY, 1) != 0;
                _loaded = true;
            }
            return _visible;
        }
        set
        {
            _visible = value;
            _loaded = true;
            PlayerPrefs.SetInt(KEY, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
