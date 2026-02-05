using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Crafting recipe definition
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("Result")]
    public int resultItemID;
    public int resultCount = 1;

    [Header("Ingredients")]
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

    [Header("Category")]
    public RecipeCategory category;

    [Header("Crafting Condition (Optional)")]
    [Tooltip("-1 = no condition, otherwise requires this tool/station")]
    public int requiredToolID = -1;

    /// <summary>
    /// Check if can craft with current inventory
    /// </summary>
    public bool CanCraft(InventoryManager inventory)
    {
        foreach (var ingredient in ingredients)
        {
            int currentCount = inventory.GetItemCount(ingredient.itemID);
            if (currentCount < ingredient.count)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Consume ingredients from inventory
    /// </summary>
    public void ConsumeIngredients(InventoryManager inventory)
    {
        foreach (var ingredient in ingredients)
        {
            inventory.RemoveItem(ingredient.itemID, ingredient.count);
        }
    }
}

/// <summary>
/// Recipe ingredient
/// </summary>
[System.Serializable]
public class RecipeIngredient
{
    public int itemID;
    public int count;
}

/// <summary>
/// Recipe category
/// </summary>
public enum RecipeCategory
{
    All,        // All recipes
    Tool,       // Tools (pickaxe, shovel, etc.)
    Building,   // Building (crafting table, shelter, etc.)
    Storage,    // Storage (chest, etc.)
    Food,       // Food (cooked meals)
    Medicine,   // Recovery items (bandage, ointment, etc.)
    Weapon,     // Weapons
    Armor,      // Armor
    Accessory,  // Accessories
    Material    // Intermediate materials (seaweed fiber, etc.)
}
