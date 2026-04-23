using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGridMover : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public FieldTimeManager fieldTime;
    public PlayerCrouch crouch;
    public PlayerStats playerStats;
    public RestController rest;
    public GridOccupancyRegistry occupancy;
    public FieldMultiEnemyAttack multiEnemyAttack;
    public FieldTurnCoordinator turnCoordinator;

    [Header("Move")]
    public float moveDuration = 0.12f;
    [Tooltip("웅크린 이동 시 이동시간 배율 (1보다 크면 더 느려짐)")]
    [Min(1f)] public float crouchMoveDurationMultiplier = 1.45f;
    [Tooltip("일어선 상태에서 한 칸 이동 시 소모 Time")]
    public int baseMoveTimeCost = 1;
    [Tooltip("웅크린 상태에서 한 칸 이동 시 소모 Time (기본 2)")]
    [Min(1)] public int moveTimeCostWhenCrouching = 2;

    [Header("Hunger Cost")]
    [Tooltip("기본 이동 1칸당 배고픔 소모량")]
    [Min(0f)] public float baseMoveHungerCost = 1f;
    [Tooltip("웅크린 이동 시 배고픔 배율")]
    [Min(0f)] public float crouchMoveHungerMultiplier = 1.5f;

    [Header("Occupancy")]
    [Tooltip("점유 시스템이 있을 때, 점유된 셀로 이동을 막습니다.")]
    public bool blockMoveIntoOccupiedCell = true;

    [Header("Move Visibility Assist")]
    [Tooltip("앞에 있는 오브젝트 칸으로 진입할 때 이동 중 플레이어를 보이게 보정합니다.")]
    public bool keepPlayerVisibleWhileMovingIntoObjectCell = true;
    [Range(0.2f, 1f)]
    [Tooltip("오브젝트 칸에 도착해 플레이어가 내부에 있을 때 적용할 알파")]
    public float frontObjectMoveAlpha = 0.55f;
    [Min(0)]
    [Tooltip("이동 중 플레이어 sortingOrder 보정치")]
    public int movingPlayerSortingBoost = 200;

    public Vector2Int CurrentCell { get; private set; }
    /// <summary>true이면 WASD 이동 차단 (주시 시스템 등에서 사용)</summary>
    [HideInInspector] public bool lockMovement = false;

    /// <summary>
    /// 외부에서 플레이어의 현재 셀 위치를 강제로 설정 (던전 스폰 등)
    /// </summary>
    public void SetCurrentCell(Vector2Int cell)
    {
        CurrentCell = cell;

        // 점유 시스템 업데이트
        if (occupancy != null)
        {
            occupancy.Release(transform);
            occupancy.TryOccupy(transform, cell);
        }
    }

    /// <summary>
    /// 집중전투 퇴장 등: 격자 셀과 월드 위치를 한 번에 맞춤.
    /// </summary>
    public void WarpToFieldCell(Vector2Int cell)
    {
        if (grid == null)
            grid = FindObjectOfType<GridBoard>();
        if (grid == null || !grid.InBounds(cell))
            return;

        transform.position = grid.CellToWorld(cell);
        SetCurrentCell(cell);
    }

    private bool _isMoving;
    private SpriteRenderer[] _playerSpriteRenderers;
    private int[] _basePlayerSortingOrders;
    private bool _playerSortingBoostApplied;
    private readonly List<SpriteRenderer> _moveOcclusionTargets = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> _arrivedCellOccluders = new List<SpriteRenderer>();
    private readonly List<Color> _arrivedCellBaseColors = new List<Color>();
    private readonly List<Harvestable> _cellHarvestables = new List<Harvestable>();
    private float _hungerRemainder;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (crouch == null) crouch = GetComponent<PlayerCrouch>() ?? FindObjectOfType<PlayerCrouch>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        if (multiEnemyAttack == null) multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();
        if (turnCoordinator == null) turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();
        CachePlayerSortingRenderers();

        Debug.Log("[PlayerGridMover] Awake - Grid: " + (grid != null ? grid.name : "NULL"));
    }

    /// <summary>
    /// 시야/표시는 transform 기준이어야 하는데 CurrentCell만 쓰면 스폰·애니·다른 스크립트 후 위치와 어긋날 수 있음.
    /// 이동 중이 아닐 때 매 프레임 월드→셀을 맞춤.
    /// </summary>
    private void LateUpdate()
    {
        if (grid == null || _isMoving) return;
        Vector2Int fromWorld = grid.WorldToCell(transform.position);
        if (fromWorld != CurrentCell)
            SetCurrentCell(fromWorld);
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;

        EndMoveVisibilityAssist();
        ClearArrivedCellTransparency();

        if (occupancy != null)
            occupancy.Release(transform);
    }

    /// <summary>
    /// 씬 로드 시 참조 다시 찾기 (던전/필드 전환 시 필수)
    /// </summary>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 딜레이 후 참조 찾기 (DungeonController.Start()가 GridBoard 설정한 후에 실행되도록)
        StartCoroutine(RefreshReferencesDelayed());
    }

    /// <summary>
    /// 같은 씬에서 필드로 복귀할 때 (ModeManager). 씬 로드 없이 GridBoard 등을 다시 맞춤.
    /// </summary>
    public void NotifyReferencesNeedRefresh()
    {
        StartCoroutine(RefreshReferencesDelayed());
    }

    private System.Collections.IEnumerator RefreshReferencesDelayed()
    {
        // 다른 Start()들이 실행될 때까지 대기
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        // 새 씬의 GridBoard 찾기
        grid = FindObjectOfType<GridBoard>();
        fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        playerStats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
        rest = FindObjectOfType<RestController>();
        occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();
        turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();

        Debug.Log($"[PlayerGridMover] RefreshReferences - Grid: {(grid != null ? grid.name : "NULL")}");

        if (grid != null)
        {
            Debug.Log($"[PlayerGridMover] GridBoard blockedMoveCells count: {grid.blockedMoveCells.Count}");
            Debug.Log($"[PlayerGridMover] GridBoard size: {grid.width}x{grid.height}");
        }

        // 현재 위치로 CurrentCell 동기화
        if (grid != null)
        {
            CurrentCell = grid.WorldToCell(transform.position);

            if (occupancy != null)
            {
                occupancy.TryOccupy(transform, CurrentCell);
            }

            Debug.Log($"[PlayerGridMover] Synced CurrentCell: {CurrentCell}");
        }

        ApplyArrivedCellTransparency(CurrentCell);
    }

    private void Start()
    {
        // 첫 씬 로드 시 초기화
        if (grid != null)
        {
            CurrentCell = grid.WorldToCell(transform.position);
            transform.position = grid.CellToWorld(CurrentCell);

            if (occupancy != null)
            {
                occupancy.TryOccupy(transform, CurrentCell);
            }

            Debug.Log("[PlayerGridMover] Start - CurrentCell: " + CurrentCell);
        }

        ApplyArrivedCellTransparency(CurrentCell);
    }

    private void Update()
    {
        if (DeathManager.IsDeathInputLocked)
            return;

        if (rest != null && rest.IsResting)
            return;

        if (_isMoving) return;
        if (grid == null) return;
        if (lockMovement) return;
        if (turnCoordinator != null && turnCoordinator.IsBusy) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (turnCoordinator != null)
            {
                turnCoordinator.TryCommitPlayerAction(1, true);
            }
            else
            {
                fieldTime?.Advance(1);
                multiEnemyAttack?.OnPlayerTurnEnd();
            }

            return;
        }

        Vector2Int dir = ReadMoveInput();
        if (dir == Vector2Int.zero) return;

        TryMove(dir);
    }

    private Vector2Int ReadMoveInput()
    {
        int dx = 0;
        int dy = 0;

        // ✅ 대각선 이동 지원 - 두 키 동시에 누르면 대각선
        bool up = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
        bool down = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
        bool left = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
        bool right = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);

        // 현재 누르고 있는 키도 체크 (대각선용)
        bool holdUp = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool holdDown = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        bool holdLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool holdRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        // 방향 결정
        if (up || (holdUp && (left || right))) dy = 1;
        else if (down || (holdDown && (left || right))) dy = -1;

        if (left || (holdLeft && (up || down))) dx = -1;
        else if (right || (holdRight && (up || down))) dx = 1;

        return new Vector2Int(dx, dy);
    }

    private int GetMoveTimeCost()
    {
        if (crouch != null && crouch.IsCrouching)
            return Mathf.Max(1, moveTimeCostWhenCrouching);
        return Mathf.Max(0, baseMoveTimeCost);
    }

    private float GetMoveDuration()
    {
        float duration = Mathf.Max(0.0001f, moveDuration);
        if (crouch != null && crouch.IsCrouching)
            duration *= Mathf.Max(1f, crouchMoveDurationMultiplier);
        return duration;
    }

    /// <summary>HUD 등에 표시용</summary>
    public int GetMoveTimeCostForDisplay() => GetMoveTimeCost();

    private void TryMove(Vector2Int dir)
    {
        Vector2Int target = CurrentCell + dir;

        Debug.Log("[PlayerGridMover] TryMove to " + target);

        if (turnCoordinator != null && turnCoordinator.IsBusy)
            return;

        if (!grid.InBounds(target))
        {
            Debug.Log("[PlayerGridMover] Out of bounds!");
            return;
        }

        bool isBlocked = grid.IsMoveBlocked(target);
        Debug.Log("[PlayerGridMover] IsMoveBlocked(" + target + ") = " + isBlocked);

        if (isBlocked)
        {
            Debug.Log("[PlayerGridMover] BLOCKED at " + target + "!");
            return;
        }

        Debug.Log("[PlayerGridMover] NOT blocked, moving to " + target);

        if (blockMoveIntoOccupiedCell && occupancy != null)
        {
            Transform occ = occupancy.GetOccupant(target);
            if (occ != null && occ != transform)
            {
                Debug.Log("[PlayerGridMover] Cell occupied by " + occ.name);
                return;
            }
        }

        if (occupancy != null)
        {
            if (!occupancy.TryMoveReserve(transform, target))
            {
                Debug.Log("[PlayerGridMover] Move reserve failed!");
                return;
            }
        }

        int cost = GetMoveTimeCost();
        BeginMoveVisibilityAssist(target);

        // ✅ 이동 완료 후 시간 진행하도록 수정
        StartCoroutine(MoveRoutine(target, cost));
    }

    private IEnumerator MoveRoutine(Vector2Int targetCell, int timeCost)
    {
        _isMoving = true;

        Vector3 start = transform.position;
        Vector3 end = grid.CellToWorld(targetCell);

        float t = 0f;
        float invDur = 1f / GetMoveDuration();

        while (t < 1f)
        {
            t += Time.deltaTime * invDur;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = end;
        CurrentCell = targetCell;

        TryActivateTouchedCoralGlow(CurrentCell);
        ApplyArrivedCellTransparency(CurrentCell);
        EndMoveVisibilityAssist();

        _isMoving = false;

        // 이동 직후 즉시 배고픔 소모를 적용한다.
        ApplyMoveHungerCost();

        // ✅ 이동 완료 후 턴 처리 (시간 진행 + 적 턴)
        if (turnCoordinator != null)
        {
            yield return turnCoordinator.CommitPlayerActionAndWait(timeCost, true);
        }
        else
        {
            Debug.Log($"[FieldTime] Player move complete, advancing time by {timeCost}");
            fieldTime?.Advance(timeCost);
            multiEnemyAttack?.OnPlayerTurnEnd();
        }
    }

    private void CachePlayerSortingRenderers()
    {
        _playerSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (_playerSpriteRenderers == null)
            return;

        _basePlayerSortingOrders = new int[_playerSpriteRenderers.Length];
        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            if (_playerSpriteRenderers[i] == null) continue;
            _basePlayerSortingOrders[i] = _playerSpriteRenderers[i].sortingOrder;
        }
    }

    private void BeginMoveVisibilityAssist(Vector2Int targetCell)
    {
        EndMoveVisibilityAssist();

        if (!keepPlayerVisibleWhileMovingIntoObjectCell || grid == null)
            return;

        // 현재 칸도 숨김(반투명 적용 중)이고 다음 칸도 숨김 오브젝트 칸이면
        // 이동 중 플레이어를 앞으로 끌어올리지 않고 계속 숨김 상태를 유지한다.
        bool movingHiddenToHidden = _arrivedCellOccluders.Count > 0;

        CollectCellOccluderRenderers(targetCell, _moveOcclusionTargets);
        if (_moveOcclusionTargets.Count == 0)
            return;

        if (movingHiddenToHidden)
            return;

        ApplyPlayerSortingBoost();
    }

    private void EndMoveVisibilityAssist()
    {
        _moveOcclusionTargets.Clear();

        RevertPlayerSortingBoost();
    }

    private void ApplyPlayerSortingBoost()
    {
        if (_playerSortingBoostApplied)
            return;

        if (_playerSpriteRenderers == null || _basePlayerSortingOrders == null || _playerSpriteRenderers.Length == 0)
            CachePlayerSortingRenderers();

        if (_playerSpriteRenderers == null || _basePlayerSortingOrders == null)
            return;

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            if (_playerSpriteRenderers[i] == null) continue;
            _playerSpriteRenderers[i].sortingOrder = _basePlayerSortingOrders[i] + movingPlayerSortingBoost;
        }

        _playerSortingBoostApplied = true;
    }

    private void RevertPlayerSortingBoost()
    {
        if (!_playerSortingBoostApplied) return;
        if (_playerSpriteRenderers == null || _basePlayerSortingOrders == null) return;

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            if (_playerSpriteRenderers[i] == null) continue;
            _playerSpriteRenderers[i].sortingOrder = _basePlayerSortingOrders[i];
        }

        _playerSortingBoostApplied = false;
    }

    private void ApplyArrivedCellTransparency(Vector2Int cell)
    {
        ClearArrivedCellTransparency();

        if (!keepPlayerVisibleWhileMovingIntoObjectCell || grid == null)
            return;

        CollectCellOccluderRenderers(cell, _arrivedCellOccluders);
        if (_arrivedCellOccluders.Count == 0)
            return;

        float targetAlpha = Mathf.Clamp01(frontObjectMoveAlpha);
        for (int i = 0; i < _arrivedCellOccluders.Count; i++)
        {
            SpriteRenderer sr = _arrivedCellOccluders[i];
            if (sr == null) continue;

            _arrivedCellBaseColors.Add(sr.color);
            Color c = sr.color;
            c.a = targetAlpha;
            sr.color = c;
        }
    }

    private void ClearArrivedCellTransparency()
    {
        int restoreCount = Mathf.Min(_arrivedCellOccluders.Count, _arrivedCellBaseColors.Count);
        for (int i = 0; i < restoreCount; i++)
        {
            SpriteRenderer sr = _arrivedCellOccluders[i];
            if (sr == null) continue;
            sr.color = _arrivedCellBaseColors[i];
        }

        _arrivedCellOccluders.Clear();
        _arrivedCellBaseColors.Clear();
    }

    private void CollectCellOccluderRenderers(Vector2Int cell, List<SpriteRenderer> results)
    {
        results.Clear();
        if (grid == null) return;

        SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.gameObject.activeInHierarchy)
                continue;

            // 플레이어 본인 렌더러는 제외
            if (sr.transform.IsChildOf(transform))
                continue;

            // 환경 오브젝트(채집물/셀 차단물)만 가시성 보정 대상으로 사용
            if (sr.GetComponentInParent<Harvestable>() == null &&
                sr.GetComponentInParent<GridCellBlocker>() == null)
                continue;

            if (grid.WorldToCell(sr.transform.position) != cell)
                continue;

            if (!results.Contains(sr))
                results.Add(sr);
        }
    }

    private void TryActivateTouchedCoralGlow(Vector2Int cell)
    {
        if (grid == null) return;
        if (FieldVisionSystem.Instance == null) return;

        Harvestable.GetHarvestablesAtCell(grid, cell, _cellHarvestables);
        for (int i = 0; i < _cellHarvestables.Count; i++)
        {
            Harvestable h = _cellHarvestables[i];
            if (h == null || h.isHarvested) continue;
            if (h.itemID != 2) continue; // coral only

            FieldVisionSystem.Instance.ActivateTouchedCoralGlow(cell);
            break;
        }
    }

    private void ApplyMoveHungerCost()
    {
        if (playerStats == null)
            return;

        float mul = (crouch != null && crouch.IsCrouching) ? crouchMoveHungerMultiplier : 1f;
        float amount = Mathf.Max(0f, baseMoveHungerCost) * Mathf.Max(0f, mul);
        _hungerRemainder += amount;

        int consume = Mathf.FloorToInt(_hungerRemainder);
        if (consume <= 0)
            return;

        _hungerRemainder -= consume;
        playerStats.hunger -= consume;
        playerStats.ClampAll();
    }
}