using UnityEngine;

/// <summary>
/// 개별 상태이상 인스턴스 데이터
/// </summary>
[System.Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public int remainingTurns;  // 남은 턴 (-1 = 영구)
    public int stacks;          // 중첩 수 (불완전 전용)
    
    /// <summary>
    /// 상태이상 적용자 (스턴 1턴 1회 체크용)
    /// </summary>
    public Transform appliedBy;
    
    public StatusEffect(StatusEffectType type, int duration, int stacks = 1, Transform appliedBy = null)
    {
        this.type = type;
        this.remainingTurns = duration;
        this.stacks = Mathf.Max(1, stacks);
        this.appliedBy = appliedBy;
    }
    
    /// <summary>
    /// 지속시간 갱신 (출혈 등)
    /// </summary>
    public void RefreshDuration(int newDuration)
    {
        remainingTurns = Mathf.Max(remainingTurns, newDuration);
    }
    
    /// <summary>
    /// 중첩 추가 (불완전 등)
    /// </summary>
    public void AddStacks(int amount, int maxStacks)
    {
        stacks = Mathf.Min(stacks + amount, maxStacks);
    }
    
    /// <summary>
    /// 턴 경과
    /// </summary>
    public void TickTurn()
    {
        if (remainingTurns > 0)
            remainingTurns--;
    }
    
    public bool IsExpired => remainingTurns == 0;
}

/// <summary>
/// 상태이상 상수 정의
/// </summary>
public static class StatusEffectConstants
{
    // 출혈
    public const int BLEEDING_DAMAGE = 3;
    public const int BLEEDING_DURATION = 3;
    
    // 불완전
    public const int IMPERFECT_MAX_STACKS = 4;
    public const float IMPERFECT_DAMAGE_PER_STACK = 0.25f; // 기본 공격력의 25%
    
    // 스턴
    public const int STUN_DURATION = 1;
    public const int STUN_AP_REDUCTION = 1;
}
