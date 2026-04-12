using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �ʵ� �þ�/���� ���� �ý���
/// - �÷��̾�/�� �þ� ���� (LoS + �Ÿ�)
/// - ����(��ȣ, ��Ǯ) ����
/// - ��ũ: �÷��̾� �þ� ���ʽ� + �� �νķ� ����
/// </summary>
public class FieldVisionSystem : MonoBehaviour
{
    public static FieldVisionSystem Instance { get; private set; }

    [Header("Refs")]
    public GridBoard gridBoard;
    public PlayerCrouch playerCrouch;

    [Header("Player Vision")]
    public int playerVisionRange = 5;

    [Min(0)] public int crouchPlayerVisionBonus = 1;

    [Header("Enemy Detection")]
    public int enemyDetectionRange = 5;

    [Header("Cover")]
    [Range(0f, 1f)] public float coverDetectionPenalty = 0.90f;
    [Range(0f, 1f)] public float crouchDetectionPenalty = 0.35f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private HashSet<Vector2Int> _coverCells = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();
    }

    private void Start()
    {
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        ResolvePlayerCrouch();
        RefreshCoverCells();

        Debug.Log($"[VisionSystem] Init - playerVision={playerVisionRange}(crouch+{crouchPlayerVisionBonus}), enemyDetect={enemyDetectionRange}, cover={_coverCells.Count}");
    }

    private PlayerCrouch ResolvePlayerCrouch()
    {
        if (playerCrouch == null)
            playerCrouch = FindObjectOfType<PlayerCrouch>();
        return playerCrouch;
    }

    public static FieldVisionSystem EnsureInstance()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<FieldVisionSystem>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject go = new GameObject("[FieldVisionSystem]");
        go.AddComponent<FieldVisionSystem>();
        return Instance;
    }

    public int GetEffectivePlayerVisionRange()
    {
        var c = ResolvePlayerCrouch();
        int r = Mathf.Max(0, playerVisionRange);
        if (c != null && c.IsCrouching)
            r += Mathf.Max(0, crouchPlayerVisionBonus);
        return r;
    }

    public int GetBasePlayerVisionRange() => Mathf.Max(0, playerVisionRange);

    public void RefreshCoverCells()
    {
        _coverCells.Clear();

        if (gridBoard == null) return;

        Harvestable[] harvestables = FindObjectsOfType<Harvestable>();

        foreach (var h in harvestables)
        {
            if (h == null || h.isHarvested) continue;

            if (h.itemID == 1 || h.itemID == 2)
            {
                Vector2Int cell = gridBoard.WorldToCell(h.transform.position);
                _coverCells.Add(cell);

                gridBoard.RegisterCellBlock(cell, false, true);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[VisionSystem] cover cells: {_coverCells.Count}");
    }

    public void RemoveCoverCell(Vector2Int cell)
    {
        if (_coverCells.Remove(cell))
        {
            if (gridBoard != null)
                gridBoard.UnregisterCellBlock(cell, false, true);
        }
    }

    public bool IsCoverCell(Vector2Int cell)
    {
        return _coverCells.Contains(cell);
    }

    public bool HasVision(Vector2Int from, Vector2Int to, int range)
    {
        if (FieldCombatUtils.Chebyshev(from, to) > range)
            return false;

        return FieldCombatUtils.HasLineOfSight(gridBoard, from, to);
    }

    public HashSet<Vector2Int> GetPlayerVisibleCells(Vector2Int playerCell)
    {
        return GetVisibleCells(playerCell, GetEffectivePlayerVisionRange());
    }

    public HashSet<Vector2Int> GetVisibleCells(Vector2Int center, int range)
    {
        HashSet<Vector2Int> visible = new HashSet<Vector2Int>();
        visible.Add(center);

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > range) continue;

                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                if (gridBoard != null && !gridBoard.InBounds(cell)) continue;

                if (FieldCombatUtils.HasLineOfSight(gridBoard, center, cell))
                    visible.Add(cell);
            }
        }

        return visible;
    }

    public bool TryDetectPlayer(Vector2Int enemyCell, Vector2Int playerCell)
    {
        return TryDetectPlayer(enemyCell, playerCell, out _);
    }

    public bool TryDetectPlayer(Vector2Int enemyCell, Vector2Int playerCell, out float detectionChance)
    {
        detectionChance = 0f;

        if (FieldCombatUtils.Chebyshev(enemyCell, playerCell) > enemyDetectionRange)
            return false;

        if (!FieldCombatUtils.HasLineOfSight(gridBoard, enemyCell, playerCell))
            return false;

        detectionChance = 1.0f;

        if (IsCoverCell(playerCell))
            detectionChance -= coverDetectionPenalty;

        var c = ResolvePlayerCrouch();
        if (c != null && c.IsCrouching)
            detectionChance -= crouchDetectionPenalty;

        detectionChance = Mathf.Clamp01(detectionChance);

        bool detected = Random.value <= detectionChance;

        if (showDebugLogs && detectionChance < 1f)
            Debug.Log($"[VisionSystem] detect: chance={detectionChance:P0}, result={detected} (cover={IsCoverCell(playerCell)}, crouch={c?.IsCrouching})");

        return detected;
    }

    public bool CanSeePlayer(Vector2Int enemyCell, Vector2Int playerCell)
    {
        return HasVision(enemyCell, playerCell, enemyDetectionRange);
    }
}
