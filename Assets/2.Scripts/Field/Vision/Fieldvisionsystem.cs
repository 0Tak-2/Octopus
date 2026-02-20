using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드 시야/감지 통합 시스템
/// - 플레이어/적 모두의 시야 계산 (LoS + 범위)
/// - 엄폐물(산호, 해초) 위 은신 판정
/// - 웅크리기 은신 보너스
/// </summary>
public class FieldVisionSystem : MonoBehaviour
{
    public static FieldVisionSystem Instance { get; private set; }

    [Header("Refs")]
    public GridBoard gridBoard;
    public PlayerCrouch playerCrouch;

    [Header("Player Vision")]
    [Tooltip("플레이어 기본 시야 범위 (체비셰프)")]
    public int playerVisionRange = 5;

    [Header("Enemy Detection")]
    [Tooltip("적 기본 감지 범위 (체비셰프)")]
    public int enemyDetectionRange = 5;

    [Header("Cover (엄폐물)")]
    [Tooltip("엄폐물(산호/해초) 위에 있을 때 인식률 감소")]
    [Range(0f, 1f)] public float coverDetectionPenalty = 0.50f;

    [Tooltip("웅크리기 추가 인식률 감소")]
    [Range(0f, 1f)] public float crouchDetectionPenalty = 0.20f;

    [Header("Debug")]
    public bool showDebugLogs = false;

    // 엄폐물 셀 목록 (산호, 해초가 있는 칸)
    private HashSet<Vector2Int> _coverCells = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (playerCrouch == null) playerCrouch = FindObjectOfType<PlayerCrouch>();

        // 엄폐물 셀 수집
        RefreshCoverCells();

        Debug.Log($"[VisionSystem] 초기화 완료 - 플레이어시야={playerVisionRange}, 적감지={enemyDetectionRange}, " +
                  $"엄폐={coverDetectionPenalty:P0}, 웅크림={crouchDetectionPenalty:P0}, 엄폐물={_coverCells.Count}개");
    }

    /// <summary>
    /// 씬에 없으면 자동 생성 (다른 스크립트에서 호출용)
    /// </summary>
    public static FieldVisionSystem EnsureInstance()
    {
        if (Instance != null) return Instance;

        // 씬에서 찾기
        var existing = FindObjectOfType<FieldVisionSystem>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        // 자동 생성
        GameObject go = new GameObject("[FieldVisionSystem]");
        var sys = go.AddComponent<FieldVisionSystem>();
        Debug.Log("[VisionSystem] 씬에 없어서 자동 생성됨");
        return sys;
    }

    /// <summary>
    /// 씬의 모든 Harvestable을 스캔해서 엄폐물 셀 등록 +
    /// 산호/해초를 시야차단(blockedVision)에도 등록
    /// </summary>
    public void RefreshCoverCells()
    {
        _coverCells.Clear();

        if (gridBoard == null) return;

        Harvestable[] harvestables = FindObjectsOfType<Harvestable>();

        foreach (var h in harvestables)
        {
            if (h == null || h.isHarvested) continue;

            // 산호(itemID=2), 해초(itemID=1)만 엄폐물 + 시야차단
            if (h.itemID == 1 || h.itemID == 2)
            {
                Vector2Int cell = gridBoard.WorldToCell(h.transform.position);
                _coverCells.Add(cell);

                // 시야 차단 등록 (이동은 막지 않음)
                gridBoard.RegisterCellBlock(cell, false, true);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[VisionSystem] 엄폐물 셀 {_coverCells.Count}개 등록 완료");
    }

    /// <summary>
    /// 엄폐물 셀 제거 (채집 후 호출)
    /// </summary>
    public void RemoveCoverCell(Vector2Int cell)
    {
        if (_coverCells.Remove(cell))
        {
            // 시야 차단도 해제
            if (gridBoard != null)
                gridBoard.UnregisterCellBlock(cell, false, true);
        }
    }

    /// <summary>
    /// 해당 셀이 엄폐물(산호/해초)인지 확인
    /// </summary>
    public bool IsCoverCell(Vector2Int cell)
    {
        return _coverCells.Contains(cell);
    }

    // =============================================================
    // 시야 판정 (LoS + 범위)
    // =============================================================

    /// <summary>
    /// from에서 to까지 시야가 있는지 확인 (LoS + 범위)
    /// </summary>
    public bool HasVision(Vector2Int from, Vector2Int to, int range)
    {
        // 범위 체크
        if (FieldCombatUtils.Chebyshev(from, to) > range)
            return false;

        // LoS 체크 (Bresenham)
        return FieldCombatUtils.HasLineOfSight(gridBoard, from, to);
    }

    /// <summary>
    /// 플레이어 기준 시야 범위 내 보이는 셀 목록
    /// </summary>
    public HashSet<Vector2Int> GetPlayerVisibleCells(Vector2Int playerCell)
    {
        return GetVisibleCells(playerCell, playerVisionRange);
    }

    /// <summary>
    /// 특정 위치에서 range 범위 내 보이는 셀 목록
    /// </summary>
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

    // =============================================================
    // 적 → 플레이어 감지 판정
    // =============================================================

    /// <summary>
    /// 적이 플레이어를 감지할 수 있는지 판정
    /// 반환: 감지 성공 여부
    /// </summary>
    public bool TryDetectPlayer(Vector2Int enemyCell, Vector2Int playerCell)
    {
        return TryDetectPlayer(enemyCell, playerCell, out _);
    }

    /// <summary>
    /// 적이 플레이어를 감지할 수 있는지 판정 (확률 포함)
    /// </summary>
    public bool TryDetectPlayer(Vector2Int enemyCell, Vector2Int playerCell, out float detectionChance)
    {
        detectionChance = 0f;

        // 1. 범위 체크
        if (FieldCombatUtils.Chebyshev(enemyCell, playerCell) > enemyDetectionRange)
            return false;

        // 2. LoS 체크
        if (!FieldCombatUtils.HasLineOfSight(gridBoard, enemyCell, playerCell))
            return false;

        // 3. 기본 인식률 100%
        detectionChance = 1.0f;

        // 4. 엄폐물 감소
        if (IsCoverCell(playerCell))
            detectionChance -= coverDetectionPenalty;

        // 5. 웅크리기 감소
        if (playerCrouch != null && playerCrouch.IsCrouching)
            detectionChance -= crouchDetectionPenalty;

        // 최소 0%
        detectionChance = Mathf.Clamp01(detectionChance);

        // 6. 확률 판정
        bool detected = Random.value <= detectionChance;

        if (showDebugLogs && detectionChance < 1f)
            Debug.Log($"[VisionSystem] 감지 판정: 확률={detectionChance:P0}, 결과={detected}" +
                      $" (엄폐={IsCoverCell(playerCell)}, 웅크림={playerCrouch?.IsCrouching})");

        return detected;
    }

    /// <summary>
    /// 적이 이미 어그로 상태일 때 플레이어를 계속 볼 수 있는지 (LoS만 체크, 확률 무시)
    /// </summary>
    public bool CanSeePlayer(Vector2Int enemyCell, Vector2Int playerCell)
    {
        return HasVision(enemyCell, playerCell, enemyDetectionRange);
    }
}