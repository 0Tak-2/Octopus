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
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (crouch == null) crouch = GetComponent<PlayerCrouch>() ?? FindObjectOfType<PlayerCrouch>();
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();

        Debug.Log("[PlayerGridMover] Awake - Grid: " + (grid != null ? grid.name : "NULL"));
    }

    private void Start()
    {
        CurrentCell = grid.WorldToCell(transform.position);
        transform.position = grid.CellToWorld(CurrentCell);

        if (occupancy != null)
        {
            occupancy.TryOccupy(transform, CurrentCell);
        }

        Debug.Log("[PlayerGridMover] Start - CurrentCell: " + CurrentCell + ", blockedMoveCells: " + grid.blockedMoveCells.Count);
        Debug.Log($"[PlayerGridMover] grid={grid?.name} id={grid?.GetInstanceID()}");
    }

    private void OnDisable()
    {
        if (occupancy != null)
            occupancy.Release(transform);
    }

    private void Update()
    {
        if (rest != null && rest.IsResting)
            return;

        if (_isMoving) return;
        if (grid == null) return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            fieldTime?.Advance(1);
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
        fieldTime?.Advance(cost);

        StartCoroutine(MoveRoutine(target));
    }

    private IEnumerator MoveRoutine(Vector2Int targetCell)
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
    }
}