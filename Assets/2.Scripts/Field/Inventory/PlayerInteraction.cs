using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 플레이어 상호작용 - F키로 채집/습득
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;

    [Header("Interaction")]
    public float interactionRange = 1.5f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private void Awake()
    {
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (fieldTimeManager == null) fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
    }

    private void Update()
    {
        // UI 클릭 중이면 입력 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // F키 입력
        if (Input.GetKeyDown(KeyCode.F))
        {
            TryInteract();
        }
    }

    /// <summary>
    /// 상호작용 시도
    /// </summary>
    private void TryInteract()
    {
        // 1. 떨어진 아이템 체크
        DroppedItem nearestItem = FindNearestDroppedItem();
        if (nearestItem != null)
        {
            PickupItem(nearestItem);
            return;
        }

        // 2. 채집 가능한 오브젝트 체크 (해초, 산호, 조개 등)
        Harvestable nearestHarvestable = FindNearestHarvestable();
        if (nearestHarvestable != null)
        {
            HarvestObject(nearestHarvestable);
            return;
        }

        if (showDebugLogs)
            Debug.Log("[Interaction] Nothing to interact with");
    }

    /// <summary>
    /// 가장 가까운 떨어진 아이템 찾기
    /// </summary>
    private DroppedItem FindNearestDroppedItem()
    {
        DroppedItem[] items = FindObjectsOfType<DroppedItem>();
        DroppedItem nearest = null;
        float nearestDist = float.MaxValue;

        // 플레이어 셀 위치
        Vector2Int playerCell = gridBoard != null ? gridBoard.WorldToCell(transform.position) : Vector2Int.zero;

        foreach (var item in items)
        {
            // 그리드 기반 거리 체크
            Vector2Int itemCell = gridBoard != null ? gridBoard.WorldToCell(item.transform.position) : Vector2Int.zero;
            int gridDistance = Mathf.Abs(playerCell.x - itemCell.x) + Mathf.Abs(playerCell.y - itemCell.y);

            float worldDist = Vector3.Distance(transform.position, item.transform.position);

            // 같은 칸(0)만 허용
            if (gridDistance == 0 && worldDist < nearestDist)
            {
                nearest = item;
                nearestDist = worldDist;
            }
        }

        return nearest;
    }

    /// <summary>
    /// 가장 가까운 채집 가능 오브젝트 찾기
    /// </summary>
    private Harvestable FindNearestHarvestable()
    {
        Harvestable[] harvestables = FindObjectsOfType<Harvestable>();

        Debug.Log($"[Interaction] Found {harvestables.Length} harvestables in scene");

        Harvestable nearest = null;
        float nearestDist = float.MaxValue;

        // 플레이어 셀 위치
        Vector2Int playerCell = gridBoard != null ? gridBoard.WorldToCell(transform.position) : Vector2Int.zero;

        foreach (var harvestable in harvestables)
        {
            if (harvestable.isHarvested) continue;

            // 그리드 기반 거리 체크
            Vector2Int harvestableCell = gridBoard != null ? gridBoard.WorldToCell(harvestable.transform.position) : Vector2Int.zero;
            int gridDistance = Mathf.Abs(playerCell.x - harvestableCell.x) + Mathf.Abs(playerCell.y - harvestableCell.y);

            float worldDist = Vector3.Distance(transform.position, harvestable.transform.position);
            Debug.Log($"[Interaction] {harvestable.itemName} at grid distance {gridDistance} (world: {worldDist:F2})");

            // 같은 칸(0)만 허용
            if (gridDistance == 0 && worldDist < nearestDist)
            {
                nearest = harvestable;
                nearestDist = worldDist;
            }
        }

        if (nearest != null)
        {
            Debug.Log($"[Interaction] Nearest: {nearest.itemName} at {nearestDist:F2}");
        }
        else
        {
            Debug.Log("[Interaction] No harvestable in same cell");
        }

        return nearest;
    }

    /// <summary>
    /// 아이템 줍기 (1턴 소모)
    /// </summary>
    private void PickupItem(DroppedItem item)
    {
        if (item == null) return;

        if (showDebugLogs)
            Debug.Log($"[Interaction] Picking up {item.itemName}");

        // 아이템 줍기 (플레이어 위치 전달)
        item.Pickup(transform.position);

        // 1턴 소모
        if (fieldTimeManager != null)
        {
            fieldTimeManager.Advance(1);
        }

        // 적들 반격
        TriggerEnemyTurn();
    }

    /// <summary>
    /// 채집 가능 오브젝트 채집 (1턴 소모)
    /// </summary>
    private void HarvestObject(Harvestable harvestable)
    {
        if (harvestable == null) return;

        // 채집 가능한지 확인 (도구 체크)
        string reason;
        if (!harvestable.CanHarvest(out reason))
        {
            Debug.Log($"[Interaction] Cannot harvest: {reason}");
            // 경고 메시지는 Console에만 표시
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[Interaction] Harvesting {harvestable.itemName}");

        // 채집
        harvestable.Harvest();

        // 1턴 소모
        if (fieldTimeManager != null)
        {
            fieldTimeManager.Advance(1);
        }

        // 적들 반격
        TriggerEnemyTurn();
    }

    /// <summary>
    /// 적 턴 발동
    /// </summary>
    private void TriggerEnemyTurn()
    {
        FieldMultiEnemyAttack multiAttack = FindObjectOfType<FieldMultiEnemyAttack>();
        if (multiAttack != null)
        {
            multiAttack.OnPlayerTurnEnd();
        }
    }
}