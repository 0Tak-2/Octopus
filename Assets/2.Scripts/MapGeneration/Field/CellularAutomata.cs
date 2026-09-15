using UnityEngine;

/// <summary>
/// Cellular Automata 맵 생성 알고리즘
/// 동굴/숲 같은 유기적인 맵 생성
/// </summary>
public static class CellularAutomata
{
    /// <summary>
    /// CA로 맵 생성 (FieldDefinition 기반)
    /// </summary>
    public static MapData Generate(FieldDefinition fieldDef, int seed)
    {
        if (seed == 0) seed = Random.Range(1, 999999);

        // 운 나쁜 시드면 살아남는 영역이 너무 작아 자원·적이 들어갈 자리가 없다.
        // 시드를 변형해가며 몇 번 다시 만든다. 시드에서 파생되므로 재현성은 유지된다.
        const int MaxAttempts = 8;
        MapData best = null;
        int bestSize = -1;

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // 지형 전용 난수 구간. 여기서 난수를 몇 번 쓰든 이후 적·자원 배치에 영향을 주지 않는다.
            using (new RngScope(seed, "field.terrain." + attempt))
            {
                MapData map = new MapData(fieldDef.width, fieldDef.height);

                InitializeRandom(map, fieldDef.wallProbability);

                for (int i = 0; i < fieldDef.caIterations; i++)
                {
                    map = ApplyCellularAutomata(map, 5);
                }

                // 외곽 테두리 두껍게 (톱니 방지)
                CreateThickBorder(map, fieldDef.borderThickness);

                // 내부 벽 돌기/구멍 스무딩
                map = SmoothWalls(map, fieldDef.smoothPasses);

                // 스무딩 후 외곽 다시 보장
                CreateThickBorder(map, fieldDef.borderThickness);

                FloodFillCleanup(map);

                if (IsUsableMap(map))
                    return map;

                // 전부 실패하면 그중 가장 나은 것이라도 쓴다.
                if (_lastLargestRegionSize > bestSize)
                {
                    bestSize = _lastLargestRegionSize;
                    best = map;
                }
            }
        }

        Debug.LogWarning($"[CellularAutomata] {MaxAttempts}회 시도했지만 충분히 넓은 맵을 못 만들었습니다. " +
                         $"가장 넓은 영역 {bestSize}칸을 사용합니다. (wallProbability/caIterations 조정 필요)");
        return best;
    }

    /// <summary>
    /// CA로 맵 생성 (FieldMapConfig 기반, 기존 호환)
    /// </summary>
    public static MapData Generate(FieldMapConfig config, int seed)
    {
        // 시드 설정
        if (seed == 0) seed = Random.Range(1, 999999);

        using (new RngScope(seed, "field.terrain"))
        {
            MapData map = new MapData(config.width, config.height);

            // 1단계: 랜덤 초기화
            InitializeRandom(map, config.initialWallProbability);

            // 2단계: CA 반복 적용
            for (int i = 0; i < config.iterations; i++)
            {
                map = ApplyCellularAutomata(map, config.wallThreshold);
            }

            // 3단계: 두꺼운 외곽 벽 + 스무딩
            CreateThickBorder(map, thickness: 2);
            map = SmoothWalls(map, passes: 2);
            CreateThickBorder(map, thickness: 2);

            // 4단계: 고립된 영역 제거 (접근 가능한 영역만 남김)
            FloodFillCleanup(map);

            return map;
        }
    }

    /// <summary>
    /// 1단계: 랜덤 노이즈로 초기화
    /// </summary>
    private static void InitializeRandom(MapData map, float wallProb)
    {
        for (int x = 0; x < map.width; x++)
        {
            for (int y = 0; y < map.height; y++)
            {
                // 외곽은 무조건 벽
                if (x == 0 || x == map.width - 1 || y == 0 || y == map.height - 1)
                {
                    map.SetTile(x, y, TileType.Wall);
                }
                else
                {
                    // 내부는 확률적으로 벽/바닥
                    bool isWall = Random.value < wallProb;
                    map.SetTile(x, y, isWall ? TileType.Wall : TileType.Empty);
                }
            }
        }
    }

    /// <summary>
    /// 2단계: Cellular Automata 룰 적용
    /// 4-5 룰: 주변 8칸 중 5칸 이상 벽이면 벽으로
    /// </summary>
    private static MapData ApplyCellularAutomata(MapData oldMap, int wallThreshold)
    {
        MapData newMap = new MapData(oldMap.width, oldMap.height);

        for (int x = 0; x < oldMap.width; x++)
        {
            for (int y = 0; y < oldMap.height; y++)
            {
                // 외곽은 그대로 벽
                if (x == 0 || x == oldMap.width - 1 || y == 0 || y == oldMap.height - 1)
                {
                    newMap.SetTile(x, y, TileType.Wall);
                    continue;
                }

                // 이웃 벽 개수 세기
                int wallCount = CountWallNeighbors(oldMap, x, y);

                // 룰 적용
                if (wallCount >= wallThreshold)
                    newMap.SetTile(x, y, TileType.Wall);
                else
                    newMap.SetTile(x, y, TileType.Empty);
            }
        }

        return newMap;
    }

    /// <summary>
    /// 주변 8칸의 벽 개수 세기
    /// </summary>
    private static int CountWallNeighbors(MapData map, int x, int y)
    {
        int count = 0;

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue; // 자기 자신 제외

                int nx = x + dx;
                int ny = y + dy;

                // 범위 밖은 벽으로 취급
                if (!map.InBounds(nx, ny) || map.GetTile(nx, ny) == TileType.Wall)
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 외곽 1칸 벽 (기존 호환용, 새 코드에서는 CreateThickBorder 사용)
    /// </summary>
    private static void CreateBorder(MapData map)
    {
        for (int x = 0; x < map.width; x++)
        {
            map.SetTile(x, 0, TileType.Wall);
            map.SetTile(x, map.height - 1, TileType.Wall);
        }

        for (int y = 0; y < map.height; y++)
        {
            map.SetTile(0, y, TileType.Wall);
            map.SetTile(map.width - 1, y, TileType.Wall);
        }
    }

    /// <summary>
    /// 두꺼운 외곽 테두리 생성 (톱니 방지)
    /// </summary>
    private static void CreateThickBorder(MapData map, int thickness)
    {
        for (int x = 0; x < map.width; x++)
        {
            for (int y = 0; y < map.height; y++)
            {
                if (x < thickness || x >= map.width - thickness ||
                    y < thickness || y >= map.height - thickness)
                {
                    map.SetTile(x, y, TileType.Wall);
                }
            }
        }
    }

    /// <summary>
    /// 벽 스무딩: 1~2칸짜리 벽 돌기와 작은 구멍을 정리
    /// - 벽인데 이웃 벽이 너무 적으면 → 바닥
    /// - 바닥인데 이웃 벽이 너무 많으면 → 벽
    /// </summary>
    private static MapData SmoothWalls(MapData map, int passes)
    {
        for (int p = 0; p < passes; p++)
        {
            MapData newMap = new MapData(map.width, map.height);

            for (int x = 0; x < map.width; x++)
            {
                for (int y = 0; y < map.height; y++)
                {
                    // 외곽은 그대로 벽
                    if (x == 0 || x == map.width - 1 || y == 0 || y == map.height - 1)
                    {
                        newMap.SetTile(x, y, TileType.Wall);
                        continue;
                    }

                    int wallNeighbors = CountWallNeighbors(map, x, y);
                    bool isWall = map.GetTile(x, y) == TileType.Wall;

                    if (isWall)
                    {
                        // 벽인데 이웃 벽이 4개 미만이면 → 돌기로 판단, 제거
                        newMap.SetTile(x, y, wallNeighbors >= 4 ? TileType.Wall : TileType.Empty);
                    }
                    else
                    {
                        // 바닥인데 이웃 벽이 5개 이상이면 → 작은 구멍으로 판단, 메움
                        newMap.SetTile(x, y, wallNeighbors >= 5 ? TileType.Wall : TileType.Empty);
                    }
                }
            }

            map = newMap;
        }

        return map;
    }

    /// <summary>
    /// 4단계: FloodFill로 가장 큰 영역만 남기기
    /// (고립된 작은 공간 제거)
    /// </summary>
    /// <summary>
    /// 살아남은 최대 영역이 전체 바닥 대비 이 비율보다 작으면 '실패한 맵'으로 본다.
    /// 운 나쁜 시드에서 40칸짜리 영역만 남아도 그대로 쓰던 문제를 막는다.
    /// </summary>
    private const float MinRegionRatio = 0.35f;

    /// <summary>최대 영역의 칸 수. 재시도 판단에 쓴다.</summary>
    private static int _lastLargestRegionSize;

    private static void FloodFillCleanup(MapData map)
    {
        // 가장 큰 빈 공간 찾기
        bool[,] visited = new bool[map.width, map.height];
        int maxSize = 0;
        Vector2Int bestStart = Vector2Int.zero;

        for (int x = 1; x < map.width - 1; x++)
        {
            for (int y = 1; y < map.height - 1; y++)
            {
                if (map.IsWalkable(x, y) && !visited[x, y])
                {
                    int size = FloodFillCount(map, visited, x, y);
                    if (size > maxSize)
                    {
                        maxSize = size;
                        bestStart = new Vector2Int(x, y);
                    }
                }
            }
        }

        _lastLargestRegionSize = maxSize;

        // 가장 큰 영역 외에는 모두 벽으로
        visited = new bool[map.width, map.height];
        FloodFillMark(map, visited, bestStart.x, bestStart.y);

        for (int x = 1; x < map.width - 1; x++)
        {
            for (int y = 1; y < map.height - 1; y++)
            {
                if (!visited[x, y])
                    map.SetTile(x, y, TileType.Wall);
            }
        }
    }

    /// <summary>생성된 맵이 쓸 만한 크기인가. 아니면 다른 시드로 다시 만들어야 한다.</summary>
    private static bool IsUsableMap(MapData map)
    {
        int inner = Mathf.Max(1, (map.width - 2) * (map.height - 2));
        return _lastLargestRegionSize >= inner * MinRegionRatio;
    }

    /// <summary>
    /// FloodFill로 영역 크기 세기
    /// </summary>
    // 재귀 FloodFill 은 맵이 커지면 스택 오버플로로 죽는다(30x30 은 버텨도 150x150 은 못 버틴다).
    // 맵 확장을 막는 제약이라 명시적 스택으로 바꿨다. 결과는 재귀판과 동일하다.
    private static readonly System.Collections.Generic.Stack<Vector2Int> _fillStack
        = new System.Collections.Generic.Stack<Vector2Int>();

    private static int FloodFillCount(MapData map, bool[,] visited, int startX, int startY)
    {
        return FloodFill(map, visited, startX, startY);
    }

    /// <summary>
    /// FloodFill로 영역 마킹
    /// </summary>
    private static void FloodFillMark(MapData map, bool[,] visited, int startX, int startY)
    {
        FloodFill(map, visited, startX, startY);
    }

    /// <summary>4방향 FloodFill. 방문 표시를 하고 칸 수를 센다.</summary>
    private static int FloodFill(MapData map, bool[,] visited, int startX, int startY)
    {
        if (!map.InBounds(startX, startY) || visited[startX, startY] || !map.IsWalkable(startX, startY))
            return 0;

        _fillStack.Clear();
        _fillStack.Push(new Vector2Int(startX, startY));
        visited[startX, startY] = true;

        int count = 0;

        while (_fillStack.Count > 0)
        {
            Vector2Int cell = _fillStack.Pop();
            count++;

            TryPush(map, visited, cell.x + 1, cell.y);
            TryPush(map, visited, cell.x - 1, cell.y);
            TryPush(map, visited, cell.x, cell.y + 1);
            TryPush(map, visited, cell.x, cell.y - 1);
        }

        return count;
    }

    private static void TryPush(MapData map, bool[,] visited, int x, int y)
    {
        if (!map.InBounds(x, y) || visited[x, y] || !map.IsWalkable(x, y))
            return;

        // 스택에 넣는 시점에 방문 표시해야 같은 칸이 여러 번 들어가지 않는다.
        visited[x, y] = true;
        _fillStack.Push(new Vector2Int(x, y));
    }
}