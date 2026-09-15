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
    // ���� ���� relicName, description �Ʒ��� �߰� ����
    [Header("Localization Keys")]
    public string nameKey;   // ��: "RELIC_NAME_CRACKED_SHELL"
    public string descKey;   // ��: "RELIC_DESC_CRACKED_SHELL"

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
    
    // ============================================
    // 중첩 성장 곡선
    // ============================================
    // 유물은 "미미한 효과가 쌓여 결국 체감된다"가 설계 의도다.
    // 그래서 중첩은 단계마다 수익이 체감하고, 최대 단계가 있다.
    // 상한이 없으면 후반에 초반 챕터가 무의미해지고, 체감이 선형이면 초반에 아무것도 느껴지지 않는다.
    //
    // 증가폭:  1단계 +1.00, 2단계 +0.70, 3단계 +0.49, 4단계 +0.34, 5단계 +0.24
    // 누적 배율: 1.00 → 1.70 → 2.19 → 2.53 → 2.77 (상한)

    // 수치는 RelicManager(씬 컴포넌트)에서 조절한다. 유물마다 다를 값이 아니라
    // 게임 전체의 성장 곡선이므로 한 곳에서 관리하는 게 맞다.
    // RelicManager 가 없을 때(에디터 프리뷰 등)는 아래 기본값을 쓴다.
    public const int DefaultMaxStackLevel = 5;
    public const float DefaultStackFalloff = 0.7f;

    /// <summary>중첩 최대 단계. 이 이상 얻어도 효과는 더 오르지 않는다.</summary>
    public static int MaxStackLevel =>
        RelicManager.Instance != null
            ? Mathf.Max(1, RelicManager.Instance.maxStackLevel)
            : DefaultMaxStackLevel;

    /// <summary>단계별 증가폭 감쇠율. 0.7 = 다음 단계는 직전 증가폭의 70%.</summary>
    public static float StackFalloff =>
        RelicManager.Instance != null
            ? Mathf.Clamp(RelicManager.Instance.stackFalloff, 0.1f, 1f)
            : DefaultStackFalloff;

    /// <summary>
    /// Get scaled bonuses based on stack count.
    /// 수익 체감 + 상한. 자세한 곡선은 위 주석 참고.
    /// </summary>
    public float GetStackMultiplier(int stackCount)
    {
        if (stackCount <= 0) return 0f;

        int levels = Mathf.Min(stackCount, MaxStackLevel);

        float total = 0f;
        float increment = 1f;
        for (int i = 0; i < levels; i++)
        {
            total += increment;
            increment *= StackFalloff;
        }

        return total;
    }

    /// <summary>중첩이 상한에 닿았는가. 이후 획득분은 다른 보상으로 돌려야 한다.</summary>
    public static bool IsStackMaxed(int stackCount) => stackCount >= MaxStackLevel;
    
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
