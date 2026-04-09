using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Attribute : MonoBehaviour
{
    public string rolename;
    public int   health;
    public int   healthmax;
    public float atk;    // 攻击力（支持小数叠加）
    public float def;    // 防御力（支持小数叠加）
    public int   speed;
    public float CR;     // 暴击率（支持小数叠加）
    public float CD;     // 暴击伤害（支持小数叠加）
    public int   EVA;
    public float DR;     // 经验效率（支持小数叠加）
    public int   regen;  // 自然回血
    public int   exp;
    public int   expmax;
    public int   level;
}
