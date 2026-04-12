using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 제작 시스템 매니저
/// </summary>
public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("모든 레시피")]
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
    /// 카테고리별 레시피 목록
    /// </summary>
    public List<CraftingRecipe> GetRecipesByCategory(RecipeCategory category)
    {
        if (category == RecipeCategory.All)
            return allRecipes;

        return allRecipes.Where(r => r.category == category).ToList();
    }

    /// <summary>
    /// 재료가 충분하면 제작 가능한 레시피만
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

        // 재료 소비
        recipe.ConsumeIngredients(inventory);

        // 결과물 추가 (인벤토리 함수 사용)
        ItemData resultData = ItemDatabase.Instance.GetItemData(recipe.resultItemID);
        if (resultData != null)
        {
            inventory.AddItemFromCrafting(
                recipe.resultItemID,
                resultData.itemName,
                recipe.resultCount
            );

            // 도구 자동 장착
            AutoEquipTool(recipe.resultItemID, resultData);

            Debug.Log($"[CraftingManager] Crafted {resultData.itemName} x{recipe.resultCount}");
            return true;
        }

        Debug.LogError("[CraftingManager] Result item not found in database!");
        return false;
    }

    /// <summary>
    /// 제작 직후 채굴 도구 자동 장착 (ItemData 기준 — 산호 곡괭이 401 등).
    /// </summary>
    private void AutoEquipTool(int itemID, ItemData itemData)
    {
        if (ToolManager.Instance == null || itemData == null) return;

        if (itemData.IsGatheringPickaxe())
            ToolManager.Instance.EquipPickaxe(itemID);
        else if (itemData.IsGatheringShovel())
            ToolManager.Instance.EquipShovel(itemID);
    }
}
