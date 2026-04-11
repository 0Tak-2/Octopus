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

[System.Serializable]
public class EncounterContext
{
    public PlayerStats playerStats;
    public Vector2Int playerCell;

    public EnemyInstance primaryEnemy;
    public Vector2Int primaryEnemyCell;

    public List<EnemyEntry> enemies;
    public EnvironmentSnapshot environment;

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
    }

    /// <summary>다중 적 + 환경 스캔 생성자</summary>
    public EncounterContext(
        PlayerStats playerStats,
        Vector2Int playerCell,
        EnemyInstance primaryEnemy,
        Vector2Int primaryEnemyCell,
        List<EnemyEntry> enemies,
        EnvironmentSnapshot environment)
    {
        this.playerStats = playerStats;
        this.playerCell = playerCell;
        this.primaryEnemy = primaryEnemy;
        this.primaryEnemyCell = primaryEnemyCell;
        this.enemies = enemies ?? new List<EnemyEntry>();
        this.environment = environment;
    }

    public bool IsMultiEnemy => enemies != null && enemies.Count > 1;
    public bool HasEnvironment => environment != null;
}