/// <summary>
/// 상태이상 종류
/// </summary>
public enum StatusEffectType
{
    /// <summary>
    /// 출혈: 턴 종료 시 3 피해, 3턴 지속, 중첩 없음 (지속시간 갱신)
    /// </summary>
    Bleeding,
    
    /// <summary>
    /// 불완전: 자체 피해 없음. 다른 공격 시 중첩당 기본 공격력의 25% 추가 피해. 최대 4중첩
    /// </summary>
    Imperfect,
    
    /// <summary>
    /// 스턴: 필드 - 1턴 행동 불가 / 집중전투 - AP 1 감소 (없으면 1턴 불가). 1턴, 동일 적 1턴 1회
    /// </summary>
    Stun
}
