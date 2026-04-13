using UnityEngine;

/// <summary>
/// 필드 간 포털(가장자리 출입) 방향 및 정렬(어느 높이/너비로 나오는지) 계산
/// </summary>
public static class FieldTransitionUtil
{
    public static EdgeSide Opposite(EdgeSide e)
    {
        return e switch
        {
            EdgeSide.Right => EdgeSide.Left,
            EdgeSide.Left => EdgeSide.Right,
            EdgeSide.Top => EdgeSide.Bottom,
            EdgeSide.Bottom => EdgeSide.Top,
            _ => EdgeSide.Right
        };
    }

    /// <summary>
    /// 출구가 있는 가장자리와 셀을 알 때, 그 변을 따라 0~1 위치(입구 정렬용)
    /// </summary>
    public static float ComputeAlignT(EdgeSide exitEdge, Vector2Int cell, int mapWidth, int mapHeight)
    {
        switch (exitEdge)
        {
            case EdgeSide.Right:
            case EdgeSide.Left:
                return (cell.y - 1f) / Mathf.Max(1f, mapHeight - 2f);
            case EdgeSide.Top:
            case EdgeSide.Bottom:
                return (cell.x - 1f) / Mathf.Max(1f, mapWidth - 2f);
            default:
                return 0.5f;
        }
    }

    public static EdgeSide InferClosestEdge(Vector2Int cell, int mapWidth, int mapHeight)
    {
        int dR = mapWidth - 1 - cell.x;
        int dL = cell.x;
        int dT = mapHeight - 1 - cell.y;
        int dB = cell.y;
        int m = Mathf.Min(Mathf.Min(dR, dL), Mathf.Min(dT, dB));
        if (m == dR) return EdgeSide.Right;
        if (m == dL) return EdgeSide.Left;
        if (m == dT) return EdgeSide.Top;
        return EdgeSide.Bottom;
    }
}
