using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// �÷��̾� ��ȣ�ۿ� - FŰ�� ä��/����
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;
    public FieldTurnCoordinator turnCoordinator;

    [Header("Interaction")]
    public float interactionRange = 1.5f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private void Awake()
    {
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (fieldTimeManager == null) fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (turnCoordinator == null) turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();
    }

    [Header("Input")]
    public KeyCode interactKey = KeyCode.E;

    private void Update()
    {
        if (turnCoordinator != null && turnCoordinator.IsBusy)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            TryInteract();
        }
    }

    /// <summary>
    /// ��ȣ�ۿ� �õ�
    /// </summary>
    private void TryInteract()
    {
        // 1. ������ ������ üũ
        DroppedItem nearestItem = FindNearestDroppedItem();
        if (nearestItem != null)
        {
            PickupItem(nearestItem);
            return;
        }

        // 2. ä�� ������ ������Ʈ üũ (����, ��ȣ, ���� ��)
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
    /// ���� ����� ������ ������ ã��
    /// </summary>
    private DroppedItem FindNearestDroppedItem()
    {
        DroppedItem[] items = FindObjectsOfType<DroppedItem>();
        DroppedItem nearest = null;
        float nearestDist = float.MaxValue;

        // �÷��̾� �� ��ġ
        Vector2Int playerCell = gridBoard != null ? gridBoard.WorldToCell(transform.position) : Vector2Int.zero;

        foreach (var item in items)
        {
            // �׸��� ��� �Ÿ� üũ
            Vector2Int itemCell = gridBoard != null ? gridBoard.WorldToCell(item.transform.position) : Vector2Int.zero;
            int gridDistance = Mathf.Abs(playerCell.x - itemCell.x) + Mathf.Abs(playerCell.y - itemCell.y);

            float worldDist = Vector3.Distance(transform.position, item.transform.position);

            // ���� ĭ(0)�� ���
            if (gridDistance == 0 && worldDist < nearestDist)
            {
                nearest = item;
                nearestDist = worldDist;
            }
        }

        return nearest;
    }

    /// <summary>
    /// ���� ����� ä�� ���� ������Ʈ ã��
    /// </summary>
    private Harvestable FindNearestHarvestable()
    {
        Harvestable[] harvestables = FindObjectsOfType<Harvestable>();

        Debug.Log($"[Interaction] Found {harvestables.Length} harvestables in scene");

        Harvestable nearest = null;
        float nearestDist = float.MaxValue;

        // �÷��̾� �� ��ġ
        Vector2Int playerCell = gridBoard != null ? gridBoard.WorldToCell(transform.position) : Vector2Int.zero;

        foreach (var harvestable in harvestables)
        {
            if (harvestable.isHarvested) continue;

            // �׸��� ��� �Ÿ� üũ
            Vector2Int harvestableCell = gridBoard != null ? gridBoard.WorldToCell(harvestable.transform.position) : Vector2Int.zero;
            int gridDistance = Mathf.Abs(playerCell.x - harvestableCell.x) + Mathf.Abs(playerCell.y - harvestableCell.y);

            float worldDist = Vector3.Distance(transform.position, harvestable.transform.position);
            Debug.Log($"[Interaction] {harvestable.itemName} at grid distance {gridDistance} (world: {worldDist:F2})");

            // ���� ĭ(0)�� ���
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
    /// ������ �ݱ� (1�� �Ҹ�)
    /// </summary>
    private void PickupItem(DroppedItem item)
    {
        if (item == null) return;

        if (showDebugLogs)
            Debug.Log($"[Interaction] Picking up {item.itemName}");

        // ������ �ݱ� (�÷��̾� ��ġ ����)
        item.Pickup(transform.position);

        CommitInteractionTurn();
    }

    /// <summary>
    /// ä�� ���� ������Ʈ ä�� (1�� �Ҹ�)
    /// </summary>
    private void HarvestObject(Harvestable harvestable)
    {
        if (harvestable == null) return;

        // ä�� �������� Ȯ�� (���� üũ)
        string reason;
        if (!harvestable.CanHarvest(out reason))
        {
            Debug.Log($"[Interaction] Cannot harvest: {reason}");
            // ��� �޽����� Console���� ǥ��
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[Interaction] Harvesting {harvestable.itemName}");

        // ä��
        harvestable.Harvest();

        CommitInteractionTurn();
    }

    /// <summary>
    /// �� �� �ߵ�
    /// </summary>
    private void CommitInteractionTurn()
    {
        if (turnCoordinator == null)
            turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();

        if (turnCoordinator != null)
        {
            turnCoordinator.TryCommitPlayerAction(1, true);
        }
        else
        {
            fieldTimeManager?.Advance(1);
            FieldMultiEnemyAttack multiAttack = FindObjectOfType<FieldMultiEnemyAttack>();
            multiAttack?.OnPlayerTurnEnd();
        }
    }
}