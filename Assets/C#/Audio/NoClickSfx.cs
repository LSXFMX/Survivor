using UnityEngine;

/// <summary>
/// Marker 组件：挂在某个 Button 所在 GameObject 上，<see cref="ButtonClickSfxAutoBinder"/> 将跳过它，不为其追加点击音效。
/// 用于：本身有自己专属音效的特殊按钮（例如抽奖按钮，将来若想换音效）。
/// </summary>
public class NoClickSfx : MonoBehaviour { }
