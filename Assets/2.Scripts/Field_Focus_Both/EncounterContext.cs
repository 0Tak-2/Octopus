using UnityEngine;

[System.Serializable]
public class EncounterContext
{
    public PlayerStats playerStats;
    public EnemyInstance enemy;

    public Vector2Int playerCell;
    public Vector2Int enemyCell;

    public EncounterContext(PlayerStats playerStats, EnemyInstance enemy, Vector2Int playerCell, Vector2Int enemyCell)
    {
        this.playerStats = playerStats;
        this.enemy = enemy;
        this.playerCell = playerCell;
        this.enemyCell = enemyCell;
    }
}
