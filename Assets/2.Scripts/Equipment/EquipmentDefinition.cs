using UnityEngine;

/// <summary>
/// 장비 정의 (ScriptableObject)
/// 에디터에서 생성: Create > Equipment > Equipment Definition
/// </summary>
[CreateAssetMenu(menuName = "Equipment/Equipment Definition", fileName = "NewEquipment")]
public class EquipmentDefinition : ScriptableObject
{
    [Header("기본 정보")]
    public string equipmentName = "새 장비";
    
    [TextArea(2, 4)]
    public string description = "장비 설명";
    
    public Sprite icon;
    
    [Header("분류")]
    public EquipmentType equipmentType = EquipmentType.Weapon;
    
    [Header("스탯 보너스")]
    [Tooltip("공격력 증가")]
    public int bonusATK = 0;
    
    [Tooltip("방어력 증가")]
    public int bonusDEF = 0;
    
    [Tooltip("최대 HP 증가")]
    public int bonusMaxHP = 0;
    
    [Tooltip("회피율 증가 (0.03 = 3%)")]
    [Range(0f, 0.5f)]
    public float bonusEVA = 0f;
    
    [Tooltip("치명타 확률 증가 (0.02 = 2%)")]
    [Range(0f, 0.5f)]
    public float bonusCRIT = 0f;
    
    [Tooltip("치명타 피해량 증가 (0.1 = 10%)")]
    [Range(0f, 1f)]
    public float bonusCRIT_DMG = 0f;
    
    [Header("특수 효과")]
    [Tooltip("스턴 확률 증가 (무거운 촉수 추)")]
    [Range(0f, 0.5f)]
    public float stunChanceBonus = 0f;
    
    [Header("제작 재료 (참고용)")]
    [TextArea(2, 3)]
    public string craftingMaterials = "";
}
