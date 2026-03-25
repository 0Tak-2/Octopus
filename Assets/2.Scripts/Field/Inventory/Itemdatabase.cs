using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 데이터베이스 - 모든 아이템 정보
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("아이템 목록")]
    public List<ItemData> itemDataList = new List<ItemData>();

    private Dictionary<int, ItemData> itemDataDict = new Dictionary<int, ItemData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildDatabase();
    }

    /// <summary>
    /// 데이터베이스 구축
    /// </summary>
    private void BuildDatabase()
    {
        itemDataDict.Clear();

        foreach (var data in itemDataList)
        {
            if (data != null && !itemDataDict.ContainsKey(data.itemID))
            {
                itemDataDict[data.itemID] = data;
            }
        }

        Debug.Log($"[ItemDatabase] Loaded {itemDataDict.Count} items");
    }

    /// <summary>
    /// ID로 아이템 검색
    /// </summary>
    public ItemData GetItemData(int itemID)
    {
        if (itemDataDict.ContainsKey(itemID))
        {
            return itemDataDict[itemID];
        }

        Debug.LogWarning($"[ItemDatabase] Item ID {itemID} not found!");
        return null;
    }

    /// <summary>
    /// EquipmentDefinition으로 ItemData 역검색
    /// </summary>
    public ItemData GetItemByEquipment(EquipmentDefinition equipDef)
    {
        if (equipDef == null) return null;

        foreach (var data in itemDataList)
        {
            if (data != null && data.equipmentDefinition == equipDef)
                return data;
        }

        return null;
    }

    /// <summary>
    /// ColorModuleDefinition으로 ItemData 역검색
    /// </summary>
    public ItemData GetItemByColorModule(ColorModuleDefinition moduleDef)
    {
        if (moduleDef == null) return null;

        foreach (var data in itemDataList)
        {
            if (data != null && data.colorModuleDefinition == moduleDef)
                return data;
        }

        return null;
    }
}