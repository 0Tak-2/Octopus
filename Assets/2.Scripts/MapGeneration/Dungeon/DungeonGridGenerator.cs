using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// ���� ��� ���� ������
/// �̷� ���� ����, ���� ����, ��Ȯ�� ���̾ƿ�
/// </summary>
public class DungeonGridGenerator
{
    /// <summary>
    /// ���� �� ����
    /// </summary>
    public static DungeonFloorData GenerateFloor(int floorNumber, int gridWidth, int gridHeight, int seed,
        int minRooms = 7, int maxRooms = 12, int minRoomSize = 5, int maxRoomSize = 8,
        int minCorridorLength = 3, int maxCorridorLength = 7, int corridorWidth = 1)
    {
        // 레이아웃 전용 난수 구간. 적 배치 쪽 난수와 분리해 서로 흔들지 않게 한다.
        using (new RngScope(seed, "dungeon.layout"))
        {
            return GenerateFloorInternal(floorNumber, gridWidth, gridHeight,
                minRooms, maxRooms, minRoomSize, maxRoomSize,
                minCorridorLength, maxCorridorLength, corridorWidth);
        }
    }

    private static DungeonFloorData GenerateFloorInternal(int floorNumber, int gridWidth, int gridHeight,
        int minRooms, int maxRooms, int minRoomSize, int maxRoomSize,
        int minCorridorLength, int maxCorridorLength, int corridorWidth)
    {
        // ��ü �� ũ�� ��� (�ִ� �� ũ�� + �ִ� ���� ���� ����)
        int totalWidth = gridWidth * (maxRoomSize + maxCorridorLength) + maxCorridorLength;
        int totalHeight = gridHeight * (maxRoomSize + maxCorridorLength) + maxCorridorLength;

        DungeonFloorData floor = new DungeonFloorData(floorNumber, totalWidth, totalHeight);

        // 1. ���ڿ� �� ��ġ (���� ����)
        bool[,] grid = GenerateConnectedGrid(gridWidth, gridHeight, minRooms, maxRooms);

        // 2. �� ���� (ũ�� �پ�, ���� ���� ����)
        List<GridCell> cells = CreateRoomsWithRandomSpacing(grid, floor, minRoomSize, maxRoomSize,
            minCorridorLength, maxCorridorLength);

        // 3. ������ ����
        ConnectRooms(cells, floor, corridorWidth);

        // 4. �� Ÿ�� ���� (���� �� �߾�)
        AssignRoomTypes(floor, floorNumber, cells, gridWidth, gridHeight);

        Debug.Log($"[GridDungeon] {floorNumber}�� ����: {floor.rooms.Count}�� ��");

        return floor;
    }

    /// <summary>
    /// ����� ���� ���� (��� ���� ����ǵ��� ����)
    /// </summary>
    private static bool[,] GenerateConnectedGrid(int width, int height, int minRooms, int maxRooms)
    {
        bool[,] grid = new bool[width, height];

        // ������ (�߾�)
        int centerX = width / 2;
        int centerY = height / 2;
        grid[centerX, centerY] = true;

        List<Vector2Int> rooms = new List<Vector2Int> { new Vector2Int(centerX, centerY) };
        List<Vector2Int> frontier = new List<Vector2Int>();

        // �߾ӿ��� ������ ĭ���� frontier�� �߰�
        AddFrontier(centerX, centerY, width, height, grid, frontier);

        // ��ǥ �� ����
        int targetRooms = Random.Range(minRooms, maxRooms + 1);

        // Prim's Algorithm���� ����� �̷� ����
        while (frontier.Count > 0 && rooms.Count < targetRooms)
        {
            // ���� frontier ����
            int idx = Random.Range(0, frontier.Count);
            Vector2Int pos = frontier[idx];
            frontier.RemoveAt(idx);

            // �̹� ���̸� ��ŵ
            if (grid[pos.x, pos.y]) continue;

            // ������ ���� �ִ��� Ȯ��
            if (HasAdjacentRoom(pos.x, pos.y, grid))
            {
                grid[pos.x, pos.y] = true;
                rooms.Add(pos);
                AddFrontier(pos.x, pos.y, width, height, grid, frontier);
            }
        }

        // �ּ� �� ���� ����
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
    /// Frontier�� ���� ĭ �߰�
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
    /// ������ ���� �ִ��� Ȯ��
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
    /// ���� ������� �� ���� (���� ����)
    /// </summary>
    private static List<GridCell> CreateRoomsWithRandomSpacing(bool[,] grid, DungeonFloorData floor,
        int minRoomSize, int maxRoomSize, int minCorridorLength, int maxCorridorLength)
    {
        List<GridCell> cells = new List<GridCell>();
        int roomId = 0;

        int gridWidth = grid.GetLength(0);
        int gridHeight = grid.GetLength(1);

        // �� ���� ��ġ�� ���� ������ ���
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

        // �� ����
        for (int gx = 0; gx < gridWidth; gx++)
        {
            for (int gy = 0; gy < gridHeight; gy++)
            {
                if (!grid[gx, gy]) continue;

                // ���� �� ũ��
                int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
                int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);

                // ���� ���� ��ġ (���� ������ ���)
                int startX = xOffsets[gx];
                int startY = yOffsets[gy];

                int centerX = startX + roomWidth / 2;
                int centerY = startY + roomHeight / 2;

                // �� ����
                DungeonRoom room = new DungeonRoom(roomId++, centerX, centerY, roomWidth, roomHeight);
                floor.rooms.Add(room);

                // ���� �� ���� ����
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

                // Ÿ�ϸʿ� �� �׸���
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    for (int y = startY; y < startY + roomHeight; y++)
                    {
                        if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                            floor.tiles[x, y] = 1; // �ٴ�
                    }
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// ���� ������� �� ���� (���� ����) - ��� �� ��
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

                // ���� �� ũ��
                int roomWidth = Random.Range(minRoomSize, maxRoomSize + 1);
                int roomHeight = Random.Range(minRoomSize, maxRoomSize + 1);

                // ���� ���� ��ġ ���
                int startX = corridorSpacing + gx * (maxRoomSize + corridorSpacing);
                int startY = corridorSpacing + gy * (maxRoomSize + corridorSpacing);

                int centerX = startX + roomWidth / 2;
                int centerY = startY + roomHeight / 2;

                // �� ����
                DungeonRoom room = new DungeonRoom(roomId++, centerX, centerY, roomWidth, roomHeight);
                floor.rooms.Add(room);

                // ���� �� ���� ����
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

                // Ÿ�ϸʿ� �� �׸���
                for (int x = startX; x < startX + roomWidth; x++)
                {
                    for (int y = startY; y < startY + roomHeight; y++)
                    {
                        if (x >= 0 && x < floor.width && y >= 0 && y < floor.height)
                            floor.tiles[x, y] = 1; // �ٴ�
                    }
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// ������ ����� �ڿ������� ������ ����
    /// </summary>
    private static void ConnectRooms(List<GridCell> cells, DungeonFloorData floor, int corridorWidth)
    {
        // ���� �� ���� (���� �˻���)
        Dictionary<Vector2Int, GridCell> cellMap = new Dictionary<Vector2Int, GridCell>();
        foreach (var cell in cells)
        {
            cellMap[new Vector2Int(cell.gridX, cell.gridY)] = cell;
        }

        // �� �濡�� ������ ������ ���� ����
        foreach (var cell in cells)
        {
            // ������ ��
            Vector2Int rightPos = new Vector2Int(cell.gridX + 1, cell.gridY);
            if (cellMap.ContainsKey(rightPos))
            {
                CreateNaturalHorizontalCorridor(cell, cellMap[rightPos], floor, corridorWidth);
                cell.room.connectedRooms.Add(cellMap[rightPos].room);
                cellMap[rightPos].room.connectedRooms.Add(cell.room);
            }

            // �Ʒ��� ��
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
    /// �ڿ������� ���� ���� ���� (���� ��ġ + ����)
    /// </summary>
    private static void CreateNaturalHorizontalCorridor(GridCell from, GridCell to, DungeonFloorData floor, int corridorWidth)
    {
        // ������ ���� ���� ��� (���� ���� ����)
        int safeMargin = Mathf.Max(1, corridorWidth);

        // ��� ���� ���� �ⱸ (������ �� ����)
        int fromX = from.startX + from.roomWidth - 1; // �� ������ Ÿ��
        int fromY = from.startY + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, from.roomHeight - safeMargin));

        // ���� ���� ���� �Ա� (���� ��)
        int toX = to.startX; // �� ù Ÿ��
        int toY = to.startY + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, to.roomHeight - safeMargin));

        // 70% Ȯ���� ����, 30%�� ����
        bool addBend = Random.value < 0.7f;

        if (addBend && (toX - fromX) > 4)
        {
            // L�� ���� (����)
            int bendX = fromX + Random.Range(2, toX - fromX - 1);

            // 1. ���μ� (��� �� ������)
            DrawHorizontalLine(floor, fromX, bendX, fromY, corridorWidth);

            // 2. ���μ� (������ �� ���� ����)
            DrawVerticalLine(floor, bendX, fromY, toY, corridorWidth);

            // 3. ���μ� (������ �� ����)
            DrawHorizontalLine(floor, bendX, toX, toY, corridorWidth);
        }
        else
        {
            // ���� ����
            int midY = (fromY + toY) / 2;
            DrawHorizontalLine(floor, fromX, toX, midY, corridorWidth);
        }
    }

    /// <summary>
    /// �ڿ������� ���� ���� ���� (���� ��ġ + ����)
    /// </summary>
    private static void CreateNaturalVerticalCorridor(GridCell from, GridCell to, DungeonFloorData floor, int corridorWidth)
    {
        // ������ ���� ���� ��� (���� ���� ����)
        int safeMargin = Mathf.Max(1, corridorWidth);

        // ��� ���� ���� �ⱸ (�Ʒ��� �� ����)
        int fromX = from.startX + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, from.roomWidth - safeMargin));
        int fromY = from.startY + from.roomHeight - 1; // �� ������ Ÿ��

        // ���� ���� ���� �Ա� (���� ��)
        int toX = to.startX + Random.Range(safeMargin, Mathf.Max(safeMargin + 1, to.roomWidth - safeMargin));
        int toY = to.startY; // �� ù Ÿ��

        // 70% Ȯ���� ����, 30%�� ����
        bool addBend = Random.value < 0.7f;

        if (addBend && (toY - fromY) > 4)
        {
            // L�� ���� (����)
            int bendY = fromY + Random.Range(2, toY - fromY - 1);

            // 1. ���μ� (��� �� ������)
            DrawVerticalLine(floor, fromX, fromY, bendY, corridorWidth);

            // 2. ���μ� (������ �� ���� ��ġ)
            DrawHorizontalLine(floor, fromX, toX, bendY, corridorWidth);

            // 3. ���μ� (������ �� ����)
            DrawVerticalLine(floor, toX, bendY, toY, corridorWidth);
        }
        else
        {
            // ���� ����
            int midX = (fromX + toX) / 2;
            DrawVerticalLine(floor, midX, fromY, toY, corridorWidth);
        }
    }

    /// <summary>
    /// ���μ� �׸���
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
                    floor.tiles[x, drawY] = 2; // ����
            }
        }
    }

    /// <summary>
    /// ���μ� �׸���
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
                    floor.tiles[drawX, y] = 2; // ����
            }
        }
    }

    /// <summary>
    /// �� Ÿ�� ���� (���� ���� �߾�)
    /// </summary>
    private static void AssignRoomTypes(DungeonFloorData floor, int floorNumber, List<GridCell> cells, int gridWidth, int gridHeight)
    {
        if (floor.rooms.Count == 0) return;

        // ���� �� (�߾ӿ� ���� ����� ��)
        int centerX = gridWidth / 2;
        int centerY = gridHeight / 2;

        GridCell centerCell = cells.OrderBy(c =>
            Mathf.Abs(c.gridX - centerX) + Mathf.Abs(c.gridY - centerY)
        ).First();

        floor.startRoom = centerCell.room;
        floor.startRoom.roomType = DungeonRoomType.Start;

        // ���� �� (���� �濡�� ���� �� ��)
        GridCell bossCell = cells.OrderByDescending(c =>
            Mathf.Abs(c.gridX - centerCell.gridX) + Mathf.Abs(c.gridY - centerCell.gridY)
        ).First();

        floor.bossRoom = bossCell.room;
        floor.bossRoom.roomType = DungeonRoomType.Boss;

        // ��� �� (���� �� ��ó)
        if (floor.rooms.Count > 2)
        {
            GridCell stairsCell = cells
                .Where(c => c.room != floor.startRoom && c.room != floor.bossRoom)
                .OrderBy(c => Mathf.Abs(c.gridX - bossCell.gridX) + Mathf.Abs(c.gridY - bossCell.gridY))
                .First();

            floor.stairsRoom = stairsCell.room;
            floor.stairsRoom.roomType = DungeonRoomType.Stairs;
        }

        // �ⱸ �� (���� �� ��ó)
        if (floor.rooms.Count > 1)
        {
            GridCell exitCell = cells
                .Where(c => c.room != floor.startRoom && c.room != floor.bossRoom && c.room != floor.stairsRoom)
                .OrderBy(c => Mathf.Abs(c.gridX - centerCell.gridX) + Mathf.Abs(c.gridY - centerCell.gridY))
                .First();

            floor.exitRoom = exitCell.room;
            floor.exitRoom.roomType = DungeonRoomType.Exit;
        }

        // ����Ʈ �� (���� 1-2��)
        int eliteCount = Mathf.Min(2, (floor.rooms.Count - 4) / 2); // �� ������ ���� ����
        var eliteRooms = floor.rooms
            .Where(r => r.roomType == DungeonRoomType.Normal)
            .OrderBy(x => Random.value)
            .Take(eliteCount);

        foreach (var room in eliteRooms)
        {
            room.roomType = DungeonRoomType.Elite;
        }

        // ������ �� (���� 1-2��)
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
/// ���� �� ����
/// </summary>
public class GridCell
{
    public int gridX, gridY;           // ���� ��ǥ
    public int startX, startY;         // ���� Ÿ�� ���� ��ġ
    public int roomWidth, roomHeight;  // ���� �� ũ��
    public DungeonRoom room;           // �� ���� ��
}