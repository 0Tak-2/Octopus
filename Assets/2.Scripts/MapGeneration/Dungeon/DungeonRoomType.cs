/// <summary>
/// 던전 방 타입
/// </summary>
public enum DungeonRoomType
{
    Start,      // 시작 방 (던전 입구)
    Normal,     // 일반 몹 방
    Item,       // 아이템 방 (나중에 구현)
    Elite,      // 엘리트 방
    Boss,       // 보스 방
    Stairs,     // 계단 (다음 층으로)
    Exit        // 출구 (필드로 복귀)
}
