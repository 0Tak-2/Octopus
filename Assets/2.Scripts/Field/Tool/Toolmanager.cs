using UnityEngine;

/// <summary>
/// ?????? ?????? ???? ????
/// </summary>
public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    [Header("Current Tools")]
    public int equippedPickaxeID = -1; // ?????? ???? (-1 = ????)
    public int equippedShovelID = -1; // ?????? ?? (-1 = ????)

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// ???? ????
    /// </summary>
    public void EquipPickaxe(int pickaxeID)
    {
        equippedPickaxeID = pickaxeID;

        ItemData itemData = ItemDatabase.Instance?.GetItemData(pickaxeID);
        string name = itemData != null ? itemData.itemName : $"ID {pickaxeID}";
        Debug.Log($"[ToolManager] Equipped pickaxe: {name}");
    }

    /// <summary>
    /// ?? ????
    /// </summary>
    public void EquipShovel(int shovelID)
    {
        equippedShovelID = shovelID;

        ItemData itemData = ItemDatabase.Instance?.GetItemData(shovelID);
        string name = itemData != null ? itemData.itemName : $"ID {shovelID}";
        Debug.Log($"[ToolManager] Equipped shovel: {name}");
    }

    /// <summary>
    /// ???? ???? ??????
    /// </summary>
    public bool HasPickaxe()
    {
        return equippedPickaxeID > 0;
    }

    /// <summary>
    /// ?? ???? ??????
    /// </summary>
    public bool HasShovel()
    {
        return equippedShovelID > 0;
    }

    /// <summary>
    /// ?????? ???? ???
    /// </summary>
    public string GetPickaxeName()
    {
        if (equippedPickaxeID <= 0) return "????";

        ItemData itemData = ItemDatabase.Instance?.GetItemData(equippedPickaxeID);
        return itemData != null ? itemData.itemName : "?? ?? ????";
    }

    /// <summary>
    /// ?????? ?? ???
    /// </summary>
    public string GetShovelName()
    {
        if (equippedShovelID <= 0) return "????";

        ItemData itemData = ItemDatabase.Instance?.GetItemData(equippedShovelID);
        return itemData != null ? itemData.itemName : "?? ?? ????";
    }
}