using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 시스템 (개선 버전)
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory")]
    public Dictionary<int, InventoryItem> items = new Dictionary<int, InventoryItem>();
    public int maxInventorySize = 20; // 최대 크기 (Inspector에서 조절)

    [Header("UI")]
    public GameObject inventoryUI;
    public bool isInventoryOpen = false;

    [Header("Pickup Text")]
    public GameObject pickupTextPrefab;

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
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
            isInventoryOpen = false;
        }
    }

    private void Update()
    {
        // TAB키로 인벤토리 토글
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
    }

    /// <summary>
    /// 아이템 추가
    /// </summary>
    public void AddItem(int itemID, string itemName, int count, Vector3 playerPosition)
    {
        AddItemInternal(itemID, itemName, count, playerPosition, true);
    }

    /// <summary>
    /// 아이템 추가 (제작용 - 플로팅 텍스트 없음)
    /// </summary>
    public void AddItemFromCrafting(int itemID, string itemName, int count)
    {
        AddItemInternal(itemID, itemName, count, Vector3.zero, false);
    }

    /// <summary>
    /// 아이템 추가 내부 구현
    /// </summary>
    private void AddItemInternal(int itemID, string itemName, int count, Vector3 playerPosition, bool showFloatingText)
    {
        // 가득 참 체크
        if (items.Count >= maxInventorySize && !items.ContainsKey(itemID))
        {
            Debug.LogWarning("[Inventory] Inventory is full!");
            return;
        }

        // 아이템 데이터 가져오기
        ItemData itemData = null;
        if (ItemDatabase.Instance != null)
        {
            itemData = ItemDatabase.Instance.GetItemData(itemID);
        }

        if (items.ContainsKey(itemID))
        {
            // 이미 있으면 개수 증가
            items[itemID].count += count;
        }
        else
        {
            // 새로운 아이템
            items[itemID] = new InventoryItem
            {
                itemID = itemID,
                itemName = itemData != null ? itemData.itemName : itemName,
                count = count,
                icon = itemData != null ? itemData.icon : null,
                description = itemData != null ? itemData.description : ""
            };
        }

        Debug.Log($"[Inventory] Added {itemName} x{count}. Total: {items[itemID].count}");

        // 플로팅 텍스트 표시 (선택적)
        if (showFloatingText)
        {
            ShowPickupText(itemName, count, playerPosition);
        }

        // 인벤토리가 열려있으면 UI 갱신
        if (isInventoryOpen)
        {
            UpdateInventoryUI();
        }
    }

    private void ShowPickupText(string itemName, int count, Vector3 position)
    {
        if (pickupTextPrefab == null) return;

        GameObject textObj = Instantiate(pickupTextPrefab, position, Quaternion.identity);
        ItemPickupText pickupText = textObj.GetComponent<ItemPickupText>();

        if (pickupText != null)
        {
            pickupText.Setup(itemName, count, position);
        }
    }

    public void OpenInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(true);
            isInventoryOpen = true;
            UpdateInventoryUI();
        }
    }

    public void CloseInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
            isInventoryOpen = false;
        }

        // 제작창도 함께 닫기
        CraftingUI craftingUI = FindObjectOfType<CraftingUI>();
        if (craftingUI != null)
        {
            craftingUI.CloseCrafting();
        }
    }

    public void ToggleInventory()
    {
        if (isInventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    private void UpdateInventoryUI()
    {
        // 인벤토리 UI 갱신
        InventoryUI ui = FindObjectOfType<InventoryUI>();
        if (ui != null)
        {
            ui.RefreshUI();
        }

        // 제작 UI도 갱신 (열려있으면)
        CraftingUI craftingUI = FindObjectOfType<CraftingUI>();
        if (craftingUI != null && craftingUI.IsOpen())
        {
            craftingUI.RefreshRecipes();
        }
    }

    /// <summary>
    /// 아이템 제거
    /// </summary>
    public bool RemoveItem(int itemID, int count)
    {
        if (!items.ContainsKey(itemID))
        {
            Debug.LogWarning($"[Inventory] Cannot remove - item {itemID} not found");
            return false;
        }

        if (items[itemID].count < count)
        {
            Debug.LogWarning($"[Inventory] Cannot remove - not enough items");
            return false;
        }

        items[itemID].count -= count;

        // 0개가 되면 삭제
        if (items[itemID].count <= 0)
        {
            items.Remove(itemID);
        }

        Debug.Log($"[Inventory] Removed item {itemID} x{count}");

        // UI 갱신
        if (isInventoryOpen)
        {
            UpdateInventoryUI();
        }

        return true;
    }

    public int GetItemCount(int itemID)
    {
        if (items.ContainsKey(itemID))
            return items[itemID].count;
        return 0;
    }
}

/// <summary>
/// 인벤토리 아이템 데이터
/// </summary>
[System.Serializable]
public class InventoryItem
{
    public int itemID;
    public string itemName;
    public int count;
    public Sprite icon;
    public string description;
}