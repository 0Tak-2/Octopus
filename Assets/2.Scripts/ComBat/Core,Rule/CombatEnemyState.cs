using UnityEngine;

/// <summary>
/// ???? ?? ?: ?? ??????�HP?? ? ???? ?? (?? ? ???).
/// </summary>
[System.Serializable]
public class CombatEnemyState
{
    public EnemyInstance fieldInstance;
    public Vector2Int fieldOriginalCell;
    public CombatUnitToken token;
    public int hp;
    public int maxHP;
    public Vector2Int cell;
    public bool isDead;
    public EnemyDefinition definition;
}
