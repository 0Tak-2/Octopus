using UnityEngine;

/// <summary>
/// Handles item drops when enemy dies
/// Items drop on the floor as DroppedItem, player picks up with F key
/// </summary>
[RequireComponent(typeof(EnemyInstance))]
public class EnemyDropHandler : MonoBehaviour
{
    [Header("Drop Item Prefab")]
    [Tooltip("???? ???????? ?????? ?????? (DroppedItem ??????? ???)")]
    public GameObject droppedItemPrefab;

    [Header("Drop Spread")]
    [Tooltip("???? ???? ?? ???? ?????? ???? ????(???? ????)")]
    public float dropSpreadRange = 0.15f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    [Header("Grid (optional)")]
    [Tooltip("???? ?????? GridBoard?? ??????.")]
    public GridBoard gridBoard;

    private EnemyInstance _enemy;
    private bool _hasDropped = false;

    /// <summary>??? ???? ?? ?? ?????? ????. ??? ?????? ?????? ???? ????? ??? ????? ?¡Æ? ???.</summary>
    private Vector2Int _lastLootCell;
    private bool _haveLootCell;

    private int _dropStackInProcess;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();
    }

    private void LateUpdate()
    {
        if (_enemy == null || _enemy.currentHP <= 0 || gridBoard == null)
            return;

        var occ = GridOccupancyRegistry.Instance;
        if (occ != null && occ.TryGetCurrentCell(transform, out var c))
            _lastLootCell = c;
        else
            _lastLootCell = gridBoard.WorldToCell(transform.position);

        _haveLootCell = true;
    }

    private Vector3 GetDropAnchorWorld()
    {
        if (gridBoard != null && _haveLootCell)
            return gridBoard.CellToWorld(_lastLootCell);

        if (gridBoard != null)
        {
            Vector2Int cell = gridBoard.WorldToCell(transform.position);
            return gridBoard.CellToWorld(cell);
        }

        return transform.position;
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

        _dropStackInProcess = 0;

        var def = _enemy.definition;

        if (showDebugLogs)
            Debug.Log($"[EnemyDropHandler] Processing drops for {def.displayName}");

        if (def.dropFood != null && Random.value <= def.foodDropChance)
            SpawnDroppedItem(def.dropFood);

        if (def.dropMaterial != null && Random.value <= def.materialDropChance)
            SpawnDroppedItem(def.dropMaterial);

        if (def.dropMaterial2 != null && Random.value <= def.material2DropChance)
            SpawnDroppedItem(def.dropMaterial2);

        if (def.dropColorModule != null && Random.value <= def.colorModuleDropChance)
            SpawnDroppedItem(def.dropColorModule);
    }

    private void SpawnDroppedItem(ItemData itemData)
    {
        if (itemData == null) return;

        if (droppedItemPrefab == null)
        {
            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.AddItem(itemData.itemID, itemData.itemName, 1, GetDropAnchorWorld());
                if (showDebugLogs)
                    Debug.Log($"[EnemyDropHandler] No prefab, added directly: {itemData.itemName}");
            }
            return;
        }

        Vector3 anchor = GetDropAnchorWorld();
        // ???? ?????? ???? ????? ????? ??? ???? ???? ???? ?¬Ú?
        Vector3 dropPos = anchor + Vector3.up * (0.03f * _dropStackInProcess++);
        if (!_haveLootCell && gridBoard == null)
        {
            dropPos += new Vector3(
                Random.Range(-dropSpreadRange, dropSpreadRange),
                Random.Range(-dropSpreadRange, dropSpreadRange),
                0f);
        }

        GameObject dropObj = Instantiate(droppedItemPrefab, dropPos, Quaternion.identity);

        DroppedItem dropped = dropObj.GetComponent<DroppedItem>();
        if (dropped == null)
            dropped = dropObj.AddComponent<DroppedItem>();

        dropped.itemID = itemData.itemID;
        dropped.itemName = itemData.itemName;
        dropped.count = 1;

        SpriteRenderer sr = dropObj.GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = dropObj.GetComponentInChildren<SpriteRenderer>();

        if (sr != null && itemData.icon != null)
            sr.sprite = itemData.icon;

        if (showDebugLogs)
            Debug.Log($"[EnemyDropHandler] Spawned drop: {itemData.itemName} at {dropPos}");
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
