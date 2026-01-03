using UnityEngine;
using System.Collections.Generic;

public class PlayerInstance : MonoBehaviour
{
    [Header("Core HP (shared)")]
    public int maxHP = 10;
    public int currentHP = 10;

    [Header("Survival (optional)")]
    public int hunger = 0;
    public int fatigue = 0;

    [Header("Status/Buffs (optional)")]
    public List<string> statusTags = new List<string>();

    public void Clamp()
    {
        if (currentHP < 0) currentHP = 0;
        if (currentHP > maxHP) currentHP = maxHP;
    }
}
