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

    [Header("속성")]
    public ItemType itemType;
    public int maxStackSize = 99;
}

/// <summary>
/// 아이템 타입
/// </summary>
public enum ItemType
{
    Resource,    // 자원 (해초, 산호)
    Food,        // 음식
    Tool,        // 도구
    Weapon,      // 무기
    Material,    // 제작 재료
    Consumable   // 소비 아이템
}