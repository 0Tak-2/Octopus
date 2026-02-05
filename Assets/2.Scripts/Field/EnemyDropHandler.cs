using UnityEngine;

/// <summary>
/// Handles item drops when enemy dies
/// Attach to enemy prefab alongside EnemyInstance
/// </summary>
[RequireComponent(typeof(EnemyInstance))]
public class EnemyDropHandler : MonoBehaviour
{
    [Header("References")]
    public InventoryManager inventoryManager;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private EnemyInstance _enemy;
    private bool _hasDropped = false;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
    }

    private void Start()
    {
        if (inventoryManager == null)
            inventoryManager = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
    }

    private void OnDisable()
    {
        // Enemy died (disabled)
        if (_enemy != null && _enemy.currentHP <= 0 && !_hasDropped)
        {
            ProcessDrops();
        }
    }

    /// <summary>
    /// Process all drops from this enemy
    /// </summary>
    public void ProcessDrops()
    {
        if (_hasDropped) return;
        _hasDropped = true;

        if (_enemy == null || _enemy.definition == null)
        {
            if (showDebugLogs)
                Debug.Log("[EnemyDropHandler] No definition, skipping drops.");
            return;
        }

        var def = _enemy.definition;

        if (showDebugLogs)
            Debug.Log($"[EnemyDropHandler] Processing drops for {def.displayName}");

        // 1. Food drop
        if (def.dropFood != null && Random.value <= def.foodDropChance)
        {
            AddToInventory(def.dropFood, 1);
            if (showDebugLogs)
                Debug.Log($"  - Dropped food: {def.dropFood.itemName}");
        }

        // 2. Material drop
        if (def.dropMaterial != null && Random.value <= def.materialDropChance)
        {
            AddToInventory(def.dropMaterial, 1);
            if (showDebugLogs)
                Debug.Log($"  - Dropped material: {def.dropMaterial.itemName}");
        }

        // 3. Secondary material drop
        if (def.dropMaterial2 != null && Random.value <= def.material2DropChance)
        {
            AddToInventory(def.dropMaterial2, 1);
            if (showDebugLogs)
                Debug.Log($"  - Dropped material2: {def.dropMaterial2.itemName}");
        }

        // 4. Color module drop (handled by ColorModuleDropper if present)
        // ColorModuleDropper handles this separately
    }

    private void AddToInventory(ItemData item, int amount)
    {
        if (item == null) return;

        if (inventoryManager == null)
        {
            inventoryManager = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        }

        if (inventoryManager != null)
        {
            // InventoryManager.AddItem(int itemID, string itemName, int count, Vector3 playerPosition)
            Vector3 dropPos = transform.position;
            inventoryManager.AddItem(item.itemID, item.itemName, amount, dropPos);
        }
        else
        {
            Debug.LogWarning("[EnemyDropHandler] InventoryManager not found!");
        }
    }

    /// <summary>
    /// Force drop processing (for manual calls)
    /// </summary>
    public void ForceProcessDrops()
    {
        _hasDropped = false;
        ProcessDrops();
    }
}