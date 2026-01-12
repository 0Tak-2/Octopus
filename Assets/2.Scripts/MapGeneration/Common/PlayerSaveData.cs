using System;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    [Header("Stats")]
    public int hp = 80;
    public int maxHP = 100;
    public int fatigue = 60;
    public int maxFatigue = 100;
    public int hunger = 70;
    public int maxHunger = 100;

    [Header("Dungeon")]
    public string currentDungeonId;
    public int returnToMapIndex;
}
