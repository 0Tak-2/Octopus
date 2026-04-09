using UnityEngine;
using System.Collections;

/// <summary>
/// 색 모듈 스킬 효과 실행기
/// 모든 공격은 색 모듈 스킬을 통해서만 이루어짐
/// 패시브 효과는 스킬 공격 후 자동 적용
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
    // 공통 패시브 후처리 (공격 스킬 후 호출)
    // ============================================

    /// <summary>
    /// 공격 스킬 실행 후 패시브 효과 처리
    /// - 기본기: 25% 확률 2회 공격
    /// - 촉수강화: 25% 확률 스턴
    /// - 포식: 적 HP 20% 이하 시 처형 + 회복
    /// </summary>
    private IEnumerator ProcessAttackPassives(DamageResult result, Vector2Int targetCell)
    {
        if (combat == null || result.isEvaded) yield break;

        // === 포식 (빨강 패시브): 적 HP 20% 이하 시 처형 ===
        if (slots != null && slots.HasPassive(PassiveID.Red_Predation))
        {
            int enemyMaxHP = combat.CurrentEnemyDef != null ? combat.CurrentEnemyDef.maxHP : combat.State.enemyHP;
            if (combat.State.enemyHP > 0 && CheckPredation(combat.State.enemyHP, enemyMaxHP, out int healAmount))
            {
                // 처형! 남은 HP 전부 제거
                int remaining = combat.State.enemyHP;
                combat.State.enemyHP = 0;

                // 회복 (입힌 피해 = 남은 HP)
                HealPlayerInCombat(remaining);

                Debug.Log($"[포식] 처형! {remaining} 피해 + {remaining} 회복! HP: {combat.State.playerHP}");

                combat.RefreshUIExternal();
                yield break; // 적이 죽었으므로 나머지 패시브 스킵
            }
        }

        // === 촉수강화 (파랑 패시브): 25% 확률 스턴 ===
        if (CheckStunOnHit())
        {
            if (combat.EnemyStatusEffects != null)
            {
                Transform enemyTransform = combat.EnemyToken != null ? combat.EnemyToken.transform : null;
                if (enemyTransform != null)
                {
                    combat.EnemyStatusEffects.TryApplyStun(playerStats.transform);
                    Debug.Log("[촉수강화] 스턴 적용!");
                }
            }
        }

        // === 기본기 (파랑 패시브): 25% 확률 2회 공격 ===
        if (CheckDoubleAttack())
        {
            Debug.Log("[기본기] 2회 공격 발동!");

            // 추가 공격 (같은 대상에게 한 번 더)
            var bonusResult = combat.DamageEnemyWithFormula();

            // 추가 공격에 대시 연출
            yield return combat.Anim_PlayerDashHitReturn(targetCell, null);

            // 추가 공격에도 촉수강화(스턴) 체크
            if (!bonusResult.isEvaded && CheckStunOnHit())
            {
                if (combat.EnemyStatusEffects != null)
                {
                    combat.EnemyStatusEffects.TryApplyStun(playerStats.transform);
                    Debug.Log("[기본기+촉수강화] 추가 공격 스턴!");
                }
            }
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
            bonusDamage = Mathf.RoundToInt(playerStats.ATK * 0.4f);
        }

        // 대시 공격 + 피해
        var result = combat.DamageEnemyWithFormula(bonusDamage);
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);

        // 출혈 부여
        combat.EnemyStatusEffects.ApplyBleeding();

        Debug.Log($"[날카로운촉수] 피해: {result.finalDamage}, 출혈 부여" + (alreadyBleeding ? " (추가 피해!)" : ""));

        // 패시브 후처리
        yield return ProcessAttackPassives(result, targetCell);
    }

    /// <summary>
    /// 파고드는촉수: 출혈 부여. 이미 출혈이면 입힌 피해의 50% 회복
    /// </summary>
    private IEnumerator ExecutePiercingTentacle(ColorModuleInstance module, Vector2Int targetCell)
    {
        if (combat == null || combat.EnemyStatusEffects == null) yield break;

        bool alreadyBleeding = combat.EnemyStatusEffects.HasEffect(StatusEffectType.Bleeding);

        // 대시 공격 + 피해
        var result = combat.DamageEnemyWithFormula();
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);

        // 이미 출혈 → 50% 회복
        if (alreadyBleeding && result.finalDamage > 0)
        {
            int healAmount = Mathf.RoundToInt(result.finalDamage * 0.5f);
            HealPlayerInCombat(healAmount);
            Debug.Log($"[파고드는촉수] {healAmount} 회복! HP: {combat.State.playerHP}");
        }

        // 출혈 부여
        combat.EnemyStatusEffects.ApplyBleeding();

        Debug.Log($"[파고드는촉수] 피해: {result.finalDamage}, 출혈 부여");

        // 패시브 후처리
        yield return ProcessAttackPassives(result, targetCell);
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

        // 대시 공격 + 피해
        var result = combat.DamageEnemyWithFormula();
        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);

        // 불완전 부여
        combat.EnemyStatusEffects.ApplyImperfect(1);

        int stacks = combat.EnemyStatusEffects.GetImperfectStacks();
        Debug.Log($"[단단한촉수] 피해: {result.finalDamage}, 불완전 {stacks}중첩");

        // 패시브 후처리
        yield return ProcessAttackPassives(result, targetCell);
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

        if (targetCell == combat.State.enemyCell)
        {
            Debug.Log("[늘어나는촉수] 적 위치로 이동 불가!");
            yield break;
        }

        if (combat.IsBlocked(targetCell))
        {
            Debug.Log("[늘어나는촉수] 벽으로 이동 불가!");
            yield break;
        }

        // 이동 실행
        combat.State.playerCell = targetCell;

        if (combat.PlayerToken != null)
        {
            combat.RefreshUIExternal();
            combat.RefreshMoveHighlightsExternal();
        }

        Debug.Log($"[늘어나는촉수] {targetCell}로 이동!");
        yield return new WaitForSeconds(0.2f);

        // 이동기이므로 공격 패시브 없음
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

        yield return combat.Anim_PlayerDashHitReturn(targetCell, null);

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

        // 패시브 후처리
        yield return ProcessAttackPassives(result, targetCell);
    }

    /// <summary>
    /// 응징하는촉수: 다음 공격 시 공격력 1.75배
    /// </summary>
    private IEnumerator ExecutePunishingTentacle(ColorModuleInstance module)
    {
        // 버프 적용 (다음 1회 공격)
        module.punishingBuffNextAttack = 1;

        // 슬롯에 있는 인스턴스와 동일한지 확인
        var allMods = slots.GetAllEquipped();
        bool foundInSlots = false;
        foreach (var m in allMods)
        {
            if (m == module)
            {
                foundInSlots = true;
                break;
            }
        }

        Debug.Log($"[응징하는촉수] 버프 설정! nextAttack={module.punishingBuffNextAttack}, 슬롯에 동일 인스턴스={foundInSlots}, module hashCode={module.GetHashCode()}");

        // 슬롯의 모든 모듈 상태도 출력
        foreach (var m in allMods)
        {
            Debug.Log($"  슬롯 모듈: {m.Name}, punishingBuff={m.punishingBuffNextAttack}, hashCode={m.GetHashCode()}");
        }

        yield return new WaitForSeconds(0.3f);
    }

    // ============================================
    // 패시브 효과 계산
    // ============================================

    /// <summary>
    /// 공격 시 패시브 보너스 ATK 배율 계산
    /// DamageEnemyWithFormula에서 자동 호출됨
    /// </summary>
    public float CalculatePassiveATKMultiplier(int playerCurrentHP, int playerMaxHP, int enemyCurrentHP, int enemyMaxHP, int enemyStatusCount)
    {
        float multiplier = 1f;
        if (slots == null) return multiplier;

        var passives = slots.GetEquippedPassives();
        Debug.Log($"[PassiveCalc] 장착된 패시브 수: {passives.Count}, 적 상태이상: {enemyStatusCount}");

        foreach (var module in passives)
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
                    float hpRatio = playerMaxHP > 0 ? (float)playerCurrentHP / playerMaxHP : 0f;
                    multiplier += hpRatio * 0.20f;
                    break;

                // 용맹함: 적 HP 높을수록 공격력 (100% → +25%)
                case PassiveID.Black_Bravery:
                    float enemyHpRatio = enemyMaxHP > 0 ? (float)enemyCurrentHP / enemyMaxHP : 0f;
                    multiplier += enemyHpRatio * 0.25f;
                    break;

                // 악랄함: 적 HP 낮을수록 공격력 (0% → +25%)
                case PassiveID.Black_Cruelty:
                    float lowHpRatio = enemyMaxHP > 0 ? 1f - ((float)enemyCurrentHP / enemyMaxHP) : 0f;
                    multiplier += lowHpRatio * 0.25f;
                    break;
            }
        }

        // 응징하는촉수 버프 (1.75배) - 사용 후 소비
        var allEquipped = slots.GetAllEquipped();
        Debug.Log($"[PassiveCalc] 전체 장착 모듈 수: {allEquipped.Count}");
        foreach (var module in allEquipped)
        {
            if (module.punishingBuffNextAttack > 0)
            {
                Debug.Log($"[PassiveCalc] 응징하는촉수 버프 활성! 소비됨");
                multiplier *= 1.75f;
                module.punishingBuffNextAttack = 0; // 사용 후 소비
                break;
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

        float ratio = enemyMaxHP > 0 ? (float)enemyCurrentHP / enemyMaxHP : 1f;
        if (ratio <= 0.2f && ratio > 0f)
        {
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
        {
            Debug.Log($"[기본기] 패시브 없음. slots null={slots == null}, hasPassive={slots?.HasPassive(PassiveID.Blue_Fundamentals)}");
            return false;
        }

        bool triggered = Random.value < 0.25f;
        Debug.Log($"[기본기] 확률 체크: {(triggered ? "발동!" : "미발동")}");
        return triggered;
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

    // ============================================
    // 헬퍼
    // ============================================

    /// <summary>
    /// 집중전투 중 플레이어 회복 (State + PlayerStats 동기화 + UI 갱신)
    /// </summary>
    private void HealPlayerInCombat(int amount)
    {
        if (amount <= 0) return;

        // PlayerStats 회복 (회복 효율 적용됨)
        if (playerStats != null)
            playerStats.Heal(amount);

        // 전투 State는 PlayerStats.hp와 동일하게 (실시간 UI·전투 HUD 일치)
        if (combat != null && combat.IsInFocusedCombat && playerStats != null)
        {
            combat.State.playerHP = Mathf.Clamp(playerStats.hp, 0, playerStats.maxHP);
            combat.RefreshUIExternal();
        }
    }
}