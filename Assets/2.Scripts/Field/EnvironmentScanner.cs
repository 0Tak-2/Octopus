using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 주시 진입 시 캡슐 영역을 계산하고 EncounterContext를 만든다.
/// - 캡슐 안의 모든 적 자동 합류 (플레이어 시야 영역 + 적 시야 영역 사이의 모든 적)
/// - 시야 반경은 FieldVisionSystem.playerVisionRange를 동적으로 사용
/// </summary>
public static class EnvironmentScanner
{
    /// <summary>
    /// 캡슐 시스템 기반 스캔.
    /// </summary>
    public static EncounterContext Scan(
        PlayerStats playerStats,
        Vector2Int playerCell,
        EnemyInstance primaryEnemy,
        GridBoard grid)
    {
        if (primaryEnemy == null || grid == null)
        {
            Debug.LogError("[EnvironmentScanner] primaryEnemy 또는 grid가 null");
            return null;
        }

        Vector2Int primaryCell = grid.WorldToCell(primaryEnemy.transform.position);

        // 1. 시야 반경 가져오기 (웅크리기/스킬 등 자동 반영)
        int radius = GetCurrentVisionRadius();

        // 2. 캡슐 영역 계산
        CapsuleRegion capsule = CapsuleRegionBuilder.Build(playerCell, primaryCell, radius, grid);

        // 3. 캡슐 안에 있는 모든 적 합류
        List<EnemyEntry> enemies = CollectEnemiesInCapsule(primaryEnemy, primaryCell, capsule, grid);

        // 4. 컨텍스트 조립
        var ctx = new EncounterContext(
            playerStats: playerStats,
            playerCell: playerCell,
            primaryEnemy: primaryEnemy,
            primaryEnemyCell: primaryCell,
            enemies: enemies,
            capsule: capsule,
            fieldGrid: grid
        );

        Debug.Log($"[EnvironmentScanner] 캡슐 스캔 완료: " +
                  $"radius={radius}, " +
                  $"적 {enemies.Count}마리, " +
                  $"전투그리드={capsule.width}x{capsule.height}");

        return ctx;
    }

    /// <summary>
    /// 현재 플레이어 시야 반경을 가져옴.
    /// FieldVisionSystem이 웅크리기/스킬에 따라 동적으로 값을 관리한다고 가정.
    /// </summary>
    private static int GetCurrentVisionRadius()
    {
        var visionSys = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        if (visionSys == null) return 4; // 안전 폴백

        return Mathf.Max(1, visionSys.playerVisionRange);
    }

    /// <summary>
    /// 캡슐 안에 있는 모든 적 수집 (주시 대상은 항상 인덱스 0).
    /// </summary>
    private static List<EnemyEntry> CollectEnemiesInCapsule(
        EnemyInstance primaryEnemy,
        Vector2Int primaryCell,
        CapsuleRegion capsule,
        GridBoard grid)
    {
        var enemies = new List<EnemyEntry>();

        // 주시 대상이 항상 인덱스 0
        enemies.Add(new EnemyEntry(primaryEnemy, primaryCell));

        var allEnemies = Object.FindObjectsOfType<EnemyInstance>();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;
            if (enemy == primaryEnemy) continue;
            if (enemy.currentHP <= 0) continue;

            Vector2Int ec = grid.WorldToCell(enemy.transform.position);

            // 캡슐 안에 있으면 합류
            if (capsule.ContainsFieldCell(ec))
                enemies.Add(new EnemyEntry(enemy, ec));
        }

        return enemies;
    }
}