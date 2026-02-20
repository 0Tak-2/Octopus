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
    [Tooltip("Alert icon prefab")]
    public GameObject alertIconPrefab;
    private GameObject _alertInstance;
    private float _alertTimer;

    [Header("Detect")]
    [Tooltip("(Deprecated) FieldVisionSystem uses own range")]
    public int aggroRange = 5;
    public bool requireLoS = true;
    public int aggroDropDistance = 15;
    public bool useCustomDetection = false;

    [Header("Wander")]
    public bool enableWander = true;
    public int wanderRadius = 4;
    public int wanderRetargetEveryTicks = 4;

    [Header("Move")]
    public bool allowDiagonal = true;
    public float stepMoveDuration = 0.08f;

    [Header("Debug")]
    public State state = State.Wander;
    public bool isAggro = false;
    public bool showDebugLogs = false;

    private EnemyInstance _enemy;
    private Vector2Int _wanderTargetCell;
    private int _wanderTickCounter;
    private bool _moving;
    private bool _wasChasing = false;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
    }

    private void Start()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (timeManager == null) timeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();

        if (!useCustomDetection && _enemy != null && _enemy.definition != null)
        {
            aggroRange = Mathf.Max(1, _enemy.definition.fieldDetectionRange);
            requireLoS = _enemy.definition.fieldRequireLoS;
            aggroDropDistance = Mathf.Max(aggroRange + 1, _enemy.definition.aggroDropDistance);
        }

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
        if (occupancy != null) occupancy.Release(transform);
        if (_alertInstance != null)
        {
            Destroy(_alertInstance);
            _alertInstance = null;
        }
    }

    private void Update()
    {
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

    // =========================================================
    // LateUpdate: Animator position override fix
    // =========================================================
    private void LateUpdate()
    {
        if (_enemy != null && _enemy.currentHP <= 0) return;
        if (_moving) return;
        if (occupancy == null || gridBoard == null) return;

        if (occupancy.TryGetCurrentCell(transform, out var cell))
        {
            Vector3 correctPos = gridBoard.CellToWorld(cell);
            if (Vector3.SqrMagnitude(transform.position - correctPos) > 0.01f)
            {
                transform.position = correctPos;
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

        Vector2Int myCell = GetMyCell();
        Vector2Int pCell = gridBoard.WorldToCell(player.position);
        int distToPlayer = FieldCombatUtils.Chebyshev(myCell, pCell);

        // ========== FieldVisionSystem detection ==========
        var visionSys = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        bool canSee = false;

        if (visionSys != null)
        {
            // LoS + range + detection chance (cover/crouch)
            canSee = visionSys.TryDetectPlayer(myCell, pCell);
        }
        else
        {
            // Fallback: old system
            canSee = distToPlayer <= aggroRange;
            if (canSee && requireLoS)
                canSee = FieldCombatUtils.HasLineOfSight(gridBoard, myCell, pCell);
        }

        // Aggro system
        if (canSee)
        {
            if (!isAggro)
            {
                isAggro = true;
                ShowAlertIcon();
            }
        }
        else if (isAggro)
        {
            // Already aggro - check if can still see (LoS only, no detection roll)
            bool canStillSee = false;
            if (visionSys != null)
                canStillSee = visionSys.CanSeePlayer(myCell, pCell);
            else
                canStillSee = distToPlayer <= aggroRange &&
                    (!requireLoS || FieldCombatUtils.HasLineOfSight(gridBoard, myCell, pCell));

            if (!canStillSee && distToPlayer > aggroDropDistance)
            {
                isAggro = false;
            }
        }

        if (canSee && !_wasChasing)
            ShowAlertIcon();
        _wasChasing = canSee || isAggro;

        // State
        if (isAggro || canSee)
            state = State.Chase;
        else if (enableWander)
            state = State.Wander;
        else
            state = State.Frozen;

        if (state == State.Frozen) return;

        if (state == State.Chase)
        {
            bool isRanged = _enemy != null && _enemy.definition != null && _enemy.definition.attackRange >= 2;
            if (isRanged && distToPlayer <= _enemy.definition.attackRange)
            {
                if (showDebugLogs)
                    Debug.Log($"[WanderChase] {_enemy.definition.displayName} in range. Stay.");
                return;
            }

            Vector2Int nextStep = ChooseNextStep(myCell, pCell);
            bool nextCellBlocked = (nextStep == myCell) || IsOccupiedByOther(nextStep);

            if (nextCellBlocked) return;

            if (nextStep != myCell)
                TryStartStepMove(myCell, nextStep);
            return;
        }

        // Wander
        _wanderTickCounter++;

        float moveChance = 0.3f;
        if (_enemy != null && _enemy.definition != null)
            moveChance = _enemy.definition.wanderMoveChance;

        if (Random.value > moveChance) return;

        if (_wanderTickCounter >= wanderRetargetEveryTicks)
        {
            PickNewWanderTarget();
            _wanderTickCounter = 0;
        }

        Vector2Int wanderStep = ChooseNextStep(myCell, _wanderTargetCell);
        if (wanderStep != myCell && !IsOccupiedByOther(wanderStep))
            TryStartStepMove(myCell, wanderStep);
    }

    // =============================================================
    // Alert Icon
    // =============================================================
    private void ShowAlertIcon()
    {
        if (_alertInstance != null)
        {
            _alertTimer = 1.5f;
            return;
        }

        float duration = 1.5f;
        if (_enemy != null && _enemy.definition != null)
            duration = _enemy.definition.detectionAlertDuration;
        _alertTimer = duration;

        if (alertIconPrefab != null)
        {
            _alertInstance = Instantiate(alertIconPrefab, transform);
            _alertInstance.transform.localPosition = Vector3.up * 1.5f;
            return;
        }

        _alertInstance = CreateDefaultAlertIcon();
    }

    private GameObject CreateDefaultAlertIcon()
    {
        GameObject alert = new GameObject("AlertIcon");
        alert.transform.SetParent(transform);
        alert.transform.localPosition = Vector3.up * 1.5f;

        TextMesh textMesh = alert.AddComponent<TextMesh>();
        textMesh.text = "!";
        textMesh.fontSize = 80;
        textMesh.color = Color.red;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.1f;

        alert.AddComponent<Billboard>();

        return alert;
    }

    private Vector2Int GetMyCell()
    {
        if (occupancy != null && occupancy.TryGetCurrentCell(transform, out var c))
            return c;
        return gridBoard.WorldToCell(transform.position);
    }

    private bool HasAllyInPath(Vector2Int from, Vector2Int to)
    {
        if (occupancy == null) return false;
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        int steps = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        if (steps == 0) return false;

        float stepX = (float)dx / steps;
        float stepY = (float)dy / steps;

        for (int i = 1; i < steps; i++)
        {
            int checkX = from.x + Mathf.RoundToInt(stepX * i);
            int checkY = from.y + Mathf.RoundToInt(stepY * i);
            Vector2Int checkCell = new Vector2Int(checkX, checkY);

            var occupant = occupancy.GetOccupant(checkCell);
            if (occupant != null && occupant != transform)
            {
                if (occupant.GetComponent<EnemyInstance>() != null)
                    return true;
            }
        }
        return false;
    }

    // =========================================================
    // Movement
    // =========================================================
    private void TryStartStepMove(Vector2Int fromCell, Vector2Int toCell)
    {
        if (occupancy != null)
        {
            if (!occupancy.TryMoveReserve(transform, toCell))
                return;
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

        if (_enemy != null)
            _enemy.MarkAsActed();
    }

    private Vector2Int ChooseNextStep(Vector2Int from, Vector2Int goal)
    {
        Vector2Int best = from;
        int bestDist = FieldCombatUtils.Chebyshev(from, goal);

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (!allowDiagonal && Mathf.Abs(dx) + Mathf.Abs(dy) == 2) continue;

                Vector2Int n = new Vector2Int(from.x + dx, from.y + dy);
                if (!IsWalkable(n)) continue;
                if (IsOccupiedByOther(n)) continue;

                int d = FieldCombatUtils.Chebyshev(n, goal);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
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
}