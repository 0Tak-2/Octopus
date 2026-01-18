using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 던전 방 데이터
/// </summary>
public class DungeonRoom
{
    public int id;
    public int x, y;          // 그리드 좌표 (방 중심)
    public int width, height; // 방 크기
    public DungeonRoomType roomType;
    public List<DungeonRoom> connectedRooms = new List<DungeonRoom>();
    
    public DungeonRoom(int id, int x, int y, int width, int height)
    {
        this.id = id;
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
        this.roomType = DungeonRoomType.Normal;
    }
    
    /// <summary>
    /// 방의 중심 좌표
    /// </summary>
    public Vector2Int Center => new Vector2Int(x, y);
    
    /// <summary>
    /// 방의 경계 체크
    /// </summary>
    public bool Contains(int px, int py)
    {
        int halfW = width / 2;
        int halfH = height / 2;
        return px >= x - halfW && px < x + halfW &&
               py >= y - halfH && py < y + halfH;
    }
    
    /// <summary>
    /// 방의 바운드 (min, max)
    /// </summary>
    public void GetBounds(out int minX, out int minY, out int maxX, out int maxY)
    {
        int halfW = width / 2;
        int halfH = height / 2;
        minX = x - halfW;
        minY = y - halfH;
        maxX = x + halfW;
        maxY = y + halfH;
    }
}
