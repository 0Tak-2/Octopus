using System.Collections.Generic;

/// <summary>
/// 던전 한 층의 데이터
/// </summary>
public class DungeonFloorData
{
    public int floorNumber;           // 1, 2, 3
    public int width, height;         // 맵 크기
    public List<DungeonRoom> rooms;   // 방 목록
    public int[,] tiles;              // 0=벽, 1=바닥, 2=복도
    
    public DungeonRoom startRoom;     // 시작 방
    public DungeonRoom bossRoom;      // 보스 방
    public DungeonRoom stairsRoom;    // 계단 방 (다음 층으로)
    public DungeonRoom exitRoom;      // 출구 방 (필드 복귀, 1층만)
    
    public DungeonFloorData(int floorNumber, int width, int height)
    {
        this.floorNumber = floorNumber;
        this.width = width;
        this.height = height;
        this.rooms = new List<DungeonRoom>();
        this.tiles = new int[width, height];
    }
    
    /// <summary>
    /// 타일이 벽인지 확인
    /// </summary>
    public bool IsWall(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return true;
        return tiles[x, y] == 0;
    }
    
    /// <summary>
    /// 타일이 이동 가능한지 확인
    /// </summary>
    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height) return false;
        return tiles[x, y] > 0; // 1=바닥, 2=복도
    }
}
