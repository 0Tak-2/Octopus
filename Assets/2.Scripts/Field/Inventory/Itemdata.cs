using System;
using UnityEngine;

/// <summary>
/// 아이템 정의 데이터
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("기본 정보")]
    public int itemID;
    public string itemName;
    public Sprite icon;

    [Header("설명")]
    [TextArea(3, 5)]
    public string description;

    [Header("Localization Keys")]
    public string nameKey;   // 예: "ITEM_NAME_SEAWEED"
    public string descKey;

    [Header("속성")]
    public ItemType itemType;
    public int maxStackSize = 99;

    [Header("장비 연결 (ItemType이 Weapon/Armor/Accessory일 때)")]
    [Tooltip("이 아이템이 장비라면 EquipmentDefinition 연결")]
    public EquipmentDefinition equipmentDefinition;

    [Header("색 모듈 연결 (ItemType이 ColorModule일 때)")]
    [Tooltip("이 아이템이 색 모듈이라면 ColorModuleDefinition 연결")]
    public ColorModuleDefinition colorModuleDefinition;

    [Header("소비 효과 (Food/Consumable)")]
    [Tooltip("배고픔 회복량")]
    public int hungerRestore = 0;

    [Tooltip("HP 회복량")]
    public int hpRestore = 0;

    [Tooltip("피로 회복량")]
    public int fatigueRestore = 0;

    /// <summary>
    /// 장비 가능한 아이템인지
    /// </summary>
    public bool IsEquipable => equipmentDefinition != null;

    /// <summary>
    /// 색 모듈 아이템인지
    /// </summary>
    public bool IsColorModule => colorModuleDefinition != null;

    /// <summary>
    /// 소비 가능한 아이템인지
    /// </summary>
    public bool IsConsumable => itemType == ItemType.Food || itemType == ItemType.Consumable;

    /// <summary>채굴용 곡괭이(암석 등). 레거시 ID 101·103 + ItemType.Tool 이름 규칙.</summary>
    public bool IsGatheringPickaxe()
    {
        if (itemID == 101 || itemID == 103) return true;
        if (itemType != ItemType.Tool) return false;
        string n = itemName ?? string.Empty;
        return n.IndexOf("pickaxe", StringComparison.OrdinalIgnoreCase) >= 0
            || n.Contains("곡괭이");
    }

    /// <summary>채굴용 삽(모래 등). 레거시 ID 104 + ItemType.Tool 이름 규칙.</summary>
    public bool IsGatheringShovel()
    {
        if (itemID == 104) return true;
        if (itemType != ItemType.Tool) return false;
        string n = itemName ?? string.Empty;
        return n.IndexOf("shovel", StringComparison.OrdinalIgnoreCase) >= 0
            || n.Contains("삽");
    }
}

/// <summary>
/// 아이템 타입
/// </summary>
public enum ItemType
{
    Resource,      // 자원 (해초, 산호)
    Food,          // 음식
    Tool,          // 도구
    Weapon,        // 무기
    Armor,         // 갑옷
    Accessory,     // 장신구
    Material,      // 제작 재료
    Consumable,    // 소비 아이템 (회복 등)
    ColorModule    // 색 모듈
}