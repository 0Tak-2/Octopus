using UnityEngine;

public class CombatMovementService
{
    private readonly int _moveRange;
    private readonly System.Func<Vector2Int, bool> _isBlocked;
    private readonly System.Func<int, int, bool> _inBounds;

    public CombatMovementService(int moveRange,
        System.Func<Vector2Int, bool> isBlocked,
        System.Func<int, int, bool> inBounds)
    {
        _moveRange = Mathf.Max(1, moveRange);
        _isBlocked = isBlocked;
        _inBounds = inBounds;
    }

    public bool TryBuildPath(Vector2Int from, Vector2Int target, Vector2Int avoidCell, out Vector2Int[] steps)
    {
        steps = null;

        if (!_inBounds(target.x, target.y)) return false;

        int dxAbs = Mathf.Abs(target.x - from.x);
        int dyAbs = Mathf.Abs(target.y - from.y);
        int cheb = Mathf.Max(dxAbs, dyAbs);

        if (cheb <= 0 || cheb > _moveRange) return false;

        if (_isBlocked(target)) return false;
        if (target == avoidCell) return false;

        if (cheb == 1)
        {
            if (!IsAdjacent8(from, target)) return false;
            steps = new[] { target };
            return true;
        }

        // moveRange=2 전제(현재 구현과 동일)
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
            if (!_inBounds(mid.x, mid.y)) continue;
            if (_isBlocked(mid)) continue;
            if (mid == avoidCell) continue;
            if (!IsAdjacent8(mid, target)) continue;

            steps = new[] { mid, target };
            return true;
        }

        // 보조 탐색
        for (int mx = -1; mx <= 1; mx++)
            for (int my = -1; my <= 1; my++)
            {
                if (mx == 0 && my == 0) continue;

                Vector2Int mid = new Vector2Int(from.x + mx, from.y + my);
                if (!_inBounds(mid.x, mid.y)) continue;
                if (_isBlocked(mid)) continue;
                if (mid == avoidCell) continue;
                if (!IsAdjacent8(mid, target)) continue;

                steps = new[] { mid, target };
                return true;
            }

        return false;
    }

    public static bool IsAdjacent8(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0);
    }

    public static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy);
    }
}
