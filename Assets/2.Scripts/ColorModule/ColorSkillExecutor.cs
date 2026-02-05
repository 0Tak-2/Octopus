using UnityEngine;
using System.Collections;

/// <summary>
/// 색 모듈 스킬 효과 실행기
/// </summary>
public class ColorSkillExecutor : MonoBehaviour
{
    public static ColorSkillExecutor Instance { get; private set; }
    
    [Header("References")]
    public FocusedCombatManager combat;
    public PlayerStats playerStats;
    public ColorModuleSlots slots;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (slots == null) slots = ColorModuleSlots.Instance;
    }
    
    /// <summary>
    /// 스킬 사용 가능 여부 체크
    /// </summary>
    public bool CanUseSkill(ColorModuleInstance module)
    {
        if (module == null || module.definition == null) return false;
        if (module.Type != ModuleType.Skill) return false;
        
        // AP 체크
        if (combat != null && combat.IsInFocusedCombat)
        {
            if (!combat.CanSpendAP(module.APCost))
                return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// 스킬 실행 (집중전투용)
    /// </summary>
    public IEnumerator ExecuteSkill(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (!CanUseSkill(module)) yield break;
        
        // AP 소비
        if (combat != null && module.APCost > 0)
            combat.SpendAP(module.APCost, allowAutoEndTurn: false);
        
        // 스킬별 효과 실행
        switch (module.Skill)
        {
            // === 빨강 스킬 ===
            case SkillID.Red_SharpTentacle:
                yield return ExecuteSharpTentacle(module, targetCell);
                break;
                
            case SkillID.Red_PiercingTentacle:
                yield return ExecutePiercingTentacle(module, targetCell);
                break;
                
            // === 파랑 스킬 ===
            case SkillID.Blue_HardTentacle:
                yield return ExecuteHardTentacle(module, targetCell);
                break;
                
            case SkillID.Blue_StretchTentacle:
                yield return ExecuteStretchTentacle(module, targetCell);
                break;
                
            // === 검정 스킬 ===
            case SkillID.Black_DeadlyTentacle:
                yield return ExecuteDeadlyTentacle(module, targetCell);
                break;
                
            case SkillID.Black_PunishingTentacle:
                yield return ExecutePunishingTentacle(module);
                break;
                
            default:
                Debug.LogWarning($"[ColorSkillExecutor] 알 수 없는 스킬: {module.Skill}");
                break;
        }
    }
    
    // ============================================
    // 빨강 스킬 구현
    // ============================================
    
    /// <summary>
    /// 날카로운촉수: 출혈 부여. 이미 출혈이면 기본 공격력의 40% 추가 피해
    /// </summary>
    private IEnumerator ExecuteSharpTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null || combat.EnemyStatusEffects == null) yield break;
        
        bool alreadyBleeding = combat.EnemyStatusEffects.HasEffect(StatusEffectType.Bleeding);
        int bonusDamage = 0;
        
        if (alreadyBleeding)
        {
            // 이미 출혈 → 40% 추가 피해
            bonusDamage = Mathf.RoundToInt(playerStats.ATK * 0.4f);
        }
        
        // 기본 공격 + 추가 피해
        var result = combat.DamageEnemyWithFormula(bonusDamage);
        
        // 출혈 부여 (지속시간 갱신)
        combat.EnemyStatusEffects.ApplyBleeding();
        
        // 대시 애니메이션
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);
        
        Debug.Log($"[날카로운촉수] 피해: {result.finalDamage}, 출혈 부여" + (alreadyBleeding ? " (추가 피해!)" : ""));
    }
    
    /// <summary>
    /// 파고드는촉수: 출혈 부여. 이미 출혈이면 입힌 피해의 50% 회복
    /// </summary>
    private IEnumerator ExecutePiercingTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null || combat.EnemyStatusEffects == null) yield break;
        
        bool alreadyBleeding = combat.EnemyStatusEffects.HasEffect(StatusEffectType.Bleeding);
        
        // 기본 공격
        var result = combat.DamageEnemyWithFormula();
        
        // 이미 출혈 → 50% 회복
        if (alreadyBleeding && result.finalDamage > 0)
        {
            int healAmount = Mathf.RoundToInt(result.finalDamage * 0.5f);
            if (playerStats != null)
            {
                playerStats.Heal(healAmount);
                Debug.Log($"[파고드는촉수] {healAmount} 회복!");
            }
        }
        
        // 출혈 부여
        combat.EnemyStatusEffects.ApplyBleeding();
        
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);
        
        Debug.Log($"[파고드는촉수] 피해: {result.finalDamage}, 출혈 부여");
    }
    
    // ============================================
    // 파랑 스킬 구현
    // ============================================
    
    /// <summary>
    /// 단단한촉수: 불완전 부여 (최대 4중첩)
    /// </summary>
    private IEnumerator ExecuteHardTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null || combat.EnemyStatusEffects == null) yield break;
        
        // 기본 공격
        var result = combat.DamageEnemyWithFormula();
        
        // 불완전 부여
        combat.EnemyStatusEffects.ApplyImperfect(1);
        
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);
        
        int stacks = combat.EnemyStatusEffects.GetImperfectStacks();
        Debug.Log($"[단단한촉수] 피해: {result.finalDamage}, 불완전 {stacks}중첩");
    }
    
    /// <summary>
    /// 늘어나는촉수: 3칸 내 위치로 이동
    /// </summary>
    private IEnumerator ExecuteStretchTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null) yield break;
        
        // 3칸 이내 체크
        int dist = Mathf.Max(
            Mathf.Abs(targetCell.x - combat.State.playerCell.x),
            Mathf.Abs(targetCell.y - combat.State.playerCell.y)
        );
        
        if (dist > 3)
        {
            Debug.Log("[늘어나는촉수] 3칸 초과!");
            yield break;
        }
        
        // 적 위치로는 이동 불가
        if (targetCell == combat.State.enemyCell)
        {
            Debug.Log("[늘어나는촉수] 적 위치로 이동 불가!");
            yield break;
        }
        
        // 벽 체크
        if (combat.IsBlocked(targetCell))
        {
            Debug.Log("[늘어나는촉수] 벽으로 이동 불가!");
            yield break;
        }
        
        // 이동 실행
        combat.State.playerCell = targetCell;
        
        // 토큰 이동 (애니메이션은 boardUI에서 처리)
        if (combat.PlayerToken != null)
        {
            // 보드 UI 갱신
            combat.RefreshUIExternal();
            combat.RefreshMoveHighlightsExternal();
        }
        
        Debug.Log($"[늘어나는촉수] {targetCell}로 이동!");
        yield return new WaitForSeconds(0.2f);
    }
    
    // ============================================
    // 검정 스킬 구현
    // ============================================
    
    /// <summary>
    /// 치명적인촉수: 해당 적에게 첫 공격 시 확정 치명타
    /// </summary>
    private IEnumerator ExecuteDeadlyTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null || combat.EnemyToken == null) yield break;
        
        Transform enemyTransform = combat.EnemyToken.transform;
        bool isFirstStrike = !module.firstStrikeUsedOn.Contains(enemyTransform);
        
        // 첫 공격이면 확정 치명타
        var result = combat.DamageEnemyWithFormula(
            skillBonusDamage: 0,
            forceCrit: isFirstStrike
        );
        
        // 첫 공격 사용 기록
        if (isFirstStrike)
        {
            module.firstStrikeUsedOn.Add(enemyTransform);
            Debug.Log($"[치명적인촉수] 확정 치명타! 피해: {result.finalDamage}");
        }
        else
        {
            Debug.Log($"[치명적인촉수] 피해: {result.finalDamage}");
        }
        
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);
    }
    
    /// <summary>
    /// 응징하는촉수: 1턴간 공격력 1.75배
    /// </summary>
    private IEnumerator ExecutePunishingTentacle(ColorModuleInstance module)
    {
        // 버프 적용 (1턴)
        module.punishingBuffTurns = 1;
        
        Debug.Log("[응징하는촉수] 다음 턴까지 공격력 1.75배!");
        
        // 이펙트 표시
        if (combat != null && combat.PlayerToken != null)
        {
            // TODO: 버프 이펙트
        }
        
        yield return new WaitForSeconds(0.3f);
    }
    
    // ============================================
    // 패시브 효과 계산
    // ============================================
    
    /// <summary>
    /// 공격 시 패시브 보너스 계산
    /// </summary>
    public float CalculatePassiveATKMultiplier(int playerCurrentHP, int playerMaxHP, int enemyCurrentHP, int enemyMaxHP, int enemyStatusCount)
    {
        float multiplier = 1f;
        if (slots == null) return multiplier;
        
        foreach (var module in slots.GetEquippedPassives())
        {
            switch (module.Passive)
            {
                // 약자감지: 적 상태이상 1개당 +6% (최대 30%)
                case PassiveID.Red_WeaknessSense:
                    float wsBonus = Mathf.Min(enemyStatusCount * 0.06f, 0.30f);
                    multiplier += wsBonus;
                    break;
                    
                // 자신감: 내 HP 비례 공격력 (100% HP → +20%)
                case PassiveID.Black_Confidence:
                    float hpRatio = (float)playerCurrentHP / playerMaxHP;
                    multiplier += hpRatio * 0.20f;
                    break;
                    
                // 용맹함: 적 HP 높을수록 공격력 (100% → +25%)
                case PassiveID.Black_Bravery:
                    float enemyHpRatio = (float)enemyCurrentHP / enemyMaxHP;
                    multiplier += enemyHpRatio * 0.25f;
                    break;
                    
                // 악랄함: 적 HP 낮을수록 공격력 (0% → +25%)
                case PassiveID.Black_Cruelty:
                    float lowHpRatio = 1f - ((float)enemyCurrentHP / enemyMaxHP);
                    multiplier += lowHpRatio * 0.25f;
                    break;
            }
        }
        
        // 응징하는촉수 버프 (1.75배)
        foreach (var module in slots.GetAllEquipped())
        {
            if (module.punishingBuffTurns > 0)
            {
                multiplier *= 1.75f;
                break; // 중복 적용 방지
            }
        }
        
        return multiplier;
    }
    
    /// <summary>
    /// 포식 패시브 체크: 적 HP 20% 이하 시 처형 + 회복
    /// </summary>
    public bool CheckPredation(int enemyCurrentHP, int enemyMaxHP, out int healAmount)
    {
        healAmount = 0;
        if (slots == null || !slots.HasPassive(PassiveID.Red_Predation))
            return false;
        
        float ratio = (float)enemyCurrentHP / enemyMaxHP;
        if (ratio <= 0.2f)
        {
            // 처형! 남은 HP만큼 회복
            healAmount = enemyCurrentHP;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 기본기 패시브: 25% 확률 2회 공격
    /// </summary>
    public bool CheckDoubleAttack()
    {
        if (slots == null || !slots.HasPassive(PassiveID.Blue_Fundamentals))
            return false;
        
        return Random.value < 0.25f;
    }
    
    /// <summary>
    /// 촉수강화 패시브: 25% 확률 스턴
    /// </summary>
    public bool CheckStunOnHit()
    {
        if (slots == null || !slots.HasPassive(PassiveID.Blue_TentacleEnhance))
            return false;
        
        return Random.value < 0.25f;
    }
}
