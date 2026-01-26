using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 제작 UI 관리
/// </summary>
public class CraftingUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject craftingPanel; // 제작 UI 패널
    public Transform recipeGridParent; // 레시피 그리드 부모
    public GameObject recipeSlotPrefab; // 레시피 슬롯 프리팹

    [Header("Tooltip")]
    public GameObject recipeTooltip; // 레시피 툴팁
    public TextMeshProUGUI tooltipText; // 툴팁 텍스트

    [Header("Category Buttons")]
    public Button btnAll;
    public Button btnTool;
    public Button btnBuilding;
    public Button btnStorage;

    [Header("Button Colors")]
    public Color selectedColor = new Color(0.3f, 0.5f, 0.7f);
    public Color normalColor = new Color(0.2f, 0.2f, 0.3f);

    private RecipeCategory currentCategory = RecipeCategory.All;
    private List<GameObject> recipeSlots = new List<GameObject>();

    private void Start()
    {
        if (craftingPanel != null)
            craftingPanel.SetActive(false);

        // 툴팁 숨기기
        if (recipeTooltip != null)
            recipeTooltip.SetActive(false);

        // 카테고리 버튼 연결
        if (btnAll != null) btnAll.onClick.AddListener(() => SelectCategory(RecipeCategory.All));
        if (btnTool != null) btnTool.onClick.AddListener(() => SelectCategory(RecipeCategory.Tool));
        if (btnBuilding != null) btnBuilding.onClick.AddListener(() => SelectCategory(RecipeCategory.Building));
        if (btnStorage != null) btnStorage.onClick.AddListener(() => SelectCategory(RecipeCategory.Storage));

        // 초기 카테고리 색상 설정 (전체 선택 상태)
        UpdateCategoryButtons();
    }

    /// <summary>
    /// 제작 UI 열기
    /// </summary>
    public void OpenCrafting()
    {
        Debug.Log("[CraftingUI] OpenCrafting called!");

        if (craftingPanel != null)
        {
            // 이미 열려있으면 무시
            if (craftingPanel.activeSelf)
            {
                Debug.Log("[CraftingUI] Already open, ignoring...");
                return;
            }

            Debug.Log("[CraftingUI] Setting panel active...");
            craftingPanel.SetActive(true);

            // Coroutine 사용 (Invoke는 비활성화된 오브젝트에서 작동 안 함)
            StartCoroutine(RefreshAfterOpenCoroutine());
        }
        else
        {
            Debug.LogError("[CraftingUI] CraftingPanel is null!");
        }
    }

    /// <summary>
    /// 열기 후 갱신 (Coroutine)
    /// </summary>
    private System.Collections.IEnumerator RefreshAfterOpenCoroutine()
    {
        yield return new WaitForSeconds(0.05f);
        RefreshAfterOpen();
    }

    /// <summary>
    /// 열기 후 갱신
    /// </summary>
    private void RefreshAfterOpen()
    {
        Debug.Log("[CraftingUI] RefreshAfterOpen called!");
        if (craftingPanel != null && craftingPanel.activeSelf)
        {
            UpdateCategoryButtons();
            RefreshRecipes();
        }
    }

    /// <summary>
    /// 제작 UI 닫기
    /// </summary>
    public void CloseCrafting()
    {
        if (craftingPanel != null)
            craftingPanel.SetActive(false);
    }

    /// <summary>
    /// 제작 UI가 열려있는지 확인
    /// </summary>
    public bool IsOpen()
    {
        return craftingPanel != null && craftingPanel.activeSelf;
    }

    /// <summary>
    /// 카테고리 선택
    /// </summary>
    private void SelectCategory(RecipeCategory category)
    {
        currentCategory = category;
        UpdateCategoryButtons();
        RefreshRecipes();
    }

    /// <summary>
    /// 카테고리 버튼 색상 업데이트
    /// </summary>
    private void UpdateCategoryButtons()
    {
        if (btnAll != null)
            btnAll.GetComponent<Image>().color = (currentCategory == RecipeCategory.All) ? selectedColor : normalColor;
        if (btnTool != null)
            btnTool.GetComponent<Image>().color = (currentCategory == RecipeCategory.Tool) ? selectedColor : normalColor;
        if (btnBuilding != null)
            btnBuilding.GetComponent<Image>().color = (currentCategory == RecipeCategory.Building) ? selectedColor : normalColor;
        if (btnStorage != null)
            btnStorage.GetComponent<Image>().color = (currentCategory == RecipeCategory.Storage) ? selectedColor : normalColor;
    }

    /// <summary>
    /// 레시피 목록 갱신
    /// </summary>
    public void RefreshRecipes()
    {
        // 기존 슬롯 제거
        foreach (var slot in recipeSlots)
        {
            if (slot != null)
                Destroy(slot);
        }
        recipeSlots.Clear();

        if (CraftingManager.Instance == null)
        {
            Debug.LogError("[CraftingUI] CraftingManager.Instance is null!");
            return;
        }

        // 레시피 가져오기
        var allRecipes = CraftingManager.Instance.GetRecipesByCategory(currentCategory);
        var craftableRecipes = CraftingManager.Instance.GetCraftableRecipes(currentCategory);

        // 제작 가능한 것 먼저 (왼쪽 위)
        foreach (var recipe in craftableRecipes)
        {
            CreateRecipeSlot(recipe, true);
        }

        // 제작 불가능한 것 나중에
        foreach (var recipe in allRecipes)
        {
            if (!craftableRecipes.Contains(recipe))
            {
                CreateRecipeSlot(recipe, false);
            }
        }

        Debug.Log($"[CraftingUI] Displayed {allRecipes.Count} recipes ({craftableRecipes.Count} craftable)");
    }

    /// <summary>
    /// 레시피 슬롯 생성
    /// </summary>
    private void CreateRecipeSlot(CraftingRecipe recipe, bool canCraft)
    {
        if (recipeSlotPrefab == null || recipeGridParent == null)
        {
            Debug.LogError("[CraftingUI] Prefab or parent is null!");
            return;
        }

        GameObject slotObj = Instantiate(recipeSlotPrefab, recipeGridParent);
        RecipeSlot slot = slotObj.GetComponent<RecipeSlot>();

        if (slot != null)
        {
            slot.Setup(recipe, canCraft, this);
            recipeSlots.Add(slotObj);
        }
    }

    /// <summary>
    /// 레시피 툴팁 표시
    /// </summary>
    public void ShowRecipeTooltip(string text)
    {
        if (recipeTooltip != null && tooltipText != null)
        {
            tooltipText.text = text;
            recipeTooltip.SetActive(true);

            // 마우스 위치에 표시
            Vector3 mousePos = Input.mousePosition;
            recipeTooltip.transform.position = mousePos + new Vector3(10f, -10f, 0f);
        }
    }

    /// <summary>
    /// 레시피 툴팁 숨기기
    /// </summary>
    public void HideRecipeTooltip()
    {
        if (recipeTooltip != null)
        {
            recipeTooltip.SetActive(false);
        }
    }

    /// <summary>
    /// 아이템 제작 시도
    /// </summary>
    public void TryCraft(CraftingRecipe recipe)
    {
        if (CraftingManager.Instance.CraftItem(recipe))
        {
            Debug.Log($"[CraftingUI] Successfully crafted!");
            RefreshRecipes(); // 제작 UI 갱신

            // 인벤토리 UI도 갱신
            InventoryUI inventoryUI = FindObjectOfType<InventoryUI>();
            if (inventoryUI != null)
            {
                inventoryUI.RefreshUI();
            }
        }
        else
        {
            Debug.LogWarning($"[CraftingUI] Failed to craft");
        }
    }
}