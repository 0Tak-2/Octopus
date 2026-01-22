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

    [Header("Alert UI")]
    [Tooltip("인식 시 표시할 느낌표 프리팹")]
    public GameObject alertIconPrefab;
    private GameObject _alertInstance;
    private float _alertTimer;

    [Header("Detect")]
    [Tooltip("(Deprecated) Definition에서 자동으로 읽어옴. 오버라이드하려면 useCustomDetection=true")]
    public int aggroRange = 6;              // Chebyshev
    [Tooltip("(Deprecated) Definition에서 자동으로 읽어옴")]
    public bool requireLoS = false;
    [Tooltip("(Deprecated) Definition에서 자동으로 읽어옴")]
    public int aggroDropDistance = 15;
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
    public bool isAggro = false; // 어그로 상태

    private EnemyInstance _enemy;
    private Vector2Int _wanderTargetCell;
    private int _wanderTickCounter;
    private bool _moving;
    private bool _wasChasing = false; // 이전 프레임에 추격 중이었는지

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
            aggroDropDistance = Mathf.Max(aggroRange + 1, _enemy.definition.aggroDropDistance);
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

        // 느낌표 제거
        if (_alertInstance != null)
        {
            Destroy(_alertInstance);
            _alertInstance = null;
        }
    }

    private void Update()
    {
        // 느낌표 타이머 처리
        if (_alertTimer > 0f)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f && _alertInstance != null)
            {
                Destroy(_alertInstance);
                _alertInstance = null;
            }
        }
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

        // 시야 체크 (첫 인식)
        bool canSee = distToPlayer <= aggroRange;
        if (canSee && requireLoS)
            canSee = FieldCombatUtils.HasLineOfSight(gridBoard, myCell, pCell);

        // 어그로 시스템
        if (canSee)
        {
            // 플레이어 발견 - 어그로 활성화
            if (!isAggro)
            {
                isAggro = true;
                ShowAlertIcon(); // 느낌표!
            }
        }
        else if (isAggro)
        {
            // 어그로 중 - 거리 체크로만 해제
            if (distToPlayer > aggroDropDistance)
            {
                // 너무 멀어짐 - 어그로 해제
                isAggro = false;
            }
            // 아니면 어그로 유지 (벽 뒤로 가도 계속 쫓아옴!)
        }

        // 인식 전환 시 느낌표 표시 (백업)
        if (canSee && !_wasChasing)
        {
            ShowAlertIcon();
        }
        _wasChasing = canSee || isAggro;

        // 상태 결정
        if (isAggro || canSee)
        {
            state = State.Chase;
        }
        else if (enableWander)
        {
            state = State.Wander;
        }
        else
        {
            state = State.Frozen;
        }

        if (state == State.Frozen) return;

        if (state == State.Chase)
        {
            if (startCombatWhenAdjacent && fieldCombat != null && distToPlayer <= combatStartRange)
            {
                if (!GetCombatActiveBestEffort(fieldCombat))
                    TryStartCombat(fieldCombat, transform, enemyTurnFirst);
                return;
            }

            // ✅ 다음 칸에 아군이 있는지만 체크 (넓은 공간에서 우회 가능하게)
            Vector2Int nextStep = ChooseNextStep(myCell, pCell);
            bool nextCellBlocked = (nextStep == myCell) || IsOccupiedByOther(nextStep);

            if (nextCellBlocked)
            {
                // 다음 칸이 막혔음

                // 원거리 공격 가능한지 체크
                bool isRanged = _enemy != null && _enemy.definition != null && _enemy.definition.attackRange >= 2;

                if (isRanged && distToPlayer <= _enemy.definition.attackRange)
                {
                    // TODO: 원거리 공격 (현재는 대기)
                    // 나중에 원거리 공격 시스템 추가
                    return;
                }

                // 근접 적이고 다음 칸 막혔음 → 대기
                return;
            }

            // 다음 칸 비었음 → 전진!
            if (nextStep != myCell)
                TryStartStepMove(myCell, nextStep);
            return;
        }

        // Wander - 확률 체크
        _wanderTickCounter++;

        // Definition에서 확률 가져오기
        float moveChance = 0.3f; // 기본값
        if (_enemy != null && _enemy.definition != null)
        {
            moveChance = _enemy.definition.wanderMoveChance;
        }

        // 확률 체크 - 실패하면 이동 안 함
        if (Random.value > moveChance)
        {
            return; // 이번 턴은 가만히 있음
        }

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

    /// <summary>
    /// 느낌표 표시
    /// </summary>
    private void ShowAlertIcon()
    {
        // 이미 표시 중이면 타이머만 리셋
        if (_alertInstance != null)
        {
            _alertTimer = _enemy?.definition?.detectionAlertDuration ?? 1.5f;
            return;
        }

        // 프리팹이 있으면 사용
        if (alertIconPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
            _alertInstance = Instantiate(alertIconPrefab, spawnPos, Quaternion.identity, transform);
        }
        else
        {
            // 프리팹이 없으면 자동 생성 (빨간 느낌표)
            _alertInstance = CreateAlertIcon();
        }

        _alertTimer = _enemy?.definition?.detectionAlertDuration ?? 1.5f;
    }

    /// <summary>
    /// 느낌표 자동 생성 (프리팹 없을 때)
    /// </summary>
    private GameObject CreateAlertIcon()
    {
        GameObject alert = new GameObject("AlertIcon");
        alert.transform.SetParent(transform);
        alert.transform.localPosition = Vector3.up * 1.5f;

        // TextMesh 사용 (기본 Unity)
        TextMesh textMesh = alert.AddComponent<TextMesh>();
        textMesh.text = "!";
        textMesh.fontSize = 80;
        textMesh.color = Color.red;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.1f;

        // 카메라를 향하도록
        var billboard = alert.AddComponent<Billboard>();

        // 애니메이션 제거 - 고정

        return alert;
    }

    private Vector2Int GetMyCell()
    {
        // 점유 레지스트리에 등록된 셀이 있으면 그걸 우선
        if (occupancy != null && occupancy.TryGetCurrentCell(transform, out var c))
            return c;

        return gridBoard.WorldToCell(transform.position);
    }

    /// <summary>
    /// 목표까지 경로에 아군(다른 적)이 있는지 체크
    /// </summary>
    private bool HasAllyInPath(Vector2Int from, Vector2Int to)
    {
        if (occupancy == null) return false;

        // 직선 경로상의 모든 셀 체크
        int dx = to.x - from.x;
        int dy = to.y - from.y;

        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0) return false;

        // 방향 벡터 정규화
        float stepX = (float)dx / steps;
        float stepY = (float)dy / steps;

        // 경로상의 각 셀 체크 (자신과 목표 제외)
        for (int i = 1; i < steps; i++)
        {
            int checkX = from.x + Mathf.RoundToInt(stepX * i);
            int checkY = from.y + Mathf.RoundToInt(stepY * i);
            Vector2Int checkCell = new Vector2Int(checkX, checkY);

            // 이 셀에 다른 적이 있는지 확인
            var occupant = occupancy.GetOccupant(checkCell);
            if (occupant != null && occupant != transform)
            {
                // 적인지 확인 (EnemyInstance 있으면 적)
                if (occupant.GetComponent<EnemyInstance>() != null)
                {
                    return true; // 경로에 아군 발견!
                }
            }
        }

        return false; // 경로 깨끗함
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

        // ✅ 이동했으므로 행동 완료 표시
        if (_enemy != null)
        {
            _enemy.MarkAsActed();
        }
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