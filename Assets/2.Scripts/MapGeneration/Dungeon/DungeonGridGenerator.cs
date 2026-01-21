using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// 격자 기반 던전 생성기
/// 미로 같은 구조, 직선 복도, 명확한 레이아웃
/// </summary>
public class DungeonGridGenerator
{
    /// <summary>
    /// 던전 층 생성
    /// </summary>
    public static DungeonFloorData GenerateFloor(int floorNumber, int gridWidth, int gridHeight, int seed,
        int minRooms = 7, int maxRooms = 12, int minRoomSize = 5, int maxRoomSize = 8,
        int minCorridorLength = 3, int maxCorridorLength = 7, int corridorWidth = 1)
    {
        Random.InitState(seed);

        // 전체 맵 크기 계산 (최대 방 크기 + 최대 복도 길이 기준)
        int totalWidth = gridWidth * (maxRoomSize + maxCorridorLength) + maxCorridorLength;
        int totalHeight = gridHeight * (maxRoomSize + maxCorridorLength) + maxCorridorLength;

        DungeonFloorData floor = new DungeonFloorData(floorNumber, totalWidth, totalHeight);

        // 1. 격자에 방 배치 (연결 보장)
        bool[,] grid = GenerateConnectedGrid(gridWidth, gridHeight, minRooms, maxRooms);

        // 2. 방 생성 (크기 다양, 랜덤 복도 길이)
        List<GridCell> cells = CreateRoomsWithRandomSpacing(grid, floor, minRoomSize, maxRoomSize,
            minCorridorLength, maxCorridorLength);

        // 3. 복도로 연결
        ConnectRooms(cells, floor, corridorWidth);

        // 4. 방 타입 지정 (시작 방 중앙)
        AssignRoomTypes(floor, floorNumber, cells, gridWidth, gridHeight);

        Debug.Log($"[GridDungeon] {floorNumber}층 생성: {floor.rooms.Count}개 방");

        return floor;
    }

    /// <summary>
    /// 연결된 격자 생성 (모든 방이 연결되도록 보장)
    /// </summary>
    private static bool[,] GenerateConnectedGrid(int width, int height, int minRooms, int maxRooms)
    {
        bool[,] grid = new bool[width, height];

        // 시작점 (중앙)
        int centerX = width / 2;
        int centerY = height / 2;
        grid[centerX, centerY] = true;

        List<Vector2Int> rooms = new List<Vector2Int> { new Vector2Int(centerX, centerY) };
        List<Vector2Int> frontier = new List<Vector2Int>();

        // 중앙에서 인접한 칸들을 frontier에 추가
        AddFrontier(centerX, centerY, width, height, grid, frontier);

        // 목표 방 개수
        int targetRooms = Random.Range(minRooms, maxRooms + 1);

        // Prim's Algorithm으로 연결된 미로 생성
        while (frontier.Count > 0 && rooms.Count < targetRooms)
        {
            // 랜덤 frontier 선택
            int idx = Random.Range(0, frontier.Count);
            Vector2Int pos = frontier[idx];
            frontier.RemoveAt(idx);

            // 이미 방이면 스킵
            if (grid[pos.x, pos.y]) continue;

            // 인접한 방이 있는지 확인
            if (HasAdjacentRoom(pos.x, pos.y, grid))
            {
                grid[pos.x, pos.y] = true;
                rooms.Add(pos);
                AddFrontier(pos.x, pos.y, width, height, grid, frontier);
            }
        }

        // 최소 방 개수 보장
        while (rooms.Count < minRooms)
        {
            int x = Random.Range(0, width);
            int y = Random.Range(0, height);
            if (!grid[x, y])
            {
                grid[x, y] = true;
                rooms.Add(new Vector2Int(x, y));
            }
        }

        return grid;
    }

    /// <summary>
    /// Frontier에 인접 칸 추가
    /// </summary>
    private static void AddFrontier(int x, int y, int width, int height, bool[,] grid, List<Vector2Int> frontier)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        foreach (var dir in dirs)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            if (nx >= 0 && nx < width && ny >= 0 && ny < height && !grid[nx, ny])
            {
                Vector2Int pos = new Vector2Int(nx, ny);
                if (!frontier.Contains(pos))
                    frontier.Add(pos);
            }
        }
    }

    /// <summary>
    /// 인접한 방이 있는지 확인
    /// </summary>
    private static bool HasAdjacentRoom(int x, int y, bool[,] grid)
    {
        Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);

        foreach (var dir in dirs)
        {
            int nx = x + dir.x;
            int ny = y + dir.y;

            if (nx >= 0 && nx < width && ny >= 0 && ny < height && grid[nx, ny])
                return true;
        }

        return false;
    }

    /// <summary>
    /// 격자 기반으로 방 생성 (랜덤 간격)
    /// </summary>
    private static List<GridCell> CreateRoomsWithRandomSpacing(bool[,] grid, DungeonFloorData floor,
        int minRoomSize, int maxRoomSize, int minCorridorLength, int maxCorridorLength)
    {
        List<GridCell> cells = new List<GridCell>();
        int roomId = 0;

        int gridWidth = grid.GetLength(0);
        int gridHeight = grid.GetLength(1);

        // 각 격자 위치의 누적 오프셋 계산
        int[] xOffsets = new int[gridWidth + 1];
        int[] yOffsets = new int[gridHeight + 1];

        xOffsets[0] = minCorridorLength;
        yOffsets[0] = minCorridorLength;

        for (int i = 1; i <= gridWidth; i++)
        {
            int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
            int corridorLen = Random.Range(minCorridorLength, maxCorridorLength + 1);
            xOffsets[i] = xOffsets[i - 1] + roomWidth + corridorLen;
        }

        for (int i = 1; i <= gridHeight; i++)
        {
            int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);
            int corridorLen = Random.Range(minCorridorLength, maxCorridorLength + 1);
            yOffsets[i] = yOffsets[i - 1] + roomHeight + corridorLen;
        }

        // 방 생성
        for (int gx = 0; gx < gridWidth; gx++)
        {
            for (int gy = 0; gy < gridHeight; gy++)
            {
                if (!grid[gx, gy]) continue;

                // 랜덤 방 크기
                int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
                int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);

                // 방의 실제 위치 (누적 오프셋 기반)
                int startX = xOffsets[gx];
                int startY = yOffsets[gy];

                int centerX = startX + roomWidth / 2;
                int centerY = startY + roomHeight / 2;

                // 방 생성
                DungeonRoom room = new DungeonRoom(roomId++, centerX, centerY, roomWidth, roomHeight);
                floor.rooms.Add(room);

                // 격자 셀 정보 저장
                GridCell cell = new GridCell
                {
                    gridX = gx,
                    gridY = gy,
                    room = room,
                    startX = startX,
                    startY = startY,
                    roomWidth = roomWidth,
                    roomHeight = roomHeight
                };
                cells.Add(cell);

                // 타일맵에 방 그리기
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    for (int y = startY; y < startY + roomHeight; y++)
                    {
                        if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                            floor.tiles[x, y] = 1; // 바닥
                    }
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// 격자 기반으로 방 생성 (고정 간격) - 사용 안 함
    /// </summary>
    private static List<GridCell> CreateRooms(bool[,] grid, DungeonFloorData floor, int minRoomSize, int maxRoomSize, int corridorSpacing)
    {
        List<GridCell> cells = new List<GridCell>();
        int roomId = 0;

        int gridWidth = grid.GetLength(0);
        int gridHeight = grid.GetLength(1);

        for (int gx = 0; gx < gridWidth; gx++)
        {
            for (int gy = 0; gy < gridHeight; gy++)
            {
                if (!grid[gx, gy]) continue;

                // 랜덤 방 크기
                int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
                int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);

                // 방의 실제 위치 계산
                int startX = corridorSpacing + gx * (maxRoomSize + corridorSpacing);
                int startY = corridorSpacing + gy * (maxRoomSize + corridorSpacing);

                int centerX = startX + roomWidth / 2;
                int centerY = startY + roomHeight / 2;

                // 방 생성
                DungeonRoom room = new DungeonRoom(roomId++, centerX, centerY, roomWidth, roomHeight);
                floor.rooms.Add(room);

                // 격자 셀 정보 저장
                GridCell cell = new GridCell
                {
                    gridX = gx,
                    gridY = gy,
                    room = room,
                    startX = startX,
                    startY = startY,
                    roomWidth = roomWidth,
                    roomHeight = roomHeight
                };
                cells.Add(cell);

                // 타일맵에 방 그리기
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    for (int y = startY; y < startY + roomHeight; y++)
                    {
                        if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                            floor.tiles[x, y] = 1; // 바닥
                    }
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// 인접한 방들을 자연스러운 복도로 연결
    /// </summary>
    private static void ConnectRooms(List<GridCell> cells, DungeonFloorData floor, int corridorWidth)
    {
        // 격자 맵 생성 (빠른 검색용)
        Dictionary<Vector2Int, GridCell> cellMap = new Dictionary<Vector2Int, GridCell>();
        foreach (var cell in cells)
        {
            cellMap[new Vector2Int(cell.gridX, cell.gridY)] = cell;
        }

        // 각 방에서 인접한 방으로 복도 연결
        foreach (var cell in cells)
        {
            // 오른쪽 방
            Vector2Int rightPos = new Vector2Int(cell.gridX + 1, cell.gridY);
            if (cellMap.ContainsKey(rightPos))
            {
                CreateNaturalHorizontalCorridor(cell, cellMap[rightPos], floor, corridorWidth);
                cell.room.connectedRooms.Add(cellMap[rightPos].room);
                cellMap[rightPos].room.connectedRooms.Add(cell.room);
            }

            // 아래쪽 방
            Vector2Int downPos = new Vector2Int(cell.gridX, cell.gridY + 1);
            if (cellMap.ContainsKey(downPos))
            {
                CreateNaturalVerticalCorridor(cell, cellMap[downPos], floor, corridorWidth);
                cell.room.connectedRooms.Add(cellMap[downPos].room);
                cellMap[downPos].room.connectedRooms.Add(cell.room);
            }
        }
    }

    /// <summary>
    /// 자연스러운 가로 복도 생성 (랜덤 위치 + 굴곡)
    /// </summary>
    private static void CreateNaturalHorizontalCorridor(GridCell from, GridCell to, DungeonFloorData floor, int corridorWidth)
    {
        // 안전한 연결 범위 계산 (복도 넓이 고려)
        int safeMargin = Mathf.Max(1, corridorWidth);

        // 출발 방의 랜덤 출구 (오른쪽 벽 안쪽)
        int fromX = from.startX + from.roomWidth - 1; // 방 마지막 타일
        int fromY = from.startY + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, from.roomHeight - safeMargin));

        // 도착 방의 랜덤 입구 (왼쪽 벽)
        int toX = to.startX; // 방 첫 타일
        int toY = to.startY + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, to.roomHeight - safeMargin));

        // 70% 확률로 꺾임, 30%는 직선
        bool addBend = Random.value < 0.7f;

        if (addBend && (toX - fromX) > 4)
        {
            // L자 복도 (굴곡)
            int bendX = fromX + Random.Range(2, toX - fromX - 1);

            // 1. 가로선 (출발 → 꺾는점)
            DrawHorizontalLine(floor, fromX, bendX, fromY, corridorWidth);

            // 2. 세로선 (꺾는점 → 도착 높이)
            DrawVerticalLine(floor, bendX, fromY, toY, corridorWidth);

            // 3. 가로선 (꺾는점 → 도착)
            DrawHorizontalLine(floor, bendX, toX, toY, corridorWidth);
        }
        else
        {
            // 직선 복도
            int midY = (fromY + toY) / 2;
            DrawHorizontalLine(floor, fromX, toX, midY, corridorWidth);
        }
    }

    /// <summary>
    /// 자연스러운 세로 복도 생성 (랜덤 위치 + 굴곡)
    /// </summary>
    private static void CreateNaturalVerticalCorridor(GridCell from, GridCell to, DungeonFloorData floor, int corridorWidth)
    {
        // 안전한 연결 범위 계산 (복도 넓이 고려)
        int safeMargin = Mathf.Max(1, corridorWidth);

        // 출발 방의 랜덤 출구 (아래쪽 벽 안쪽)
        int fromX = from.startX + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, from.roomWidth - safeMargin));
        int fromY = from.startY + from.roomHeight - 1; // 방 마지막 타일

        // 도착 방의 랜덤 입구 (위쪽 벽)
        int toX = to.startX + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, to.roomWidth - safeMargin));
        int toY = to.startY; // 방 첫 타일

        // 70% 확률로 꺾임, 30%는 직선
        bool addBend = Random.value < 0.7f;

        if (addBend && (toY - fromY) > 4)
        {
            // L자 복도 (굴곡)
            int bendY = fromY + Random.Range(2, toY - fromY - 1);

            // 1. 세로선 (출발 → 꺾는점)
            DrawVerticalLine(floor, fromX, fromY, bendY, corridorWidth);

            // 2. 가로선 (꺾는점 → 도착 위치)
            DrawHorizontalLine(floor, fromX, toX, bendY, corridorWidth);

            // 3. 세로선 (꺾는점 → 도착)
            DrawVerticalLine(floor, toX, bendY, toY, corridorWidth);
        }
        else
        {
            // 직선 복도
            int midX = (fromX + toX) / 2;
            DrawVerticalLine(floor, midX, fromY, toY, corridorWidth);
        }
    }

    /// <summary>
    /// 가로선 그리기
    /// </summary>
    private static void DrawHorizontalLine(DungeonFloorData floor, int startX, int endX, int y, int width)
    {
        int minX = Mathf.Min(startX, endX);
        int maxX = Mathf.Max(startX, endX);

        for (int x = minX; x <= maxX; x++)
        {
            for (int dy = -width / 2; dy <= width / 2; dy++)
            {
                int drawY = y + dy;
                if (x >= 0 && x < floor.width && drawY >= 0 && drawY < floor.height)
                    floor.tiles[x, drawY] = 2; // 복도
            }
        }
    }

    /// <summary>
    /// 세로선 그리기
    /// </summary>
    private static void DrawVerticalLine(DungeonFloorData floor, int x, int startY, int endY, int width)
    {
        int minY = Mathf.Min(startY, endY);
        int maxY = Mathf.Max(startY, endY);

        for (int y = minY; y <= maxY; y++)
        {
            for (int dx = -width / 2; dx <= width / 2; dx++)
            {
                int drawX = x + dx;
                if (drawX >= 0 && drawX < floor.width && y >= 0 && y < floor.height)
                    floor.tiles[drawX, y] = 2; // 복도
            }
        }
    }

    /// <summary>
    /// 방 타입 지정 (시작 방은 중앙)
    /// </summary>
    private static void AssignRoomTypes(DungeonFloorData floor, int floorNumber, List<GridCell> cells, int gridWidth, int gridHeight)
    {
        if (floor.rooms.Count == 0) return;

        // 시작 방 (중앙에 가장 가까운 방)
        int centerX = gridWidth / 2;
        int centerY = gridHeight / 2;

        GridCell centerCell = cells.OrderBy(c =>
            Mathf.Abs(c.gridX - centerX) + Mathf.Abs(c.gridY - centerY)
        ).First();

        floor.startRoom = centerCell.room;
        floor.startRoom.roomType = DungeonRoomType.Start;

        // 보스 방 (시작 방에서 가장 먼 방)
        GridCell bossCell = cells.OrderByDescending(c =>
            Mathf.Abs(c.gridX - centerCell.gridX) + Mathf.Abs(c.gridY - centerCell.gridY)
        ).First();

        floor.bossRoom = bossCell.room;
        floor.bossRoom.roomType = DungeonRoomType.Boss;

        // 계단 방 (보스 방 근처)
        if (floor.rooms.Count > 2)
        {
            GridCell stairsCell = cells
                .Where(c => c.room != floor.startRoom && c.room != floor.bossRoom)
                .OrderBy(c => Mathf.Abs(c.gridX - bossCell.gridX) + Mathf.Abs(c.gridY - bossCell.gridY))
                .First();

            floor.stairsRoom = stairsCell.room;
            floor.stairsRoom.roomType = DungeonRoomType.Stairs;
        }

        // 출구 방 (시작 방 근처)
        if (floor.rooms.Count > 1)
        {
            GridCell exitCell = cells
                .Where(c => c.room != floor.startRoom && c.room != floor.bossRoom && c.room != floor.stairsRoom)
                .OrderBy(c => Mathf.Abs(c.gridX - centerCell.gridX) + Mathf.Abs(c.gridY - centerCell.gridY))
                .First();

            floor.exitRoom = exitCell.room;
            floor.exitRoom.roomType = DungeonRoomType.Exit;
        }

        // 엘리트 방 (랜덤 1-2개)
        int eliteCount = Mathf.Min(2, (floor.rooms.Count - 4) / 2); // 방 개수의 절반 정도
        var eliteRooms = floor.rooms
            .Where(r => r.roomType == DungeonRoomType.Normal)
            .OrderBy(x => Random.value)
            .Take(eliteCount);

        foreach (var room in eliteRooms)
        {
            room.roomType = DungeonRoomType.Elite;
        }

        // 아이템 방 (랜덤 1-2개)
        int itemCount = Mathf.Min(2, floor.rooms.Count - 4 - eliteCount);
        var availableRooms = floor.rooms
            .Where(r => r.roomType == DungeonRoomType.Normal)
            .OrderBy(x => Random.value)
            .Take(itemCount);

        foreach (var room in availableRooms)
        {
            room.roomType = DungeonRoomType.Item;
        }
    }
}

/// <summary>
/// 격자 셀 정보
/// </summary>
public class GridCell
{
    public int gridX, gridY;           // 격자 좌표
    public int startX, startY;         // 실제 타일 시작 위치
    public int roomWidth, roomHeight;  // 실제 방 크기
    public DungeonRoom room;           // 이 셀의 방
}