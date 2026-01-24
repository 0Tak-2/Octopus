using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 데이터베이스 - 모든 아이템 정보 관리
/// </summary>
public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance { get; private set; }

    [Header("아이템 데이터")]
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

        // Dictionary 구성
        BuildDatabase();
    }

    /// <summary>
    /// 데이터베이스 구성
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
    /// 아이템 데이터 가져오기
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
}