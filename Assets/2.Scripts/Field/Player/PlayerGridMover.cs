using System.Collections;
using UnityEngine;

public class PlayerGridMover : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public FieldTimeManager fieldTime;
    public PlayerCrouch crouch;
    public RestController rest;
    public GridOccupancyRegistry occupancy;
    public FieldMultiEnemyAttack multiEnemyAttack;

    [Header("Move")]
    public float moveDuration = 0.12f;
    [Tooltip("일어선 상태에서 한 칸 이동 시 소모 Time")]
    public int baseMoveTimeCost = 1;
    [Tooltip("웅크린 상태에서 한 칸 이동 시 소모 Time (기본 2)")]
    [Min(1)] public int moveTimeCostWhenCrouching = 2;

    [Header("Occupancy")]
    [Tooltip("점유 시스템이 있을 때, 점유된 셀로 이동을 막습니다.")]
    public bool blockMoveIntoOccupiedCell = true;

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

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (crouch == null) crouch = GetComponent<PlayerCrouch>() ?? FindObjectOfType<PlayerCrouch>();
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        if (multiEnemyAttack == null) multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();

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

    private System.Collections.IEnumerator RefreshReferencesDelayed()
    {
        // 다른 Start()들이 실행될 때까지 대기
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        // 새 씬의 GridBoard 찾기
        grid = FindObjectOfType<GridBoard>();
        fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        rest = FindObjectOfType<RestController>();
        occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();

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
    }

    private void Update()
    {
        if (rest != null && rest.IsResting)
            return;

        if (_isMoving) return;
        if (grid == null) return;
        if (lockMovement) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            fieldTime?.Advance(1);

            // ✅ 대기 시에도 주변 적들이 공격!
            if (multiEnemyAttack != null)
            {
                multiEnemyAttack.OnPlayerTurnEnd();
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

    /// <summary>HUD 등에 표시용</summary>
    public int GetMoveTimeCostForDisplay() => GetMoveTimeCost();

    private void TryMove(Vector2Int dir)
    {
        Vector2Int target = CurrentCell + dir;

        Debug.Log("[PlayerGridMover] TryMove to " + target);

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
        // ✅ 이동 완료 후 시간 진행하도록 수정
        StartCoroutine(MoveRoutine(target, cost));
    }

    private IEnumerator MoveRoutine(Vector2Int targetCell, int timeCost)
    {
        _isMoving = true;

        Vector3 start = transform.position;
        Vector3 end = grid.CellToWorld(targetCell);

        float t = 0f;
        float invDur = 1f / Mathf.Max(0.0001f, moveDuration);

        while (t < 1f)
        {
            t += Time.deltaTime * invDur;
            transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(t));
            yield return null;
        }

        transform.position = end;
        CurrentCell = targetCell;

        _isMoving = false;

        // ✅ 이동 완료 후 시간 진행!
        Debug.Log($"[FieldTime] Player move complete, advancing time by {timeCost}");
        fieldTime?.Advance(timeCost);

        // ✅ 주변 모든 적이 공격!
        if (multiEnemyAttack != null)
        {
            multiEnemyAttack.OnPlayerTurnEnd();
        }
    }
}