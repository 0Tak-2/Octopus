using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 맵 타일 종류
/// </summary>
public enum TileType
{
    Empty = 0,      // 이동 가능한 바닥
    Wall = 1,       // 벽 (이동 불가)
    Road = 2,       // 길 (맵 연결용)
    Water = 3,      // 물
    Tree = 4,       // 나무 (장애물)
}

/// <summary>
/// 맵 데이터 (생성된 맵 정보를 담음)
/// </summary>
[System.Serializable]
public class MapData
{
    public int width;
    public int height;
    public TileType[,] tiles;
    
    [Header("Spawn Points")]
    public Vector2Int playerSpawnPos;
    public List<Vector2Int> dungeonEntrances = new List<Vector2Int>();
    public List<Vector2Int> enemySpawns = new List<Vector2Int>();
    
    [Header("Connections")]
    public Vector2Int exitToNextMap;    // 다음 맵으로 가는 출구
    public Vector2Int entranceFromPrevMap; // 이전 맵에서 오는 입구
    
    public MapData(int w, int h)
    {
        width = w;
        height = h;
        tiles = new TileType[w, h];
    }
    
    /// <summary>
    /// 범위 체크
    /// </summary>
    public bool InBounds(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    
    /// <summary>
    /// 타일 가져오기
    /// </summary>
    public TileType GetTile(int x, int y)
    {
        if (!InBounds(x, y)) return TileType.Wall;
        return tiles[x, y];
    }
    
    /// <summary>
    /// 타일 설정하기
    /// </summary>
    public void SetTile(int x, int y, TileType type)
    {
        if (InBounds(x, y))
            tiles[x, y] = type;
    }
    
    /// <summary>
    /// 해당 위치가 이동 가능한가?
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        TileType t = tiles[x, y];
        return t == TileType.Empty || t == TileType.Road;
    }
}
