using UnityEngine;

/// <summary>
/// ÇÃ·¹ÀÌ¾î°¡ Âø¿ëÇÑ µµ±¸ °ü¸®
/// </summary>
public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    [Header("Current Tools")]
    public int equippedPickaxeID = -1; // Âø¿ëÇÑ °î±ªÀÌ (-1 = ¾øÀ½)
    public int equippedShovelID = -1; // Âø¿ëÇÑ »ğ (-1 = ¾øÀ½)

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
    /// °î±ªÀÌ Âø¿ë
    /// </summary>
    public void EquipPickaxe(int pickaxeID)
    {
        equippedPickaxeID = pickaxeID;

        ItemData itemData = ItemDatabase.Instance?.GetItemData(pickaxeID);
        string name = itemData != null ? itemData.itemName : $"ID {pickaxeID}";
        Debug.Log($"[ToolManager] Equipped pickaxe: {name}");
    }

    /// <summary>
    /// »ğ Âø¿ë
    /// </summary>
    public void EquipShovel(int shovelID)
    {
        equippedShovelID = shovelID;

        ItemData itemData = ItemDatabase.Instance?.GetItemData(shovelID);
        string name = itemData != null ? itemData.itemName : $"ID {shovelID}";
        Debug.Log($"[ToolManager] Equipped shovel: {name}");
    }

    /// <summary>
    /// °î±ªÀÌ Âø¿ë ÁßÀÎÁö
    /// </summary>
    public bool HasPickaxe()
    {
        return equippedPickaxeID > 0;
    }

    /// <summary>
    /// »ğ Âø¿ë ÁßÀÎÁö
    /// </summary>
    public bool HasShovel()
    {
        return equippedShovelID > 0;
    }

    /// <summary>
    /// Âø¿ëÇÑ °î±ªÀÌ ÀÌ¸§
    /// </summary>
    public string GetPickaxeName()
    {
        if (equippedPickaxeID <= 0) return "¾øÀ½";

        ItemData itemData = ItemDatabase.Instance?.GetItemData(equippedPickaxeID);
        return itemData != null ? itemData.itemName : "¾Ë ¼ö ¾øÀ½";
    }

    /// <summary>
    /// Âø¿ëÇÑ »ğ ÀÌ¸§
    /// </summary>
    public string GetShovelName()
    {
        if (equippedShovelID <= 0) return "¾øÀ½";

        ItemData itemData = ItemDatabase.Instance?.GetItemData(equippedShovelID);
        return itemData != null ? itemData.itemName : "¾Ë ¼ö ¾øÀ½";
    }
}