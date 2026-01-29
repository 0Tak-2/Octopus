using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ���� �ý��� ����
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("������ ���")]
    public List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 씬 전환해도 유지
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// ī�װ����� ������ ��������
    /// </summary>
    public List<CraftingRecipe> GetRecipesByCategory(RecipeCategory category)
    {
        if (category == RecipeCategory.All)
            return allRecipes;

        return allRecipes.Where(r => r.category == category).ToList();
    }

    /// <summary>
    /// ���� ������ �����Ǹ� ��������
    /// </summary>
    public List<CraftingRecipe> GetCraftableRecipes(RecipeCategory category)
    {
        var recipes = GetRecipesByCategory(category);
        var inventory = InventoryManager.Instance;

        return recipes.Where(r => r.CanCraft(inventory)).ToList();
    }

    /// <summary>
    /// ������ ����
    /// </summary>
    public bool CraftItem(CraftingRecipe recipe)
    {
        var inventory = InventoryManager.Instance;

        if (!recipe.CanCraft(inventory))
        {
            Debug.LogWarning("[CraftingManager] Cannot craft - not enough materials");
            return false;
        }

        // ��� �Ҹ�
        recipe.ConsumeIngredients(inventory);

        // ����� �߰� (���ۿ� �Լ� ���)
        ItemData resultData = ItemDatabase.Instance.GetItemData(recipe.resultItemID);
        if (resultData != null)
        {
            inventory.AddItemFromCrafting(
                recipe.resultItemID,
                resultData.itemName,
                recipe.resultCount
            );

            // ���� �ڵ� ����
            AutoEquipTool(recipe.resultItemID, resultData);

            Debug.Log($"[CraftingManager] Crafted {resultData.itemName} x{recipe.resultCount}");
            return true;
        }

        Debug.LogError("[CraftingManager] Result item not found in database!");
        return false;
    }

    /// <summary>
    /// ���� �ڵ� ����
    /// </summary>
    private void AutoEquipTool(int itemID, ItemData itemData)
    {
        if (ToolManager.Instance == null) return;

        // ��� ���� (ID: 101=��ȣ ���, 103=�� ���)
        if (itemID == 101 || itemID == 103)
        {
            ToolManager.Instance.EquipPickaxe(itemID);
        }
        // �� ���� (ID: 104=�� ��)
        else if (itemID == 104)
        {
            ToolManager.Instance.EquipShovel(itemID);
        }
    }
}