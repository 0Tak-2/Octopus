/// <summary>
/// 색 모듈 색상 타입
/// </summary>
public enum ColorType
{
    /// <summary>
    /// 빨강 - 광전사 / 출혈 / 마무리
    /// 버프: 공격력 증가
    /// 디버프: 회피율 감소
    /// </summary>
    Red,
    
    /// <summary>
    /// 파랑 - 정면전 / 기본기 / 집중
    /// 버프: 회피율 증가
    /// 디버프: 타겟 변경 시 피해 감소
    /// </summary>
    Blue,
    
    /// <summary>
    /// 검정 - 암살 / 치명 / 극단적 효율
    /// 버프: 치명타 피해량 증가
    /// 디버프: 회복 효율 감소
    /// </summary>
    Black
}

/// <summary>
/// 모듈 종류 (스킬 or 패시브)
/// </summary>
public enum ModuleType
{
    Skill,
    Passive
}
