using UnityEngine;

/// <summary>
/// Handles item drops when enemy dies
/// Items drop on the floor as DroppedItem, player picks up with F key
/// </summary>
[RequireComponent(typeof(EnemyInstance))]
public class EnemyDropHandler : MonoBehaviour
{
    [Header("Drop Item Prefab")]
    [Tooltip("바닥에 떨어지는 아이템 프리팹 (DroppedItem 컴포넌트 필요)")]
    public GameObject droppedItemPrefab;

    [Header("Drop Spread")]
    [Tooltip("아이템이 떨어지는 범위 (적 위치 기준)")]
    public float dropSpreadRange = 0.3f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private EnemyInstance _enemy;
    private bool _hasDropped = false;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
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
            SpawnDroppedItem(def.dropFood);
        }

        // 2. Material drop
        if (def.dropMaterial != null && Random.value <= def.materialDropChance)
        {
            SpawnDroppedItem(def.dropMaterial);
        }

        // 3. Secondary material drop
        if (def.dropMaterial2 != null && Random.value <= def.material2DropChance)
        {
            SpawnDroppedItem(def.dropMaterial2);
        }

        // 4. Color module drop
        if (def.dropColorModule != null && Random.value <= def.colorModuleDropChance)
        {
            SpawnDroppedItem(def.dropColorModule);
        }
    }

    /// <summary>
    /// 바닥에 아이템 스폰
    /// </summary>
    private void SpawnDroppedItem(ItemData itemData)
    {
        if (itemData == null) return;

        // 프리팹이 없으면 인벤토리에 직접 추가 (fallback)
        if (droppedItemPrefab == null)
        {
            var inv = InventoryManager.Instance;
            if (inv != null)
            {
                inv.AddItem(itemData.itemID, itemData.itemName, 1, transform.position);
                if (showDebugLogs)
                    Debug.Log($"[EnemyDropHandler] No prefab, added directly: {itemData.itemName}");
            }
            return;
        }

        // 약간 랜덤 위치에 스폰
        Vector3 dropPos = transform.position + new Vector3(
            Random.Range(-dropSpreadRange, dropSpreadRange),
            Random.Range(-dropSpreadRange, dropSpreadRange),
            0f
        );

        GameObject dropObj = Instantiate(droppedItemPrefab, dropPos, Quaternion.identity);

        // DroppedItem 컴포넌트 설정
        DroppedItem dropped = dropObj.GetComponent<DroppedItem>();
        if (dropped == null)
            dropped = dropObj.AddComponent<DroppedItem>();

        dropped.itemID = itemData.itemID;
        dropped.itemName = itemData.itemName;
        dropped.count = 1;

        // 아이콘 스프라이트 설정 (있으면)
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