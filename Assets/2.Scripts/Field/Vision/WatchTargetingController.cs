using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 신 주시 시스템: Q→타겟팅→F→3턴 주시→F→집중전투 진입.
/// 기존 EnemyTargetSelector / EnemyFocusedCombatInteract를 대체.
/// </summary>
public class WatchTargetingController : MonoBehaviour
{
    /// <summary>다른 시스템이 이 플래그를 확인해서 옛 시스템 비활성화</summary>
    public static bool UseNewWatchSystem = false;

    public enum WatchState { Normal, Targeting, Watching, WatchComplete }

    [Header("Refs")]
    public PlayerGridMover playerMover;
    public PlayerStats playerStats;
    public GridBoard grid;
    public FieldTimeManager fieldTime;
    public FieldMultiEnemyAttack multiEnemyAttack;

    [Header("Keys")]
    public KeyCode targetingKey = KeyCode.Q;
    public KeyCode confirmKey = KeyCode.F;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Watch Settings")]
    [Tooltip("주시 완료까지 필요한 턴 수")]
    public int watchTurnsRequired = 3;
    [Tooltip("주시 중 턴 간 대기 시간 (적 행동 완료 대기)")]
    public float turnDelay = 0.8f;

    [Header("Highlight Visual")]
    public Color highlightColor = new Color(1f, 1f, 0f, 0.5f);
    public Color watchingColor = new Color(1f, 0.3f, 0f, 0.6f);
    public Color completeColor = new Color(0f, 1f, 0f, 0.6f);

    [Header("Debug")]
    public bool showDebugLogs = true;

    // ===== Runtime =====
    public WatchState CurrentState { get; private set; } = WatchState.Normal;

    private List<EnemyInstance> _visibleEnemies = new List<EnemyInstance>();
    private EnemyInstance _highlightedEnemy;
    private EnemyInstance _watchTarget;
    private int _watchTurnCount;
    private bool _watchBroken;
    private Coroutine _watchRoutine;

    // Visual
    private GameObject _highlightObj;
    private SpriteRenderer _highlightSR;
    private GameObject _watchCounterObj;
    private TMP_Text _watchCounterText;

    // ============================================================
    // Unity Lifecycle
    // ============================================================

    private void Awake()
    {
        if (playerMover == null) playerMover = GetComponent<PlayerGridMover>() ?? FindObjectOfType<PlayerGridMover>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (multiEnemyAttack == null) multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();
    }
    private void FollowWatchTarget()
    {
        if (_watchTarget == null) return;

        // 하이라이트 따라가기
        if (_highlightObj != null && _highlightObj.activeSelf)
            _highlightObj.transform.position = _watchTarget.transform.position;

        // 카운터 텍스트 따라가기
        if (_watchCounterObj != null && _watchCounterObj.activeSelf)
        {
            Vector3 pos = _watchTarget.transform.position;
            pos.y += 1.5f;
            _watchCounterObj.transform.position = pos;
        }
    }
    private void Start()
    {
        UseNewWatchSystem = true;
        CreateHighlightVisual();
        CreateWatchCounterUI();

        if (showDebugLogs)
            Debug.Log("[WatchTargeting] 신 주시 시스템 활성화. 옛 시스템 비활성화됨.");
    }

    private void OnDestroy()
    {
        UseNewWatchSystem = false;
        if (_highlightObj != null) Destroy(_highlightObj);
        if (_watchCounterObj != null) Destroy(_watchCounterObj);
    }

    private void Update()
    {
        // 집중전투 중이면 무시
        var fcm = FocusedCombatManager.Instance;
        if (fcm != null && fcm.IsInFocusedCombat) return;

        switch (CurrentState)
        {
            case WatchState.Normal:
                UpdateNormal();
                break;
            case WatchState.Targeting:
                UpdateTargeting();
                break;
            case WatchState.Watching:
                FollowWatchTarget();
                break;
            case WatchState.WatchComplete:
                UpdateWatchComplete();
                FollowWatchTarget();
                break;
        }
    }

    // ============================================================
    // Normal State
    // ============================================================

    private void UpdateNormal()
    {
        if (Input.GetKeyDown(targetingKey))
        {
            EnterTargeting();
        }
    }

    // ============================================================
    // Targeting State
    // ============================================================

    private void EnterTargeting()
    {
        RefreshVisibleEnemies();

        if (_visibleEnemies.Count == 0)
        {
            if (showDebugLogs) Debug.Log("[WatchTargeting] 시야 내 적 없음");
            return;
        }

        CurrentState = WatchState.Targeting;
        playerMover.lockMovement = true;

        // 가장 가까운 적으로 초기 하이라이트
        _highlightedEnemy = FindClosestEnemy();
        UpdateHighlightVisual();

        if (showDebugLogs)
            Debug.Log($"[WatchTargeting] 타겟팅 모드 진입. 시야 내 적 {_visibleEnemies.Count}마리");
    }

    private void UpdateTargeting()
    {
        // 취소
        if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(targetingKey))
        {
            CancelToNormal();
            return;
        }

        // 시야 내 적 갱신
        RefreshVisibleEnemies();
        if (_visibleEnemies.Count == 0)
        {
            CancelToNormal();
            return;
        }

        // 하이라이트된 적이 죽었거나 시야 밖이면 가장 가까운 적으로 전환
        if (_highlightedEnemy == null || !_visibleEnemies.Contains(_highlightedEnemy))
        {
            _highlightedEnemy = FindClosestEnemy();
        }

        // WASD 방향 기반 적 전환
        Vector2Int dir = ReadTargetingInput();
        if (dir != Vector2Int.zero)
        {
            var next = FindEnemyInDirection(dir);
            if (next != null)
                _highlightedEnemy = next;
        }

        // 마우스 호버
        var mouseTarget = GetEnemyUnderMouse();
        if (mouseTarget != null)
            _highlightedEnemy = mouseTarget;

        UpdateHighlightVisual();

        // F키 또는 마우스 클릭으로 주시 시작
        if (_highlightedEnemy != null)
        {
            if (Input.GetKeyDown(confirmKey) || Input.GetMouseButtonDown(0) && mouseTarget != null)
            {
                StartWatching(_highlightedEnemy);
            }
        }
    }

    private Vector2Int ReadTargetingInput()
    {
        int dx = 0, dy = 0;
        if (Input.GetKeyDown(KeyCode.W)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.S)) dy = -1;
        if (Input.GetKeyDown(KeyCode.A)) dx = -1;
        else if (Input.GetKeyDown(KeyCode.D)) dx = 1;
        return new Vector2Int(dx, dy);
    }

    private EnemyInstance FindEnemyInDirection(Vector2Int dir)
    {
        if (_highlightedEnemy == null) return null;

        Vector2Int currentCell = grid.WorldToCell(_highlightedEnemy.transform.position);
        EnemyInstance best = null;
        float bestDist = float.MaxValue;

        foreach (var enemy in _visibleEnemies)
        {
            if (enemy == _highlightedEnemy) continue;

            Vector2Int ec = grid.WorldToCell(enemy.transform.position);
            Vector2Int delta = ec - currentCell;

            // 방향 필터: 요청 방향에 적이 있는지
            bool match = false;
            if (dir.x > 0 && delta.x > 0) match = true;
            if (dir.x < 0 && delta.x < 0) match = true;
            if (dir.y > 0 && delta.y > 0) match = true;
            if (dir.y < 0 && delta.y < 0) match = true;

            if (!match) continue;

            float dist = delta.sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = enemy;
            }
        }

        return best;
    }

    private EnemyInstance GetEnemyUnderMouse()
    {
        if (Camera.main == null) return null;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2Int mouseCell = grid.WorldToCell(mouseWorld);

        foreach (var enemy in _visibleEnemies)
        {
            if (enemy == null) continue;
            Vector2Int ec = grid.WorldToCell(enemy.transform.position);
            if (ec == mouseCell) return enemy;
        }
        return null;
    }

    // ============================================================
    // Watching State (3턴 자동 경과)
    // ============================================================

    private void StartWatching(EnemyInstance target)
    {
        _watchTarget = target;
        _watchTurnCount = 0;
        _watchBroken = false;
        CurrentState = WatchState.Watching;

        HideHighlight();
        UpdateWatchCounterVisual();

        if (showDebugLogs)
            Debug.Log($"[WatchTargeting] 주시 시작: {target.name}");

        _watchRoutine = StartCoroutine(WatchRoutine());
    }

    private IEnumerator WatchRoutine()
    {
        // 데미지 이벤트 구독
        playerStats.OnDamageTaken += OnPlayerDamagedDuringWatch;

        for (int turn = 0; turn < watchTurnsRequired; turn++)
        {
            _watchTurnCount = turn + 1;
            UpdateWatchCounterVisual();

            if (showDebugLogs)
                Debug.Log($"[WatchTargeting] 주시 턴 {_watchTurnCount}/{watchTurnsRequired}");

            // 필드 시간 경과
            if (fieldTime != null)
                fieldTime.Advance(1);

            // 적들 행동
            if (multiEnemyAttack != null)
                multiEnemyAttack.OnPlayerTurnEnd();

            // 적 행동 완료 대기
            yield return new WaitForSeconds(turnDelay);

            // 데미지로 주시 깨짐 체크
            if (_watchBroken)
            {
                if (showDebugLogs) Debug.Log("[WatchTargeting] 주시 깨짐: 데미지 받음");
                playerStats.OnDamageTaken -= OnPlayerDamagedDuringWatch;
                CancelToNormal();
                yield break;
            }

            // 대상 적 사망 체크
            if (_watchTarget == null || !_watchTarget.gameObject.activeSelf || _watchTarget.currentHP <= 0)
            {
                if (showDebugLogs) Debug.Log("[WatchTargeting] 주시 깨짐: 대상 적 사망");
                playerStats.OnDamageTaken -= OnPlayerDamagedDuringWatch;
                CancelToNormal();
                yield break;
            }

            // 대상 적 거리 이탈 체크 (주시 시작 후엔 LoS 무시, 거리만 확인)
            Vector2Int targetCell = grid.WorldToCell(_watchTarget.transform.position);
            int dist = Mathf.Max(
                Mathf.Abs(playerMover.CurrentCell.x - targetCell.x),
                Mathf.Abs(playerMover.CurrentCell.y - targetCell.y)
            );
            int watchRange = FieldVisionSystem.Instance != null
                ? FieldVisionSystem.Instance.GetEffectivePlayerVisionRange() + 2  // 웅크 시 시야 반영
                : 6;
            if (dist > watchRange)
            {
                if (showDebugLogs) Debug.Log("[WatchTargeting] 주시 깨짐: 대상 거리 이탈");
                playerStats.OnDamageTaken -= OnPlayerDamagedDuringWatch;
                CancelToNormal();
                yield break;
            }
        }

        // 3턴 완료
        playerStats.OnDamageTaken -= OnPlayerDamagedDuringWatch;
        CurrentState = WatchState.WatchComplete;
        UpdateWatchCounterVisual();

        if (showDebugLogs)
            Debug.Log("[WatchTargeting] 주시 완료! F로 집중전투 진입 가능");
    }

    private void OnPlayerDamagedDuringWatch(int damage)
    {
        _watchBroken = true;
    }

    // ============================================================
    // WatchComplete State
    // ============================================================

    private void UpdateWatchComplete()
    {
        // 취소
        if (Input.GetKeyDown(cancelKey) || Input.GetKeyDown(targetingKey))
        {
            CancelToNormal();
            return;
        }

        // 대상 적 사망 체크
        if (_watchTarget == null || !_watchTarget.gameObject.activeSelf || _watchTarget.currentHP <= 0)
        {
            CancelToNormal();
            return;
        }

        // F키로 집중전투 진입
        if (Input.GetKeyDown(confirmKey))
        {
            EnterFocusCombat();
        }
    }

    [Header("Environment Scan")]
    [Tooltip("주시 대상 적 중심 환경 스캔 반경")]
    public int scanRadius = 5;

    private void EnterFocusCombat()
    {
        if (_watchTarget == null) return;

        var fcm = FocusedCombatManager.Instance;
        if (fcm == null)
        {
            Debug.LogError("[WatchTargeting] FocusedCombatManager 없음!");
            CancelToNormal();
            return;
        }

        Vector2Int playerCell = playerMover.CurrentCell;

        // 환경 스캔 → 다중 적 + 환경 스냅샷 포함 컨텍스트
        var ctx = EnvironmentScanner.Scan(
            playerStats,
            playerCell,
            _watchTarget,
            grid,
            scanRadius
        );

        if (showDebugLogs)
            Debug.Log($"[WatchTargeting] 집중전투 진입: {_watchTarget.name}, " +
                      $"적 {ctx.enemies.Count}마리, 환경 [{ctx.environment}]");

        CancelToNormal();
        fcm.EnterFocusedCombat(ctx);
    }

    // ============================================================
    // State Transitions
    // ============================================================

    private void CancelToNormal()
    {
        if (_watchRoutine != null)
        {
            StopCoroutine(_watchRoutine);
            _watchRoutine = null;
        }

        CurrentState = WatchState.Normal;
        _highlightedEnemy = null;
        _watchTarget = null;
        _watchTurnCount = 0;
        _watchBroken = false;

        playerMover.lockMovement = false;

        HideHighlight();
        HideWatchCounter();

        if (showDebugLogs)
            Debug.Log("[WatchTargeting] Normal 상태로 복귀");
    }

    // ============================================================
    // Visible Enemies
    // ============================================================

    private void RefreshVisibleEnemies()
    {
        _visibleEnemies.Clear();

        var visionSys = FieldVisionSystem.Instance;
        if (visionSys == null || playerMover == null) return;

        HashSet<Vector2Int> visibleCells = visionSys.GetPlayerVisibleCells(playerMover.CurrentCell);
        var allEnemies = FindObjectsOfType<EnemyInstance>();

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf || enemy.currentHP <= 0) continue;

            Vector2Int ec = grid.WorldToCell(enemy.transform.position);
            if (visibleCells.Contains(ec))
                _visibleEnemies.Add(enemy);
        }
    }

    private EnemyInstance FindClosestEnemy()
    {
        if (_visibleEnemies.Count == 0) return null;

        Vector2Int playerCell = playerMover.CurrentCell;
        EnemyInstance closest = null;
        float closestDist = float.MaxValue;

        foreach (var enemy in _visibleEnemies)
        {
            Vector2Int ec = grid.WorldToCell(enemy.transform.position);
            float dist = (ec - playerCell).sqrMagnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closest = enemy;
            }
        }

        return closest;
    }

    // ============================================================
    // Visuals — Highlight
    // ============================================================

    private void CreateHighlightVisual()
    {
        _highlightObj = new GameObject("WatchHighlight");
        _highlightObj.transform.SetParent(transform, false);

        _highlightSR = _highlightObj.AddComponent<SpriteRenderer>();
        _highlightSR.sprite = CreateCircleSprite();
        _highlightSR.sortingOrder = 90;
        _highlightSR.color = highlightColor;

        float size = grid != null ? grid.cellSize * 1.2f : 1.2f;
        _highlightObj.transform.localScale = new Vector3(size, size, 1f);

        _highlightObj.SetActive(false);
    }

    private void UpdateHighlightVisual()
    {
        if (_highlightedEnemy == null)
        {
            HideHighlight();
            return;
        }

        _highlightObj.SetActive(true);
        _highlightObj.transform.position = _highlightedEnemy.transform.position;

        _highlightSR.color = CurrentState switch
        {
            WatchState.Targeting => highlightColor,
            WatchState.WatchComplete => completeColor,
            _ => highlightColor
        };
    }

    private void HideHighlight()
    {
        if (_highlightObj != null)
            _highlightObj.SetActive(false);
    }

    // ============================================================
    // Visuals — Watch Counter
    // ============================================================

    private void CreateWatchCounterUI()
    {
        _watchCounterObj = new GameObject("WatchCounter");
        _watchCounterObj.transform.SetParent(transform, false);

        Canvas canvas = _watchCounterObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 110;

        RectTransform canvasRT = _watchCounterObj.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(4, 1);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(_watchCounterObj.transform, false);

        _watchCounterText = textObj.AddComponent<TextMeshProUGUI>();
        _watchCounterText.fontSize = 3;
        _watchCounterText.alignment = TextAlignmentOptions.Center;
        _watchCounterText.color = Color.white;
        _watchCounterText.outlineWidth = 0.2f;
        _watchCounterText.outlineColor = Color.black;

        RectTransform textRT = textObj.GetComponent<RectTransform>();
        textRT.sizeDelta = new Vector2(4, 1);
        textRT.localPosition = Vector3.zero;
        textRT.localScale = Vector3.one * 0.1f;

        _watchCounterObj.SetActive(false);
    }

    private void UpdateWatchCounterVisual()
    {
        if (_watchTarget == null)
        {
            HideWatchCounter();
            return;
        }

        _watchCounterObj.SetActive(true);

        Vector3 pos = _watchTarget.transform.position;
        pos.y += 1.5f;
        _watchCounterObj.transform.position = pos;

        if (CurrentState == WatchState.Watching)
        {
            _watchCounterText.text = $"({_watchTurnCount}/{watchTurnsRequired})";
            _watchCounterText.color = watchingColor;
        }
        else if (CurrentState == WatchState.WatchComplete)
        {
            _watchCounterText.text = $"({watchTurnsRequired}/{watchTurnsRequired}) [F]";
            _watchCounterText.color = completeColor;
        }

        // 하이라이트도 주시 대상 따라가도록
        if (_highlightObj != null)
        {
            _highlightObj.SetActive(true);
            _highlightObj.transform.position = _watchTarget.transform.position;
            _highlightSR.color = CurrentState == WatchState.WatchComplete ? completeColor : watchingColor;
        }
    }

    private void HideWatchCounter()
    {
        if (_watchCounterObj != null)
            _watchCounterObj.SetActive(false);
    }

    // ============================================================
    // Utility
    // ============================================================

    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        float radius = size / 2f - 1;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                // 링 형태: 바깥 테두리만 표시
                float ringWidth = 3f;
                float alpha = (dist > radius - ringWidth && dist < radius) ? 1f : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}