using UnityEngine;

/// <summary>
/// Cellular Automata 맵 생성 알고리즘
/// 동굴/숲 같은 유기적인 맵 생성
/// </summary>
public static class CellularAutomata
{
    /// <summary>
    /// CA로 맵 생성
    /// </summary>
    public static MapData Generate(FieldMapConfig config, int seed)
    {
        // 시드 설정
        if (seed == 0) seed = Random.Range(1, 999999);
        Random.InitState(seed);
        
        MapData map = new MapData(config.width, config.height);
        
        // 1단계: 랜덤 초기화
        InitializeRandom(map, config.initialWallProbability);
        
        // 2단계: CA 반복 적용
        for (int i = 0; i < config.iterations; i++)
        {
            map = ApplyCellularAutomata(map, config.wallThreshold);
        }
        
        // 3단계: 외곽 벽 생성
        CreateBorder(map);
        
        // 4단계: 고립된 영역 제거 (접근 가능한 영역만 남김)
        FloodFillCleanup(map);
        
        return map;
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
    /// 3단계: 외곽 벽 확실히 만들기
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
    /// 4단계: FloodFill로 가장 큰 영역만 남기기
    /// (고립된 작은 공간 제거)
    /// </summary>
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
    
    /// <summary>
    /// FloodFill로 영역 크기 세기
    /// </summary>
    private static int FloodFillCount(MapData map, bool[,] visited, int x, int y)
    {
        if (!map.InBounds(x, y) || visited[x, y] || !map.IsWalkable(x, y))
            return 0;
        
        visited[x, y] = true;
        int count = 1;
        
        // 4방향 탐색
        count += FloodFillCount(map, visited, x + 1, y);
        count += FloodFillCount(map, visited, x - 1, y);
        count += FloodFillCount(map, visited, x, y + 1);
        count += FloodFillCount(map, visited, x, y - 1);
        
        return count;
    }
    
    /// <summary>
    /// FloodFill로 영역 마킹
    /// </summary>
    private static void FloodFillMark(MapData map, bool[,] visited, int x, int y)
    {
        if (!map.InBounds(x, y) || visited[x, y] || !map.IsWalkable(x, y))
            return;
        
        visited[x, y] = true;
        
        // 4방향 탐색
        FloodFillMark(map, visited, x + 1, y);
        FloodFillMark(map, visited, x - 1, y);
        FloodFillMark(map, visited, x, y + 1);
        FloodFillMark(map, visited, x, y - 1);
    }
}
