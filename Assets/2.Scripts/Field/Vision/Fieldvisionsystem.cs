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

    [Header("Touched Coral Glow")]
    [Tooltip("플레이어가 건드린 산호만 일정 턴 동안 주변 시야를 제공합니다.")]
    public bool enableTouchedCoralGlow = true;
    [Range(0, 2)] public int touchedCoralGlowRange = 1;
    [Range(1, 99)] public int touchedCoralGlowTurns = 15;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private HashSet<Vector2Int> _coverCells = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> _touchedCoralGlowRemainTurns = new Dictionary<Vector2Int, int>();
    private FieldTimeManager _fieldTime;

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
        ResolveFieldTime();

        Debug.Log($"[VisionSystem] Init - playerVision={playerVisionRange}(crouch+{crouchPlayerVisionBonus}), enemyDetect={enemyDetectionRange}, cover={_coverCells.Count}");
    }

    private void OnDestroy()
    {
        if (_fieldTime != null)
            _fieldTime.OnTimeAdvanced -= OnFieldTimeAdvanced;
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

    private void ResolveFieldTime()
    {
        _fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (_fieldTime != null)
            _fieldTime.OnTimeAdvanced += OnFieldTimeAdvanced;
    }

    private void OnFieldTimeAdvanced(int delta, int newTotalTime)
    {
        if (_touchedCoralGlowRemainTurns.Count == 0) return;

        bool changed = false;
        var keys = new List<Vector2Int>(_touchedCoralGlowRemainTurns.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Vector2Int cell = keys[i];
            int remain = _touchedCoralGlowRemainTurns[cell] - Mathf.Max(0, delta);
            if (remain <= 0)
            {
                _touchedCoralGlowRemainTurns.Remove(cell);
                changed = true;
            }
            else
            {
                _touchedCoralGlowRemainTurns[cell] = remain;
                changed = true;
            }
        }

        if (changed && FogOfWarRenderer.Instance != null)
            FogOfWarRenderer.Instance.ForceRefresh();
    }

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
        HashSet<Vector2Int> visible = GetVisibleCells(playerCell, GetEffectivePlayerVisionRange());
        AddTouchedCoralGlowVisibleCells(visible);
        return visible;
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

    private void AddTouchedCoralGlowVisibleCells(HashSet<Vector2Int> visible)
    {
        if (!enableTouchedCoralGlow) return;
        if (gridBoard == null) return;
        if (_touchedCoralGlowRemainTurns.Count == 0) return;

        int glowRange = Mathf.Max(0, touchedCoralGlowRange);
        foreach (var pair in _touchedCoralGlowRemainTurns)
        {
            Vector2Int coralCell = pair.Key;
            for (int dx = -glowRange; dx <= glowRange; dx++)
            {
                for (int dy = -glowRange; dy <= glowRange; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > glowRange)
                        continue;

                    Vector2Int c = new Vector2Int(coralCell.x + dx, coralCell.y + dy);
                    if (!gridBoard.InBounds(c))
                        continue;

                    visible.Add(c);
                }
            }
        }
    }

    public void ActivateTouchedCoralGlow(Vector2Int coralCell)
    {
        if (!enableTouchedCoralGlow) return;

        int turns = Mathf.Max(1, touchedCoralGlowTurns);
        _touchedCoralGlowRemainTurns[coralCell] = turns;

        if (FogOfWarRenderer.Instance != null)
            FogOfWarRenderer.Instance.ForceRefresh();
    }

    public void ClearTouchedCoralGlowAt(Vector2Int coralCell)
    {
        if (_touchedCoralGlowRemainTurns.Remove(coralCell))
        {
            if (FogOfWarRenderer.Instance != null)
                FogOfWarRenderer.Instance.ForceRefresh();
        }
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
