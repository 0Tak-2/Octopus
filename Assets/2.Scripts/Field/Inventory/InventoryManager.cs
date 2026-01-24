using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 인벤토리 시스템
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory")]
    public Dictionary<int, InventoryItem> items = new Dictionary<int, InventoryItem>();
    public int maxInventorySize = 20; // 인벤토리 최대 크기

    [Header("UI")]
    public GameObject inventoryUI;
    public bool isInventoryOpen = false;

    [Header("Pickup Text")]
    public GameObject pickupTextPrefab; // 플로팅 텍스트 프리팹

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
        // UI 숨기기
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
            isInventoryOpen = false; // ← 이거 추가!
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
        // 인벤토리 가득 참 체크
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

        // ✅ 플로팅 텍스트 표시
        ShowPickupText(itemName, count, playerPosition);

        // ✅ 인벤토리가 열려있으면 즉시 갱신
        if (isInventoryOpen)
        {
            UpdateInventoryUI();
        }
        // 닫혀있으면 다음에 열 때 갱신됨 (OpenInventory에서)
    }

    /// <summary>
    /// 플로팅 텍스트 표시
    /// </summary>
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

    /// <summary>
    /// 인벤토리 열기
    /// </summary>
    public void OpenInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(true);
            isInventoryOpen = true;

            // ✅ 열 때마다 UI 갱신 (밀린 아이템 표시)
            UpdateInventoryUI();
        }
    }

    /// <summary>
    /// 인벤토리 닫기
    /// </summary>
    public void CloseInventory()
    {
        if (inventoryUI != null)
        {
            inventoryUI.SetActive(false);
            isInventoryOpen = false;
        }
    }

    /// <summary>
    /// 인벤토리 토글
    /// </summary>
    public void ToggleInventory()
    {
        if (isInventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    /// <summary>
    /// UI 갱신
    /// </summary>
    private void UpdateInventoryUI()
    {
        InventoryUI ui = FindObjectOfType<InventoryUI>();
        if (ui != null)
        {
            ui.RefreshUI();
        }
    }

    /// <summary>
    /// 아이템 개수 가져오기
    /// </summary>
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
    public Sprite icon; // 아이콘
    public string description; // 설명
}