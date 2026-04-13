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

    /// <summary>칸별 아이템 ID (0 = 빈 칸). 아이템 종류당 한 칸에만 스택.</summary>
    [SerializeField] private int[] slotItemIds;

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

        // ✅ 씬 전환해도 유지
        DontDestroyOnLoad(gameObject);
        EnsureSlotArray();
    }

    private void EnsureSlotArray()
    {
        if (slotItemIds == null || slotItemIds.Length != maxInventorySize)
            slotItemIds = new int[maxInventorySize];
    }

    public int GetSlotItemId(int index)
    {
        EnsureSlotArray();
        if (index < 0 || index >= maxInventorySize) return 0;
        return slotItemIds[index];
    }

    /// <summary>슬롯에 표시된 아이템이 없으면 null</summary>
    public InventoryItem GetItemInSlot(int index)
    {
        int id = GetSlotItemId(index);
        if (id == 0 || !items.TryGetValue(id, out var it)) return null;
        return it;
    }

    private int FindFirstEmptySlot()
    {
        EnsureSlotArray();
        for (int i = 0; i < maxInventorySize; i++)
        {
            if (slotItemIds[i] == 0) return i;
        }
        return -1;
    }

    private int FindSlotIndexContainingItem(int itemID)
    {
        EnsureSlotArray();
        for (int i = 0; i < maxInventorySize; i++)
        {
            if (slotItemIds[i] == itemID) return i;
        }
        return -1;
    }

    private void ClearSlotIndex(int index)
    {
        EnsureSlotArray();
        if (index >= 0 && index < maxInventorySize)
            slotItemIds[index] = 0;
    }

    /// <summary>세이브 복원용: items에 이미 들어간 뒤 칸만 연결</summary>
    public void ForceAssignSlot(int slotIndex, int itemID)
    {
        EnsureSlotArray();
        if (slotIndex < 0 || slotIndex >= maxInventorySize) return;
        if (itemID != 0 && !items.ContainsKey(itemID)) return;
        slotItemIds[slotIndex] = itemID;
    }

    /// <summary>딕셔너리만 있고 슬롯이 비어 있을 때(구 세이브 등) 순서 복구</summary>
    public void RebuildSlotsFromDictionaryIfEmpty()
    {
        EnsureSlotArray();
        bool any = false;
        for (int i = 0; i < maxInventorySize; i++)
        {
            if (slotItemIds[i] != 0) { any = true; break; }
        }
        if (any) return;

        int s = 0;
        foreach (var kvp in items)
        {
            while (s < maxInventorySize && slotItemIds[s] != 0) s++;
            if (s >= maxInventorySize) break;
            slotItemIds[s++] = kvp.Key;
        }
    }

    public void SwapSlots(int indexA, int indexB)
    {
        EnsureSlotArray();
        if (indexA < 0 || indexA >= maxInventorySize || indexB < 0 || indexB >= maxInventorySize) return;
        if (indexA == indexB) return;
        int t = slotItemIds[indexA];
        slotItemIds[indexA] = slotItemIds[indexB];
        slotItemIds[indexB] = t;
    }

    /// <summary>빈 칸이면 그대로 두고, 같은 아이템이면 스택 합침</summary>
    public bool AddItemToSlot(int slotIndex, int itemID, string itemName, int count)
    {
        EnsureSlotArray();
        if (slotIndex < 0 || slotIndex >= maxInventorySize) return false;
        if (count <= 0) return false;

        ItemData itemData = ItemDatabase.Instance != null ? ItemDatabase.Instance.GetItemData(itemID) : null;

        int existing = slotItemIds[slotIndex];
        if (existing == 0)
        {
            if (items.Count >= maxInventorySize && !items.ContainsKey(itemID))
            {
                Debug.LogWarning("[Inventory] Inventory is full!");
                return false;
            }
            if (items.ContainsKey(itemID))
            {
                items[itemID].count += count;
            }
            else
            {
                items[itemID] = new InventoryItem
                {
                    itemID = itemID,
                    itemName = itemData != null ? itemData.itemName : itemName,
                    count = count,
                    icon = itemData != null ? itemData.icon : null,
                    description = itemData != null ? itemData.description : ""
                };
            }
            slotItemIds[slotIndex] = itemID;
            return true;
        }

        if (existing == itemID)
        {
            if (!items.ContainsKey(itemID)) return false;
            items[itemID].count += count;
            return true;
        }

        return false;
    }

    /// <summary>특정 칸에서 개수만큼 제거. 0이 되면 칸 비움.</summary>
    public bool RemoveItemFromSlot(int slotIndex, int count)
    {
        EnsureSlotArray();
        if (slotIndex < 0 || slotIndex >= maxInventorySize || count <= 0) return false;
        int itemID = slotItemIds[slotIndex];
        if (itemID == 0 || !items.ContainsKey(itemID)) return false;

        items[itemID].count -= count;
        if (items[itemID].count <= 0)
        {
            items.Remove(itemID);
            slotItemIds[slotIndex] = 0;
        }
        return true;
    }

    /// <summary>세이브 복원용: 인벤 초기화 후 호출</summary>
    public void ClearAllItemsAndSlots()
    {
        items.Clear();
        EnsureSlotArray();
        for (int i = 0; i < maxInventorySize; i++)
            slotItemIds[i] = 0;
    }

    private void Start()
    {
        // UI 참조 찾기 (씬 전환 후 다시 찾아야 함)
        FindInventoryUI();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // ✅ 씬 로드 후 UI 다시 찾기 (딜레이 필요)
        StartCoroutine(FindInventoryUIDelayed());
    }

    private System.Collections.IEnumerator FindInventoryUIDelayed()
    {
        // 씬 오브젝트가 활성화될 때까지 대기
        yield return null;
        yield return null;

        FindInventoryUI();

        // 인벤토리 닫힌 상태로 시작
        isInventoryOpen = false;
    }

    /// <summary>
    /// 씬에서 인벤토리 UI 찾기
    /// </summary>
    private void FindInventoryUI()
    {
        // 기존 참조 초기화 (씬 전환 시 이전 씬 오브젝트 참조 제거)
        inventoryUI = null;

        // 1. 이름으로 찾기 (활성/비활성 모두)
        GameObject found = GameObject.Find("InventoryUI");

        // 2. 못 찾으면 비활성화된 것도 찾기
        if (found == null)
        {
            // Canvas 아래에서 찾기
            Canvas[] canvases = FindObjectsOfType<Canvas>(true);
            foreach (var canvas in canvases)
            {
                Transform child = canvas.transform.Find("InventoryUI");
                if (child != null)
                {
                    found = child.gameObject;
                    break;
                }
            }
        }

        // 3. InventoryUI 컴포넌트로 찾기
        if (found == null)
        {
            InventoryUI ui = FindObjectOfType<InventoryUI>(true);
            if (ui != null)
                found = ui.gameObject;
        }

        if (found != null)
        {
            inventoryUI = found;
            inventoryUI.SetActive(false);
            Debug.Log($"[InventoryManager] UI 찾음: {found.name}");
        }
        else
        {
            Debug.LogWarning("[InventoryManager] InventoryUI를 찾을 수 없습니다!");
        }
    }

    private void Update()
    {
        // TAB키로 통합 UI 토글 (인벤토리 + 장비 + 색상 모듈)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleUnifiedUI();
        }
    }

    /// <summary>
    /// Tab으로 인벤토리/장비/색상모듈 UI를 한꺼번에 토글.
    /// 인벤토리의 현재 상태(isInventoryOpen)를 기준으로 세 패널을 같은 상태로 맞춤.
    /// </summary>
    private void ToggleUnifiedUI()
    {
        bool targetOpen = !isInventoryOpen;

        // 1. 인벤토리
        if (targetOpen)
            OpenInventory();
        else
            CloseInventory();

        // 2. 장비창 - 현재 상태가 다르면 토글해서 동기화
        if (EquipmentUI.Instance != null && EquipmentUI.Instance.uiRoot != null)
        {
            bool equipOpen = EquipmentUI.Instance.uiRoot.activeSelf;
            if (equipOpen != targetOpen)
                EquipmentUI.Instance.ToggleUI();
        }

        // 3. 색상 모듈창 - 현재 상태가 다르면 토글해서 동기화
        if (ColorModuleUI.Instance != null && ColorModuleUI.Instance.uiRoot != null)
        {
            bool moduleOpen = ColorModuleUI.Instance.uiRoot.activeSelf;
            if (moduleOpen != targetOpen)
                ColorModuleUI.Instance.ToggleUI();
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
        EnsureSlotArray();

        // 아이템 데이터 가져오기
        ItemData itemData = null;
        if (ItemDatabase.Instance != null)
        {
            itemData = ItemDatabase.Instance.GetItemData(itemID);
        }

        if (items.ContainsKey(itemID))
        {
            // 이미 있으면 개수 증가 (같은 칸 스택)
            items[itemID].count += count;
        }
        else
        {
            // 가득 참: 새 종류 추가 불가
            if (items.Count >= maxInventorySize)
            {
                Debug.LogWarning("[Inventory] Inventory is full!");
                return;
            }

            int empty = FindFirstEmptySlot();
            if (empty < 0)
            {
                Debug.LogWarning("[Inventory] No empty slot!");
                return;
            }

            items[itemID] = new InventoryItem
            {
                itemID = itemID,
                itemName = itemData != null ? itemData.itemName : itemName,
                count = count,
                icon = itemData != null ? itemData.icon : null,
                description = itemData != null ? itemData.description : ""
            };
            slotItemIds[empty] = itemID;
        }

        Debug.Log($"[Inventory] Added {itemName} x{count}. Total: {items[itemID].count}");

        // 플로팅 텍스트 표시 (선택적)
        if (showFloatingText)
        {
            var data = ItemDatabase.Instance?.GetItemData(itemID);
            string localizedName = (data != null && !string.IsNullOrEmpty(data.nameKey))
                ? LocalizationManager.T(data.nameKey)
                : itemName;
            ShowPickupText(localizedName, count, playerPosition);
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
            EnsureInventoryCanvasRootScale(inventoryUI.transform);
            inventoryUI.SetActive(true);
            isInventoryOpen = true;
            UpdateInventoryUI();
        }
    }

    /// <summary>
    /// 씬에서 실수로 Canvas 스케일이 0인 경우 인벤이 보이지 않음 — Tab으로 열 때 복구
    /// </summary>
    private static void EnsureInventoryCanvasRootScale(Transform inventoryUiTransform)
    {
        Canvas canvas = inventoryUiTransform.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        var rt = canvas.transform as RectTransform;
        if (rt != null && rt.localScale.sqrMagnitude < 1e-6f)
            rt.localScale = Vector3.one;
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
        RebuildSlotsFromDictionaryIfEmpty();

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

        // 0개가 되면 삭제 및 칸 비우기
        if (items[itemID].count <= 0)
        {
            items.Remove(itemID);
            int si = FindSlotIndexContainingItem(itemID);
            if (si >= 0) slotItemIds[si] = 0;
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

    /// <summary>드래그 등으로 데이터만 바뀐 뒤 UI 갱신</summary>
    public void NotifyUiRefresh()
    {
        RebuildSlotsFromDictionaryIfEmpty();
        var ui = FindObjectOfType<InventoryUI>(true);
        if (ui != null)
            ui.RefreshUI();
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