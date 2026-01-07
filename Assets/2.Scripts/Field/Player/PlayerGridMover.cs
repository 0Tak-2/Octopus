using System.Collections;
using UnityEngine;

public class PlayerGridMover : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public FieldTimeManager fieldTime;
    public PlayerCrouch crouch;
    public RestController rest;
    public GridOccupancyRegistry occupancy;   // ✅ 추가

    [Header("Move")]
    public float moveDuration = 0.12f;
    public int baseMoveTimeCost = 1;
    public int crouchExtraTimeCost = 1;

    [Header("Occupancy")]
    [Tooltip("점유 시스템이 있을 때, 점유된 셀로 이동을 막습니다.")]
    public bool blockMoveIntoOccupiedCell = true;

    public Vector2Int CurrentCell { get; private set; }

    private bool _isMoving;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (crouch == null) crouch = GetComponent<PlayerCrouch>() ?? FindObjectOfType<PlayerCrouch>();
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>(); // ✅ 추가
    }

    private void Start()
    {
        CurrentCell = grid.WorldToCell(transform.position);
        transform.position = grid.CellToWorld(CurrentCell);

        // ✅ 시작 위치 점유 등록
        if (occupancy != null)
        {
            occupancy.TryOccupy(transform, CurrentCell);
        }
    }

    private void OnDisable()
    {
        // ✅ 비활성화/파괴 시 점유 해제
        if (occupancy != null)
            occupancy.Release(transform);
    }

    private void Update()
    {
        // ✅ 휴식 중에는 이동 완전 차단
        if (rest != null && rest.IsResting)
            return;

        if (_isMoving) return;
        if (grid == null) return;

        // 테스트용 Time 증가
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

        if (!grid.InBounds(target))
            return;

        if (grid.IsMoveBlocked(target))
            return;

        // ✅ 점유 체크(플레이어가 적 칸으로 들어가 겹치는 것 방지)
        if (blockMoveIntoOccupiedCell && occupancy != null)
        {
            Transform occ = occupancy.GetOccupant(target);
            if (occ != null && occ != transform)
            {
                // 점유된 칸이면 이동 금지 + Time 소비도 하지 않음
                return;
            }
        }

        // ✅ 가장 중요: "예약" 성공해야 실제 이동/Time소비
        if (occupancy != null)
        {
            // 목적지 셀이 이미 누가 예약/점유 중이면 실패
            if (!occupancy.TryMoveReserve(transform, target))
            {
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
