using System.Collections.Generic;
using UnityEngine;

public class GridOccupancyRegistry : MonoBehaviour
{
    public static GridOccupancyRegistry Instance { get; private set; }

    // cell -> occupant
    private readonly Dictionary<Vector2Int, Transform> _occupied = new();
    // mover -> current cell (빠른 해제용)
    private readonly Dictionary<Transform, Vector2Int> _currentCellOf = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>현재 셀 점유자 반환(없으면 null)</summary>
    public Transform GetOccupant(Vector2Int cell)
        => _occupied.TryGetValue(cell, out var t) ? t : null;

    public bool IsOccupied(Vector2Int cell)
        => _occupied.ContainsKey(cell);

    /// <summary>
    /// mover를 cell에 "점유"로 등록 (스폰/텔레포트/강제 세팅용)
    /// 이미 다른 점유자가 있으면 false.
    /// </summary>
    public bool TryOccupy(Transform mover, Vector2Int cell)
    {
        if (mover == null) return false;

        // 이미 다른 누가 점유 중이면 실패
        if (_occupied.TryGetValue(cell, out var exist) && exist != null && exist != mover)
            return false;

        // mover가 기존에 점유하던 셀이 있으면 해제
        if (_currentCellOf.TryGetValue(mover, out var prev))
        {
            if (_occupied.TryGetValue(prev, out var prevOcc) && prevOcc == mover)
                _occupied.Remove(prev);
        }

        _occupied[cell] = mover;
        _currentCellOf[mover] = cell;
        return true;
    }

    /// <summary>mover의 점유를 해제</summary>
    public void Release(Transform mover)
    {
        if (mover == null) return;

        if (_currentCellOf.TryGetValue(mover, out var prev))
        {
            if (_occupied.TryGetValue(prev, out var prevOcc) && prevOcc == mover)
                _occupied.Remove(prev);

            _currentCellOf.Remove(mover);
        }
    }

    /// <summary>
    /// mover가 from -> to 로 이동할 수 있는지 체크(예약).
    /// "예약"이라는 이름이지만, 여기서는 즉시 점유를 to로 옮긴다(겹침 방지 목적).
    /// 실패하면 아무 변경 없음.
    /// </summary>
    public bool TryMoveReserve(Transform mover, Vector2Int to)
    {
        if (mover == null) return false;

        // to에 다른 점유자가 있으면 실패
        if (_occupied.TryGetValue(to, out var exist) && exist != null && exist != mover)
            return false;

        // 성공: mover 점유를 to로 이전(사실상 예약=즉시점유)
        return TryOccupy(mover, to);
    }

    /// <summary>mover가 현재 어느 셀로 등록돼 있는지(없으면 false)</summary>
    public bool TryGetCurrentCell(Transform mover, out Vector2Int cell)
        => _currentCellOf.TryGetValue(mover, out cell);
}
