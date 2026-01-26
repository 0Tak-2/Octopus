using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 제작 레시피 정의
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Crafting/Recipe")]
public class CraftingRecipe : ScriptableObject
{
    [Header("결과물")]
    public int resultItemID;
    public int resultCount = 1;

    [Header("재료")]
    public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

    [Header("카테고리")]
    public RecipeCategory category;

    [Header("제작 조건 (선택)")]
    public int requiredToolID = -1; // -1이면 조건 없음 (예: 작업대 필요)

    /// <summary>
    /// 제작 가능한지 체크
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
    /// 재료 소모
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
/// 레시피 재료
/// </summary>
[System.Serializable]
public class RecipeIngredient
{
    public int itemID;
    public int count;
}

/// <summary>
/// 레시피 카테고리
/// </summary>
public enum RecipeCategory
{
    All,        // 전체
    Tool,       // 도구 (곡괭이, 삽, 검 등)
    Building,   // 제작 (작업대, 포션제작대 등)
    Storage     // 저장소 (은신처, 창고 등)
}