using UnityEngine;

/// <summary>
/// 색 모듈 런타임 인스턴스
/// Definition을 참조하며, 추가 런타임 상태를 가질 수 있음
/// </summary>
[System.Serializable]
public class ColorModuleInstance
{
    public ColorModuleDefinition definition;
    
    // === 런타임 상태 ===
    
    /// <summary>
    /// 치명적인촉수: 이미 첫 공격을 한 적 목록 (적마다 개별 판정)
    /// </summary>
    [System.NonSerialized]
    public System.Collections.Generic.HashSet<Transform> firstStrikeUsedOn = new System.Collections.Generic.HashSet<Transform>();

    /// <summary>
    /// 응징하는촉수: 버프 남은 턴
    /// </summary>
    public int punishingBuffNextAttack = 0;

    public ColorModuleInstance(ColorModuleDefinition def)
    {
        definition = def;
        firstStrikeUsedOn = new System.Collections.Generic.HashSet<Transform>();
    }
    
    /// <summary>
    /// 전투 시작 시 초기화
    /// </summary>
    public void ResetForCombat()
    {
        firstStrikeUsedOn?.Clear();
        punishingBuffNextAttack = 0;
    }

    /// <summary>
    /// 턴 종료 시 처리
    /// </summary>
    public void OnTurnEnd()
    {
        // 응징하는촉수 버프는 공격 시 소비되므로 턴에서 안 건드림
    }


    // === 편의 프로퍼티 ===
    public string Name => definition?.moduleName ?? "???";
    public ColorType Color => definition?.colorType ?? ColorType.Red;
    public ModuleType Type => definition?.moduleType ?? ModuleType.Skill;
    public int APCost => definition?.apCost ?? 1;
    public SkillID Skill => definition?.skillID ?? SkillID.None;
    public PassiveID Passive => definition?.passiveID ?? PassiveID.None;
    public Sprite Icon => definition?.icon;
}
