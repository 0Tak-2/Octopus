using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// �κ��丮 UI (���� ����)
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Transform slotsParent; // ���� �׸��� �θ�
    public GameObject itemSlotPrefab; // ������ ���� ������

    [Header("�� ���� �г� (����)")]
    public GameObject detailPanel;
    public TextMeshProUGUI detailItemName;
    public TextMeshProUGUI detailDescription;
    public Image detailIcon;

    [Header("���� (����)")]
    public GameObject tooltip;
    public TextMeshProUGUI tooltipText;

    [Header("���� UI (����)")]
    public CraftingUI craftingUI;
    public Button craftButton; // ���� ��ư

    private List<InventorySlot> itemSlots = new List<InventorySlot>();

    private void Start()
    {
        if (detailPanel != null)
            detailPanel.SetActive(false);

        if (tooltip != null)
            tooltip.SetActive(false);

        // ���� ��ư �ڵ� ����
        if (craftButton != null)
        {
            craftButton.onClick.RemoveAllListeners();
            craftButton.onClick.AddListener(OpenCrafting);
            Debug.Log("[InventoryUI] Craft button connected!");
        }
    }

    public void RefreshUI()
    {
        Debug.Log("[InventoryUI] RefreshUI called!");

        // ���� ���� ����
        foreach (var slot in itemSlots)
        {
            if (slot != null && slot.gameObject != null)
                Destroy(slot.gameObject);
        }
        itemSlots.Clear();

        InventoryManager inventory = InventoryManager.Instance;
        if (inventory == null)
        {
            Debug.LogError("[InventoryUI] InventoryManager.Instance is null!");
            return;
        }

        inventory.RebuildSlotsFromDictionaryIfEmpty();
        Debug.Log($"[InventoryUI] Item count: {inventory.items.Count}");

        for (int i = 0; i < inventory.maxInventorySize; i++)
        {
            int id = inventory.GetSlotItemId(i);
            if (id != 0 && inventory.items.TryGetValue(id, out var invItem))
            {
                Debug.Log($"[InventoryUI] Creating slot {i} for {invItem.itemName} x{invItem.count}");
                CreateItemSlot(invItem, i);
            }
            else
                CreateEmptySlot(i);
        }
    }

    private void CreateItemSlot(InventoryItem item, int slotIndex)
    {
        if (itemSlotPrefab == null || slotsParent == null)
        {
            Debug.LogError("[InventoryUI] itemSlotPrefab or slotsParent is null!");
            return;
        }

        GameObject slotObj = Instantiate(itemSlotPrefab, slotsParent);
        InventorySlot slot = slotObj.GetComponent<InventorySlot>();

        if (slot != null)
        {
            slot.Setup(item, this, slotIndex);
            itemSlots.Add(slot);
            Debug.Log($"[InventoryUI] Slot created for {item.itemName}");
        }
        else
        {
            Debug.LogError("[InventoryUI] InventorySlot component not found!");
        }
    }

    private void CreateEmptySlot(int slotIndex)
    {
        if (itemSlotPrefab == null || slotsParent == null) return;

        GameObject slotObj = Instantiate(itemSlotPrefab, slotsParent);
        InventorySlot slot = slotObj.GetComponent<InventorySlot>();

        if (slot != null)
        {
            slot.SetupEmpty(this, slotIndex);
            itemSlots.Add(slot);
        }
    }

    public void ShowDetail(InventoryItem item)
    {
        if (detailPanel == null) return;

        detailPanel.SetActive(true);

        var itemData = ItemDatabase.Instance?.GetItemData(item.itemID);
        if (itemData != null && !string.IsNullOrEmpty(itemData.nameKey))
        {
            detailItemName.text = LocalizationManager.T(itemData.nameKey);
            detailDescription.text = LocalizationManager.T(itemData.descKey);
        }
        else
        {
            detailItemName.text = item.itemName;       // ����
            detailDescription.text = item.description; // ����
        }

        if (detailIcon != null && item.icon != null)
        {
            detailIcon.sprite = item.icon;
            detailIcon.enabled = true;
        }

        Debug.Log($"[InventoryUI] Showing detail for {item.itemName}");
    }

    public void HideDetail()
    {
        if (detailPanel != null)
            detailPanel.SetActive(false);
    }

    public void ShowTooltip(string text, Vector3 position)
    {
        if (tooltip == null || tooltipText == null) return;

        tooltip.SetActive(true);
        tooltipText.text = text;

        // ���콺 ������ ��ġ (���� ���� �ƴ�!)
        Vector3 mousePos = Input.mousePosition;
        tooltip.transform.position = mousePos + new Vector3(10f, 10f, 0f);
    }

    public void HideTooltip()
    {
        if (tooltip != null)
            tooltip.SetActive(false);
    }

    /// <summary>
    /// ���� UI ����
    /// </summary>
    public void OpenCrafting()
    {
        Debug.Log("[InventoryUI] OpenCrafting called!");

        if (craftingUI != null)
        {
            Debug.Log("[InventoryUI] CraftingUI found, opening...");
            craftingUI.OpenCrafting();
        }
        else
        {
            Debug.LogWarning("[InventoryUI] CraftingUI is null!");
        }
    }
}