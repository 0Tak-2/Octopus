using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

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
    public int baseMoveTimeCost = 1;
    public int crouchExtraTimeCost = 1;

    [Header("Occupancy")]
    [Tooltip("점유 시스템이 있을 때, 점유된 셀로 이동을 막습니다.")]
    public bool blockMoveIntoOccupiedCell = true;

    public Vector2Int CurrentCell { get; private set; }


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
    private bool _isMoving;

    private void Awake()
    {
        Debug.Log("[PlayerGridMover] Awake called");
        RefreshReferences();
    }

    private void OnEnable()
    {
        Debug.Log("[PlayerGridMover] OnEnable - Refreshing references");
        // 약간의 딜레이 후 재탐색 (씬 로드 완료 대기)
        StartCoroutine(RefreshReferencesDelayed());
    }

    private IEnumerator RefreshReferencesDelayed()
    {
        yield return new WaitForEndOfFrame();
        RefreshReferences();
    }

    private void Start()
    {
        Debug.Log("[PlayerGridMover] Start called");

        // 참조 재확인
        RefreshReferences();

        if (grid != null)
        {
            CurrentCell = grid.WorldToCell(transform.position);
            transform.position = grid.CellToWorld(CurrentCell);

            if (occupancy != null)
            {
                occupancy.TryOccupy(transform, CurrentCell);
            }

            Debug.Log($"[PlayerGridMover] Start - CurrentCell: {CurrentCell}, blockedMoveCells: {grid.blockedMoveCells.Count}");
            Debug.Log($"[PlayerGridMover] grid={grid?.name} id={grid?.GetInstanceID()}");
        }
        else
        {
            Debug.LogError("[PlayerGridMover] Start - GridBoard is NULL!");
        }
    }

    /// <summary>
    /// 참조 재탐색 (씬 전환 시 필요)
    /// </summary>
    private void RefreshReferences()
    {
        // GridBoard는 씬마다 다르므로 항상 재탐색
        GridBoard newGrid = FindObjectOfType<GridBoard>();
        if (newGrid != null)
        {
            if (grid != newGrid)
            {
                Debug.Log($"[PlayerGridMover] GridBoard changed: {(grid != null ? grid.name : "null")} → {newGrid.name}");
                grid = newGrid;
            }
        }
        else
        {
            Debug.LogWarning("[PlayerGridMover] GridBoard not found in scene!");
        }

        // FieldTimeManager
        if (fieldTime == null)
        {
            fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        }

        // 나머지 참조들
        if (crouch == null) crouch = GetComponent<PlayerCrouch>();
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        if (multiEnemyAttack == null) multiEnemyAttack = FindObjectOfType<FieldMultiEnemyAttack>();

        Debug.Log($"[PlayerGridMover] References: Grid={grid?.name}, FieldTime={fieldTime != null}, Occupancy={occupancy != null}");
    }

    private void OnDisable()
    {
        if (occupancy != null)
            occupancy.Release(transform);
    }

    private void Update()
    {
        // UI 클릭 중이면 입력 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        // GridBoard 체크 (씬 전환 후 null일 수 있음)
        if (grid == null)
        {
            RefreshReferences();
            if (grid == null) return; // 여전히 없으면 이동 불가
        }

        if (rest != null && rest.IsResting)
            return;

        if (_isMoving) return;
        if (grid == null) return;

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

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) dy = 1;
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) dy = -1;
        else if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) dx = -1;
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) dx = 1;

        return new Vector2Int(dx, dy);
    }

    private int GetMoveTimeCost()
    {
        int cost = baseMoveTimeCost;
        if (crouch != null && crouch.IsCrouching)
            cost += crouchExtraTimeCost;
        return Mathf.Max(0, cost);
    }

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