using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Max Values")]
    public int baseMaxHP = 100;
    public int maxFatigue = 100;
    public int maxHunger = 100;
    
    [Header("Max HP Bonuses")]
    public int equipmentMaxHPBonus = 0;  // 장비에서 오는 보너스
    public int relicMaxHPBonus = 0;      // 유물에서 오는 보너스
    
    /// <summary>
    /// 최종 최대 HP (기본 + 장비 + 유물)
    /// </summary>
    public int maxHP => baseMaxHP + equipmentMaxHPBonus + relicMaxHPBonus;

    [Header("Current Values")]
    public int hp = 100;
    /// <summary>데미지 받을 때 발생 (최종 데미지 값 전달)</summary>
    public event System.Action<int> OnDamageTaken;
    public int fatigue = 100;
    public int hunger = 100;

    // ============================================
    // 전투 스탯 (Phase 1)
    // ============================================
    [Header("Combat Stats - Base")]
    [Tooltip("기본 공격력")]
    public int baseATK = 10;
    
    [Tooltip("기본 방어력")]
    public int baseDEF = 0;
    
    [Tooltip("기본 회피율 (0~1, 5% = 0.05)")]
    [Range(0f, 1f)]
    public float baseEVA = 0.05f;
    
    [Tooltip("기본 치명타 확률 (0~1, 5% = 0.05)")]
    [Range(0f, 1f)]
    public float baseCRIT = 0.05f;
    
    [Tooltip("기본 치명타 피해량 (1.5 = 150%)")]
    [Min(1f)]
    public float baseCRIT_DMG = 1.5f;

    [Header("Combat Stats - Bonuses (from equipment, relics, etc.)")]
    public int bonusATK = 0;
    public int bonusDEF = 0;
    public float bonusEVA = 0f;
    public float bonusCRIT = 0f;
    public float bonusCRIT_DMG = 0f;
    
    [Header("Combat Stats - Color Module Bonuses")]
    public float colorModuleATKPercent = 0f;      // 빨강 중첩 버프
    public float colorModuleEVAPercent = 0f;      // 파랑 중첩 버프
    public float colorModuleCRIT_DMGPercent = 0f; // 검정 중첩 버프
    public float colorModuleEVAPenalty = 0f;      // 빨강 중첩 디버프
    public float colorModuleHealPenalty = 0f;     // 검정 중첩 디버프 (회복 효율)

    // ============================================
    // 최종 스탯 계산 (읽기 전용)
    // ============================================
    public int ATK => Mathf.Max(1, Mathf.RoundToInt((baseATK + bonusATK) * (1f + colorModuleATKPercent)));
    public int DEF => Mathf.Max(0, baseDEF + bonusDEF);
    public float EVA => Mathf.Clamp01(baseEVA + bonusEVA + colorModuleEVAPercent - colorModuleEVAPenalty);
    public float CRIT => Mathf.Clamp01(baseCRIT + bonusCRIT);
    public float CRIT_DMG => Mathf.Max(1f, baseCRIT_DMG + bonusCRIT_DMG + colorModuleCRIT_DMGPercent);
    
    /// <summary>
    /// 회복 효율 (검정 중첩 디버프 적용)
    /// </summary>
    public float HealEfficiency => Mathf.Clamp01(1f - colorModuleHealPenalty);

    [Header("Periodic Drain (per 5 Time)")]
    public int drainIntervalTime = 5;
    public int fatigueDrainPerInterval = 1;
    public int hungerDrainPerInterval = 2;

    private FieldTimeManager _fieldTime;
    private int _nextDrainAtTime = 5;

    private void Awake()
    {
        ClampAll();
    }

    private void Start()
    {
        _fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (_fieldTime != null)
        {
            int t = _fieldTime.time;
            _nextDrainAtTime = Mathf.Max(drainIntervalTime, ((t / drainIntervalTime) + 1) * drainIntervalTime);

            _fieldTime.OnTimeAdvanced += HandleTimeAdvanced;
        }
    }

    private void OnDestroy()
    {
        if (_fieldTime != null)
            _fieldTime.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private void HandleTimeAdvanced(int delta, int newTotalTime)
    {
        while (newTotalTime >= _nextDrainAtTime)
        {
            fatigue -= Mathf.Max(0, fatigueDrainPerInterval);
            hunger -= Mathf.Max(0, hungerDrainPerInterval);
            ClampAll();

            _nextDrainAtTime += drainIntervalTime;
        }
    }

    public void ClampAll()
    {
        hp = Mathf.Clamp(hp, 0, maxHP);
        fatigue = Mathf.Clamp(fatigue, 0, maxFatigue);
        hunger = Mathf.Clamp(hunger, 0, maxHunger);
    }

    /// <summary>
    /// 휴식 시 피로/체력 회복
    /// </summary>
    public void RestRecover(int fatigueGainPerTick)
    {
        int gain = Mathf.Max(0, fatigueGainPerTick);

        int fatigueBefore = fatigue;
        fatigue = Mathf.Min(maxFatigue, fatigue + gain);

        int actualFatigueGained = fatigue - fatigueBefore;
        if (actualFatigueGained > 0)
        {
            hp = Mathf.Min(maxHP, hp + actualFatigueGained);
        }

        ClampAll();
    }

    /// <summary>
    /// 데미지를 받음 (방어력 적용)
    /// </summary>
    public void TakeDamage(int rawDamage)
    {
        // 방어력 계산: 받는 피해 = 원본 피해 - DEF (최소 1)
        int finalDamage = Mathf.Max(1, rawDamage - DEF);
        
        hp -= finalDamage;
        ClampAll();

        // ✅ 피격 효과 추가
        HitEffectManager hitEffect = HitEffectManager.Instance ?? FindObjectOfType<HitEffectManager>();
        if (hitEffect != null)
        {
            hitEffect.ShowHitEffect(transform, finalDamage);
        }

        Debug.Log($"[PlayerStats] Took {finalDamage} damage (raw: {rawDamage}, DEF: {DEF}). HP: {hp}/{maxHP}");
        OnDamageTaken?.Invoke(finalDamage);

    }
    
    /// <summary>
    /// 데미지를 받음 (방어력 무시 - 출혈 등)
    /// </summary>
    public void TakeDamageRaw(int damage)
    {
        int finalDamage = Mathf.Max(0, damage);
        hp -= finalDamage;
        ClampAll();
        
        Debug.Log($"[PlayerStats] Took {finalDamage} raw damage. HP: {hp}/{maxHP}");
        OnDamageTaken?.Invoke(finalDamage);
    }
    
    /// <summary>
    /// HP 회복 (회복 효율 적용)
    /// </summary>
    public void Heal(int amount)
    {
        int effectiveHeal = Mathf.RoundToInt(amount * HealEfficiency);
        hp = Mathf.Min(maxHP, hp + effectiveHeal);
        ClampAll();
        
        Debug.Log($"[PlayerStats] Healed {effectiveHeal} (base: {amount}, efficiency: {HealEfficiency:P0}). HP: {hp}/{maxHP}");
    }
    
    /// <summary>
    /// HP 회복 (회복 효율 무시)
    /// </summary>
    public void HealRaw(int amount)
    {
        hp = Mathf.Min(maxHP, hp + Mathf.Max(0, amount));
        ClampAll();
    }

    public bool IsStarving => hunger <= 0;
    public bool IsExhausted => fatigue <= 0;
}