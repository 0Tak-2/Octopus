using UnityEngine;

/// <summary>
/// ���� �ð��� ��ġ(transform)�� �׸��� ���� ��ġ(Occupancy)�� ����ġ�� ����
/// ��� �� �����տ� �߰��ϰų� �� ������Ʈ�� �ٿ��� ���
/// </summary>
public class EnemyPositionDebugger : MonoBehaviour
{
    [Header("Settings")]
    public bool enableDebug = true;
    public float checkInterval = 1f;
    [Tooltip("Chebyshev �Ÿ� 1 ���ϴ� �̵� ����/�ǹ� ������ ����. ���� ���� ����ġ�� Error�� ������.")]
    public bool ignoreAdjacentCellMismatch = true;
    public bool autoFix = false; // 진단 목적으로 기본 OFF. 예전에는 true 였지만 적 위치 초기화 이슈 가능성이 있어 꺼둔다.

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

            // �ð��� ��ġ (transform)
            Vector2Int visualCell = grid.WorldToCell(enemy.transform.position);

            // ���� ��ġ (occupancy)
            Vector2Int occCell = Vector2Int.zero;
            bool hasOcc = occ.TryGetCurrentCell(enemy.transform, out occCell);

            if (!hasOcc)
            {
                Debug.LogWarning($"[PosDebug] {enemy.name}: Occupancy�� ��ϵ��� ����! Visual=({visualCell})");
                continue;
            }

            if (visualCell == occCell) continue;

            int cellDist = Chebyshev(visualCell, occCell);
            // �̵� ��: TryMoveReserve�� ������ ���� ���� ��� transform�� Lerp�� ����� �� ��� ��߳�.
            if (ignoreAdjacentCellMismatch && cellDist <= 1)
                continue;

            Debug.LogError($"[PosDebug] *** ��ġ ����ġ! *** {enemy.name}: Visual=({visualCell}) vs Occupancy=({occCell}), Chebyshev={cellDist}, World={enemy.transform.position}");

            if (autoFix)
            {
                Vector3 correctPos = grid.CellToWorld(occCell);
                Debug.Log($"[PosDebug] �ڵ� ����: {enemy.name} -> ({occCell}), WorldPos={correctPos}");
                enemy.transform.position = correctPos;
            }
        }
    }
}
