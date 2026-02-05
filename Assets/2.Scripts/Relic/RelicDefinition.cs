using UnityEngine;

/// <summary>
/// Relic rarity
/// </summary>
public enum RelicRarity
{
    Common,     // Normal (gray)
    Rare,       // Rare (blue)
    Epic,       // Epic/Hero (purple)
    Legendary   // Legendary (gold)
}

/// <summary>
/// Relic definition ScriptableObject
/// Create: Assets > Create > Relic > Relic Definition
/// </summary>
[CreateAssetMenu(menuName = "Relic/Relic Definition", fileName = "NewRelic")]
public class RelicDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string relicID;
    public string relicName;
    
    [TextArea(2, 4)]
    public string description;
    
    public Sprite icon;
    
    [Header("Rarity")]
    public RelicRarity rarity = RelicRarity.Common;
    
    [Header("Stat Bonuses (per stack)")]
    [Tooltip("Max HP bonus per stack")]
    public int bonusMaxHP = 0;
    
    [Tooltip("ATK % bonus per stack (0.02 = 2%)")]
    [Range(0f, 0.5f)]
    public float bonusATKPercent = 0f;
    
    [Tooltip("DEF bonus per stack")]
    public int bonusDEF = 0;
    
    [Tooltip("EVA % bonus per stack (0.01 = 1%)")]
    [Range(0f, 0.3f)]
    public float bonusEVA = 0f;
    
    [Tooltip("CRIT % bonus per stack (0.01 = 1%)")]
    [Range(0f, 0.3f)]
    public float bonusCRIT = 0f;
    
    [Tooltip("CRIT DMG % bonus per stack (0.05 = 5%)")]
    [Range(0f, 0.5f)]
    public float bonusCRIT_DMG = 0f;
    
    [Header("Special Bonuses (per stack)")]
    [Tooltip("Max Fatigue bonus")]
    public int bonusMaxFatigue = 0;
    
    [Tooltip("Hunger consumption reduction % (0.05 = 5%)")]
    [Range(0f, 0.5f)]
    public float hungerReductionPercent = 0f;
    
    [Tooltip("Damage reduction % when condition met (0.05 = 5%)")]
    [Range(0f, 0.3f)]
    public float damageReductionPercent = 0f;
    
    [Header("Conditional Bonuses")]
    [Tooltip("HP % threshold for conditional effect (0.5 = 50%)")]
    [Range(0f, 1f)]
    public float conditionHPThreshold = 0f;
    
    [Tooltip("Fatigue % threshold for conditional effect (0.3 = 30%)")]
    [Range(0f, 1f)]
    public float conditionFatigueThreshold = 0f;
    
    [Tooltip("DEF bonus when HP above threshold")]
    public int conditionalDEFBonus = 0;
    
    /// <summary>
    /// Get scaled bonuses based on stack count
    /// Stack multiplier: 1, 2, 4, 8, 16, 32...
    /// </summary>
    public float GetStackMultiplier(int stackCount)
    {
        if (stackCount <= 0) return 0f;
        if (stackCount == 1) return 1f;
        
        // 1, 2, 4, 8, 16, 32 progression
        // Stack 1 = 1x, Stack 2 = 2x, Stack 3 = 4x, Stack 4 = 8x...
        return Mathf.Pow(2, stackCount - 1);
    }
    
    /// <summary>
    /// Calculate total MaxHP bonus for given stack count
    /// </summary>
    public int GetTotalMaxHPBonus(int stackCount)
    {
        return Mathf.RoundToInt(bonusMaxHP * GetStackMultiplier(stackCount));
    }
    
    /// <summary>
    /// Calculate total ATK% bonus for given stack count
    /// </summary>
    public float GetTotalATKPercent(int stackCount)
    {
        return bonusATKPercent * GetStackMultiplier(stackCount);
    }
    
    /// <summary>
    /// Calculate total DEF bonus for given stack count
    /// </summary>
    public int GetTotalDEFBonus(int stackCount)
    {
        return Mathf.RoundToInt(bonusDEF * GetStackMultiplier(stackCount));
    }
}
