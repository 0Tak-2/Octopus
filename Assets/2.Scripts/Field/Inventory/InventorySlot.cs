using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 슬롯 (마우스 이벤트 포함)
/// 좌클릭: 상세 정보 표시
/// 우클릭: 아이템 사용/장착/해제 토글
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.3f, 1f);
    public Color hoverColor = new Color(0.3f, 0.3f, 0.4f, 1f);
    public Color emptyColor = new Color(0.1f, 0.1f, 0.15f, 0.5f);

    private InventoryItem item;
    private InventoryUI inventoryUI;

    public void Setup(InventoryItem item, InventoryUI ui)
    {
        this.item = item;
        this.inventoryUI = ui;

        if (iconImage != null)
        {
            if (item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        if (countText != null)
        {
            if (item.count > 1)
            {
                countText.text = item.count.ToString();
                countText.enabled = true;
            }
            else
            {
                countText.enabled = false;
            }
        }

        if (background != null)
        {
            background.color = normalColor;
        }
    }

    public void SetupEmpty(InventoryUI ui)
    {
        this.item = null;
        this.inventoryUI = ui;

        if (iconImage != null)
            iconImage.enabled = false;

        if (countText != null)
            countText.enabled = false;

        if (background != null)
            background.color = emptyColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item == null) return;

        if (background != null)
            background.color = hoverColor;

        var itemData = ItemDatabase.Instance?.GetItemData(item.itemID);
        string displayName = (itemData != null && !string.IsNullOrEmpty(itemData.nameKey))
            ? LocalizationManager.T(itemData.nameKey)
            : item.itemName;
        inventoryUI.ShowTooltip(displayName, transform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (item == null) return;

        if (background != null)
            background.color = normalColor;

        if (inventoryUI != null)
            inventoryUI.HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (item == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (inventoryUI != null)
                inventoryUI.ShowDetail(item);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            TryUseOrEquip();
        }
    }

    /// <summary>
    /// 아이템 타입에 따라 사용 또는 장착/해제 토글
    /// </summary>
    private void TryUseOrEquip()
    {
        if (item == null) return;

        ItemData itemData = null;
        if (ItemDatabase.Instance != null)
            itemData = ItemDatabase.Instance.GetItemData(item.itemID);

        if (itemData == null)
        {
            Debug.LogWarning($"[InventorySlot] ItemData를 찾을 수 없음: ID={item.itemID}");
            return;
        }

        // === 음식: 배고픔 회복 ===
        if (itemData.itemType == ItemType.Food)
        {
            UseFood(itemData);
            return;
        }

        // === 소비 아이템: HP 회복 ===
        if (itemData.itemType == ItemType.Consumable)
        {
            UseConsumable(itemData);
            return;
        }

        // === 장비: 장착/해제 토글 ===
        if (itemData.IsEquipable)
        {
            ToggleEquipItem(itemData);
            return;
        }

        // === 색 모듈: 장착/해제 토글 ===
        if (itemData.IsColorModule)
        {
            ToggleColorModule(itemData);
            return;
        }

        Debug.Log($"[InventorySlot] {item.itemName}은(는) 사용할 수 없습니다.");
    }

    // ============================================
    // 음식 사용
    // ============================================
    private void UseFood(ItemData itemData)
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        if (stats.hunger >= stats.maxHunger)
        {
            Debug.Log("[InventorySlot] 배고픔이 이미 가득 찼습니다.");
            return;
        }

        stats.hunger = Mathf.Min(stats.maxHunger, stats.hunger + itemData.hungerRestore);

        if (itemData.fatigueRestore > 0)
            stats.fatigue = Mathf.Min(stats.maxFatigue, stats.fatigue + itemData.fatigueRestore);

        stats.ClampAll();

        InventoryManager.Instance?.RemoveItem(item.itemID, 1);
        Debug.Log($"[InventorySlot] {item.itemName} 사용! 배고픔 +{itemData.hungerRestore}");
        inventoryUI?.RefreshUI();
    }

    // ============================================
    // 소비 아이템 사용 (HP 회복)
    // ============================================
    private void UseConsumable(ItemData itemData)
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        if (itemData.hpRestore > 0)
        {
            if (stats.hp >= stats.maxHP)
            {
                Debug.Log("[InventorySlot] HP가 이미 가득 찼습니다.");
                return;
            }
            stats.Heal(itemData.hpRestore);
        }

        if (itemData.fatigueRestore > 0)
            stats.fatigue = Mathf.Min(stats.maxFatigue, stats.fatigue + itemData.fatigueRestore);

        if (itemData.hungerRestore > 0)
            stats.hunger = Mathf.Min(stats.maxHunger, stats.hunger + itemData.hungerRestore);

        stats.ClampAll();

        InventoryManager.Instance?.RemoveItem(item.itemID, 1);
        Debug.Log($"[InventorySlot] {item.itemName} 사용! HP +{itemData.hpRestore}");
        inventoryUI?.RefreshUI();
    }

    // ============================================
    // 장비 장착: 인벤토리에서 제거 → 장비 슬롯으로 이동
    // ============================================
    private void ToggleEquipItem(ItemData itemData)
    {
        EquipmentManager equipMgr = EquipmentManager.Instance;
        if (equipMgr == null)
        {
            Debug.LogWarning("[InventorySlot] EquipmentManager를 찾을 수 없습니다.");
            return;
        }

        // 빈 슬롯에 장착
        bool success = equipMgr.AutoEquip(itemData.equipmentDefinition);
        if (success)
        {
            // 인벤토리에서 제거
            InventoryManager.Instance?.RemoveItem(item.itemID, 1);
            Debug.Log($"[InventorySlot] {item.itemName} 장착! (인벤토리 → 장비 슬롯)");
        }
        else
        {
            Debug.Log("[InventorySlot] 빈 장비 슬롯이 없습니다!");
        }

        inventoryUI?.RefreshUI();
    }

    // ============================================
    // 색 모듈 장착: 인벤토리에서 제거 → 모듈 슬롯으로 이동
    // ============================================
    private void ToggleColorModule(ItemData itemData)
    {
        ColorModuleSlots moduleSlots = ColorModuleSlots.Instance;
        if (moduleSlots == null)
        {
            Debug.LogWarning("[InventorySlot] ColorModuleSlots를 찾을 수 없습니다.");
            return;
        }

        // 빈 슬롯에 장착
        ColorModuleInstance moduleInstance = new ColorModuleInstance(itemData.colorModuleDefinition);
        bool equipped = false;

        for (int i = 0; i < ColorModuleSlots.MAX_SLOTS; i++)
        {
            if (moduleSlots.GetSlot(i) == null)
            {
                moduleSlots.Equip(i, moduleInstance);
                // 인벤토리에서 제거
                InventoryManager.Instance?.RemoveItem(item.itemID, 1);
                Debug.Log($"[InventorySlot] {item.itemName} 모듈 장착! (인벤토리 → 슬롯 {i})");
                equipped = true;
                break;
            }
        }

        if (!equipped)
            Debug.Log("[InventorySlot] 빈 모듈 슬롯이 없습니다! (최대 5개)");

        inventoryUI?.RefreshUI();
    }
}