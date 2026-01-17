using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드맵 생성 메인 클래스
/// Inspector에서 Generate 버튼으로 맵 생성 가능
/// </summary>
public class FieldMapGenerator : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("맵 생성 설정 (ScriptableObject)")]
    public FieldMapConfig config;
    
    [Header("References")]
    [Tooltip("GridBoard (기존 시스템)")]
    public GridBoard gridBoard;
    
    [Tooltip("맵 렌더러")]
    public MapRenderer mapRenderer;
    
    [Tooltip("플레이어")]
    public Transform player;
    
    [Header("Exit/Entrance")]
    [Tooltip("다음 맵 출구 프리팹")]
    public GameObject mapExitPrefab;
    
    [Tooltip("이전 맵 입구 프리팹 (선택)")]
    public GameObject mapEntrancePrefab;
    
    [Header("Runtime")]
    [Tooltip("생성된 맵 데이터 (읽기 전용)")]
    public MapData currentMap;
    
    [Header("Debug")]
    public bool logGeneration = true;
    
    private void Start()
    {
        // 게임 시작 시 자동 생성
        GenerateMap();
    }
    
    /// <summary>
    /// Inspector에서 호출할 수 있는 맵 생성 메서드
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        if (config == null)
        {
            Debug.LogError("[FieldMapGenerator] Config가 없습니다!");
            return;
        }
        
        GenerateAndRender(config.seed);
    }
    
    /// <summary>
    /// 맵 생성 + 렌더링 + 스폰
    /// </summary>
    public void GenerateAndRender(int seed)
    {
        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 맵 생성 시작 (Seed: {seed})");
        
        // 1. 맵 데이터 생성 (Cellular Automata)
        currentMap = CellularAutomata.Generate(config, seed);
        
        // 2. 플레이어 스폰 위치 찾기
        currentMap.playerSpawnPos = FindPlayerSpawnPosition(currentMap);
        
        // 3. 던전 입구 위치 찾기
        FindDungeonEntrancePositions(currentMap);
        
        // 4. 적 스폰 위치 찾기
        FindEnemySpawnPositions(currentMap);
        
        // 5. 맵 출구/입구 위치 찾기
        FindMapConnectionPositions(currentMap);
        
        // 6. 렌더링 (Tilemap + GridBoard 업데이트)
        if (mapRenderer != null)
        {
            mapRenderer.RenderMap(currentMap);
            Debug.Log($"[FieldMapGenerator] gridBoard={gridBoard?.name} id={gridBoard?.GetInstanceID()} / renderer.gridBoard={mapRenderer?.gridBoard?.name} id={mapRenderer?.gridBoard?.GetInstanceID()}");

        }

        // 7. 플레이어 배치
        SpawnPlayer();
        
        // 8. 던전 입구 프리팹 생성
        SpawnDungeonEntrances();
        
        // 9. 적 프리팹 생성
        SpawnEnemies();
        
        // 10. 맵 출구/입구 프리팹 생성
        SpawnMapConnections();
        
        if (logGeneration)
        {
            Debug.Log($"[FieldMapGenerator] 맵 생성 완료!");
            Debug.Log($"- 던전 입구: {currentMap.dungeonEntrances.Count}개");
            Debug.Log($"- 적 스폰: {currentMap.enemySpawns.Count}개");
        }
    }
    
    /// <summary>
    /// 플레이어 스폰 위치 찾기 (맵 중앙 부근의 빈 공간)
    /// </summary>
    private Vector2Int FindPlayerSpawnPosition(MapData map)
    {
        // GameManager에서 이전 맵 정보 확인
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.currentMapIndex > 0)
        {
            // 이전 맵에서 왔으면 입구 위치에 스폰
            if (map.entranceFromPrevMap != Vector2Int.zero)
                return map.entranceFromPrevMap;
        }
        
        // 첫 맵이거나 정보 없으면 중앙에 스폰
        int centerX = map.width / 2;
        int centerY = map.height / 2;
        
        // 중앙에서 가까운 빈 공간 찾기
        for (int radius = 0; radius < map.width / 2; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = centerX + dx;
                    int y = centerY + dy;
                    
                    if (map.IsWalkable(x, y))
                        return new Vector2Int(x, y);
                }
            }
        }
        
        // 못 찾으면 (1, 1) 반환
        return new Vector2Int(1, 1);
    }
    
    /// <summary>
    /// 던전 입구 위치 찾기
    /// </summary>
    private void FindDungeonEntrancePositions(MapData map)
    {
        int count = Random.Range(config.minDungeonEntrances, config.maxDungeonEntrances + 1);
        
        for (int i = 0; i < count; i++)
        {
            Vector2Int pos = FindRandomWalkablePosition(map, map.playerSpawnPos, 5f); // 플레이어에서 5칸 이상 떨어진 곳
            if (pos != Vector2Int.zero)
                map.dungeonEntrances.Add(pos);
        }
    }
    
    /// <summary>
    /// 적 스폰 위치 찾기
    /// </summary>
    private void FindEnemySpawnPositions(MapData map)
    {
        for (int i = 0; i < config.enemySpawnCount; i++)
        {
            Vector2Int pos = FindRandomWalkablePosition(map, map.playerSpawnPos, 3f); // 플레이어에서 3칸 이상 떨어진 곳
            if (pos != Vector2Int.zero)
                map.enemySpawns.Add(pos);
        }
    }
    
    /// <summary>
    /// 맵 연결 지점 찾기 (출구/입구)
    /// </summary>
    private void FindMapConnectionPositions(MapData map)
    {
        // 오른쪽 끝에 출구 (다음 맵으로)
        map.exitToNextMap = FindEdgeWalkablePosition(map, EdgeSide.Right);
        
        // 왼쪽 끝에 입구 (이전 맵에서)
        map.entranceFromPrevMap = FindEdgeWalkablePosition(map, EdgeSide.Left);
        
        // 출구/입구 주변을 Road 타일로 변경
        if (map.exitToNextMap != Vector2Int.zero)
            CreateRoadArea(map, map.exitToNextMap);
        
        if (map.entranceFromPrevMap != Vector2Int.zero)
            CreateRoadArea(map, map.entranceFromPrevMap);
    }
    
    /// <summary>
    /// 맵 가장자리에서 빈 공간 찾기
    /// </summary>
    private Vector2Int FindEdgeWalkablePosition(MapData map, EdgeSide side)
    {
        int x = 0, y = 0;
        
        switch (side)
        {
            case EdgeSide.Right:
                x = map.width - 2;
                y = map.height / 2;
                break;
            case EdgeSide.Left:
                x = 1;
                y = map.height / 2;
                break;
            case EdgeSide.Top:
                x = map.width / 2;
                y = map.height - 2;
                break;
            case EdgeSide.Bottom:
                x = map.width / 2;
                y = 1;
                break;
        }
        
        // 해당 위치 근처에서 빈 공간 찾기
        for (int radius = 0; radius < 5; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int checkX = x;
                int checkY = y + dy;
                
                if (map.IsWalkable(checkX, checkY))
                    return new Vector2Int(checkX, checkY);
            }
        }
        
        return Vector2Int.zero;
    }
    
    /// <summary>
    /// 출구/입구 주변을 Road 타일로 만들기
    /// </summary>
    private void CreateRoadArea(MapData map, Vector2Int center)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int x = center.x + dx;
                int y = center.y + dy;
                
                if (map.InBounds(x, y) && map.IsWalkable(x, y))
                    map.SetTile(x, y, TileType.Road);
            }
        }
    }
    
    /// <summary>
    /// 랜덤 빈 공간 찾기
    /// </summary>
    private Vector2Int FindRandomWalkablePosition(MapData map, Vector2Int avoidPos, float minDistance)
    {
        int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int x = Random.Range(2, map.width - 2);
            int y = Random.Range(2, map.height - 2);
            
            if (!map.IsWalkable(x, y))
                continue;
            
            // 거리 체크
            float dist = Vector2Int.Distance(new Vector2Int(x, y), avoidPos);
            if (dist < minDistance)
                continue;
            
            // 주변도 비어있는지 체크 (프리팹 배치 공간 확보)
            bool hasSpace = true;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (!map.IsWalkable(x + dx, y + dy))
                    {
                        hasSpace = false;
                        break;
                    }
                }
                if (!hasSpace) break;
            }
            
            if (hasSpace)
                return new Vector2Int(x, y);
        }
        
        return Vector2Int.zero;
    }
    
    /// <summary>
    /// 플레이어 스폰
    /// </summary>
    private void SpawnPlayer()
    {
        if (player != null && gridBoard != null)
        {
            Vector3 spawnWorld = gridBoard.CellToWorld(currentMap.playerSpawnPos);
            player.position = spawnWorld;
            
            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] 플레이어 스폰: {currentMap.playerSpawnPos}");
        }
    }
    
    /// <summary>
    /// 던전 입구 프리팹 생성
    /// </summary>
    private void SpawnDungeonEntrances()
    {
        if (config.dungeonEntrancePrefab == null) return;
        if (gridBoard == null) return;
        
        foreach (var pos in currentMap.dungeonEntrances)
        {
            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject entrance = Instantiate(config.dungeonEntrancePrefab, worldPos, Quaternion.identity);
            entrance.name = $"DungeonEntrance_{pos.x}_{pos.y}";
            
            // DungeonEntranceInteract 컴포넌트 확인
            var interact = entrance.GetComponent<DungeonEntranceInteract>();
            if (interact == null)
            {
                interact = entrance.AddComponent<DungeonEntranceInteract>();
            }
            
            // 던전 ID 설정
            interact.dungeonId = $"Dungeon_C{GameManager.Instance?.currentChapter ?? 1}_M{GameManager.Instance?.currentMapIndex ?? 0}_{pos.x}_{pos.y}";
        }
    }
    
    /// <summary>
    /// 적 프리팹 생성
    /// </summary>
    private void SpawnEnemies()
    {
        if (config.enemyPrefab == null) return;
        if (gridBoard == null) return;
        
        foreach (var pos in currentMap.enemySpawns)
        {
            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject enemy = Instantiate(config.enemyPrefab, worldPos, Quaternion.identity);
            enemy.name = $"Enemy_{pos.x}_{pos.y}";
        }
    }
    
    /// <summary>
    /// 맵 출구/입구 프리팹 생성
    /// </summary>
    private void SpawnMapConnections()
    {
        if (gridBoard == null) return;
        
        GameManager gm = GameManager.Instance;
        if (gm == null) return;
        
        // 다음 맵 출구 (마지막 맵이 아니면)
        if (gm.currentMapIndex < gm.mapsPerChapter - 1 && mapExitPrefab != null)
        {
            if (currentMap.exitToNextMap != Vector2Int.zero)
            {
                Vector3 exitWorld = gridBoard.CellToWorld(currentMap.exitToNextMap);
                GameObject exitObj = Instantiate(mapExitPrefab, exitWorld, Quaternion.identity);
                exitObj.name = "MapExit_ToNext";
                
                // MapExitTrigger 설정
                var trigger = exitObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = exitObj.AddComponent<MapExitTrigger>();
                
                trigger.isNextMap = true;
            }
        }
        
        // 이전 맵 입구 (첫 맵이 아니면)
        if (gm.currentMapIndex > 0 && mapEntrancePrefab != null)
        {
            if (currentMap.entranceFromPrevMap != Vector2Int.zero)
            {
                Vector3 entranceWorld = gridBoard.CellToWorld(currentMap.entranceFromPrevMap);
                GameObject entranceObj = Instantiate(mapEntrancePrefab, entranceWorld, Quaternion.identity);
                entranceObj.name = "MapEntrance_FromPrev";
                
                // MapExitTrigger 설정 (뒤로가기)
                var trigger = entranceObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = entranceObj.AddComponent<MapExitTrigger>();
                
                trigger.isNextMap = false;
            }
        }
    }
}

/// <summary>
/// 맵 가장자리 방향
/// </summary>
public enum EdgeSide
{
    Right,
    Left,
    Top,
    Bottom
}
