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

    [Header("Detect")]
    [Tooltip("플레이어 감지 거리(체비셰프).")]
    public int aggroRange = 6;
    public bool requireLoS = false;

    [Header("Wander")]
    public bool enableWander = true;
    public int wanderRadius = 4;
    public int wanderRetargetEveryTicks = 4;

    [Header("Move")]
    [Tooltip("8방 이동(체비셰프 거리와 자연스럽게 맞음)")]
    public bool allowDiagonal = true;
    [Tooltip("한 칸 '슥슥' 이동 연출 시간")]
    public float stepMoveDuration = 0.08f;

    [Tooltip("전투(필드전투/집중전투 등) 진행 중이면 적 배회/추격을 멈춤")]
    public bool pauseWhenAnyCombatActive = true;

    [Header("Avoid Overlap")]
    [Tooltip("다른 적이 점유 중인 셀로 이동하지 않도록 함")]
    public bool avoidOccupiedCell = true;

    [Tooltip("셀 점유 판정 반경(셀 크기/콜라이더 크기에 맞춰 조절)")]
    public float occupiedCheckRadius = 0.18f;

    [Tooltip("점유 셀 때문에 길이 막히면, 목표와 멀어지는 방향으로 한 칸 물러나기")]
    public bool backoffWhenBlocked = true;

    [Header("Enemy Query")]
    [Tooltip("적 레이어(Enemy 권장). 점유 체크/근접 전투 시작 판단에 사용")]
    public LayerMask enemyLayerMask = ~0;

    [Header("Combat Link (optional)")]
    [Tooltip("플레이어와 인접(또는 설정 거리)하면 FieldCombatController로 전투 시작 요청")]
    public bool startCombatWhenAdjacent = true;

    [Tooltip("전투 시작 거리(체비셰프). 1이면 인접 포함.")]
    public int combatStartRange = 1;

    [Tooltip("씬의 FieldCombatController(있으면 자동 찾음)")]
    public FieldCombatController fieldCombat;

    [Tooltip("전투 시작 시 적 선공 여부(임시).")]
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

        // Enemy 레이어만 쓰는 게 제일 안전하긴 함 (인스펙터에서 Enemy만 찍어줘)
        // 비워두면 그냥 ~0(전부)라서 점유 체크가 과할 수 있음.
        // enemyLayerMask는 꼭 Enemy만 포함 권장.
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
    }

    private void OnTimeAdvanced(int delta, int newTotalTime)
    {
        // delta만큼 틱 처리 (한번에 time이 여러칸 증가해도 안정)
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

        Vector2Int myCell = gridBoard.WorldToCell(transform.position);
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
            // 인접하면 전투 시작(옵션)
            if (startCombatWhenAdjacent && fieldCombat != null && distToPlayer <= combatStartRange)
            {
                if (!GetCombatActiveBestEffort(fieldCombat))
                {
                    TryStartCombat(fieldCombat, transform, enemyTurnFirst);
                }
                return;
            }

            Vector2Int next = ChooseNextStep(myCell, pCell, preferCloser: true);
            if (next != myCell)
                StartCoroutine(StepMove(myCell, next));

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

        Vector2Int wanderNext = ChooseNextStep(myCell, _wanderTargetCell, preferCloser: true);
        if (wanderNext != myCell)
            StartCoroutine(StepMove(myCell, wanderNext));
    }

    // =========================================================
    // Step Choice (핵심: 점유 셀 회피 + 막히면 백오프)
    // =========================================================
    private Vector2Int ChooseNextStep(Vector2Int from, Vector2Int goal, bool preferCloser)
    {
        // 후보 8방/4방 중:
        // 1) 목표와의 거리 감소하는 셀 중, 워커블 + (옵션)비점유 우선
        // 2) 거리 동일한 셀 중, 워커블 + 비점유
        // 3) (옵션) 막히면 목표에서 멀어지는 셀(백오프)
        Vector2Int best = from;
        int curDist = FieldCombatUtils.Chebyshev(from, goal);

        // 1) reduce
        if (preferCloser)
        {
            if (TryPickCandidate(from, goal, wantRelation: -1, out best))
                return best;
        }

        // 2) equal
        if (TryPickCandidate(from, goal, wantRelation: 0, out best))
            return best;

        // 3) backoff
        if (backoffWhenBlocked)
        {
            if (TryPickCandidate(from, goal, wantRelation: +1, out best))
                return best;
        }

        return from;
    }

    /// <summary>
    /// wantRelation:
    /// -1 : goal 거리 감소하는 후보
    ///  0 : 동일한 후보
    /// +1 : goal 거리 증가하는 후보(백오프)
    /// </summary>
    private bool TryPickCandidate(Vector2Int from, Vector2Int goal, int wantRelation, out Vector2Int picked)
    {
        picked = from;
        int curDist = FieldCombatUtils.Chebyshev(from, goal);

        // 방향 우선순위를 약간 랜덤화해서 서로 밀어내기(고정 패턴 방지)
        // 간단히 dx/dy loop를 랜덤 순서로 섞지는 않고, 후보를 다 보며 첫 적합을 고름.
        // 필요하면 여기에서 후보 배열을 만들어 Shuffle해도 됨.

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!allowDiagonal && Mathf.Abs(dx) + Mathf.Abs(dy) == 2) continue;

                Vector2Int n = new Vector2Int(from.x + dx, from.y + dy);
                if (!IsWalkable(n)) continue;

                if (avoidOccupiedCell && IsEnemyOccupied(n)) continue;

                int nd = FieldCombatUtils.Chebyshev(n, goal);
                int rel = nd.CompareTo(curDist); // -1 감소, 0 동일, +1 증가

                if (rel == wantRelation)
                {
                    picked = n;
                    return true;
                }
            }

        return false;
    }

    // =========================================================
    // Wander
    // =========================================================
    private void PickNewWanderTarget()
    {
        if (gridBoard == null) return;
        Vector2Int myCell = gridBoard.WorldToCell(transform.position);

        for (int t = 0; t < 16; t++)
        {
            int dx = Random.Range(-wanderRadius, wanderRadius + 1);
            int dy = Random.Range(-wanderRadius, wanderRadius + 1);
            Vector2Int c = new Vector2Int(myCell.x + dx, myCell.y + dy);

            if (!IsWalkable(c)) continue;
            if (avoidOccupiedCell && IsEnemyOccupied(c)) continue;

            _wanderTargetCell = c;
            return;
        }

        _wanderTargetCell = myCell;
    }

    // =========================================================
    // Movement
    // =========================================================
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
    // Occupied / Walkable
    // =========================================================
    private bool IsEnemyOccupied(Vector2Int cell)
    {
        if (gridBoard == null) return false;

        Vector3 w = gridBoard.CellToWorld(cell);
        Vector2 wp = new Vector2(w.x, w.y);

        Collider2D[] cols = Physics2D.OverlapCircleAll(wp, Mathf.Max(0.01f, occupiedCheckRadius), enemyLayerMask);
        if (cols == null || cols.Length == 0) return false;

        for (int i = 0; i < cols.Length; i++)
        {
            var inst = cols[i].GetComponentInParent<EnemyInstance>();
            if (inst == null) continue;
            if (inst.transform == transform) continue;
            if (inst.currentHP <= 0) continue;
            return true;
        }

        return false;
    }

    private bool IsWalkable(Vector2Int cell)
    {
        // GridBoard에 이동 막힘 체크 함수가 있으면 사용. 없으면 true.
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
    // Combat Active / Start Combat (보호수준 회피)
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

    /// <summary>
    /// FieldCombatController.StartCombat(...)가 private/protected여도 호출 가능한 public 엔트리를 우선 시도.
    /// - 우선순위: StartCombatPublic / StartCombatRequest / RequestStartCombat / BeginCombatFromFieldAI / EnterFromFieldAI
    /// - 없으면 아무 것도 안 함(로그 없이 조용히 실패)
    /// </summary>
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

        // (Transform, bool) 또는 (Transform) 형태 모두 시도
        foreach (var name in candidates)
        {
            var m2 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Transform), typeof(bool) }, null);
            if (m2 != null)
            {
                m2.Invoke(fc, new object[] { enemy, enemyFirst });
                return;
            }

            var m1 = t.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                new[] { typeof(Transform) }, null);
            if (m1 != null)
            {
                m1.Invoke(fc, new object[] { enemy });
                return;
            }
        }
    }
}
