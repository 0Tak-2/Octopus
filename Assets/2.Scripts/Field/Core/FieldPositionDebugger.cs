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
    public bool autoFix = true; // true면 불일치 시 자동으로 위치 동기화

    private float _timer;

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

            // 불일치 감지
            if (visualCell != occCell)
            {
                Debug.LogError($"[PosDebug] *** 위치 불일치! *** {enemy.name}: Visual=({visualCell}) vs Occupancy=({occCell}), World={enemy.transform.position}");

                if (autoFix)
                {
                    // Occupancy 기준으로 transform.position 동기화
                    Vector3 correctPos = grid.CellToWorld(occCell);
                    Debug.Log($"[PosDebug] 자동 수정: {enemy.name} -> ({occCell}), WorldPos={correctPos}");
                    enemy.transform.position = correctPos;
                }
            }
        }
    }
}