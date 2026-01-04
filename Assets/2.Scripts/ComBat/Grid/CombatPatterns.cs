using UnityEngine;

public static class CombatPatterns
{
    // ´ÜÀÏ
    public static readonly Vector2Int[] Single =
    {
        new Vector2Int(0, 0)
    };

    // ½ÊÀÚ°¡(+): 5Ä­ (2,4,5,6,8)
    public static readonly Vector2Int[] Plus =
    {
        new Vector2Int(0, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
    };

    // XÀÚ: 5Ä­ (1,3,5,7,9)
    public static readonly Vector2Int[] X =
    {
        new Vector2Int(0, 0),
        new Vector2Int(-1, 1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1),
    };

    public static Vector2Int[] GetOffsets(AttackPattern pattern)
    {
        switch (pattern)
        {
            case AttackPattern.CrossPlus: return Plus;
            case AttackPattern.CrossX: return X;
            default: return Single;
        }
    }
}
