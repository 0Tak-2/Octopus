using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 제작 시스템 관리
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("레시피 목록")]
    public List<CraftingRecipe> allRecipes = new List<CraftingRecipe>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 카테고리별 레시피 가져오기
    /// </summary>
    public List<CraftingRecipe> GetRecipesByCategory(RecipeCategory category)
    {
        if (category == RecipeCategory.All)
            return allRecipes;

        return allRecipes.Where(r => r.category == category).ToList();
    }

    /// <summary>
    /// 제작 가능한 레시피만 가져오기
    /// </summary>
    public List<CraftingRecipe> GetCraftableRecipes(RecipeCategory category)
    {
        var recipes = GetRecipesByCategory(category);
        var inventory = InventoryManager.Instance;

        return recipes.Where(r => r.CanCraft(inventory)).ToList();
    }

    /// <summary>
    /// 아이템 제작
    /// </summary>
    public bool CraftItem(CraftingRecipe recipe)
    {
        var inventory = InventoryManager.Instance;

        if (!recipe.CanCraft(inventory))
        {
            Debug.LogWarning("[CraftingManager] Cannot craft - not enough materials");
            return false;
        }

        // 재료 소모
        recipe.ConsumeIngredients(inventory);

        // 결과물 추가 (제작용 함수 사용)
        ItemData resultData = ItemDatabase.Instance.GetItemData(recipe.resultItemID);
        if (resultData != null)
        {
            inventory.AddItemFromCrafting(
                recipe.resultItemID,
                resultData.itemName,
                recipe.resultCount
            );

            // 도구 자동 착용
            AutoEquipTool(recipe.resultItemID, resultData);

            Debug.Log($"[CraftingManager] Crafted {resultData.itemName} x{recipe.resultCount}");
            return true;
        }

        Debug.LogError("[CraftingManager] Result item not found in database!");
        return false;
    }

    /// <summary>
    /// 도구 자동 착용
    /// </summary>
    private void AutoEquipTool(int itemID, ItemData itemData)
    {
        if (ToolManager.Instance == null) return;

        // 곡괭이 착용 (ID: 101=산호 곡괭이, 103=돌 곡괭이)
        if (itemID == 101 || itemID == 103)
        {
            ToolManager.Instance.EquipPickaxe(itemID);
        }
        // 삽 착용 (ID: 104=돌 삽)
        else if (itemID == 104)
        {
            ToolManager.Instance.EquipShovel(itemID);
        }
    }
}