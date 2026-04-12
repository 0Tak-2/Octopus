using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어와 주시 적 사이의 캡슐(스타디움) 영역을 계산.
/// 점-선분 거리 ≤ radius인 모든 셀을 캡슐 안으로 판정.
/// </summary>
public static class CapsuleRegionBuilder
{
    /// <summary>
    /// 캡슐 영역 계산 + bounding box + 좌표 매핑까지 한 번에 만들어줌.
    /// </summary>
    /// <param name="playerCell">플레이어 필드 셀</param>
    /// <param name="enemyCell">주시 적 필드 셀</param>
    /// <param name="radius">시야 반경 (FieldVisionSystem.playerVisionRange 권장)</param>
    /// <param name="grid">필드 GridBoard (경계 판정용)</param>
    public static CapsuleRegion Build(
        Vector2Int playerCell,
        Vector2Int enemyCell,
        int radius,
        GridBoard grid)
    {
        var region = new CapsuleRegion
        {
            fieldCells = new HashSet<Vector2Int>(),
            playerFieldCell = playerCell,
            primaryEnemyFieldCell = enemyCell,
            radiusUsed = radius
        };

        // bounding box 계산: 두 점 ± radius
        int minX = Mathf.Min(playerCell.x, enemyCell.x) - radius;
        int maxX = Mathf.Max(playerCell.x, enemyCell.x) + radius;
        int minY = Mathf.Min(playerCell.y, enemyCell.y) - radius;
        int maxY = Mathf.Max(playerCell.y, enemyCell.y) + radius;

        // 필드 경계로 클램프
        if (grid != null)
        {
            minX = Mathf.Max(minX, 0);
            minY = Mathf.Max(minY, 0);
            maxX = Mathf.Min(maxX, grid.width - 1);
            maxY = Mathf.Min(maxY, grid.height - 1);
        }

        // bounding box 안의 모든 셀에 대해 캡슐 판정
        Vector2 p = new Vector2(playerCell.x, playerCell.y);
        Vector2 e = new Vector2(enemyCell.x, enemyCell.y);
        float r2 = radius * radius;

        int actualMinX = int.MaxValue, actualMaxX = int.MinValue;
        int actualMinY = int.MaxValue, actualMaxY = int.MinValue;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector2 c = new Vector2(x, y);
                float distSq = PointToSegmentDistanceSq(c, p, e);

                if (distSq <= r2)
                {
                    var cell = new Vector2Int(x, y);
                    region.fieldCells.Add(cell);

                    if (x < actualMinX) actualMinX = x;
                    if (x > actualMaxX) actualMaxX = x;
                    if (y < actualMinY) actualMinY = y;
                    if (y > actualMaxY) actualMaxY = y;
                }
            }
        }

        // 안전 폴백: 캡슐 셀이 0개면 두 점만이라도 포함
        if (region.fieldCells.Count == 0)
        {
            region.fieldCells.Add(playerCell);
            region.fieldCells.Add(enemyCell);
            actualMinX = Mathf.Min(playerCell.x, enemyCell.x);
            actualMaxX = Mathf.Max(playerCell.x, enemyCell.x);
            actualMinY = Mathf.Min(playerCell.y, enemyCell.y);
            actualMaxY = Mathf.Max(playerCell.y, enemyCell.y);
        }

        region.boundsMin = new Vector2Int(actualMinX, actualMinY);
        region.boundsMax = new Vector2Int(actualMaxX, actualMaxY);
        region.width = actualMaxX - actualMinX + 1;
        region.height = actualMaxY - actualMinY + 1;

        Debug.Log($"[CapsuleRegion] 생성 완료: " +
                  $"radius={radius}, " +
                  $"플레이어={playerCell}, 적={enemyCell}, " +
                  $"전투그리드={region.width}x{region.height}, " +
                  $"셀 수={region.fieldCells.Count}");

        return region;
    }

    /// <summary>
    /// 점 c에서 선분(a→b)까지의 최단 거리의 제곱.
    /// 제곱으로 비교하면 sqrt 비용 절약.
    /// </summary>
    private static float PointToSegmentDistanceSq(Vector2 c, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        Vector2 ac = c - a;

        float abLenSq = ab.sqrMagnitude;

        // 두 점이 같으면 점-점 거리
        if (abLenSq < 0.0001f)
            return ac.sqrMagnitude;

        // 투영 계수 t (0=a, 1=b)
        float t = Vector2.Dot(ac, ab) / abLenSq;
        t = Mathf.Clamp01(t);

        // 선분 위 가장 가까운 점
        Vector2 closest = a + t * ab;
        return (c - closest).sqrMagnitude;
    }
}