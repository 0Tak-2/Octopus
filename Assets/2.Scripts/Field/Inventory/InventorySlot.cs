using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// ?????? ???? (????J ???? ????)
/// ?????: ?? ???? ???
/// ?????: ?????? ???/????/???? ???
/// ?????: ??? ???? ?????????? ???????? ???/???
/// </summary>
public class InventorySlot : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
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
    private int _slotIndex = -1;

    public int SlotIndex => _slotIndex;

    public void Setup(InventoryItem item, InventoryUI ui, int slotIndex)
    {
        this.item = item;
        this.inventoryUI = ui;
        _slotIndex = slotIndex;

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

    public void SetupEmpty(InventoryUI ui, int slotIndex)
    {
        this.item = null;
        this.inventoryUI = ui;
        _slotIndex = slotIndex;

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
        // eligibleForClick: ?????? ????? ?????? ????? false (OnEndDrag???? OnPointerClick?? ???? ?? ?? ??? _dragging ??????? ??????)
        if (!eventData.eligibleForClick) return;
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
    /// ?????? ???? ???? ??? ??? ????/???? ???
    /// </summary>
    private void TryUseOrEquip()
    {
        if (item == null) return;

        ItemData itemData = null;
        if (ItemDatabase.Instance != null)
            itemData = ItemDatabase.Instance.GetItemData(item.itemID);

        if (itemData == null)
        {
            Debug.LogWarning($"[InventorySlot] ItemData?? ??? ?? ????: ID={item.itemID}");
            return;
        }

        // === ????: ????? ??? ===
        if (itemData.itemType == ItemType.Food)
        {
            UseFood(itemData);
            return;
        }

        // === ??? ??????: HP ??? ===
        if (itemData.itemType == ItemType.Consumable)
        {
            UseConsumable(itemData);
            return;
        }

        // === ???: ????/???? ??? ===
        if (itemData.IsEquipable)
        {
            ToggleEquipItem(itemData);
            return;
        }

        // === ?? ???: ????/???? ??? ===
        if (itemData.IsColorModule)
        {
            ToggleColorModule(itemData);
            return;
        }

        Debug.Log($"[InventorySlot] {item.itemName}??(??) ????? ?? ???????.");
    }

    // ============================================
    // ???? ???
    // ============================================
    private void UseFood(ItemData itemData)
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        if (stats.hunger >= stats.maxHunger)
        {
            Debug.Log("[InventorySlot] ??????? ??? ???? ??????.");
            return;
        }

        stats.hunger = Mathf.Min(stats.maxHunger, stats.hunger + itemData.hungerRestore);

        if (itemData.fatigueRestore > 0)
            stats.fatigue = Mathf.Min(stats.maxFatigue, stats.fatigue + itemData.fatigueRestore);

        stats.ClampAll();

        InventoryManager.Instance?.RemoveItem(item.itemID, 1);
        Debug.Log($"[InventorySlot] {item.itemName} ???! ????? +{itemData.hungerRestore}");
        inventoryUI?.RefreshUI();
    }

    // ============================================
    // ??? ?????? ??? (HP ???)
    // ============================================
    private void UseConsumable(ItemData itemData)
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        if (itemData.hpRestore > 0)
        {
            if (stats.hp >= stats.maxHP)
            {
                Debug.Log("[InventorySlot] HP?? ??? ???? ??????.");
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
        Debug.Log($"[InventorySlot] {item.itemName} ???! HP +{itemData.hpRestore}");
        inventoryUI?.RefreshUI();
    }

    // ============================================
    // ??? ????: ?????????? ???? ?? ??? ???????? ???
    // ============================================
    private void ToggleEquipItem(ItemData itemData)
    {
        EquipmentManager equipMgr = EquipmentManager.Instance;
        if (equipMgr == null)
        {
            Debug.LogWarning("[InventorySlot] EquipmentManager?? ??? ?? ???????.");
            return;
        }

        // ?? ????? ????
        bool success = equipMgr.AutoEquip(itemData.equipmentDefinition);
        if (success)
        {
            // ?????????? ????
            InventoryManager.Instance?.RemoveItem(item.itemID, 1);
            Debug.Log($"[InventorySlot] {item.itemName} ????! (?????? ?? ??? ????)");
        }
        else
        {
            Debug.Log("[InventorySlot] ?? ??? ?????? ???????!");
        }

        inventoryUI?.RefreshUI();
    }

    // ============================================
    // ?? ??? ????: ?????????? ???? ?? ??? ???????? ???
    // ============================================
    private void ToggleColorModule(ItemData itemData)
    {
        ColorModuleSlots moduleSlots = ColorModuleSlots.Instance;
        if (moduleSlots == null)
        {
            Debug.LogWarning("[InventorySlot] ColorModuleSlots?? ??? ?? ???????.");
            return;
        }

        // ?? ????? ????
        ColorModuleInstance moduleInstance = new ColorModuleInstance(itemData.colorModuleDefinition);
        bool equipped = false;

        for (int i = 0; i < ColorModuleSlots.MAX_SLOTS; i++)
        {
            if (moduleSlots.GetSlot(i) == null)
            {
                moduleSlots.Equip(i, moduleInstance);
                // ?????????? ????
                InventoryManager.Instance?.RemoveItem(item.itemID, 1);
                Debug.Log($"[InventorySlot] {item.itemName} ??? ????! (?????? ?? ???? {i})");
                equipped = true;
                break;
            }
        }

        if (!equipped)
            Debug.Log("[InventorySlot] ?? ??? ?????? ???????! (??? 5??)");

        inventoryUI?.RefreshUI();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (item == null || _slotIndex < 0) return;
        InventoryDragSession.BeginInventory(_slotIndex);

        if (iconImage != null && iconImage.sprite != null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                GameObject g = new GameObject("InvDragGhost");
                g.transform.SetParent(c.transform, false);
                g.transform.SetAsLastSibling();
                var img = g.AddComponent<Image>();
                img.sprite = iconImage.sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                var rt = g.GetComponent<RectTransform>();
                var src = iconImage.rectTransform;
                rt.sizeDelta = src.rect.size;
                InventoryDragSession.DragGhost = g;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InventoryDragSession.DragGhost != null)
            InventoryDragSession.DragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragSession.Clear();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_slotIndex < 0) return;
        if (InventoryDragResolver.TryDropOnInventorySlot(_slotIndex))
            InventoryDragSession.Clear();
    }
}