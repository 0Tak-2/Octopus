using System.Collections.Generic;
using UnityEngine;

public class BuildResult
{
    public CombatGridData gridData;
    public Vector2Int playerSpawn;
    public Vector2Int primaryEnemySpawn;
    public List<Vector2Int> enemySpawns;

    /// <summary>캡슐 시스템에서 사용한 영역 정보 (전투 중 캡슐 밖 셀 판정에 필요)</summary>
    public CapsuleRegion capsule;
}

/// <summary>
/// 캡슐 영역을 기반으로 집중전투 필드를 생성.
/// - 필드 좌표를 그대로 보존 (벽/지형 위치 동일)
/// - 캡슐 밖은 Wall로 처리 (시각적으로는 검정 먹물)
/// - 캡슐 안의 모든 적/채집물 그대로 합류
/// </summary>
public static class FocusCombatBuilder
{
    /// <summary>
    /// 캡슐 시스템 기반 빌드 (신 시스템).
    /// EncounterContext.capsule이 설정돼 있어야 함.
    /// </summary>
    public static BuildResult Build(EncounterContext ctx)
    {
        if (ctx == null || ctx.capsule == null)
        {
            Debug.LogError("[FocusCombatBuilder] capsule이 없는 컨텍스트로 호출됨");
            return null;
        }

        var capsule = ctx.capsule;
        var grid = ctx.fieldGrid;

        var result = new BuildResult
        {
            capsule = capsule,
            gridData = new CombatGridData(capsule.width, capsule.height),
            enemySpawns = new List<Vector2Int>()
        };

        // 1. 모든 셀 채우기
        FillGridFromCapsule(result.gridData, capsule, grid);

        // 2. 플레이어/적 스폰 좌표 매핑
        result.playerSpawn = capsule.FieldToCombat(capsule.playerFieldCell);
        result.primaryEnemySpawn = capsule.FieldToCombat(capsule.primaryEnemyFieldCell);

        // 3. 적 스폰 리스트 (필드 좌표 → 전투 좌표)
        if (ctx.enemies != null)
        {
            for (int i = 0; i < ctx.enemies.Count; i++)
            {
                var entry = ctx.enemies[i];
                Vector2Int combatCell = capsule.FieldToCombat(entry.fieldCell);

                // 적이 캡슐 밖이면 (이론적으로 없어야 하지만 안전장치)
                if (!capsule.ContainsFieldCell(entry.fieldCell))
                {
                    Debug.LogWarning($"[FocusCombatBuilder] 적 {entry.instance?.name}이 캡슐 밖에 있음. 스킵.");
                    continue;
                }

                result.enemySpawns.Add(combatCell);
            }
        }

        // 4. 안전: 스폰 셀이 Wall로 채워졌으면 Empty로 강제
        EnsureSpawnIsEmpty(result.gridData, result.playerSpawn);
        EnsureSpawnIsEmpty(result.gridData, result.primaryEnemySpawn);
        foreach (var es in result.enemySpawns)
            EnsureSpawnIsEmpty(result.gridData, es);

        Debug.Log($"[FocusCombatBuilder] 캡슐 필드 생성: " +
                  $"{capsule.width}x{capsule.height}, " +
                  $"플레이어={result.playerSpawn}, " +
                  $"적 {result.enemySpawns.Count}마리");

        return result;
    }

    /// <summary>
    /// 캡슐 영역을 순회하며 각 셀을 적절한 타일 타입으로 채움.
    /// </summary>
    private static void FillGridFromCapsule(CombatGridData gridData, CapsuleRegion capsule, GridBoard fieldGrid)
    {
        // 채집물 위치 캐시 (Harvestable.fieldCell → CombatTileType)
        Dictionary<Vector2Int, CombatTileType> harvestableTiles = BuildHarvestableMap(capsule, fieldGrid);

        for (int cx = 0; cx < capsule.width; cx++)
        {
            for (int cy = 0; cy < capsule.height; cy++)
            {
                Vector2Int combatCell = new Vector2Int(cx, cy);
                Vector2Int fieldCell = capsule.CombatToField(combatCell);

                // 1. 캡슐 밖 → Wall (시각적으로는 검정 먹물)
                if (!capsule.ContainsFieldCell(fieldCell))
                {
                    gridData.Set(cx, cy, CombatTileType.Wall);
                    continue;
                }

                // 2. 필드 벽인지 체크
                if (fieldGrid != null && fieldGrid.IsMoveBlocked(fieldCell))
                {
                    gridData.Set(cx, cy, CombatTileType.Wall);
                    continue;
                }

                // 3. 채집물 (해초/언덕)
                if (harvestableTiles.TryGetValue(fieldCell, out var harvestType))
                {
                    gridData.Set(cx, cy, harvestType);
                    continue;
                }

                // 4. 그 외는 빈 칸
                gridData.Set(cx, cy, CombatTileType.Empty);
            }
        }
    }

    /// <summary>
    /// 캡슐 안에 있는 채집물들을 카테고리별 CombatTileType으로 매핑.
    /// </summary>
    private static Dictionary<Vector2Int, CombatTileType> BuildHarvestableMap(CapsuleRegion capsule, GridBoard fieldGrid)
    {
        var map = new Dictionary<Vector2Int, CombatTileType>();
        if (fieldGrid == null) return map;

        var harvestables = Object.FindObjectsOfType<Harvestable>();
        foreach (var h in harvestables)
        {
            if (h == null || h.isHarvested) continue;

            Vector2Int hc = fieldGrid.WorldToCell(h.transform.position);
            if (!capsule.ContainsFieldCell(hc)) continue;

            string name = h.itemName?.ToLower() ?? "";
            CombatTileType type = CombatTileType.Empty;

            if (name.Contains("해초") || name.Contains("수풀") || name.Contains("seaweed"))
                type = CombatTileType.Seaweed;
            else if (name.Contains("바위") || name.Contains("rock") || name.Contains("돌"))
                type = CombatTileType.Hill;

            if (type != CombatTileType.Empty && !map.ContainsKey(hc))
                map[hc] = type;
        }

        return map;
    }

    /// <summary>
    /// 스폰 위치가 Wall로 채워졌으면 Empty로 강제 (안전장치).
    /// 캡슐 모서리에 있는 플레이어/적이 벽으로 잘못 잡히는 경우 방지.
    /// </summary>
    private static void EnsureSpawnIsEmpty(CombatGridData gridData, Vector2Int cell)
    {
        if (!gridData.InBounds(cell.x, cell.y)) return;
        if (gridData.IsWall(cell.x, cell.y))
        {
            gridData.Set(cell.x, cell.y, CombatTileType.Empty);
            Debug.LogWarning($"[FocusCombatBuilder] 스폰 셀 {cell}이 Wall이라 Empty로 강제 변경");
        }
    }
}