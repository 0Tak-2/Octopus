using UnityEngine;

/// <summary>
/// 전투 데미지 계산 결과
/// </summary>
public struct DamageResult
{
    public int finalDamage;
    public bool isCritical;
    public bool isEvaded;
    public int baseDamage;
    public int bonusDamage;      // 스킬 추가 피해
    public int imperfectBonus;   // 불완전 추가 피해
    
    public override string ToString()
    {
        if (isEvaded) return "회피!";
        string crit = isCritical ? " (치명타!)" : "";
        return $"{finalDamage}{crit}";
    }
}

/// <summary>
/// 전투 데미지 계산 유틸리티
/// 
/// 전투 공식:
/// - 최종 피해 = (기본 공격력 + 스킬 추가 피해) × 치명타 배율 - 방어력
/// - 최소 피해 = 1
/// - 회피 판정: 공격 전 EVA% 확률로 회피 (피해 0)
/// - 치명타 판정: CRIT% 확률로 CRIT_DMG% 배율 적용
/// - 방어력 계산: 받는 피해 = 원본 피해 - DEF (최소 1)
/// </summary>
public static class CombatCalculator
{
    /// <summary>
    /// 플레이어 → 적 공격 계산
    /// </summary>
    /// <param name="playerStats">플레이어 스탯</param>
    /// <param name="targetStatusEffects">타겟의 상태이상 매니저 (불완전 소비용)</param>
    /// <param name="skillBonusDamage">스킬 추가 피해</param>
    /// <param name="targetDEF">적 방어력</param>
    /// <param name="targetEVA">적 회피율</param>
    /// <param name="forceCrit">확정 치명타 (검정 스킬)</param>
    /// <param name="attackMultiplier">공격력 배율 (응징하는촉수 등)</param>
    /// <returns>계산 결과</returns>
    public static DamageResult CalculatePlayerAttack(
        PlayerStats playerStats,
        StatusEffectManager targetStatusEffects = null,
        int skillBonusDamage = 0,
        int targetDEF = 0,
        float targetEVA = 0f,
        bool forceCrit = false,
        float attackMultiplier = 1f,
        float targetDamageMultiplier = 1f)
    {
        var result = new DamageResult();
        
        // 1. 회피 판정
        if (targetEVA > 0f && Random.value < targetEVA)
        {
            result.isEvaded = true;
            result.finalDamage = 0;
            return result;
        }
        
        // 2. 기본 공격력 계산
        result.baseDamage = Mathf.RoundToInt(playerStats.ATK * attackMultiplier);
        result.bonusDamage = skillBonusDamage;
        
        // 3. 불완전 추가 피해 (타겟에 불완전 상태이상이 있으면)
        if (targetStatusEffects != null)
        {
            result.imperfectBonus = targetStatusEffects.ConsumeImperfect(playerStats.ATK);
        }
        
        // 4. 치명타 판정
        result.isCritical = forceCrit || Random.value < playerStats.CRIT;
        float critMultiplier = result.isCritical ? playerStats.CRIT_DMG : 1f;
        
        // 5. 최종 피해 계산
        float rawDamage = (result.baseDamage + result.bonusDamage + result.imperfectBonus) * critMultiplier;
        rawDamage *= Mathf.Max(0f, targetDamageMultiplier);
        int afterDef = Mathf.RoundToInt(rawDamage) - targetDEF;
        
        // 최소 1 피해
        result.finalDamage = Mathf.Max(1, afterDef);
        
        return result;
    }
    
    /// <summary>
    /// 적 → 플레이어 공격 계산
    /// </summary>
    /// <param name="enemyATK">적 공격력</param>
    /// <param name="playerStats">플레이어 스탯</param>
    /// <param name="playerStatusEffects">플레이어 상태이상 (불완전 소비용)</param>
    /// <returns>계산 결과</returns>
    public static DamageResult CalculateEnemyAttack(
        int enemyATK,
        PlayerStats playerStats,
        StatusEffectManager playerStatusEffects = null)
    {
        var result = new DamageResult();
        
        // 1. 회피 판정 (플레이어 EVA)
        if (playerStats.EVA > 0f && Random.value < playerStats.EVA)
        {
            result.isEvaded = true;
            result.finalDamage = 0;
            return result;
        }
        
        // 2. 기본 공격력
        result.baseDamage = enemyATK;
        
        // 3. 불완전 추가 피해 (플레이어에 불완전이 있으면)
        if (playerStatusEffects != null)
        {
            result.imperfectBonus = playerStatusEffects.ConsumeImperfect(enemyATK);
        }
        
        // 4. 방어력 계산
        int rawDamage = result.baseDamage + result.imperfectBonus;
        rawDamage = Mathf.RoundToInt(rawDamage * (1f - Mathf.Clamp01(playerStats.EngraveDamageTakenReduction)));
        int afterDef = rawDamage - playerStats.DEF;
        
        // 최소 1 피해
        result.finalDamage = Mathf.Max(1, afterDef);
        
        return result;
    }
    
    /// <summary>
    /// 단순 피해 계산 (스탯 없이)
    /// </summary>
    public static int CalculateSimpleDamage(int baseDamage, int defense)
    {
        return Mathf.Max(1, baseDamage - defense);
    }
    
    /// <summary>
    /// 출혈로 인한 이미 출혈 상태 적에게 추가 피해 계산 (빨강 스킬)
    /// </summary>
    public static int CalculateBleedingBonusDamage(int baseATK, float percent)
    {
        return Mathf.RoundToInt(baseATK * percent);
    }
    
    /// <summary>
    /// 포식 (빨강 패시브) - 적 HP 20% 이하 시 처형 + 회복
    /// </summary>
    public static bool CheckExecuteThreshold(int currentHP, int maxHP, float threshold = 0.2f)
    {
        return (float)currentHP / maxHP <= threshold;
    }
}
