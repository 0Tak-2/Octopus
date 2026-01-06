using UnityEngine;

/// <summary>
/// 필드 전투 유틸:
/// - 체비셰프 거리
/// - LOS(그리드 기반 Bresenham)
/// - GridBoard가 제공하는 "시야막힘" 판정 함수/데이터를 사용(없으면 fallback: 이동막힘으로 대체)
/// </summary>
public static class FieldCombatUtils
{
    public static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy);
    }

    /// <summary>
    /// GridBoard에 다음 중 하나가 존재한다고 가정하고 우선 사용:
    /// - bool IsVisionBlocked(Vector2Int cell)
    /// - bool IsBlockedVision(Vector2Int cell)
    /// - bool IsMoveBlocked(Vector2Int cell) (fallback)
    ///  
    /// 실제 프로젝트 GridBoard 구현명이 다를 수 있어서, 리플렉션으로 최대한 호환되게 호출.
    /// </summary>
    public static bool HasLineOfSight(object gridBoard, Vector2Int from, Vector2Int to)
    {
        // 같은 칸은 LOS true
        if (from == to) return true;
        if (gridBoard == null) return true; // 보드 없으면 일단 막지 않음(디버그 편의)

        // Bresenham line (from -> to), 중간 칸의 시야블록 검사
        int x0 = from.x;
        int y0 = from.y;
        int x1 = to.x;
        int y1 = to.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = (x0 < x1) ? 1 : -1;
        int sy = (y0 < y1) ? 1 : -1;
        int err = dx - dy;

        // 시작칸은 제외, 목표칸은 "가시성" 판정에 포함시키지 않는 편이 보통 자연스러움(벽 위에 서는 케이스 등)
        // 여기서는 "중간칸"만 검사하고, 목표칸은 검사하지 않음.
        int x = x0;
        int y = y0;

        while (!(x == x1 && y == y1))
        {
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }

            // 목표칸 도달하면 break (목표칸은 검사 X)
            if (x == x1 && y == y1) break;

            var cell = new Vector2Int(x, y);
            if (IsVisionBlockingCell(gridBoard, cell))
                return false;
        }

        return true;
    }

    private static bool IsVisionBlockingCell(object gridBoard, Vector2Int cell)
    {
        // 우선순위: IsVisionBlocked / IsBlockedVision / IsMoveBlocked
        var t = gridBoard.GetType();

        // 1) IsVisionBlocked(Vector2Int)
        var m1 = t.GetMethod("IsVisionBlocked");
        if (m1 != null)
        {
            var r = m1.Invoke(gridBoard, new object[] { cell });
            if (r is bool b) return b;
        }

        // 2) IsBlockedVision(Vector2Int)
        var m2 = t.GetMethod("IsBlockedVision");
        if (m2 != null)
        {
            var r = m2.Invoke(gridBoard, new object[] { cell });
            if (r is bool b) return b;
        }

        // 3) IsMoveBlocked(Vector2Int) fallback
        var m3 = t.GetMethod("IsMoveBlocked");
        if (m3 != null)
        {
            var r = m3.Invoke(gridBoard, new object[] { cell });
            if (r is bool b) return b;
        }

        return false;
    }
}
