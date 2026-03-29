using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 색 모듈 관리 UI (인벤토리 + 장착 슬롯)
/// </summary>
public class ColorModuleUI : MonoBehaviour
{
    public static ColorModuleUI Instance { get; private set; }

    [Header("References")]
    public ColorModuleInventory inventory;
    public ColorModuleSlots slots;

    [Header("UI Root")]
    public GameObject uiRoot;
    public KeyCode toggleKey = KeyCode.M;

    [Header("Slot UI (5칸)")]
    public ColorModuleSlotUI[] slotUIs = new ColorModuleSlotUI[5];

    [Header("Inventory List")]
    public Transform inventoryContent;
    public GameObject moduleListItemPrefab;

    [Header("Module Info Panel")]
    public TMP_Text moduleNameText;
    public TMP_Text moduleDescText;
    public TMP_Text moduleTypeText;
    public Image moduleIconImage;

    [Header("Color Count Display")]
    public TMP_Text redCountText;
    public TMP_Text blueCountText;
    public TMP_Text blackCountText;

    private ColorModuleInstance _selectedModule;
    private int _selectedSlot = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (inventory == null) inventory = ColorModuleInventory.Instance;
        if (slots == null) slots = ColorModuleSlots.Instance;

        if (inventory != null)
            inventory.OnInventoryChanged += RefreshInventoryList;
        if (slots != null)
            slots.OnSlotsChanged += RefreshSlots;

        // 초기 상태
        if (uiRoot != null)
            uiRoot.SetActive(false);

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnInventoryChanged -= RefreshInventoryList;
        if (slots != null)
            slots.OnSlotsChanged -= RefreshSlots;
    }

    private void Update()
    {
        // 토글 키
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleUI();
        }
    }

    public void ToggleUI()
    {
        if (uiRoot == null) return;

        bool isActive = uiRoot.activeSelf;
        uiRoot.SetActive(!isActive);

        if (!isActive)
            RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshSlots();
        RefreshInventoryList();
        RefreshColorCounts();
        ClearSelection();
    }

    // ============================================
    // 슬롯 UI
    // ============================================

    private void RefreshSlots()
    {
        if (slots == null) return;

        for (int i = 0; i < slotUIs.Length && i < ColorModuleSlots.MAX_SLOTS; i++)
        {
            if (slotUIs[i] == null) continue;

            var module = slots.GetSlot(i);
            slotUIs[i].SetModule(module);
            slotUIs[i].SetSlotIndex(i);
        }

        RefreshColorCounts();
    }

    public void OnSlotClicked(int slotIndex)
    {
        _selectedSlot = slotIndex;

        var module = slots?.GetSlot(slotIndex);
        if (module != null)
        {
            ShowModuleInfo(module);
        }
        else
        {
            ClearModuleInfo();
        }
    }

    public void OnSlotRightClicked(int slotIndex)
    {
        if (slots == null) return;

        var module = slots.GetSlot(slotIndex);
        if (module == null) return;

        // 인벤토리에 해당 모듈 아이템이 있는지 확인, 없으면 추가
        EnsureModuleInInventory(module.definition);

        // 모듈 해제
        slots.Unequip(slotIndex);
        Debug.Log($"[ColorModuleUI] 슬롯 {slotIndex} 모듈 해제 → 인벤토리로 복귀");
    }

    /// <summary>
    /// 모듈에 대응하는 아이템이 인벤토리에 없으면 추가
    /// (Inspector에서 직접 장착한 경우 대비)
    /// </summary>
    private void EnsureModuleInInventory(ColorModuleDefinition moduleDef)
    {
        if (moduleDef == null) return;

        var db = ItemDatabase.Instance;
        var inv = InventoryManager.Instance;
        if (db == null || inv == null) return;

        ItemData itemData = db.GetItemByColorModule(moduleDef);
        if (itemData == null)
        {
            Debug.LogWarning($"[ColorModuleUI] {moduleDef.moduleName}에 대응하는 ItemData가 없습니다.");
            return;
        }

        // 인벤토리에 이미 있는지 확인
        if (!inv.items.ContainsKey(itemData.itemID))
        {
            // 없으면 추가
            inv.AddItemFromCrafting(itemData.itemID, itemData.itemName, 1);
            Debug.Log($"[ColorModuleUI] {itemData.itemName}을(를) 인벤토리에 추가");
        }
    }

    // ============================================
    // 인벤토리 목록
    // ============================================

    private void RefreshInventoryList()
    {
        if (inventoryContent == null || inventory == null) return;

        // 기존 아이템 제거
        foreach (Transform child in inventoryContent)
        {
            Destroy(child.gameObject);
        }

        // 보유 모듈 표시
        foreach (var module in inventory.OwnedModules)
        {
            CreateInventoryItem(module);
        }
    }

    private void CreateInventoryItem(ColorModuleInstance module)
    {
        if (moduleListItemPrefab == null) return;

        var item = Instantiate(moduleListItemPrefab, inventoryContent);

        // 이름 텍스트
        var nameText = item.GetComponentInChildren<TMP_Text>();
        if (nameText != null)
        {
            string colorTag = module.Color switch
            {
                ColorType.Red => "<color=red>●</color>",
                ColorType.Blue => "<color=blue>●</color>",
                ColorType.Black => "<color=black>●</color>",
                _ => "●"
            };
            string typeTag = module.Type == ModuleType.Skill
    ? $"[{LocalizationManager.T("UI_SKILL")}]"
    : $"[{LocalizationManager.T("UI_PASSIVE")}]";
            string moduleName = !string.IsNullOrEmpty(module.definition?.nameKey)
                ? LocalizationManager.T(module.definition.nameKey) : module.Name;
            nameText.text = $"{colorTag} {moduleName} {typeTag}";
        }

        // 아이콘
        var icon = item.transform.Find("Icon")?.GetComponent<Image>();
        if (icon != null && module.Icon != null)
            icon.sprite = module.Icon;

        // 장착 여부 표시
        bool equipped = slots != null && slots.IsEquipped(module.definition);
        var equippedMark = item.transform.Find("EquippedMark");
        if (equippedMark != null)
            equippedMark.gameObject.SetActive(equipped);

        // 클릭 이벤트
        var button = item.GetComponent<Button>();
        if (button != null)
        {
            var capturedModule = module;
            button.onClick.AddListener(() => OnInventoryItemClicked(capturedModule));
        }
    }

    private void OnInventoryItemClicked(ColorModuleInstance module)
    {
        _selectedModule = module;
        ShowModuleInfo(module);
    }

    // ============================================
    // 모듈 정보 패널
    // ============================================

    private void ShowModuleInfo(ColorModuleInstance module)
    {
        if (module == null) return;

        moduleNameText.text = !string.IsNullOrEmpty(module.definition?.nameKey)
    ? LocalizationManager.T(module.definition.nameKey) : module.Name;
        moduleDescText.text = !string.IsNullOrEmpty(module.definition?.descKey)
            ? LocalizationManager.T(module.definition.descKey)
            : (module.definition?.description ?? "");

        if (moduleTypeText != null)
        {
            string colorName = module.Color switch
            {
                ColorType.Red => LocalizationManager.T("UI_COLOR_RED"),
                ColorType.Blue => LocalizationManager.T("UI_COLOR_BLUE"),
                ColorType.Black => LocalizationManager.T("UI_COLOR_BLACK"),
                _ => "?"
            };
            string typeName = module.Type == ModuleType.Skill
                ? $"{LocalizationManager.T("UI_SKILL")} (AP {module.APCost})"
                : LocalizationManager.T("UI_PASSIVE");
            moduleTypeText.text = $"{colorName} {typeName}";
        }

        if (moduleIconImage != null)
        {
            if (module.Icon != null)
            {
                moduleIconImage.sprite = module.Icon;
                moduleIconImage.enabled = true;
            }
            else
            {
                moduleIconImage.enabled = false;
            }
        }
    }

    private void ClearModuleInfo()
    {
        if (moduleNameText != null) moduleNameText.text = "";
        if (moduleDescText != null) moduleDescText.text = "";
        if (moduleTypeText != null) moduleTypeText.text = "";
        if (moduleIconImage != null) moduleIconImage.enabled = false;
    }

    private void ClearSelection()
    {
        _selectedModule = null;
        _selectedSlot = -1;
        ClearModuleInfo();
    }

    // ============================================
    // 장착 버튼
    // ============================================

    /// <summary>
    /// 선택한 모듈을 특정 슬롯에 장착
    /// </summary>
    public void EquipSelectedToSlot(int slotIndex)
    {
        if (_selectedModule == null || slots == null) return;

        slots.Equip(slotIndex, _selectedModule);
        RefreshAll();
    }

    /// <summary>
    /// 선택한 슬롯 해제
    /// </summary>
    public void UnequipSelectedSlot()
    {
        if (_selectedSlot < 0 || slots == null) return;

        var module = slots.GetSlot(_selectedSlot);
        if (module != null)
            EnsureModuleInInventory(module.definition);

        slots.Unequip(_selectedSlot);
        RefreshAll();
    }

    // ============================================
    // 색상 개수 표시
    // ============================================

    private void RefreshColorCounts()
    {
        if (slots == null) return;

        int red = slots.GetColorCount(ColorType.Red);
        int blue = slots.GetColorCount(ColorType.Blue);
        int black = slots.GetColorCount(ColorType.Black);

        if (redCountText != null)
            redCountText.text = $"{LocalizationManager.T("UI_COLOR_RED")}: {red}";

        if (blueCountText != null)
            blueCountText.text = $"{LocalizationManager.T("UI_COLOR_BLUE")}: {blue}";

        if (blackCountText != null)
            blackCountText.text = $"{LocalizationManager.T("UI_COLOR_BLACK")}: {black}";
    }
}