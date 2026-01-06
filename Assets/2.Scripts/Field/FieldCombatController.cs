using System.Collections;
using UnityEngine;

public class FieldCombatController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;
    public PlayerStats playerStats;

    [Header("Enemy (1v1)")]
    public Transform enemy;
    public string enemyTag = "Enemy";
    private EnemyInstance enemyInstanceCache;

    [Header("Detection / End")]
    public int detectionRangeChebyshev = 6;
    public bool returnEnemyToHomeOnEnd = true;

    [Header("Death Handling")]
    public bool disableEnemyOnDeath = true;
    public bool destroyEnemyOnDeath = false;

    [Header("Initiative")]
    public bool enemyStartsFirstOnDetect = true;

    [Header("Turn Timing (Visual)")]
    [Tooltip("플레이어가 Time을 쓴 뒤, 적 턴이 시작되기까지의 실제 시간 딜레이(겹침 방지).")]
    public float enemyTurnStartDelaySeconds = 0.18f;

    [Tooltip("적 턴 시작 딜레이 코루틴을 중복 실행하지 않도록 방지.")]
    public bool preventOverlapDelay = true;

    [Header("Player Turn")]
    public KeyCode waitTurnKey = KeyCode.Space;
    public KeyCode enterFocusedCombatKey = KeyCode.E;

    public int playerAttackRange = 1;
    public int playerAttackDamage = 1;
    public bool requireLoSForPlayerAttack = true;

    [Header("Enemy Turn")]
    public bool enemyChasesWhenOutOfRange = true;
    public int enemyAttackRange = 1;
    public int enemyDamage = 1;
    public bool enemyRequiresLoSToAttack = true;

    [Header("Smooth Move / Dash Visuals")]
    public float stepMoveDuration = 0.12f;
    public float dashDuration = 0.08f;
    public float dashReturnDuration = 0.06f;

    [Header("Move Block Debug")]
    public bool ignoreMoveBlockedForDebug = false;
    public bool logChaseDebug = false;

    [Header("Click Raycast")]
    public Camera worldCamera;
    public LayerMask enemyClickLayerMask = ~0;

    [Header("Debug")]
    public bool logTransitions = true;

    public FieldCombatState State { get; private set; } = new FieldCombatState();

    private Vector2Int playerCellCached;
    private Vector2Int enemyCellCached;

    // "플레이어가 Time을 썼다"를 기다리는 플래그 (이벤트 기반)
    private bool _waitingForPlayerTimeSpend = false;

    // 적 턴 시작 딜레이 코루틴 핸들
    private Coroutine _enemyTurnDelayCoroutine;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;

        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();

        if (playerStats == null && player != null)
            playerStats = player.GetComponent<PlayerStats>();

        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();
    }

    private void OnEnable()
    {
        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();

        if (fieldTimeManager != null)
            fieldTimeManager.OnTimeAdvanced += HandleTimeAdvanced;
    }

    private void OnDisable()
    {
        if (fieldTimeManager != null)
            fieldTimeManager.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private void HandleTimeAdvanced(int delta, int newTotalTime)
    {
        // 전투 아닐 때는 무시
        if (State.phase == FieldCombatState.Phase.None || State.phase == FieldCombatState.Phase.Ended)
            return;

        // ✅ 핵심: 플레이어 턴에서 Time이 증가했다면 -> (딜레이 후) 적 턴 시작
        if (State.phase == FieldCombatState.Phase.PlayerTurn && _waitingForPlayerTimeSpend)
        {
            _waitingForPlayerTimeSpend = false;

            if (logTransitions)
                Debug.Log($"[FieldCombat] Player spent time (+{delta}) -> schedule EnemyTurn after {enemyTurnStartDelaySeconds:0.00}s");

            ScheduleEnemyTurnAfterDelay();
        }
    }

    private void ScheduleEnemyTurnAfterDelay()
    {
        // 중복 방지: 이미 딜레이 코루틴이 돌고 있으면 다시 시작하지 않음
        if (preventOverlapDelay && _enemyTurnDelayCoroutine != null)
            return;

        _enemyTurnDelayCoroutine = StartCoroutine(EnemyTurnDelayRoutine());
    }

    private IEnumerator EnemyTurnDelayRoutine()
    {
        // 실제 시간 딜레이 (겹침 방지)
        if (enemyTurnStartDelaySeconds > 0f)
            yield return new WaitForSeconds(enemyTurnStartDelaySeconds);

        // 전투가 끝났으면 중단
        if (State.phase == FieldCombatState.Phase.None || State.phase == FieldCombatState.Phase.Ended)
        {
            _enemyTurnDelayCoroutine = null;
            yield break;
        }

        // 플레이어가 죽었거나 적이 죽었으면 중단
        if (playerStats != null && playerStats.hp <= 0)
        {
            _enemyTurnDelayCoroutine = null;
            yield break;
        }
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0)
        {
            _enemyTurnDelayCoroutine = null;
            yield break;
        }

        // ✅ 적 턴으로 전환
        State.phase = FieldCombatState.Phase.EnemyTurn;

        _enemyTurnDelayCoroutine = null;
    }

    private void Update()
    {
        if (player == null || gridBoard == null) return;

        // Not in combat: auto-detect start
        if (State.phase == FieldCombatState.Phase.None || State.phase == FieldCombatState.Phase.Ended)
        {
            TryAutoStartCombat();
            return;
        }

        // 적 턴 딜레이가 걸린 상태에서 움직임/입력 겹침 방지:
        // busy가 true면 적 턴 실행 중, false면 평시
        if (State.busy) return;

        // Death end
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0)
        {
            HandleEnemyDeathAndEnd();
            return;
        }
        if (playerStats != null && playerStats.hp <= 0)
        {
            EndCombat(escaped: false);
            return;
        }

        // Escape end
        if (!IsEnemyStillDetectingPlayer())
        {
            EndCombat(escaped: true);
            return;
        }

        // Drive turns: Update is the ONLY place that starts enemy coroutine
        if (State.phase == FieldCombatState.Phase.EnemyTurn)
        {
            State.busy = true; // prevent double-start
            StartCoroutine(EnemyTurnRoutine());
            return;
        }

        if (State.phase == FieldCombatState.Phase.PlayerTurn)
        {
            HandlePlayerTurn();
            return;
        }
    }

    // =========================================================
    // Start / End
    // =========================================================
    private void TryAutoStartCombat()
    {
        Transform target = enemy;

        if (target == null)
        {
            var go = GameObject.FindGameObjectWithTag(enemyTag);
            if (go != null) target = go.transform;
        }
        if (target == null) return;
        if (!target.gameObject.activeInHierarchy) return;

        var inst = target.GetComponent<EnemyInstance>();
        if (inst != null && inst.currentHP <= 0) return;

        var pCell = gridBoard.WorldToCell(player.position);
        var eCell = gridBoard.WorldToCell(target.position);

        if (FieldCombatUtils.Chebyshev(pCell, eCell) > detectionRangeChebyshev) return;
        if (!FieldCombatUtils.HasLineOfSight(gridBoard, eCell, pCell)) return;

        StartCombat(target, enemyFirst: enemyStartsFirstOnDetect);
    }

    private void StartCombat(Transform targetEnemy, bool enemyFirst)
    {
        enemy = targetEnemy;
        enemyInstanceCache = enemy != null ? enemy.GetComponent<EnemyInstance>() : null;
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return;

        var enemyHome = gridBoard.WorldToCell(enemy.position);
        State.ResetForNewCombat(player, enemy, enemyHome, detectionRangeChebyshev, returnEnemyToHomeOnEnd);

        playerCellCached = gridBoard.WorldToCell(player.position);
        enemyCellCached = gridBoard.WorldToCell(enemy.position);

        State.busy = false;
        State.playerActedThisTurn = false;
        State.turnIndex = 0;

        // 딜레이 코루틴 정리
        if (_enemyTurnDelayCoroutine != null)
        {
            StopCoroutine(_enemyTurnDelayCoroutine);
            _enemyTurnDelayCoroutine = null;
        }

        State.phase = enemyFirst ? FieldCombatState.Phase.EnemyTurn : FieldCombatState.Phase.PlayerTurn;

        // 플레이어 턴이면 time spend 기다림 ON
        _waitingForPlayerTimeSpend = (State.phase == FieldCombatState.Phase.PlayerTurn);

        if (logTransitions)
            Debug.Log($"[FieldCombat] START vs {enemy.name} (first={(enemyFirst ? "Enemy" : "Player")})");
    }

    private void EndCombat(bool escaped)
    {
        if (logTransitions)
            Debug.Log($"[FieldCombat] END (escaped={escaped})");

        // 딜레이 코루틴 정리
        if (_enemyTurnDelayCoroutine != null)
        {
            StopCoroutine(_enemyTurnDelayCoroutine);
            _enemyTurnDelayCoroutine = null;
        }

        if (State.returnEnemyToHomeOnEnd && enemy != null && escaped)
        {
            enemyCellCached = State.enemyHomeCell;
            enemy.position = gridBoard.CellToWorld(enemyCellCached);
        }

        State.phase = FieldCombatState.Phase.Ended;
        State.busy = false;
        State.playerActedThisTurn = false;
        State.turnIndex = 0;
        _waitingForPlayerTimeSpend = false;
    }

    private void HandleEnemyDeathAndEnd()
    {
        if (enemy != null)
        {
            if (destroyEnemyOnDeath) Destroy(enemy.gameObject);
            else if (disableEnemyOnDeath) enemy.gameObject.SetActive(false);
        }
        EndCombat(escaped: false);
    }

    // =========================================================
    // Enemy Turn (does NOT advance Time)
    // =========================================================
    private IEnumerator EnemyTurnRoutine()
    {
        yield return null;

        if (enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            EndCombat(escaped: false);
            State.busy = false;
            yield break;
        }

        playerCellCached = gridBoard.WorldToCell(player.position);
        enemyCellCached = gridBoard.WorldToCell(enemy.position);

        int dist = FieldCombatUtils.Chebyshev(enemyCellCached, playerCellCached);
        bool los = FieldCombatUtils.HasLineOfSight(gridBoard, enemyCellCached, playerCellCached);

        bool canAttack = dist <= enemyAttackRange && (!enemyRequiresLoSToAttack || los);

        if (canAttack)
        {
            yield return DashHitAndReturn(enemy, gridBoard.CellToWorld(enemyCellCached), gridBoard.CellToWorld(playerCellCached));
            DealDamageToPlayer(enemyDamage);
            if (logTransitions) Debug.Log($"[FieldCombat] Enemy attacks for {enemyDamage}");
        }
        else
        {
            if (enemyChasesWhenOutOfRange)
            {
                if (TryGetChaseStep(enemyCellCached, playerCellCached, out Vector2Int next))
                {
                    Vector3 from = enemy.position;
                    Vector3 to = gridBoard.CellToWorld(next);

                    enemyCellCached = next;
                    yield return SmoothMove(enemy, from, to, stepMoveDuration);

                    if (logTransitions) Debug.Log($"[FieldCombat] Enemy moves to {next}");
                }
                else
                {
                    if (logChaseDebug) Debug.LogWarning($"[FieldCombat] Chase failed: e={enemyCellCached} p={playerCellCached}");
                }
            }
        }

        // Next: Player turn
        State.phase = FieldCombatState.Phase.PlayerTurn;
        _waitingForPlayerTimeSpend = true; // 다음 플레이어 time spend 기다림

        State.busy = false;
    }

    // =========================================================
    // Player Turn
    // =========================================================
    private void HandlePlayerTurn()
    {
        // 플레이어 턴이면 항상 대기 플래그 ON
        if (!_waitingForPlayerTimeSpend)
            _waitingForPlayerTimeSpend = true;

        // (0) Wait -> Time +1 (이벤트로 적 턴 예약)
        if (Input.GetKeyDown(waitTurnKey))
        {
            if (logTransitions) Debug.Log("[FieldCombat] Player waits.");
            AdvanceTimeBy1();
            return;
        }

        // (1) Focused combat
        if (Input.GetKeyDown(enterFocusedCombatKey))
        {
            if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return;
            TryEnterFocusedCombat();
            return;
        }

        // (2) Click attack -> dash + damage + Time +1
        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetClickedEnemy(out Transform clicked) && enemy != null && clicked == enemy)
            {
                StartCoroutine(PlayerClickAttackRoutine());
            }
        }

        // (3) 이동은 PlayerGridMover가 Advance(1) -> 이벤트가 적 턴 예약
    }

    private IEnumerator PlayerClickAttackRoutine()
    {
        if (State.busy) yield break;

        if (enemy == null || !enemy.gameObject.activeInHierarchy) yield break;
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) yield break;

        playerCellCached = gridBoard.WorldToCell(player.position);
        enemyCellCached = gridBoard.WorldToCell(enemy.position);

        int dist = FieldCombatUtils.Chebyshev(playerCellCached, enemyCellCached);
        if (dist > playerAttackRange) yield break;

        if (requireLoSForPlayerAttack && !FieldCombatUtils.HasLineOfSight(gridBoard, playerCellCached, enemyCellCached))
            yield break;

        State.busy = true;

        Vector3 start = player.position;
        Vector3 target = gridBoard.CellToWorld(enemyCellCached);
        yield return DashHitAndReturn(player, start, target);

        DealDamageToEnemy(playerAttackDamage);

        if (logTransitions)
            Debug.Log($"[FieldCombat] Player attacks for {playerAttackDamage}");

        State.busy = false;

        AdvanceTimeBy1(); // 이벤트로 적 턴 예약(딜레이 포함)
    }

    // =========================================================
    // Detection
    // =========================================================
    private bool IsEnemyStillDetectingPlayer()
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return false;
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return false;

        playerCellCached = gridBoard.WorldToCell(player.position);
        enemyCellCached = gridBoard.WorldToCell(enemy.position);

        if (FieldCombatUtils.Chebyshev(enemyCellCached, playerCellCached) > detectionRangeChebyshev)
            return false;

        return FieldCombatUtils.HasLineOfSight(gridBoard, enemyCellCached, playerCellCached);
    }

    // =========================================================
    // Chase
    // =========================================================
    private bool TryGetChaseStep(Vector2Int from, Vector2Int to, out Vector2Int next)
    {
        next = from;

        Vector2Int[] dirs4 =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        int curDist = FieldCombatUtils.Chebyshev(from, to);

        Vector2Int best = from;
        int bestDist = curDist;

        for (int i = 0; i < dirs4.Length; i++)
        {
            var c = from + dirs4[i];
            int nd = FieldCombatUtils.Chebyshev(c, to);

            if (nd >= curDist) continue;
            if (!CanMoveTo(c)) continue;

            if (nd < bestDist)
            {
                bestDist = nd;
                best = c;
            }
        }

        if (best != from)
        {
            next = best;
            return true;
        }

        for (int i = 0; i < dirs4.Length; i++)
        {
            var c = from + dirs4[i];
            if (CanMoveTo(c))
            {
                next = c;
                return true;
            }
        }

        return false;
    }

    private bool CanMoveTo(Vector2Int cell)
    {
        if (ignoreMoveBlockedForDebug) return true;

        var m = gridBoard.GetType().GetMethod("IsMoveBlocked");
        if (m != null)
        {
            var r = m.Invoke(gridBoard, new object[] { cell });
            if (r is bool blocked)
                return !blocked;
        }

        return true;
    }

    // =========================================================
    // Visuals
    // =========================================================
    private IEnumerator SmoothMove(Transform tf, Vector3 from, Vector3 to, float duration)
    {
        if (tf == null) yield break;
        if (duration <= 0f) { tf.position = to; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            tf.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
        tf.position = to;
    }

    private IEnumerator DashHitAndReturn(Transform tf, Vector3 startPos, Vector3 targetPos)
    {
        if (tf == null) yield break;

        Vector3 dir = (targetPos - startPos);
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Vector3 dashPos = startPos + dir * 0.6f;

        yield return SmoothMove(tf, startPos, dashPos, dashDuration);
        yield return SmoothMove(tf, dashPos, startPos, dashReturnDuration);
    }

    // =========================================================
    // Click
    // =========================================================
    private bool TryGetClickedEnemy(out Transform hitTransform)
    {
        hitTransform = null;
        if (worldCamera == null) return false;

        Vector3 mouse = Input.mousePosition;

        Vector3 world = worldCamera.ScreenToWorldPoint(mouse);
        Vector2 world2 = new Vector2(world.x, world.y);
        RaycastHit2D hit2D = Physics2D.Raycast(world2, Vector2.zero, 0f, enemyClickLayerMask);
        if (hit2D.collider != null)
        {
            hitTransform = hit2D.collider.transform;
            return true;
        }

        Ray ray = worldCamera.ScreenPointToRay(mouse);
        if (Physics.Raycast(ray, out RaycastHit hit3D, 500f, enemyClickLayerMask))
        {
            hitTransform = hit3D.collider.transform;
            return true;
        }

        return false;
    }

    // =========================================================
    // Damage / Time
    // =========================================================
    private void DealDamageToPlayer(int dmg)
    {
        if (playerStats == null) return;
        playerStats.hp -= Mathf.Max(0, dmg);
        playerStats.ClampAll();
    }

    private void DealDamageToEnemy(int dmg)
    {
        if (enemyInstanceCache == null)
            enemyInstanceCache = enemy != null ? enemy.GetComponent<EnemyInstance>() : null;

        if (enemyInstanceCache == null) return;

        enemyInstanceCache.currentHP -= Mathf.Max(0, dmg);
        enemyInstanceCache.Clamp();

        if (enemyInstanceCache.currentHP <= 0)
            HandleEnemyDeathAndEnd();
    }

    private void AdvanceTimeBy1()
    {
        if (fieldTimeManager == null) return;
        fieldTimeManager.Advance(1);
    }

    // =========================================================
    // Focused combat hook
    // =========================================================
    private void TryEnterFocusedCombat()
    {
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return;

        MonoBehaviour focused = null;
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            if (mb == null) continue;
            if (mb.GetType().Name == "FocusedCombatManager")
            {
                focused = mb;
                break;
            }
        }

        if (focused == null)
        {
            Debug.LogWarning("[FieldCombat] FocusedCombatManager not found.");
            return;
        }

        string[] candidates = { "EnterCombat", "StartCombat", "BeginCombat", "Begin" };
        foreach (var name in candidates)
        {
            var m = focused.GetType().GetMethod(name);
            if (m == null) continue;

            var p = m.GetParameters();
            if (p.Length == 1 && p[0].ParameterType == typeof(Transform))
            {
                m.Invoke(focused, new object[] { enemy });
                if (logTransitions) Debug.Log("[FieldCombat] Enter FocusedCombat via E");

                EndCombat(escaped: false);
                return;
            }
        }

        Debug.LogWarning("[FieldCombat] FocusedCombatManager found, but no compatible entry method.");
    }
}
