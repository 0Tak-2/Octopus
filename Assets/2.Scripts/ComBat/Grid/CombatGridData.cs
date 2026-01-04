using UnityEngine;

public class CombatGridData
{
    public readonly int width;
    public readonly int height;

    private readonly CombatTileType[,] _tiles;

    public CombatGridData(int w, int h)
    {
        width = w;
        height = h;
        _tiles = new CombatTileType[w, h];
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public CombatTileType Get(int x, int y)
    {
        if (!InBounds(x, y)) return CombatTileType.Wall;
        return _tiles[x, y];
    }

    public void Set(int x, int y, CombatTileType t)
    {
        if (!InBounds(x, y)) return;
        _tiles[x, y] = t;
    }

    public bool IsEmpty(int x, int y) => Get(x, y) == CombatTileType.Empty;
    public bool IsWall(int x, int y) => Get(x, y) == CombatTileType.Wall;
}
