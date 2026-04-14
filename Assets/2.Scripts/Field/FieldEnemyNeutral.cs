using System.Collections;
using UnityEngine;

/// <summary>
/// AI5 - Neutral Enemy (중립)
/// - Does NOT aggro first
/// - When hit, becomes hostile and fights back
/// - Example: Anchovy, Sardine
/// </summary>
[RequireComponent(typeof(EnemyInstance))]
public class FieldEnemyNeutral : MonoBehaviour
{
    public enum State { Idle, Wander, Hostile }

    [Header("Refs")]
    public Transform player;
    public GridBoard gridBoard;
    public FieldTimeManager timeManager;
    public GridOccupancyRegistry occupancy;

    [Header("Alert UI")]
    public GameObject alertIconPrefab;
    private GameObject _alertInstance;
    private float _alertTimer;

    [Header("Wander")]
    public bool enableWander = true;
    public int wanderRadius = 3;
    public int wanderRetargetEveryTicks = 5;
    [Range(0f, 1f)] public float wanderMoveChance = 0.2f;

    [Header("Hostile Settings")]
    [Tooltip("How many turns to stay hostile after being hit")]
    public int hostileDuration = 5;
    [Tooltip("Chase range when hostile")]
    public int hostileChaseRange = 8;

    [Header("Move")]
    public bool allowDiagonal = true;
    public float stepMoveDuration = 0.08f;

    [Header("Debug")]
    public State state = State.Idle;
    public bool showDebugLogs = false;

    private EnemyInstance _enemy;
    private Vector2Int _wanderTargetCell;
    private int _wanderTickCounter;
    private bool _moving;
    private int _hostileTimer = 0;
    private int _lastHP;

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

        // Track HP for hit detection
        if (_enemy != null)
            _lastHP = _enemy.currentHP;

        // Register occupancy
        if (occupancy != null && gridBoard != null)
        {
            Vector2Int myCell = gridBoard.WorldToCell(transform.position);
            transform.position = gridBoard.CellToWorld(myCell);  // ← 이 줄 추가
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
        // Alert timer
        if (_alertTimer > 0f)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f && _alertInstance != null)
            {
                Destroy(_alertInstance);
                _alertInstance = null;
            }
        }

        // Check if we got hit (HP decreased)
        if (_enemy != null && _enemy.currentHP < _lastHP)
        {
            OnHit();
            _lastHP = _enemy.currentHP;
        }
    }

    /// <summary>
    /// Called when this enemy takes damage
    /// </summary>
    private void OnHit()
    {
        if (state != State.Hostile)
        {
            state = State.Hostile;
            ShowAlertIcon("!");

            if (showDebugLogs)
                Debug.Log($"[Neutral] {_enemy?.definition?.displayName ?? name} became hostile!");
        }

        // Reset/extend hostile timer
        _hostileTimer = hostileDuration;
    }


    // LateUpdate: Animator position override fix
    private void LateUpdate()
    {
        if (_enemy != null && _enemy.currentHP <= 0) return;
        if (_moving) return;
        if (occupancy == null || gridBoard == null) return;

        if (occupancy.TryGetCurrentCell(transform, out var cell))
        {
            Vector3 correctPos = gridBoard.CellToWorld(cell);
            float snapThreshold = Mathf.Max(0.1f, gridBoard.cellSize * 0.35f);
            if (Vector3.SqrMagnitude(transform.position - correctPos) > snapThreshold * snapThreshold)
                transform.position = correctPos;
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
        if (gridBoard == null) return;

        Vector2Int myCell = GetMyCell();

        // Hostile timer countdown
        if (_hostileTimer > 0)
        {
            _hostileTimer--;
            if (_hostileTimer <= 0 && state == State.Hostile)
            {
                state = enableWander ? State.Wander : State.Idle;
                if (showDebugLogs)
                    Debug.Log($"[Neutral] {_enemy?.definition?.displayName ?? name} calmed down.");
            }
        }

        // State machine
        switch (state)
        {
            case State.Idle:
                // Do nothing, just stand
                if (enableWander && Random.value < 0.1f)
                    state = State.Wander;
                break;

            case State.Wander:
                DoWander(myCell);
                break;

            case State.Hostile:
                DoHostileChase(myCell);
                break;
        }
    }

    private void DoWander(Vector2Int myCell)
    {
        _wanderTickCounter++;

        // Move chance check
        if (Random.value > wanderMoveChance)
            return;

        // Retarget
        if (_wanderTickCounter >= wanderRetargetEveryTicks)
        {
            PickNewWanderTarget();
            _wanderTickCounter = 0;
        }

        // Move toward target
        Vector2Int nextStep = ChooseNextStep(myCell, _wanderTargetCell);
        if (nextStep != myCell && !IsOccupiedByOther(nextStep))
        {
            TryStartStepMove(myCell, nextStep);
        }
    }

    private void DoHostileChase(Vector2Int myCell)
    {
        if (player == null) return;

        Vector2Int pCell = gridBoard.WorldToCell(player.position);
        int dist = FieldCombatUtils.Chebyshev(myCell, pCell);

        // If too far, give up
        if (dist > hostileChaseRange)
        {
            _hostileTimer = 0;
            state = enableWander ? State.Wander : State.Idle;
            return;
        }

        // 시야 + 엄폐(해초/산호): 기하학적 LoS와 틱당 인식 확률(TryDetectPlayer)
        var visionSys = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        if (visionSys != null && !visionSys.TryDetectPlayer(myCell, pCell))
            return;

        // Adjacent = attack handled by FieldMultiEnemyAttack
        if (dist <= 1)
            return;

        // Chase
        Vector2Int nextStep = ChooseNextStep(myCell, pCell);
        if (nextStep != myCell && !IsOccupiedByOther(nextStep))
        {
            TryStartStepMove(myCell, nextStep);
        }
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

    // =========================================================
    // Helpers
    // =========================================================
    private Vector2Int GetMyCell()
    {
        if (occupancy != null && occupancy.TryGetCurrentCell(transform, out var c))
            return c;
        return gridBoard.WorldToCell(transform.position);
    }

    private bool IsOccupiedByOther(Vector2Int cell)
    {
        if (occupancy == null) return false;
        var occ = occupancy.GetOccupant(cell);
        return occ != null && occ != transform;
    }

    private bool IsWalkable(Vector2Int cell)
    {
        // Use reflection to check GridBoard walkability
        var t = gridBoard.GetType();
        foreach (var name in new[] { "IsCellBlocked", "IsMoveBlocked", "IsBlocked" })
        {
            var m = t.GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
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

    // =========================================================
    // Alert Icon
    // =========================================================
    private void ShowAlertIcon(string text = "!")
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

        // Default alert
        _alertInstance = CreateDefaultAlertIcon(text);
    }

    private GameObject CreateDefaultAlertIcon(string text)
    {
        GameObject alert = new GameObject("AlertIcon");
        alert.transform.SetParent(transform);
        alert.transform.localPosition = Vector3.up * 1.5f;

        TextMesh textMesh = alert.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.fontSize = 80;
        textMesh.color = Color.yellow; // Yellow for neutral becoming hostile
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.1f;

        alert.AddComponent<Billboard>();
        
        return alert;
    }

    /// <summary>
    /// Check if this enemy is currently hostile (for combat system)
    /// </summary>
    public bool IsHostile => state == State.Hostile;
}