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

    [Header("Death Handling")]
    public bool disableEnemyOnDeath = true;
    public bool destroyEnemyOnDeath = false;

    [Header("Initiative")]
    public bool enemyStartsFirstOnDetect = true;

    [Header("Turn Timing (Visual)")]
    [Tooltip("플레이어가 Time을 쓴 뒤, 적 턴이 시작되기까지의 실제 시간 딜레이(겹침 방지).")]
    public float enemyTurnStartDelaySeconds = 0.18f;

    [Header("Visual")]
    public float stepMoveDuration = 0.12f;
    public float dashDuration = 0.08f;
    public float dashReturnDuration = 0.06f;

    [Header("Debug")]
    public bool logTransitions = true;

    public FieldCombatState State { get; private set; } = new FieldCombatState();

    private Vector2Int playerCellCached;
    private Vector2Int enemyCellCached;

    // "플레이어가 Time을 썼다"를 기다리는 플래그 (이벤트 기반)
    private bool _waitingForPlayerTimeSpend = false;

    // 적 턴 시작 딜레이 코루틴 핸들
    private Coroutine _enemyTurnDelayCoroutine;

    // ✅ EnemyDefinition에서 읽어올 스탯 캐시
    private int _cachedPlayerAttackRange = 1;
    private int _cachedPlayerAttackDamage = 1;
    private int _cachedEnemyAttackRange = 1;
    private int _cachedEnemyDamage = 1;

    private void Awake()
    {
        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();

        if (playerStats == null && player != null)
            playerStats = player.GetComponent<PlayerStats>();

        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();
    }

    public void StartCombatPublic(Transform enemy, bool enemyTurnFirst = true)
    {
        StartCombat(enemy, enemyTurnFirst);
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
        if (_enemyTurnDelayCoroutine != null)
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

        // Not in combat: 대기 (FieldEnemyWanderChase가 전투 시작을 담당)
        if (State.phase == FieldCombatState.Phase.None || State.phase == FieldCombatState.Phase.Ended)
        {
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

        // Escape end (FieldEnemyWanderChase의 aggroRange보다 멀어지면 자동 종료)
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
    // Start / End (외부에서 호출)
    // =========================================================

    private void StartCombat(Transform targetEnemy, bool enemyFirst)
    {
        enemy = targetEnemy;
        enemyInstanceCache = enemy != null ? enemy.GetComponent<EnemyInstance>() : null;
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return;

        // ✅ EnemyDefinition에서 필드 전투 스탯 로드
        LoadEnemyStatsFromDefinition();

        var enemyHome = gridBoard.WorldToCell(enemy.position);
        State.ResetForNewCombat(player, enemy, enemyHome, _cachedEnemyAttackRange, true);

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

        bool canAttack = dist <= _cachedEnemyAttackRange && los;

        if (canAttack)
        {
            yield return DashHitAndReturn(enemy, gridBoard.CellToWorld(enemyCellCached), gridBoard.CellToWorld(playerCellCached));
            DealDamageToPlayer(_cachedEnemyDamage);
            if (logTransitions) Debug.Log($"[FieldCombat] Enemy attacks for {_cachedEnemyDamage}");
        }
        else
        {
            // 사거리 밖이면 추격 (1칸만)
            if (TryGetChaseStep(enemyCellCached, playerCellCached, out Vector2Int next))
            {
                Vector3 from = enemy.position;
                Vector3 to = gridBoard.CellToWorld(next);

                enemyCellCached = next;
                yield return SmoothMove(enemy, from, to, stepMoveDuration);

                if (logTransitions) Debug.Log($"[FieldCombat] Enemy moves to {next}");
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
    // Player Turn (입력은 외부 스크립트에서 처리 권장)
    // =========================================================
    private void HandlePlayerTurn()
    {
        // 플레이어 턴이면 항상 대기 플래그 ON
        if (!_waitingForPlayerTimeSpend)
            _waitingForPlayerTimeSpend = true;

        // ✅ TODO: 플레이어 입력은 PlayerFieldCombat.cs 같은 별도 스크립트로 분리 권장
        // 현재는 최소 기능만 유지

        // (1) Focused combat (E키 - 임시)
        if (Input.GetKeyDown(KeyCode.E))
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
        if (dist > _cachedPlayerAttackRange) yield break;

        if (!FieldCombatUtils.HasLineOfSight(gridBoard, playerCellCached, enemyCellCached))
            yield break;

        State.busy = true;

        Vector3 start = player.position;
        Vector3 target = gridBoard.CellToWorld(enemyCellCached);
        yield return DashHitAndReturn(player, start, target);

        DealDamageToEnemy(_cachedPlayerAttackDamage);

        if (logTransitions)
            Debug.Log($"[FieldCombat] Player attacks for {_cachedPlayerAttackDamage}");

        State.busy = false;

        AdvanceTimeBy1(); // 이벤트로 적 턴 예약(딜레이 포함)
    }

    // =========================================================
    // Detection (적의 시야에서 벗어나면 전투 종료)
    // =========================================================
    private bool IsEnemyStillDetectingPlayer()
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy) return false;
        if (enemyInstanceCache != null && enemyInstanceCache.currentHP <= 0) return false;

        // ✅ EnemyDefinition의 fieldDetectionRange 사용
        if (enemyInstanceCache != null && enemyInstanceCache.definition != null)
        {
            int detectionRange = Mathf.Max(1, enemyInstanceCache.definition.fieldDetectionRange);
            bool requireLoS = enemyInstanceCache.definition.fieldRequireLoS;

            playerCellCached = gridBoard.WorldToCell(player.position);
            enemyCellCached = gridBoard.WorldToCell(enemy.position);

            if (FieldCombatUtils.Chebyshev(enemyCellCached, playerCellCached) > detectionRange)
                return false;

            if (requireLoS)
                return FieldCombatUtils.HasLineOfSight(gridBoard, enemyCellCached, playerCellCached);

            return true;
        }

        // Fallback: 기본값 6
        playerCellCached = gridBoard.WorldToCell(player.position);
        enemyCellCached = gridBoard.WorldToCell(enemy.position);

        if (FieldCombatUtils.Chebyshev(enemyCellCached, playerCellCached) > 6)
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
        if (gridBoard == null) return false;
        
        // GridBoard가 IsMoveBlocked 메서드를 가지고 있는지 확인
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
    // Stats Loading (EnemyDefinition에서 읽어오기)
    // =========================================================
    private void LoadEnemyStatsFromDefinition()
    {
        // 기본값
        _cachedPlayerAttackRange = 1;
        _cachedPlayerAttackDamage = 1;
        _cachedEnemyAttackRange = 1;
        _cachedEnemyDamage = 1;

        if (enemyInstanceCache == null || enemyInstanceCache.definition == null)
            return;

        var def = enemyInstanceCache.definition;

        // 적 스탯은 EnemyDefinition에서
        _cachedEnemyAttackRange = Mathf.Max(1, def.attackRange);
        _cachedEnemyDamage = Mathf.Max(0, def.attackDamage);

        // 플레이어 스탯은 여기서는 기본값 (PlayerStats나 다른 곳에서 관리 가능)
        _cachedPlayerAttackRange = 1;
        _cachedPlayerAttackDamage = 1;
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
    // Click (간단한 레이캐스트)
    // =========================================================
    private bool TryGetClickedEnemy(out Transform hitTransform)
    {
        hitTransform = null;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 mouse = Input.mousePosition;

        // 2D 레이캐스트 시도
        Vector3 world = cam.ScreenToWorldPoint(mouse);
        Vector2 world2 = new Vector2(world.x, world.y);
        RaycastHit2D hit2D = Physics2D.Raycast(world2, Vector2.zero, 0f);
        if (hit2D.collider != null)
        {
            hitTransform = hit2D.collider.transform;
            return true;
        }

        // 3D 레이캐스트 시도
        Ray ray = cam.ScreenPointToRay(mouse);
        if (Physics.Raycast(ray, out RaycastHit hit3D, 500f))
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
