using System.Collections;
using UnityEngine;
using TMPro;

public class FocusedCombatManager : MonoBehaviour
{
    public static FocusedCombatManager Instance { get; private set; }

    [Header("Roots")]
    public GameObject fieldRoot;
    public GameObject combatUIRoot;

    [Header("Fade")]
    public ScreenFader fader;
    public float fadeOutDuration = 0.25f;
    public float fadeInDuration = 0.25f;

    [Header("Combat Board")]
    public CombatBoardUI boardUI;

    [Header("Unit Token Prefabs (UI)")]
    public CombatUnitToken playerTokenPrefab;
    public CombatUnitToken enemyTokenPrefab;

    [Header("Player Hint (UI)")]
    public TMP_Text playerHintPrefab;
    public Vector2 playerHintOffset = new Vector2(0f, 28f);
    private TMP_Text _playerHintInstance;

    [Header("Map Sizes")]
    public Vector2Int[] mapSizes = new Vector2Int[]
    {
        new Vector2Int(9,9),
        new Vector2Int(11,7),
        new Vector2Int(7,11)
    };

    [Header("Generation")]
    public CombatSpawnPlanner spawnPlanner;
    public CombatTerrainGenerator terrainGenerator;

    [Tooltip("생성 실패 시, 지형 없이 빈 맵으로 진행할지(안전장치).")]
    public bool fallbackToEmptyOnFail = true;

    public CombatGridData gridData { get; private set; }

    [Header("Input Lock Targets (disable during focused combat)")]
    public MonoBehaviour[] disableDuringCombat;

    [Header("Turn / AP")]
    public int startAP = 2;
    public int maxAP = 5;

    [Header("Movement")]
    [Tooltip("한 번의 이동 액션으로 도달 가능한 최대 거리(8방향 기준). 기본 2.")]
    public int moveRange = 2;

    [Tooltip("1칸 이동이 보이도록 하는 스텝 딜레이(초).")]
    public float stepDelay = 0.15f;

    [Header("Grid Blocker (legacy)")]
    [Tooltip("레거시: 막힌 칸을 알려주는 컴포넌트(선택). gridData가 있을 땐 gridData(Wall)가 우선.")]
    public MonoBehaviour blockerBehaviour;
    private ICombatGridBlocker _blocker;

    [Header("UI (Left Panel)")]
    public TMP_Text apText;
    public TMP_Text hpText;

    [Header("Action Controller")]
    public PlayerCombatActionController actionController;

    [Header("HP (temp)")]
    public int playerMaxHP = 10;
    public int enemyMaxHP = 10;

    public bool IsInFocusedCombat { get; private set; }

    // ===== Runtime =====
    private Vector2Int _playerCell;
    private Vector2Int _enemyCell;

    private CombatUnitToken _playerTokenInstance;
    private CombatUnitToken _enemyTokenInstance;

    private bool _isPlayerTurn = false;
    private bool _isMoving = false;

    private int _currentAP = 0;
    private bool _freeMoveUsedThisTurn = false;

    private int _playerHP;
    private int _enemyHP;

    // ===== Public read-only state =====
    public Vector2Int PlayerCell => _playerCell;
    public Vector2Int EnemyCell => _enemyCell;

    public bool IsBusyForInput => _isMoving;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (combatUIRoot != null)
            combatUIRoot.SetActive(false);

        if (fader != null)
            fader.ForceClear();

        ResolveBlocker();

        if (actionController == null)
            actionController = GetComponent<PlayerCombatActionController>();

        if (actionController != null && actionController.combat == null)
            actionController.combat = this;
    }

    private void ResolveBlocker()
    {
        _blocker = null;
        if (blockerBehaviour == null) return;

        _blocker = blockerBehaviour as ICombatGridBlocker;
        if (_blocker == null)
            _blocker = blockerBehaviour.GetComponent<ICombatGridBlocker>();
    }

    public void EnterFocusedCombat()
    {
        if (IsInFocusedCombat) return;
        StartCoroutine(EnterRoutine());
    }

    public void ExitFocusedCombat()
    {
        if (!IsInFocusedCombat) return;
        StartCoroutine(ExitRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        IsInFocusedCombat = true;

        if (fader == null)
        {
            DoEnter();
            yield break;
        }

        yield return StartCoroutine(fader.FadeOutIn(DoEnter, fadeOutDuration, fadeInDuration));
    }

    private IEnumerator ExitRoutine()
    {
        if (fader == null)
        {
            DoExit();
            yield break;
        }

        yield return StartCoroutine(fader.FadeOutIn(DoExit, fadeOutDuration, fadeInDuration));
    }

    private void DoEnter()
    {
        SetDisableDuringCombat(true);

        if (fieldRoot != null) fieldRoot.SetActive(false);
        if (combatUIRoot != null) combatUIRoot.SetActive(true);

        ResolveBlocker();

        // 맵 선택 + 생성
        Vector2Int size = mapSizes[Random.Range(0, mapSizes.Length)];

        if (boardUI != null)
        {
            boardUI.Build(size.x, size.y);

            boardUI.OnTileClicked -= HandleTileClicked;
            boardUI.OnTileClicked += HandleTileClicked;
        }

        // ✅ 스폰 플래너 (맵 끝 + 최대한 반대 + 대칭)
        if (spawnPlanner != null)
            spawnPlanner.PickEdgeSpawns(size.x, size.y, out _playerCell, out _enemyCell);
        else
        {
            // 플래너 없으면 안전 기본값(아래/위 중앙)
            int cx = (size.x - 1) / 2;
            _playerCell = new Vector2Int(cx, 0);
            _enemyCell = new Vector2Int(cx, size.y - 1);
        }

        // ✅ 지형 생성 (스폰은 항상 Empty, 스폰 주변 8방 1칸은 벽 금지)
        gridData = null;
        if (terrainGenerator != null)
        {
            bool ok = terrainGenerator.Generate(size.x, size.y, _playerCell, _enemyCell, out CombatGridData gen);
            if (ok) gridData = gen;
            else
            {
                Debug.LogWarning("[FocusedCombatManager] Terrain generation failed.");
                if (fallbackToEmptyOnFail)
                    gridData = new CombatGridData(size.x, size.y);
            }
        }
        else
        {
            // 생성기 없으면 빈 맵
            gridData = new CombatGridData(size.x, size.y);
        }

        // 지형 시각 적용
        if (boardUI != null && gridData != null)
            boardUI.ApplyTerrainVisual(gridData);

        // 토큰 배치(스폰은 Empty 보장)
        if (boardUI != null)
        {
            if (playerTokenPrefab != null)
                _playerTokenInstance = boardUI.PlaceToken(playerTokenPrefab, _playerCell.x, _playerCell.y);

            if (enemyTokenPrefab != null)
                _enemyTokenInstance = boardUI.PlaceToken(enemyTokenPrefab, _enemyCell.x, _enemyCell.y);
        }

        // HP/AP 초기화
        _playerHP = playerMaxHP;
        _enemyHP = enemyMaxHP;

        _currentAP = Mathf.Clamp(startAP, 0, maxAP);

        BeginPlayerTurn(initialStart: true);
        HidePlayerHint();
    }

    private void DoExit()
    {
        if (boardUI != null)
        {
            boardUI.OnTileClicked -= HandleTileClicked;
            boardUI.ClearHighlights();
        }

        if (actionController != null)
            actionController.CancelAction();

        if (combatUIRoot != null) combatUIRoot.SetActive(false);
        if (fieldRoot != null) fieldRoot.SetActive(true);

        SetDisableDuringCombat(false);

        IsInFocusedCombat = false;

        gridData = null;

        if (fader != null)
            fader.ForceClear();
    }

    private void BeginPlayerTurn(bool initialStart)
    {
        _isPlayerTurn = true;
        _isMoving = false;

        if (!initialStart)
            _currentAP = Mathf.Min(_currentAP + 1, maxAP);

        _freeMoveUsedThisTurn = false;

        RefreshUI();
        RefreshMoveHighlights();
    }

    // ======================================================================
    // Tile Click Routing (Attack Mode -> ActionController, else Move)
    // ======================================================================
    private void HandleTileClicked(int x, int y)
    {
        if (actionController != null && actionController.IsInActionMode)
        {
            actionController.OnTileClicked(x, y);
            return;
        }

        HandleMoveTileClicked(x, y);
    }

    // ======================================================================
    // Movement (max 2 tiles, step-by-step, blocks respected, cost after completion)
    // ======================================================================
    private void HandleMoveTileClicked(int x, int y)
    {
        if (!IsInFocusedCombat) return;
        if (!_isPlayerTurn) return;
        if (_playerTokenInstance == null) return;
        if (_isMoving) return;

        Vector2Int target = new Vector2Int(x, y);

        if (target == _playerCell) return;
        if (target == _enemyCell) return;

        // 이동 완료 후 1회 차감
        int moveCost = (_freeMoveUsedThisTurn) ? 1 : 0;
        if (moveCost > 0 && _currentAP < moveCost) return;

        if (!TryBuildPath(_playerCell, target, out Vector2Int[] pathSteps))
            return;

        StartCoroutine(MoveRoutine(pathSteps, moveCost));
    }

    private IEnumerator MoveRoutine(Vector2Int[] steps, int moveCost)
    {
        _isMoving = true;

        if (boardUI != null) boardUI.ClearHighlights();

        for (int i = 0; i < steps.Length; i++)
        {
            Vector2Int step = steps[i];

            if (IsBlocked(step) || step == _enemyCell)
            {
                _isMoving = false;
                RefreshUI();
                RefreshMoveHighlights();
                yield break;
            }

            boardUI.MoveExistingToken(_playerTokenInstance, step.x, step.y);
            _playerCell = step;

            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        if (moveCost > 0) _currentAP -= moveCost;
        if (!_freeMoveUsedThisTurn) _freeMoveUsedThisTurn = true;

        _isMoving = false;

        RefreshUI();
        RefreshMoveHighlights();
        HidePlayerHint();
    }

    /// <summary>
    /// from -> target 으로 갈 수 있는 "최대 moveRange 스텝" 경로.
    /// (현재는 moveRange=2 전제의 기존 로직 유지)
    /// 다음 단계에서 pathfinding(BFS)로 교체 예정.
    /// </summary>
    private bool TryBuildPath(Vector2Int from, Vector2Int target, out Vector2Int[] steps)
    {
        steps = null;

        if (boardUI == null) return false;
        if (!boardUI.InBounds(target.x, target.y)) return false;

        int dxAbs = Mathf.Abs(target.x - from.x);
        int dyAbs = Mathf.Abs(target.y - from.y);
        int cheb = Mathf.Max(dxAbs, dyAbs);

        if (cheb <= 0 || cheb > Mathf.Max(1, moveRange)) return false;

        if (IsBlocked(target)) return false;

        if (cheb == 1)
        {
            if (!IsAdjacent8(from, target)) return false;
            steps = new Vector2Int[] { target };
            return true;
        }

        // moveRange=2 기준 2스텝
        int sx = (target.x > from.x) ? 1 : (target.x < from.x ? -1 : 0);
        int sy = (target.y > from.y) ? 1 : (target.y < from.y ? -1 : 0);

        Vector2Int[] mids;

        if (dxAbs == 2 && dyAbs == 0)
            mids = new[] { new Vector2Int(from.x + sx, from.y) };
        else if (dxAbs == 0 && dyAbs == 2)
            mids = new[] { new Vector2Int(from.x, from.y + sy) };
        else if (dxAbs == 2 && dyAbs == 2)
            mids = new[] { new Vector2Int(from.x + sx, from.y + sy) };
        else
            mids = new[]
            {
                new Vector2Int(from.x + sx, from.y),
                new Vector2Int(from.x, from.y + sy),
                new Vector2Int(from.x + sx, from.y + sy),
            };

        for (int i = 0; i < mids.Length; i++)
        {
            Vector2Int mid = mids[i];

            if (!boardUI.InBounds(mid.x, mid.y)) continue;
            if (IsBlocked(mid)) continue;
            if (mid == _enemyCell) continue;

            if (!IsAdjacent8(mid, target)) continue;

            steps = new Vector2Int[] { mid, target };
            return true;
        }

        // 보조 탐색
        for (int mx = -1; mx <= 1; mx++)
        {
            for (int my = -1; my <= 1; my++)
            {
                if (mx == 0 && my == 0) continue;

                Vector2Int mid = new Vector2Int(from.x + mx, from.y + my);

                if (!boardUI.InBounds(mid.x, mid.y)) continue;
                if (IsBlocked(mid)) continue;
                if (mid == _enemyCell) continue;

                if (!IsAdjacent8(mid, target)) continue;

                steps = new Vector2Int[] { mid, target };
                return true;
            }
        }

        return false;
    }

    private bool IsAdjacent8(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0);
    }

    // ======================================================================
    // Hint
    // ======================================================================
    public void ShowPlayerHint(string text)
    {
        if (_playerTokenInstance == null) return;
        if (playerHintPrefab == null) return;

        if (_playerHintInstance == null)
        {
            _playerHintInstance = Instantiate(playerHintPrefab, _playerTokenInstance.transform);
            _playerHintInstance.name = "PlayerHintTMP";

            var rt = _playerHintInstance.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = playerHintOffset;
            rt.localScale = Vector3.one;
        }

        _playerHintInstance.text = text;
        _playerHintInstance.gameObject.SetActive(true);
    }

    public void HidePlayerHint()
    {
        if (_playerHintInstance != null)
            _playerHintInstance.gameObject.SetActive(false);
    }

    // ======================================================================
    // Highlighting
    // ======================================================================
    private void RefreshMoveHighlights()
    {
        if (boardUI == null) return;

        if (actionController != null && actionController.IsInActionMode)
            return;

        boardUI.ClearHighlights();

        if (!IsInFocusedCombat) return;
        if (!_isPlayerTurn) return;
        if (_playerTokenInstance == null) return;
        if (_isMoving) return;

        int moveCost = (_freeMoveUsedThisTurn) ? 1 : 0;
        if (moveCost > 0 && _currentAP < moveCost) return;

        int r = Mathf.Max(1, moveRange);

        for (int x = _playerCell.x - r; x <= _playerCell.x + r; x++)
        {
            for (int y = _playerCell.y - r; y <= _playerCell.y + r; y++)
            {
                if (!boardUI.InBounds(x, y)) continue;

                Vector2Int target = new Vector2Int(x, y);
                if (target == _playerCell) continue;
                if (target == _enemyCell) continue;

                int dx = Mathf.Abs(x - _playerCell.x);
                int dy = Mathf.Abs(y - _playerCell.y);
                if (Mathf.Max(dx, dy) > r) continue;

                if (TryBuildPath(_playerCell, target, out _))
                    boardUI.SetHighlight(x, y, true);
            }
        }
    }

    // ======================================================================
    // Public API for Action System
    // ======================================================================
    public bool IsBlocked(Vector2Int cell)
    {
        // ✅ 최신: gridData(Wall)가 우선
        if (gridData != null)
        {
            if (!gridData.InBounds(cell.x, cell.y)) return true;
            if (gridData.Get(cell.x, cell.y) == CombatTileType.Wall) return true;
        }

        // 레거시 blocker(선택)
        if (_blocker == null) return false;
        return _blocker.IsBlocked(cell.x, cell.y);
    }

    public bool CanSpendAP(int amount) => amount <= _currentAP;

    public void SpendAP(int amount)
    {
        if (amount <= 0) return;
        _currentAP = Mathf.Max(0, _currentAP - amount);
        RefreshUI();
    }

    public void DamageEnemy(int dmg)
    {
        if (dmg <= 0) return;

        _enemyHP = Mathf.Max(0, _enemyHP - dmg);

        if (_enemyTokenInstance != null)
            StartCoroutine(Anim_HitPulse(_enemyTokenInstance.GetComponent<RectTransform>()));

        RefreshUI();

        if (_enemyHP <= 0)
        {
            ExitFocusedCombat();
        }
    }

    public void RefreshUIExternal() => RefreshUI();
    public void RefreshMoveHighlightsExternal() => RefreshMoveHighlights();

    // ======================================================================
    // UI
    // ======================================================================
    private void RefreshUI()
    {
        if (apText != null)
        {
            string free = _freeMoveUsedThisTurn ? "Used" : "Ready";
            apText.text = $"AP: {_currentAP}/{maxAP}  | Free Move: {free}";
        }

        if (hpText != null)
        {
            hpText.text = $"HP: {_playerHP}/{playerMaxHP}";
        }
    }

    // ======================================================================
    // Animation: Player dash-hit-return (for requireEnemyOnTarget)
    // ======================================================================
    public IEnumerator Anim_PlayerDashHitReturn(Vector2Int targetCell, System.Action onImpact)
    {
        if (boardUI == null) yield break;
        if (_playerTokenInstance == null) yield break;

        _isMoving = true;

        RectTransform tokenRT = _playerTokenInstance.GetComponent<RectTransform>();
        Transform originalParent = tokenRT.parent;

        Vector3 start = tokenRT.position;
        Vector3 end = boardUI.GetTileWorldCenter(targetCell.x, targetCell.y);

        if (boardUI.boardRoot != null)
            tokenRT.SetParent(boardUI.boardRoot, worldPositionStays: true);

        float goTime = 0.06f;
        float pauseTime = 0.03f;
        float backTime = 0.07f;

        yield return LerpWorldPos(tokenRT, start, end, goTime);

        onImpact?.Invoke();
        yield return new WaitForSeconds(pauseTime);

        yield return LerpWorldPos(tokenRT, end, start, backTime);

        tokenRT.SetParent(originalParent, worldPositionStays: false);
        tokenRT.anchorMin = Vector2.zero;
        tokenRT.anchorMax = Vector2.one;
        tokenRT.offsetMin = Vector2.zero;
        tokenRT.offsetMax = Vector2.zero;
        tokenRT.localScale = Vector3.one;

        _isMoving = false;

        RefreshUI();
        RefreshMoveHighlights();
    }

    private IEnumerator LerpWorldPos(RectTransform rt, Vector3 a, Vector3 b, float dur)
    {
        if (rt == null) yield break;

        if (dur <= 0f)
        {
            rt.position = b;
            yield break;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            rt.position = Vector3.LerpUnclamped(a, b, u);
            yield return null;
        }
        rt.position = b;
    }

    private IEnumerator Anim_HitPulse(RectTransform rt)
    {
        if (rt == null) yield break;

        Vector3 baseScale = rt.localScale;
        rt.localScale = baseScale * 1.15f;
        yield return new WaitForSeconds(0.06f);
        rt.localScale = baseScale;
    }

    // ======================================================================
    // Input Lock Targets
    // ======================================================================
    private void SetDisableDuringCombat(bool disable)
    {
        if (disableDuringCombat == null) return;

        for (int i = 0; i < disableDuringCombat.Length; i++)
        {
            if (disableDuringCombat[i] == null) continue;
            disableDuringCombat[i].enabled = !disable;
        }
    }
}
