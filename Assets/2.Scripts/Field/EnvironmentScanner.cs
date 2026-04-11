using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주시 대상 적 주변을 스캔해서 다중 적 EncounterContext를 생성.
/// </summary>
public static class EnvironmentScanner
{
    public static EncounterContext Scan(
        PlayerStats playerStats,
        Vector2Int playerCell,
        EnemyInstance primaryEnemy,
        GridBoard grid,
        int scanRadius = 5)
    {
        Vector2Int primaryCell = grid.WorldToCell(primaryEnemy.transform.position);

        // 1. 스캔 범위 내 적 수집
        List<EnemyEntry> enemies = new List<EnemyEntry>();
        enemies.Add(new EnemyEntry(primaryEnemy, primaryCell));

        var allEnemies = Object.FindObjectsOfType<EnemyInstance>();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;
            if (enemy == primaryEnemy) continue;
            if (enemy.currentHP <= 0) continue;

            Vector2Int ec = grid.WorldToCell(enemy.transform.position);
            int dist = Mathf.Max(
                Mathf.Abs(ec.x - primaryCell.x),
                Mathf.Abs(ec.y - primaryCell.y)
            );

            if (dist <= scanRadius)
                enemies.Add(new EnemyEntry(enemy, ec));
        }

        // 2. 환경 카운트
        EnvironmentSnapshot env = ScanEnvironment(grid, primaryCell, scanRadius);

        // 3. 조립
        var ctx = new EncounterContext(
            playerStats, playerCell,
            primaryEnemy, primaryCell,
            enemies, env
        );

        Debug.Log($"[EnvironmentScanner] 스캔 완료: " +
                  $"적 {enemies.Count}마리, 환경 [{env}], " +
                  $"중심={primaryCell}, 반경={scanRadius}");

        return ctx;
    }

    private static EnvironmentSnapshot ScanEnvironment(
        GridBoard grid, Vector2Int center, int radius)
    {
        var snap = new EnvironmentSnapshot();

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > radius) continue;
                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                if (!grid.InBounds(cell)) continue;

                if (grid.IsMoveBlocked(cell))
                {
                    snap.wallCount++;
                    continue;
                }
            }
        }

        var harvestables = Object.FindObjectsOfType<Harvestable>();
        foreach (var h in harvestables)
        {
            if (h == null || h.isHarvested) continue;

            Vector2Int hc = grid.WorldToCell(h.transform.position);
            int dist = Mathf.Max(
                Mathf.Abs(hc.x - center.x),
                Mathf.Abs(hc.y - center.y)
            );
            if (dist > radius) continue;

            string name = h.itemName?.ToLower() ?? "";
            if (name.Contains("해초") || name.Contains("수풀") || name.Contains("seaweed"))
                snap.seaweedCount++;
            else if (name.Contains("바위") || name.Contains("rock") || name.Contains("돌"))
                snap.hillCount++;
        }

        snap.wallCount = Mathf.Min(snap.wallCount, 20);

        return snap;
    }
}