using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 레시피 슬롯
/// </summary>
public class RecipeSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public GameObject ingredientsParent; // 재료 표시 부모 (사용 안 함)
    public TextMeshProUGUI ingredientText; // 간단한 재료 텍스트 (숨김)
    public Image background;

    [Header("Tooltip")]
    public GameObject tooltipPrefab; // 툴팁 프리팹 (선택)

    [Header("Colors")]
    public Color craftableColor = new Color(0.3f, 0.5f, 0.3f, 1f); // 초록
    public Color notCraftableColor = new Color(0.3f, 0.3f, 0.3f, 1f); // 회색

    private CraftingRecipe recipe;
    private CraftingUI craftingUI;
    private bool canCraft;
    private GameObject tooltipInstance;

    /// <summary>
    /// 슬롯 설정
    /// </summary>
    public void Setup(CraftingRecipe recipe, bool canCraft, CraftingUI ui)
    {
        this.recipe = recipe;
        this.canCraft = canCraft;
        this.craftingUI = ui;

        // 결과물 아이템 데이터 가져오기
        ItemData resultData = ItemDatabase.Instance?.GetItemData(recipe.resultItemID);

        if (resultData == null)
        {
            Debug.LogError($"[RecipeSlot] Item {recipe.resultItemID} not found!");
            return;
        }

        // 아이콘
        if (iconImage != null && resultData.icon != null)
        {
            iconImage.sprite = resultData.icon;
            iconImage.enabled = true;
        }

        // 이름
        if (nameText != null)
        {
            nameText.text = !string.IsNullOrEmpty(resultData.nameKey)
    ? LocalizationManager.T(resultData.nameKey) : resultData.itemName;
        }

        // 재료 텍스트 숨기기
        if (ingredientText != null)
        {
            ingredientText.gameObject.SetActive(false);
        }

        // 재료 부모 숨기기
        if (ingredientsParent != null)
        {
            ingredientsParent.SetActive(false);
        }

        // 배경 색상
        if (background != null)
        {
            background.color = canCraft ? craftableColor : notCraftableColor;
        }
    }

    /// <summary>
    /// 마우스 올렸을 때
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    /// <summary>
    /// 마우스 나갔을 때
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    /// <summary>
    /// 툴팁 표시
    /// </summary>
    private void ShowTooltip()
    {
        if (recipe == null) return;

        // CraftingUI의 툴팁 사용
        if (craftingUI != null)
        {
            string tooltipText = BuildTooltipText();
            craftingUI.ShowRecipeTooltip(tooltipText);
        }
    }

    /// <summary>
    /// 툴팁 숨기기
    /// </summary>
    private void HideTooltip()
    {
        if (craftingUI != null)
        {
            craftingUI.HideRecipeTooltip();
        }
    }

    /// <summary>
    /// 툴팁 텍스트 생성
    /// </summary>
    private string BuildTooltipText()
    {
        ItemData resultData = ItemDatabase.Instance?.GetItemData(recipe.resultItemID);
        if (resultData == null) return "";

        string rName = !string.IsNullOrEmpty(resultData.nameKey)
    ? LocalizationManager.T(resultData.nameKey) : resultData.itemName;
        string rDesc = !string.IsNullOrEmpty(resultData.descKey)
            ? LocalizationManager.T(resultData.descKey) : resultData.description;
        string text = $"<b>{rName}</b>\n";
        text += $"{rDesc}\n\n";
        text += $"<b>{LocalizationManager.T("UI_REQUIRED_MATERIALS")}</b>\n";

        foreach (var ingredient in recipe.ingredients)
        {
            ItemData ingData = ItemDatabase.Instance.GetItemData(ingredient.itemID);
            if (ingData != null)
            {
                int currentCount = InventoryManager.Instance.GetItemCount(ingredient.itemID);
                string colorTag = (currentCount >= ingredient.count) ? "<color=green>" : "<color=red>";
                string iName = !string.IsNullOrEmpty(ingData.nameKey)
    ? LocalizationManager.T(ingData.nameKey) : ingData.itemName;
                text += $"{colorTag}• {iName}: {currentCount}/{ingredient.count}</color>\n";
            }
        }

        return text;
    }

    /// <summary>
    /// 클릭 시 제작
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (recipe == null || craftingUI == null) return;

        if (canCraft)
        {
            Debug.Log($"[RecipeSlot] Clicked - attempting craft");
            craftingUI.TryCraft(recipe);
        }
        else
        {
            Debug.Log($"[RecipeSlot] Cannot craft - not enough materials");
        }
    }
}