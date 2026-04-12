using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyEntry
{
    public EnemyInstance instance;
    public Vector2Int fieldCell;

    public EnemyEntry(EnemyInstance inst, Vector2Int cell)
    {
        instance = inst;
        fieldCell = cell;
    }
}

[System.Serializable]
public class EnvironmentSnapshot
{
    public int wallCount;
    public int seaweedCount;
    public int hillCount;
    public int currentCount;

    public override string ToString()
    {
        return $"벽={wallCount}, 해초={seaweedCount}, 언덕={hillCount}, 해류={currentCount}";
    }
}

/// <summary>
/// 캡슐(스타디움) 영역 정보.
/// 필드 좌표 → 전투 그리드 좌표 매핑까지 모두 포함.
/// </summary>
[System.Serializable]
public class CapsuleRegion
{
    /// <summary>캡슐에 포함된 필드 셀 집합</summary>
    public HashSet<Vector2Int> fieldCells;

    /// <summary>캡슐의 bounding box (필드 좌표)</summary>
    public Vector2Int boundsMin;
    public Vector2Int boundsMax;

    /// <summary>전투 그리드 크기</summary>
    public int width;
    public int height;

    /// <summary>플레이어와 주시 적 필드 좌표 (스폰 위치 계산용)</summary>
    public Vector2Int playerFieldCell;
    public Vector2Int primaryEnemyFieldCell;

    /// <summary>캡슐 생성에 사용된 시야 반경</summary>
    public int radiusUsed;

    /// <summary>필드 좌표 → 전투 그리드 좌표</summary>
    public Vector2Int FieldToCombat(Vector2Int fieldCell)
    {
        return fieldCell - boundsMin;
    }

    /// <summary>전투 그리드 좌표 → 필드 좌표</summary>
    public Vector2Int CombatToField(Vector2Int combatCell)
    {
        return combatCell + boundsMin;
    }

    /// <summary>해당 필드 셀이 캡슐 안에 있는지</summary>
    public bool ContainsFieldCell(Vector2Int fieldCell)
    {
        return fieldCells != null && fieldCells.Contains(fieldCell);
    }

    /// <summary>해당 전투 셀이 캡슐 안인지</summary>
    public bool ContainsCombatCell(Vector2Int combatCell)
    {
        return ContainsFieldCell(CombatToField(combatCell));
    }
}

[System.Serializable]
public class EncounterContext
{
    public PlayerStats playerStats;
    public Vector2Int playerCell;

    public EnemyInstance primaryEnemy;
    public Vector2Int primaryEnemyCell;

    public List<EnemyEntry> enemies;

    /// <summary>(레거시) 환경 카운트 — 캡슐 시스템에선 사용 안 함</summary>
    public EnvironmentSnapshot environment;

    /// <summary>캡슐 영역 정보 (신 시스템)</summary>
    public CapsuleRegion capsule;

    /// <summary>필드 GridBoard 참조 (벽/채집물 복사용)</summary>
    public GridBoard fieldGrid;

    // 기존 1v1 호환
    public EnemyInstance enemy => primaryEnemy;
    public Vector2Int enemyCell => primaryEnemyCell;

    /// <summary>기존 1v1 호환 생성자</summary>
    public EncounterContext(PlayerStats playerStats, EnemyInstance enemy, Vector2Int playerCell, Vector2Int enemyCell)
    {
        this.playerStats = playerStats;
        this.playerCell = playerCell;
        this.primaryEnemy = enemy;
        this.primaryEnemyCell = enemyCell;
        this.enemies = new List<EnemyEntry> { new EnemyEntry(enemy, enemyCell) };
        this.environment = null;
        this.capsule = null;
        this.fieldGrid = null;
    }

    /// <summary>신 캡슐 시스템 생성자</summary>
    public EncounterContext(
        PlayerStats playerStats,
        Vector2Int playerCell,
        EnemyInstance primaryEnemy,
        Vector2Int primaryEnemyCell,
        List<EnemyEntry> enemies,
        CapsuleRegion capsule,
        GridBoard fieldGrid)
    {
        this.playerStats = playerStats;
        this.playerCell = playerCell;
        this.primaryEnemy = primaryEnemy;
        this.primaryEnemyCell = primaryEnemyCell;
        this.enemies = enemies ?? new List<EnemyEntry>();
        this.environment = null;
        this.capsule = capsule;
        this.fieldGrid = fieldGrid;
    }

    public bool IsMultiEnemy => enemies != null && enemies.Count > 1;
    public bool HasEnvironment => environment != null;

    /// <summary>캡슐 시스템 사용 여부 (신 시스템 진입 판단)</summary>
    public bool HasCapsule => capsule != null;
}