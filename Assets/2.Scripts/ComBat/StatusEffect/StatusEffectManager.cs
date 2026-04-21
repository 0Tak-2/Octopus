using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상태이상 관리자 - 단일 유닛(플레이어 or 적)에 부착
/// </summary>
public class StatusEffectManager : MonoBehaviour
{
    [Header("Debug")]
    public bool showDebugLogs = false;
    
    private List<StatusEffect> _effects = new List<StatusEffect>();
    
    // 스턴 적용자 추적 (동일 적 1턴 1회 체크)
    private HashSet<Transform> _stunnedByThisTurn = new HashSet<Transform>();
    private PlayerStats _ownerPlayerStats;

    private void Awake()
    {
        _ownerPlayerStats = GetComponent<PlayerStats>();
    }
    
    /// <summary>
    /// 현재 활성화된 상태이상 목록 (읽기 전용)
    /// </summary>
    public IReadOnlyList<StatusEffect> ActiveEffects => _effects;
    
    /// <summary>
    /// 특정 타입의 상태이상이 있는지
    /// </summary>
    public bool HasEffect(StatusEffectType type)
    {
        foreach (var e in _effects)
            if (e.type == type) return true;
        return false;
    }
    
    /// <summary>
    /// 특정 타입의 상태이상 가져오기
    /// </summary>
    public StatusEffect GetEffect(StatusEffectType type)
    {
        foreach (var e in _effects)
            if (e.type == type) return e;
        return null;
    }
    
    /// <summary>
    /// 불완전 중첩 수 가져오기
    /// </summary>
    public int GetImperfectStacks()
    {
        var effect = GetEffect(StatusEffectType.Imperfect);
        return effect?.stacks ?? 0;
    }
    
    /// <summary>
    /// 상태이상 개수 (약자감지 패시브용)
    /// </summary>
    public int TotalEffectCount => _effects.Count;
    
    // ============================================
    // 상태이상 적용
    // ============================================
    
    /// <summary>
    /// 출혈 적용 (이미 있으면 지속시간 갱신)
    /// </summary>
    public void ApplyBleeding()
    {
        int duration = AdjustDurationForOwner(StatusEffectConstants.BLEEDING_DURATION);
        var existing = GetEffect(StatusEffectType.Bleeding);
        if (existing != null)
        {
            existing.RefreshDuration(duration);
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 출혈 지속시간 갱신 ({existing.remainingTurns}턴)");
        }
        else
        {
            _effects.Add(new StatusEffect(StatusEffectType.Bleeding, duration));
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 출혈 부여!");
        }
    }
    
    /// <summary>
    /// 불완전 적용 (최대 4중첩)
    /// </summary>
    public void ApplyImperfect(int stacks = 1)
    {
        var existing = GetEffect(StatusEffectType.Imperfect);
        if (existing != null)
        {
            existing.AddStacks(stacks, StatusEffectConstants.IMPERFECT_MAX_STACKS);
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 불완전 {existing.stacks}중첩");
        }
        else
        {
            _effects.Add(new StatusEffect(StatusEffectType.Imperfect, -1, stacks)); // 영구 (소비될 때까지)
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 불완전 부여! ({stacks}중첩)");
        }
    }
    
    /// <summary>
    /// 스턴 적용 (동일 적 1턴 1회 제한)
    /// </summary>
    /// <param name="appliedBy">스턴을 건 주체</param>
    /// <returns>성공 여부</returns>
    public bool TryApplyStun(Transform appliedBy)
    {
        int duration = AdjustDurationForOwner(StatusEffectConstants.STUN_DURATION);

        // 동일 적이 이번 턴에 이미 스턴을 걸었으면 실패
        if (appliedBy != null && _stunnedByThisTurn.Contains(appliedBy))
        {
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 스턴 실패 (동일 적 1턴 1회)");
            return false;
        }
        
        // 이미 스턴 상태면 지속시간 갱신
        var existing = GetEffect(StatusEffectType.Stun);
        if (existing != null)
        {
            existing.RefreshDuration(duration);
        }
        else
        {
            _effects.Add(new StatusEffect(StatusEffectType.Stun, duration, 1, appliedBy));
        }
        
        if (appliedBy != null)
            _stunnedByThisTurn.Add(appliedBy);
        
        if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 스턴!");
        return true;
    }
    
    // ============================================
    // 턴 처리
    // ============================================
    
    /// <summary>
    /// 턴 시작 시 호출 (스턴 적용자 추적 초기화)
    /// </summary>
    public void OnTurnStart()
    {
        _stunnedByThisTurn.Clear();
    }
    
    /// <summary>
    /// 턴 종료 시 호출 - 출혈 피해 처리 & 지속시간 감소
    /// </summary>
    /// <returns>출혈 피해량 (0이면 출혈 없음)</returns>
    public int OnTurnEnd()
    {
        int bleedDamage = 0;
        
        // 출혈 피해 계산
        var bleeding = GetEffect(StatusEffectType.Bleeding);
        if (bleeding != null)
        {
            bleedDamage = StatusEffectConstants.BLEEDING_DAMAGE;
            if (_ownerPlayerStats != null)
            {
                float resist = EngraveEffectRuntime.GetPlayerDotResistPercent();
                bleedDamage = Mathf.Max(0, Mathf.RoundToInt(bleedDamage * (1f - resist)));
            }
            if (showDebugLogs) Debug.Log($"[StatusEffect] {gameObject.name}: 출혈 피해 {bleedDamage}");
        }
        
        // 모든 효과 턴 감소
        foreach (var e in _effects)
        {
            e.TickTurn();
        }
        
        // 만료된 효과 제거
        _effects.RemoveAll(e => e.IsExpired);
        
        return bleedDamage;
    }
    
    /// <summary>
    /// 불완전 소비 (공격 시 호출) - 추가 피해 계산 후 중첩 제거
    /// </summary>
    /// <param name="baseAttack">기본 공격력</param>
    /// <returns>추가 피해량</returns>
    public int ConsumeImperfect(int baseAttack)
    {
        var imperfect = GetEffect(StatusEffectType.Imperfect);
        if (imperfect == null || imperfect.stacks <= 0)
            return 0;
        
        // 중첩당 25% 추가 피해
        float bonusDamage = baseAttack * StatusEffectConstants.IMPERFECT_DAMAGE_PER_STACK * imperfect.stacks;
        int finalBonus = Mathf.RoundToInt(bonusDamage);
        
        if (showDebugLogs) 
            Debug.Log($"[StatusEffect] {gameObject.name}: 불완전 {imperfect.stacks}중첩 소비 → +{finalBonus} 피해");
        
        // 불완전 제거
        _effects.Remove(imperfect);
        
        return finalBonus;
    }
    
    /// <summary>
    /// 스턴 상태인지 확인
    /// </summary>
    public bool IsStunned => HasEffect(StatusEffectType.Stun);
    
    /// <summary>
    /// 모든 상태이상 제거
    /// </summary>
    public void ClearAll()
    {
        _effects.Clear();
        _stunnedByThisTurn.Clear();
    }
    
    /// <summary>
    /// 특정 타입 상태이상 제거
    /// </summary>
    public void RemoveEffect(StatusEffectType type)
    {
        _effects.RemoveAll(e => e.type == type);
    }

    private int AdjustDurationForOwner(int baseDuration)
    {
        if (_ownerPlayerStats == null || baseDuration <= 0)
            return baseDuration;

        float reduce = EngraveEffectRuntime.GetPlayerStatusDurationReductionPercent();
        return Mathf.Max(1, Mathf.RoundToInt(baseDuration * (1f - reduce)));
    }
}
