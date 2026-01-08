using System.Collections;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(EnemyInstance))]
public class FieldEnemyWanderChase : MonoBehaviour
{
    public enum State { Wander, Chase, Frozen }

    [Header("Refs")]
    public Transform player;
    public GridBoard gridBoard;
    public FieldTimeManager timeManager;
    public GridOccupancyRegistry occupancy;

    [Header("Detect")]
    [Tooltip("(Deprecated) Definition에서 자동으로 읽어옴. 오버라이드하려면 useCustomDetection=true")]
    public int aggroRange = 6;              // Chebyshev
    [Tooltip("(Deprecated) Definition에서 자동으로 읽어옴")]
    public bool requireLoS = false;
    [Tooltip("Definition 대신 위 값을 사용")]
    public bool useCustomDetection = false;

    [Header("Wander")]
    public bool enableWander = true;
    public int wanderRadius = 4;
    public int wanderRetargetEveryTicks = 4;

    [Header("Move")]
    public bool allowDiagonal = true;
    public float stepMoveDuration = 0.08f;

    [Tooltip("전투 진행 중이면 적 배회/추격을 멈춤")]
    public bool pauseWhenAnyCombatActive = true;

    [Header("Combat Link (optional)")]
    public bool startCombatWhenAdjacent = true;
    public int combatStartRange = 1;
    public FieldCombatController fieldCombat;
    public bool enemyTurnFirst = true;

    [Header("Debug")]
    public State state = State.Wander;

    private EnemyInstance _enemy;
    private Vector2Int _wanderTargetCell;
    private int _wanderTickCounter;
    private bool _moving;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
    }

    private void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (timeManager == null) timeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (fieldCombat == null) fieldCombat = FindObjectOfType<FieldCombatController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();

        // ✅ EnemyDefinition에서 시야 읽어오기
        if (!useCustomDetection && _enemy != null && _enemy.definition != null)
        {
            aggroRange = Mathf.Max(1, _enemy.definition.fieldDetectionRange);
            requireLoS = _enemy.definition.fieldRequireLoS;
        }

        // ✅ 시작 위치 점유 등록
        if (occupancy != null && gridBoard != null)
        {
            Vector2Int myCell = gridBoard.WorldToCell(transform.position);
            occupancy.TryOccupy(transform, myCell);
        }

        PickNewWanderTarget();
    }

    private void OnEnable()
    {
        if (timeManager == null) timeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (timeManager != null) timeManager.OnTimeAdvanced += OnTimeAdvanced;
    }

    private void OnDisable()
    {
        if (timeManager != null) timeManager.OnTimeAdvanced -= OnTimeAdvanced;

        // ✅ 비활성/파괴 시 점유 해제
        if (occupancy != null) occupancy.Release(transform);
    }

    private void OnTimeAdvanced(int delta, int newTotalTime)
    {
        int ticks = Mathf.Max(1, delta);
        for (int i = 0; i < ticks; i++)
            TickOnce();
    }

    private void TickOnce()
    {
        if (_enemy != null && _enemy.currentHP <= 0) return;
        if (_moving) return;
        if (gridBoard == null || player == null) return;

        if (pauseWhenAnyCombatActive && fieldCombat != null)
        {
            if (GetCombatActiveBestEffort(fieldCombat))
                return;
        }

        Vector2Int myCell = GetMyCell();
        Vector2Int pCell = gridBoard.WorldToCell(player.position);
        int distToPlayer = FieldCombatUtils.Chebyshev(myCell, pCell);

        bool canSee = distToPlayer <= aggroRange;
        if (canSee && requireLoS)
            canSee = FieldCombatUtils.HasLineOfSight(gridBoard, myCell, pCell);

        if (canSee) state = State.Chase;
        else if (enableWander) state = State.Wander;
        else state = State.Frozen;

        if (state == State.Frozen) return;

        if (state == State.Chase)
        {
            if (startCombatWhenAdjacent && fieldCombat != null && distToPlayer <= combatStartRange)
            {
                if (!GetCombatActiveBestEffort(fieldCombat))
                    TryStartCombat(fieldCombat, transform, enemyTurnFirst);
                return;
            }

            Vector2Int next = ChooseNextStep(myCell, pCell);
            if (next != myCell)
                TryStartStepMove(myCell, next);
            return;
        }

        // Wander
        _wanderTickCounter++;
        if (_wanderTickCounter >= Mathf.Max(1, wanderRetargetEveryTicks) ||
            FieldCombatUtils.Chebyshev(myCell, _wanderTargetCell) <= 0)
        {
            _wanderTickCounter = 0;
            PickNewWanderTarget();
        }

        Vector2Int wNext = ChooseNextStep(myCell, _wanderTargetCell);
        if (wNext != myCell)
            TryStartStepMove(myCell, wNext);
    }

    private Vector2Int GetMyCell()
    {
        // 점유 레지스트리에 등록된 셀이 있으면 그걸 우선
        if (occupancy != null && occupancy.TryGetCurrentCell(transform, out var c))
            return c;

        return gridBoard.WorldToCell(transform.position);
    }

    // =========================================================
    // 이동 시작(예약 먼저!)
    // =========================================================
    private void TryStartStepMove(Vector2Int fromCell, Vector2Int toCell)
    {
        // ✅ 먼저 점유 "예약"(사실상 목적지 점유)
        if (occupancy != null)
        {
            if (!occupancy.TryMoveReserve(transform, toCell))
            {
                // 목적지가 이미 점유됨 -> 이번 틱은 이동 포기
                return;
            }
        }

        StartCoroutine(StepMove(fromCell, toCell));
    }

    private IEnumerator StepMove(Vector2Int fromCell, Vector2Int toCell)
    {
        _moving = true;

        Vector3 a = gridBoard.CellToWorld(fromCell);
        Vector3 b = gridBoard.CellToWorld(toCell);

        float dur = Mathf.Max(0.001f, stepMoveDuration);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            transform.position = Vector3.Lerp(a, b, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = b;
        _moving = false;
    }

    // =========================================================
    // 다음 스텝 선택 (워크/점유 체크 포함)
    // =========================================================
    private Vector2Int ChooseNextStep(Vector2Int from, Vector2Int goal)
    {
        Vector2Int best = from;
        int bestDist = FieldCombatUtils.Chebyshev(from, goal);

        // 후보 중 거리 줄어드는 것 우선
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!allowDiagonal && Mathf.Abs(dx) + Mathf.Abs(dy) == 2) continue;

                Vector2Int n = new Vector2Int(from.x + dx, from.y + dy);

                if (!IsWalkable(n)) continue;
                if (IsOccupiedByOther(n)) continue; // ✅ 점유 체크

                int d = FieldCombatUtils.Chebyshev(n, goal);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }

        // 줄어드는 게 없으면(막힘) 그냥 제자리
        return best;
    }

    private bool IsOccupiedByOther(Vector2Int cell)
    {
        if (occupancy == null) return false;
        var occ = occupancy.GetOccupant(cell);
        return occ != null && occ != transform;
    }

    private bool IsWalkable(Vector2Int cell)
    {
        var t = gridBoard.GetType();

        foreach (var name in new[] { "IsCellBlocked", "IsMoveBlocked", "IsBlocked", "IsWalkBlocked" })
        {
            var m = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null)
            {
                object r = m.Invoke(gridBoard, new object[] { cell });
                if (r is bool b) return !b;
            }
        }
        return true;
    }

    // =========================================================
    // Wander target
    // =========================================================
    private void PickNewWanderTarget()
    {
        if (gridBoard == null) return;

        Vector2Int myCell = GetMyCell();

        for (int t = 0; t < 16; t++)
        {
            int dx = Random.Range(-wanderRadius, wanderRadius + 1);
            int dy = Random.Range(-wanderRadius, wanderRadius + 1);
            Vector2Int c = new Vector2Int(myCell.x + dx, myCell.y + dy);

            if (!IsWalkable(c)) continue;
            if (IsOccupiedByOther(c)) continue;

            _wanderTargetCell = c;
            return;
        }

        _wanderTargetCell = myCell;
    }

    // =========================================================
    // Combat helpers (reflection-safe)
    // =========================================================
    private bool GetCombatActiveBestEffort(FieldCombatController fc)
    {
        if (fc == null) return false;

        var t = fc.GetType();
        foreach (var name in new[] { "IsInCombat", "isInCombat", "InCombat", "inCombat", "Active", "active" })
        {
            var p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(bool))
            {
                object v = p.GetValue(fc);
                if (v is bool b) return b;
            }

            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(bool))
            {
                object v = f.GetValue(fc);
                if (v is bool b) return b;
            }
        }

        return false;
    }

    private void TryStartCombat(FieldCombatController fc, Transform enemy, bool enemyFirst)
    {
        if (fc == null || enemy == null) return;

        var t = fc.GetType();
        string[] candidates =
        {
            "StartCombatPublic",
            "StartCombatRequest",
            "RequestStartCombat",
            "BeginCombatFromFieldAI",
            "EnterFromFieldAI"
        };

        foreach (var name in candidates)
        {
            var m2 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Transform), typeof(bool) }, null);
            if (m2 != null) { m2.Invoke(fc, new object[] { enemy, enemyFirst }); return; }

            var m1 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Transform) }, null);
            if (m1 != null) { m1.Invoke(fc, new object[] { enemy }); return; }
        }
    }
}
