using System.Collections;
using UnityEngine;

public class PlayerGridMover : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public FieldTimeManager fieldTime;
    public PlayerCrouch crouch;
    public RestController rest;

    [Header("Move")]
    public float moveDuration = 0.12f;
    public int baseMoveTimeCost = 1;
    public int crouchExtraTimeCost = 1;

    public Vector2Int CurrentCell { get; private set; }

    private bool _isMoving;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (crouch == null) crouch = GetComponent<PlayerCrouch>() ?? FindObjectOfType<PlayerCrouch>();
        if (rest == null) rest = FindObjectOfType<RestController>();
    }

    private void Start()
    {
        CurrentCell = grid.WorldToCell(transform.position);
        transform.position = grid.CellToWorld(CurrentCell);
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
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, moveDuration);
            transform.position = Vector3.Lerp(start, end, t);
            yield return null;
        }

        transform.position = end;
        CurrentCell = targetCell;

        _isMoving = false;
    }
}
