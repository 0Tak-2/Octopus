using System.Collections.Generic;
using UnityEngine;

public class BuildResult
{
    public CombatGridData gridData;
    public Vector2Int playerSpawn;
    public Vector2Int primaryEnemySpawn;
    public List<Vector2Int> enemySpawns;
}

/// <summary>
/// EnvironmentSnapshot + 다중 적 정보로 10x10 전투 필드를 생성.
/// </summary>
public static class FocusCombatBuilder
{
    public static readonly int FIELD_SIZE = 10;

    public static BuildResult Build(EncounterContext ctx)
    {
        int w = FIELD_SIZE;
        int h = FIELD_SIZE;

        var result = new BuildResult();
        result.gridData = new CombatGridData(w, h);

        PlaceSpawnPositions(ctx, w, h, result);
        HashSet<Vector2Int> reserved = BuildReservedCells(result);

        if (ctx.HasEnvironment)
            PlaceEnvironment(result.gridData, ctx.environment, reserved, w, h);

        Debug.Log($"[FocusCombatBuilder] 필드 생성 완료: {w}x{h}, " +
                  $"플레이어={result.playerSpawn}, " +
                  $"적 {result.enemySpawns.Count}마리");

        return result;
    }

    private static void PlaceSpawnPositions(EncounterContext ctx, int w, int h, BuildResult result)
    {
        int cx = w / 2;
        int primaryY = h - 3;
        result.primaryEnemySpawn = new Vector2Int(cx, primaryY);

        int playerY = 2;
        result.playerSpawn = new Vector2Int(cx, playerY);

        result.enemySpawns = new List<Vector2Int>();
        result.enemySpawns.Add(result.primaryEnemySpawn);

        if (ctx.enemies != null && ctx.enemies.Count > 1)
        {
            var usedCells = new HashSet<Vector2Int>
            {
                result.playerSpawn,
                result.primaryEnemySpawn
            };

            for (int i = 1; i < ctx.enemies.Count; i++)
            {
                Vector2Int spawn = FindNearbyOpen(
                    result.primaryEnemySpawn,
                    usedCells, w, h, 1, 3
                );
                result.enemySpawns.Add(spawn);
                usedCells.Add(spawn);
            }
        }
    }

    private static Vector2Int FindNearbyOpen(
        Vector2Int center, HashSet<Vector2Int> used,
        int w, int h, int minDist, int maxDist)
    {
        for (int dist = minDist; dist <= maxDist; dist++)
        {
            var candidates = new List<Vector2Int>();
            for (int dx = -dist; dx <= dist; dx++)
            {
                for (int dy = -dist; dy <= dist; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != dist) continue;
                    Vector2Int c = new Vector2Int(center.x + dx, center.y + dy);
                    if (c.x < 1 || c.x >= w - 1 || c.y < 1 || c.y >= h - 1) continue;
                    if (used.Contains(c)) continue;
                    candidates.Add(c);
                }
            }
            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];
        }

        for (int x = 1; x < w - 1; x++)
            for (int y = 1; y < h - 1; y++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (!used.Contains(c)) return c;
            }

        return center + Vector2Int.right;
    }

    private static HashSet<Vector2Int> BuildReservedCells(BuildResult result)
    {
        var reserved = new HashSet<Vector2Int>();
        AddWithNeighbors(reserved, result.playerSpawn);
        foreach (var es in result.enemySpawns)
            AddWithNeighbors(reserved, es);
        return reserved;
    }

    private static void AddWithNeighbors(HashSet<Vector2Int> set, Vector2Int cell)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                set.Add(new Vector2Int(cell.x + dx, cell.y + dy));
    }

    private static void PlaceEnvironment(
        CombatGridData grid, EnvironmentSnapshot env,
        HashSet<Vector2Int> reserved, int w, int h)
    {
        int walls = Mathf.Clamp(Mathf.RoundToInt(env.wallCount * 0.3f), 0, 12);
        int seaweeds = Mathf.Clamp(Mathf.RoundToInt(env.seaweedCount * 0.4f), 0, 5);
        int hills = Mathf.Clamp(Mathf.RoundToInt(env.hillCount * 0.4f), 0, 4);
        int currents = Mathf.Clamp(Mathf.RoundToInt(env.currentCount * 0.4f), 0, 3);

        PlaceWallClusters(grid, walls, reserved, w, h);
        PlaceSingleTiles(grid, CombatTileType.Seaweed, seaweeds, reserved, w, h);
        PlaceSingleTiles(grid, CombatTileType.Hill, hills, reserved, w, h);
        PlaceSingleTiles(grid, CombatTileType.Current, currents, reserved, w, h);

        Debug.Log($"[FocusCombatBuilder] 지형 배치: " +
                  $"벽={walls}, 해초={seaweeds}, 언덕={hills}, 해류={currents}");
    }

    private static void PlaceWallClusters(
        CombatGridData grid, int totalWalls,
        HashSet<Vector2Int> reserved, int w, int h)
    {
        if (totalWalls <= 0) return;

        int placed = 0;
        int attempts = 0;

        while (placed < totalWalls && attempts < 200)
        {
            attempts++;

            int clusterSize = Mathf.Min(Random.Range(2, 4), totalWalls - placed);

            int sx = Random.Range(1, w - 1);
            int sy = Random.Range(1, h - 1);
            Vector2Int start = new Vector2Int(sx, sy);

            if (reserved.Contains(start)) continue;
            if (grid.Get(sx, sy) != CombatTileType.Empty) continue;

            var cluster = new List<Vector2Int> { start };
            var frontier = new List<Vector2Int> { start };

            while (cluster.Count < clusterSize && frontier.Count > 0)
            {
                var from = frontier[Random.Range(0, frontier.Count)];
                var dirs = new Vector2Int[]
                {
                    Vector2Int.up, Vector2Int.down,
                    Vector2Int.left, Vector2Int.right
                };

                for (int i = dirs.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (dirs[i], dirs[j]) = (dirs[j], dirs[i]);
                }

                bool grew = false;
                foreach (var d in dirs)
                {
                    Vector2Int next = from + d;
                    if (next.x < 1 || next.x >= w - 1 || next.y < 1 || next.y >= h - 1) continue;
                    if (reserved.Contains(next)) continue;
                    if (cluster.Contains(next)) continue;
                    if (grid.Get(next.x, next.y) != CombatTileType.Empty) continue;

                    cluster.Add(next);
                    frontier.Add(next);
                    grew = true;
                    break;
                }

                if (!grew) frontier.Remove(from);
            }

            foreach (var c in cluster)
            {
                grid.Set(c.x, c.y, CombatTileType.Wall);
                reserved.Add(c);
                placed++;
            }
        }
    }

    private static void PlaceSingleTiles(
        CombatGridData grid, CombatTileType type, int count,
        HashSet<Vector2Int> reserved, int w, int h)
    {
        int placed = 0;
        for (int i = 0; i < 100 && placed < count; i++)
        {
            int x = Random.Range(1, w - 1);
            int y = Random.Range(1, h - 1);
            Vector2Int c = new Vector2Int(x, y);

            if (reserved.Contains(c)) continue;
            if (grid.Get(x, y) != CombatTileType.Empty) continue;

            grid.Set(x, y, type);
            reserved.Add(c);
            placed++;
        }
    }
}