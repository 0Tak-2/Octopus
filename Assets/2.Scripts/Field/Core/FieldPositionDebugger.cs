using UnityEngine;

/// <summary>
/// 적의 시각적 위치(transform)와 그리드 점유 위치(Occupancy)의 불일치를 진단
/// 모든 적 프리팹에 추가하거나 빈 오브젝트에 붙여서 사용
/// </summary>
public class EnemyPositionDebugger : MonoBehaviour
{
    [Header("Settings")]
    public bool enableDebug = true;
    public float checkInterval = 1f;
    [Tooltip("Chebyshev 거리 1 이하는 이동 보간/피벗 오차로 흔함. 끄면 인접 불일치도 Error로 찍힌다.")]
    public bool ignoreAdjacentCellMismatch = true;
    public bool autoFix = true; // true면 불일치 시 Occupancy 기준으로 위치 동기화

    private float _timer;

    private static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void Update()
    {
        if (!enableDebug) return;

        _timer += Time.deltaTime;
        if (_timer < checkInterval) return;
        _timer = 0f;

        CheckAllEnemies();
    }

    private void CheckAllEnemies()
    {
        var occ = GridOccupancyRegistry.Instance;
        var grid = FindObjectOfType<GridBoard>();
        if (occ == null || grid == null) return;

        EnemyInstance[] enemies = FindObjectsOfType<EnemyInstance>();

        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.currentHP <= 0 || !enemy.gameObject.activeSelf) continue;

            // 시각적 위치 (transform)
            Vector2Int visualCell = grid.WorldToCell(enemy.transform.position);

            // 점유 위치 (occupancy)
            Vector2Int occCell = Vector2Int.zero;
            bool hasOcc = occ.TryGetCurrentCell(enemy.transform, out occCell);

            if (!hasOcc)
            {
                Debug.LogWarning($"[PosDebug] {enemy.name}: Occupancy에 등록되지 않음! Visual=({visualCell})");
                continue;
            }

            if (visualCell == occCell) continue;

            int cellDist = Chebyshev(visualCell, occCell);
            // 이동 중: TryMoveReserve가 목적지 셀을 먼저 잡고 transform은 Lerp로 따라옴 → 잠깐 어긋남.
            if (ignoreAdjacentCellMismatch && cellDist <= 1)
                continue;

            Debug.LogError($"[PosDebug] *** 위치 불일치! *** {enemy.name}: Visual=({visualCell}) vs Occupancy=({occCell}), Chebyshev={cellDist}, World={enemy.transform.position}");

            if (autoFix)
            {
                Vector3 correctPos = grid.CellToWorld(occCell);
                Debug.Log($"[PosDebug] 자동 수정: {enemy.name} -> ({occCell}), WorldPos={correctPos}");
                enemy.transform.position = correctPos;
            }
        }
    }
}
