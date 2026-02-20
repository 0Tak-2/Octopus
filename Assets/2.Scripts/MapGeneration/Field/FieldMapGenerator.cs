using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드맵 생성 메인 클래스
/// FieldDefinition 기반으로 적, 자원, 던전 입구 스폰
/// WorldProgressManager에서 현재 필드를 자동으로 가져옴
/// </summary>
public class FieldMapGenerator : MonoBehaviour
{
    [Header("Config (Fallback)")]
    [Tooltip("기존 맵 생성 설정 (FieldDefinition이 없을 때 사용)")]
    public FieldMapConfig config;

    [Header("Field Definition (자동 연결)")]
    [Tooltip("현재 필드 정의 (비워두면 WorldProgressManager에서 자동 가져옴)")]
    public FieldDefinition fieldDefinition;

    [Header("References")]
    [Tooltip("GridBoard (기존 시스템)")]
    public GridBoard gridBoard;

    [Tooltip("맵 렌더러")]
    public MapRenderer mapRenderer;

    [Tooltip("플레이어")]
    public Transform player;

    [Header("Exit/Entrance")]
    [Tooltip("다음 필드 출구 프리팹")]
    public GameObject mapExitPrefab;

    [Tooltip("이전 필드 입구 프리팹 (선택)")]
    public GameObject mapEntrancePrefab;

    [Header("Dungeon")]
    [Tooltip("던전 입구 프리팹 (FieldDefinition용)")]
    public GameObject dungeonEntrancePrefab;

    [Header("Resource Prefabs")]
    [Tooltip("해초 프리팹")]
    public GameObject seaweedPrefab;
    [Tooltip("산호 프리팹")]
    public GameObject coralPrefab;
    [Tooltip("암석 프리팹")]
    public GameObject rockPrefab;
    [Tooltip("모래 프리팹")]
    public GameObject sandPrefab;
    [Tooltip("해면 프리팹")]
    public GameObject spongePrefab;

    [Header("Runtime")]
    [Tooltip("생성된 맵 데이터 (읽기 전용)")]
    public MapData currentMap;

    [Header("Debug")]
    public bool logGeneration = true;

    // 스폰된 오브젝트 추적 (맵 재생성 시 정리용)
    private List<GameObject> spawnedObjects = new List<GameObject>();

    private void Start()
    {
        // 던전 씬에서는 필드맵 생성하지 않음
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName == "DungeonScene")
        {
            Debug.Log("[FieldMapGenerator] 던전 씬이므로 맵 생성 스킵");
            return;
        }

        // WorldProgressManager에서 현재 필드 가져오기
        if (fieldDefinition == null && WorldProgressManager.Instance != null)
        {
            fieldDefinition = WorldProgressManager.Instance.CurrentField;
            if (logGeneration && fieldDefinition != null)
                Debug.Log($"[FieldMapGenerator] WorldProgressManager에서 필드 가져옴: {fieldDefinition.fieldName}");
        }

        // 게임 시작 시 자동 생성
        GenerateMap();
    }

    /// <summary>
    /// Inspector에서 호출할 수 있는 맵 생성 메서드
    /// </summary>
    [ContextMenu("Generate Map")]
    public void GenerateMap()
    {
        // FieldDefinition이 있으면 그걸 사용, 없으면 기존 config 사용
        if (fieldDefinition != null)
        {
            GenerateFromFieldDefinition();
        }
        else if (config != null)
        {
            GenerateFromConfig();
        }
        else
        {
            Debug.LogError("[FieldMapGenerator] FieldDefinition도 Config도 없습니다!");
        }
    }

    // ============================================
    // FieldDefinition 기반 생성 (새 시스템)
    // ============================================

    private void GenerateFromFieldDefinition()
    {
        // 시드 결정
        int seed = 0;
        if (GameManager.Instance != null)
        {
            seed = GameManager.Instance.GetFieldMapSeed();
        }
        if (seed == 0)
        {
            seed = Random.Range(1, int.MaxValue);
        }

        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 필드맵 생성 시작 - {fieldDefinition.fieldName} (Seed: {seed})");

        // 이전 스폰 오브젝트 정리
        ClearSpawnedObjects();

        // 1. 맵 데이터 생성 (FieldDefinition의 크기/설정 사용)
        currentMap = CellularAutomata.Generate(fieldDefinition, seed);

        // 2. 플레이어 스폰 위치
        currentMap.playerSpawnPos = FindPlayerSpawnPosition(currentMap);

        // 3. 던전 입구 위치
        FindDungeonEntrancePositions(currentMap);

        // 4. 적 스폰 위치 (카테고리별)
        FindEnemySpawnPositions(currentMap);

        // 5. 맵 출구/입구 위치
        FindMapConnectionPositions(currentMap);

        // 6. 렌더링
        if (mapRenderer != null)
        {
            mapRenderer.RenderMap(currentMap);
        }

        // 7. 플레이어 배치
        SpawnPlayer();

        // 8. 던전 입구 생성
        SpawnDungeonEntrances();

        // 9. 적 생성 (카테고리별)
        SpawnEnemiesFromDefinition();

        // 10. 자원 생성
        SpawnResources();

        // 10-1. 시야 시스템에 엄폐물(산호/해초) 등록
        var visionSys = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        if (visionSys != null)
        {
            visionSys.RefreshCoverCells();
            if (logGeneration)
                Debug.Log("[FieldMapGenerator] 시야 시스템 엄폐물 등록 완료");
        }

        // 11. 맵 출구/입구 생성
        SpawnMapConnections();

        if (logGeneration)
        {
            Debug.Log($"[FieldMapGenerator] 맵 생성 완료! - {fieldDefinition.fieldName}");
            Debug.Log($"- 던전 입구: {currentMap.dungeonEntrances.Count}개");
            Debug.Log($"- 적 스폰: {currentMap.enemySpawns.Count}개");
        }
    }

    // ============================================
    // 기존 Config 기반 생성 (Fallback)
    // ============================================

    private void GenerateFromConfig()
    {
        int seed = config.seed;

        if (GameManager.Instance != null)
        {
            seed = GameManager.Instance.GetFieldMapSeed();
            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] GameManager에서 시드 가져옴: {seed}");
        }
        else if (seed == 0)
        {
            seed = Random.Range(1, int.MaxValue);
        }

        GenerateAndRender(seed);
    }

    /// <summary>
    /// 기존 맵 생성 + 렌더링 + 스폰 (Config 기반)
    /// </summary>
    public void GenerateAndRender(int seed)
    {
        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 맵 생성 시작 (Config, Seed: {seed})");

        ClearSpawnedObjects();

        currentMap = CellularAutomata.Generate(config, seed);
        currentMap.playerSpawnPos = FindPlayerSpawnPosition(currentMap);
        FindDungeonEntrancePositions(currentMap);
        FindEnemySpawnPositions(currentMap);
        FindMapConnectionPositions(currentMap);

        if (mapRenderer != null)
        {
            mapRenderer.RenderMap(currentMap);
        }

        SpawnPlayer();
        SpawnDungeonEntrances();
        SpawnEnemies();
        SpawnMapConnections();

        // 시야 시스템에 엄폐물 등록 (씬에 이미 배치된 해초/산호 포함)
        var visionSys2 = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        if (visionSys2 != null)
            visionSys2.RefreshCoverCells();

        if (logGeneration)
        {
            Debug.Log($"[FieldMapGenerator] 맵 생성 완료!");
            Debug.Log($"- 던전 입구: {currentMap.dungeonEntrances.Count}개");
            Debug.Log($"- 적 스폰: {currentMap.enemySpawns.Count}개");
        }
    }

    // ============================================
    // 스폰 위치 찾기
    // ============================================

    private Vector2Int FindPlayerSpawnPosition(MapData map)
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.currentMapIndex > 0)
        {
            if (map.entranceFromPrevMap != Vector2Int.zero)
                return map.entranceFromPrevMap;
        }

        int centerX = map.width / 2;
        int centerY = map.height / 2;

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

        return new Vector2Int(1, 1);
    }

    private void FindDungeonEntrancePositions(MapData map)
    {
        int count;

        if (fieldDefinition != null && fieldDefinition.dungeons != null)
        {
            // FieldDefinition: 던전 수만큼 입구 생성
            count = fieldDefinition.dungeons.Length;
        }
        else if (config != null)
        {
            count = Random.Range(config.minDungeonEntrances, config.maxDungeonEntrances + 1);
        }
        else
        {
            count = 1;
        }

        for (int i = 0; i < count; i++)
        {
            Vector2Int pos = FindRandomWalkablePosition(map, map.playerSpawnPos, 5f);
            if (pos != Vector2Int.zero)
                map.dungeonEntrances.Add(pos);
        }
    }

    private void FindEnemySpawnPositions(MapData map)
    {
        int totalCount;

        if (fieldDefinition != null)
        {
            totalCount = fieldDefinition.TotalEnemyCount;
        }
        else if (config != null)
        {
            totalCount = config.enemySpawnCount;
        }
        else
        {
            totalCount = 5;
        }

        for (int i = 0; i < totalCount; i++)
        {
            Vector2Int pos = FindRandomWalkablePosition(map, map.playerSpawnPos, 3f);
            if (pos != Vector2Int.zero)
                map.enemySpawns.Add(pos);
        }
    }

    private void FindMapConnectionPositions(MapData map)
    {
        map.exitToNextMap = FindEdgeWalkablePosition(map, EdgeSide.Right);
        map.entranceFromPrevMap = FindEdgeWalkablePosition(map, EdgeSide.Left);

        if (map.exitToNextMap != Vector2Int.zero)
            CreateRoadArea(map, map.exitToNextMap);
        if (map.entranceFromPrevMap != Vector2Int.zero)
            CreateRoadArea(map, map.entranceFromPrevMap);
    }

    // ============================================
    // 적 스폰 (FieldDefinition 카테고리별)
    // ============================================

    private void SpawnEnemiesFromDefinition()
    {
        if (fieldDefinition == null) return;
        if (gridBoard == null) return;

        int spawnIndex = 0;

        // 1. 일반 적
        spawnIndex = SpawnEnemyCategory(
            fieldDefinition.normalEnemyPrefabs,
            fieldDefinition.normalEnemyCount,
            spawnIndex, "Normal");

        // 2. 엘리트 적
        spawnIndex = SpawnEnemyCategory(
            fieldDefinition.eliteEnemyPrefabs,
            fieldDefinition.eliteEnemyCount,
            spawnIndex, "Elite");

        // 3. 중립 적
        spawnIndex = SpawnEnemyCategory(
            fieldDefinition.neutralEnemyPrefabs,
            fieldDefinition.neutralEnemyCount,
            spawnIndex, "Neutral");

        // 4. 초식 적
        spawnIndex = SpawnEnemyCategory(
            fieldDefinition.passiveEnemyPrefabs,
            fieldDefinition.passiveEnemyCount,
            spawnIndex, "Passive");

        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 적 스폰 완료: 총 {spawnIndex}마리");
    }

    /// <summary>
    /// 카테고리별 적 스폰 (프리팹 배열에서 랜덤 선택)
    /// </summary>
    private int SpawnEnemyCategory(GameObject[] prefabs, int count, int startIndex, string category)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0)
            return startIndex;

        for (int i = 0; i < count; i++)
        {
            int spawnIdx = startIndex + i;
            if (spawnIdx >= currentMap.enemySpawns.Count)
            {
                if (logGeneration)
                    Debug.LogWarning($"[FieldMapGenerator] {category} 적 스폰 위치 부족! ({i}/{count})");
                return spawnIdx;
            }

            Vector2Int pos = currentMap.enemySpawns[spawnIdx];
            Vector3 worldPos = gridBoard.CellToWorld(pos);

            // 프리팹 배열에서 랜덤 선택
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            GameObject enemy = Instantiate(prefab, worldPos, Quaternion.identity);
            enemy.name = $"{category}_{prefab.name}_{pos.x}_{pos.y}";

            spawnedObjects.Add(enemy);

            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] {category} 적 스폰: {prefab.name} at {pos}");
        }

        return startIndex + count;
    }

    // ============================================
    // 자원 스폰
    // ============================================

    private void SpawnResources()
    {
        if (fieldDefinition == null) return;
        if (gridBoard == null) return;

        int totalResources = 0;

        totalResources += SpawnResourceType(seaweedPrefab, fieldDefinition.seaweedCount, "Seaweed");
        totalResources += SpawnResourceType(coralPrefab, fieldDefinition.coralCount, "Coral");
        totalResources += SpawnResourceType(rockPrefab, fieldDefinition.rockCount, "Rock");
        totalResources += SpawnResourceType(sandPrefab, fieldDefinition.sandCount, "Sand");
        totalResources += SpawnResourceType(spongePrefab, fieldDefinition.spongeCount, "Sponge");

        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 자원 스폰 완료: 총 {totalResources}개");
    }

    /// <summary>
    /// 자원 타입별 스폰
    /// </summary>
    private int SpawnResourceType(GameObject prefab, int count, string resourceName)
    {
        if (prefab == null || count <= 0) return 0;

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            Vector2Int pos = FindRandomWalkablePosition(currentMap, currentMap.playerSpawnPos, 2f);
            if (pos == Vector2Int.zero) continue;

            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject resource = Instantiate(prefab, worldPos, Quaternion.identity);
            resource.name = $"{resourceName}_{pos.x}_{pos.y}";

            spawnedObjects.Add(resource);
            spawned++;
        }

        if (logGeneration && spawned > 0)
            Debug.Log($"[FieldMapGenerator] {resourceName} 스폰: {spawned}개");

        return spawned;
    }

    // ============================================
    // 던전 입구 스폰
    // ============================================

    private void SpawnDungeonEntrances()
    {
        if (gridBoard == null) return;

        // FieldDefinition 모드
        if (fieldDefinition != null && fieldDefinition.dungeons != null)
        {
            SpawnDungeonEntrancesFromDefinition();
            return;
        }

        // Config fallback 모드
        if (config != null && config.dungeonEntrancePrefab != null)
        {
            SpawnDungeonEntrancesFromConfig();
        }
    }

    /// <summary>
    /// FieldDefinition 기반 던전 입구 생성
    /// 각 던전마다 1개의 입구 생성, DungeonDefinition과 연결
    /// </summary>
    private void SpawnDungeonEntrancesFromDefinition()
    {
        GameObject entrancePrefab = dungeonEntrancePrefab;

        // fallback: config의 프리팹 사용
        if (entrancePrefab == null && config != null)
            entrancePrefab = config.dungeonEntrancePrefab;

        if (entrancePrefab == null)
        {
            Debug.LogWarning("[FieldMapGenerator] 던전 입구 프리팹이 없습니다!");
            return;
        }

        for (int i = 0; i < fieldDefinition.dungeons.Length && i < currentMap.dungeonEntrances.Count; i++)
        {
            DungeonDefinition dungeon = fieldDefinition.dungeons[i];
            if (dungeon == null) continue;

            Vector2Int pos = currentMap.dungeonEntrances[i];
            Vector3 worldPos = gridBoard.CellToWorld(pos);

            GameObject entrance = Instantiate(entrancePrefab, worldPos, Quaternion.identity);
            entrance.name = $"DungeonEntrance_{dungeon.dungeonID}";

            // DungeonEntranceInteract 설정
            var interact = entrance.GetComponent<DungeonEntranceInteract>();
            if (interact == null)
                interact = entrance.AddComponent<DungeonEntranceInteract>();

            interact.dungeonId = dungeon.dungeonID;

            spawnedObjects.Add(entrance);

            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] 던전 입구 생성: {dungeon.dungeonName} ({dungeon.dungeonID}) at {pos}");
        }
    }

    /// <summary>
    /// Config 기반 던전 입구 생성 (기존 방식)
    /// </summary>
    private void SpawnDungeonEntrancesFromConfig()
    {
        foreach (var pos in currentMap.dungeonEntrances)
        {
            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject entrance = Instantiate(config.dungeonEntrancePrefab, worldPos, Quaternion.identity);
            entrance.name = $"DungeonEntrance_{pos.x}_{pos.y}";

            var interact = entrance.GetComponent<DungeonEntranceInteract>();
            if (interact == null)
                interact = entrance.AddComponent<DungeonEntranceInteract>();

            interact.dungeonId = $"Dungeon_C{GameManager.Instance?.currentChapter ?? 1}_M{GameManager.Instance?.currentMapIndex ?? 0}_{pos.x}_{pos.y}";

            spawnedObjects.Add(entrance);
        }
    }

    // ============================================
    // 기존 적 스폰 (Config fallback)
    // ============================================

    private void SpawnEnemies()
    {
        if (config == null || config.enemyPrefab == null) return;
        if (gridBoard == null) return;

        foreach (var pos in currentMap.enemySpawns)
        {
            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject enemy = Instantiate(config.enemyPrefab, worldPos, Quaternion.identity);
            enemy.name = $"Enemy_{pos.x}_{pos.y}";
            spawnedObjects.Add(enemy);
        }
    }

    // ============================================
    // 플레이어 스폰
    // ============================================

    private void SpawnPlayer()
    {
        if (player != null && gridBoard != null)
        {
            if (GameManager.Instance != null)
            {
                Vector2Int entrancePos = GameManager.Instance.playerData.dungeonEntrancePosition;

                if (entrancePos != Vector2Int.zero)
                {
                    Vector3 entranceWorld = gridBoard.CellToWorld(entrancePos);
                    player.position = entranceWorld;

                    if (logGeneration)
                        Debug.Log($"[FieldMapGenerator] 플레이어 던전 입구로 복귀: {entrancePos}");

                    GameManager.Instance.playerData.dungeonEntrancePosition = Vector2Int.zero;
                    return;
                }
            }

            Vector3 spawnWorld = gridBoard.CellToWorld(currentMap.playerSpawnPos);
            player.position = spawnWorld;

            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] 플레이어 스폰: {currentMap.playerSpawnPos}");
        }
    }

    // ============================================
    // 맵 연결 (출구/입구)
    // ============================================

    private void SpawnMapConnections()
    {
        if (gridBoard == null) return;

        // FieldDefinition 모드
        if (fieldDefinition != null)
        {
            SpawnMapConnectionsFromDefinition();
            return;
        }

        // Config fallback
        SpawnMapConnectionsFromConfig();
    }

    private void SpawnMapConnectionsFromDefinition()
    {
        // 다음 필드 출구
        if (fieldDefinition.hasNextFieldExit && fieldDefinition.nextField != null && mapExitPrefab != null)
        {
            if (currentMap.exitToNextMap != Vector2Int.zero)
            {
                Vector3 exitWorld = gridBoard.CellToWorld(currentMap.exitToNextMap);
                GameObject exitObj = Instantiate(mapExitPrefab, exitWorld, Quaternion.identity);
                exitObj.name = "MapExit_ToNext";

                var trigger = exitObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = exitObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = true;

                spawnedObjects.Add(exitObj);

                if (logGeneration)
                    Debug.Log($"[FieldMapGenerator] 다음 필드 출구 생성: -> {fieldDefinition.nextField.fieldName}");
            }
        }

        // 이전 필드 입구
        if (fieldDefinition.previousField != null && mapEntrancePrefab != null)
        {
            if (currentMap.entranceFromPrevMap != Vector2Int.zero)
            {
                Vector3 entranceWorld = gridBoard.CellToWorld(currentMap.entranceFromPrevMap);
                GameObject entranceObj = Instantiate(mapEntrancePrefab, entranceWorld, Quaternion.identity);
                entranceObj.name = "MapEntrance_FromPrev";

                var trigger = entranceObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = entranceObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = false;

                spawnedObjects.Add(entranceObj);

                if (logGeneration)
                    Debug.Log($"[FieldMapGenerator] 이전 필드 입구 생성: <- {fieldDefinition.previousField.fieldName}");
            }
        }
    }

    private void SpawnMapConnectionsFromConfig()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.currentMapIndex < gm.mapsPerChapter - 1 && mapExitPrefab != null)
        {
            if (currentMap.exitToNextMap != Vector2Int.zero)
            {
                Vector3 exitWorld = gridBoard.CellToWorld(currentMap.exitToNextMap);
                GameObject exitObj = Instantiate(mapExitPrefab, exitWorld, Quaternion.identity);
                exitObj.name = "MapExit_ToNext";

                var trigger = exitObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = exitObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = true;

                spawnedObjects.Add(exitObj);
            }
        }

        if (gm.currentMapIndex > 0 && mapEntrancePrefab != null)
        {
            if (currentMap.entranceFromPrevMap != Vector2Int.zero)
            {
                Vector3 entranceWorld = gridBoard.CellToWorld(currentMap.entranceFromPrevMap);
                GameObject entranceObj = Instantiate(mapEntrancePrefab, entranceWorld, Quaternion.identity);
                entranceObj.name = "MapEntrance_FromPrev";

                var trigger = entranceObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = entranceObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = false;

                spawnedObjects.Add(entranceObj);
            }
        }
    }

    // ============================================
    // 유틸리티
    // ============================================

    /// <summary>
    /// 스폰된 오브젝트 정리
    /// </summary>
    private void ClearSpawnedObjects()
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        spawnedObjects.Clear();
    }

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

    private Vector2Int FindRandomWalkablePosition(MapData map, Vector2Int avoidPos, float minDistance)
    {
        int maxAttempts = 100;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int x = Random.Range(2, map.width - 2);
            int y = Random.Range(2, map.height - 2);

            if (!map.IsWalkable(x, y))
                continue;

            float dist = Vector2Int.Distance(new Vector2Int(x, y), avoidPos);
            if (dist < minDistance)
                continue;

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