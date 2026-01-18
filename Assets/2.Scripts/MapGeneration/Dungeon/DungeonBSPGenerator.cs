using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BSP(Binary Space Partitioning) 알고리즘으로 던전 생성
/// 방 7개 이상, 5x5~7x7 크기, 1타일 복도
/// </summary>
public class DungeonBSPGenerator
{
    private const int MIN_ROOM_SIZE = 6;
    private const int MAX_ROOM_SIZE = 10;
    private const int MIN_ROOMS = 7;

    /// <summary>
    /// 던전 층 생성
    /// </summary>
    public static DungeonFloorData GenerateFloor(int floorNumber, int mapWidth, int mapHeight, int seed)
    {
        Random.InitState(seed);

        DungeonFloorData floor = new DungeonFloorData(floorNumber, mapWidth, mapHeight);

        // 1. BSP로 공간 분할 후 방 생성
        List<BSPNode> leafNodes = GenerateRooms(mapWidth, mapHeight, floor);

        // 2. 방이 7개 미만이면 재시도
        if (floor.rooms.Count < MIN_ROOMS)
        {
            Debug.LogWarning($"[DungeonBSP] 방 개수 부족 ({floor.rooms.Count}), 재생성...");
            return GenerateFloor(floorNumber, mapWidth, mapHeight, seed + 1);
        }

        // 3. 복도로 방 연결
        ConnectRooms(leafNodes, floor);

        // 4. 방 타입 지정
        AssignRoomTypes(floor, floorNumber);

        return floor;
    }

    /// <summary>
    /// BSP로 공간 분할 및 방 생성
    /// </summary>
    private static List<BSPNode> GenerateRooms(int width, int height, DungeonFloorData floor)
    {
        BSPNode root = new BSPNode(0, 0, width, height);

        // 재귀적으로 분할
        SplitNode(root, 0);

        // 리프 노드(실제 방이 들어갈 공간) 수집
        List<BSPNode> leafNodes = new List<BSPNode>();
        CollectLeafNodes(root, leafNodes);

        // 각 리프 노드에 방 생성
        int roomId = 0;
        foreach (var node in leafNodes)
        {
            CreateRoomInNode(node, floor, roomId++);
        }

        return leafNodes;
    }

    /// <summary>
    /// 노드를 재귀적으로 분할
    /// </summary>
    private static void SplitNode(BSPNode node, int depth)
    {
        // 최대 깊이 도달 or 노드가 너무 작으면 중단
        if (depth >= 4 || node.width < MIN_ROOM_SIZE * 2 + 4 || node.height < MIN_ROOM_SIZE * 2 + 4)
            return;

        // 분할 방향 결정 (너비가 크면 세로 분할, 높이가 크면 가로 분할)
        bool splitHorizontal = Random.value > 0.5f;

        if (node.width > node.height && node.width / (float)node.height >= 1.25f)
            splitHorizontal = false; // 세로 분할
        else if (node.height > node.width && node.height / (float)node.width >= 1.25f)
            splitHorizontal = true;  // 가로 분할

        if (splitHorizontal)
        {
            // 가로 분할
            int splitY = Random.Range(node.y + MIN_ROOM_SIZE + 2, node.y + node.height - MIN_ROOM_SIZE - 2);
            node.leftChild = new BSPNode(node.x, node.y, node.width, splitY - node.y);
            node.rightChild = new BSPNode(node.x, splitY, node.width, node.y + node.height - splitY);
        }
        else
        {
            // 세로 분할
            int splitX = Random.Range(node.x + MIN_ROOM_SIZE + 2, node.x + node.width - MIN_ROOM_SIZE - 2);
            node.leftChild = new BSPNode(node.x, node.y, splitX - node.x, node.height);
            node.rightChild = new BSPNode(splitX, node.y, node.x + node.width - splitX, node.height);
        }

        // 자식 노드도 재귀적으로 분할
        SplitNode(node.leftChild, depth + 1);
        SplitNode(node.rightChild, depth + 1);
    }

    /// <summary>
    /// 리프 노드(분할 안 된 노드) 수집
    /// </summary>
    private static void CollectLeafNodes(BSPNode node, List<BSPNode> leafNodes)
    {
        if (node.leftChild == null && node.rightChild == null)
        {
            leafNodes.Add(node);
            return;
        }

        if (node.leftChild != null) CollectLeafNodes(node.leftChild, leafNodes);
        if (node.rightChild != null) CollectLeafNodes(node.rightChild, leafNodes);
    }

    /// <summary>
    /// 노드 내부에 방 생성
    /// </summary>
    private static void CreateRoomInNode(BSPNode node, DungeonFloorData floor, int roomId)
    {
        // 노드보다 작은 랜덤 크기 방 생성
        int roomWidth = Random.Range(MIN_ROOM_SIZE, Mathf.Min(MAX_ROOM_SIZE, node.width - 2) + 1);
        int roomHeight = Random.Range(MIN_ROOM_SIZE, Mathf.Min(MAX_ROOM_SIZE, node.height - 2) + 1);

        // 노드 내부 랜덤 위치
        int roomX = node.x + Random.Range(1, node.width - roomWidth - 1) + roomWidth / 2;
        int roomY = node.y + Random.Range(1, node.height - roomHeight - 1) + roomHeight / 2;

        DungeonRoom room = new DungeonRoom(roomId, roomX, roomY, roomWidth, roomHeight);
        floor.rooms.Add(room);
        node.room = room;

        // 타일맵에 방 그리기
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);
        for (int x = minX; x < maxX; x++)
        {
            for (int y = minY; y < maxY; y++)
            {
                if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                    floor.tiles[x, y] = 1; // 1 = 바닥
            }
        }
    }

    /// <summary>
    /// 방들을 복도로 연결
    /// </summary>
    private static void ConnectRooms(List<BSPNode> leafNodes, DungeonFloorData floor)
    {
        for (int i = 0; i < leafNodes.Count - 1; i++)
        {
            DungeonRoom roomA = leafNodes[i].room;
            DungeonRoom roomB = leafNodes[i + 1].room;

            if (roomA == null || roomB == null) continue;

            // L자 복도 생성
            CreateLCorridor(roomA.Center, roomB.Center, floor);

            // 방 연결 정보 저장
            roomA.connectedRooms.Add(roomB);
            roomB.connectedRooms.Add(roomA);
        }

        // 추가 연결 (더 촘촘하게)
        for (int i = 0; i < leafNodes.Count; i++)
        {
            if (Random.value < 0.3f && i + 2 < leafNodes.Count) // 30% 확률로 추가 연결
            {
                DungeonRoom roomA = leafNodes[i].room;
                DungeonRoom roomB = leafNodes[i + 2].room;

                if (roomA != null && roomB != null)
                {
                    CreateLCorridor(roomA.Center, roomB.Center, floor);
                    roomA.connectedRooms.Add(roomB);
                    roomB.connectedRooms.Add(roomA);
                }
            }
        }
    }

    /// <summary>
    /// L자 복도 생성 (1타일 너비)
    /// </summary>
    private static void CreateLCorridor(Vector2Int start, Vector2Int end, DungeonFloorData floor)
    {
        int x = start.x;
        int y = start.y;

        // 가로 먼저
        while (x != end.x)
        {
            if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                floor.tiles[x, y] = 2; // 2 = 복도

            x += (end.x > x) ? 1 : -1;
        }

        // 세로
        while (y != end.y)
        {
            if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                floor.tiles[x, y] = 2; // 2 = 복도

            y += (end.y > y) ? 1 : -1;
        }
    }

    /// <summary>
    /// 방 타입 지정
    /// </summary>
    private static void AssignRoomTypes(DungeonFloorData floor, int floorNumber)
    {
        if (floor.rooms.Count == 0) return;

        // 시작 방 (첫 번째 방)
        floor.startRoom = floor.rooms[0];
        floor.startRoom.roomType = DungeonRoomType.Start;

        // 보스 방 (마지막 방)
        floor.bossRoom = floor.rooms[floor.rooms.Count - 1];
        floor.bossRoom.roomType = DungeonRoomType.Boss;

        // 계단 방 (보스 방 이전)
        if (floor.rooms.Count > 1)
        {
            floor.stairsRoom = floor.rooms[floor.rooms.Count - 2];
            floor.stairsRoom.roomType = DungeonRoomType.Stairs;
        }

        // 출구 방 (시작 방 다음, 모든 층)
        if (floor.rooms.Count > 1)
        {
            floor.exitRoom = floor.rooms[1];
            floor.exitRoom.roomType = DungeonRoomType.Exit;
        }

        // 아이템 방 (랜덤 1-2개)
        int itemRoomCount = Random.Range(1, 3);
        for (int i = 0; i < itemRoomCount && i + 2 < floor.rooms.Count - 2; i++)
        {
            DungeonRoom room = floor.rooms[2 + i];
            if (room.roomType == DungeonRoomType.Normal)
                room.roomType = DungeonRoomType.Item;
        }

        // 나머지는 일반 몹 방
    }
}

/// <summary>
/// BSP 트리 노드
/// </summary>
public class BSPNode
{
    public int x, y, width, height;
    public BSPNode leftChild, rightChild;
    public DungeonRoom room;

    public BSPNode(int x, int y, int width, int height)
    {
        this.x = x;
        this.y = y;
        this.width = width;
        this.height = height;
    }
}
