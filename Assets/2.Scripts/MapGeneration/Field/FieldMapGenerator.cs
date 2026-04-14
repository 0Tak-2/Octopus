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

    [Tooltip("스폰한 적·자원·입구·출구의 부모. 비우면 gridBoard.transform (필드 루트 비활성화 시 함께 숨김)")]
    public Transform runtimeSpawnParent;

    [Header("Exit/Entrance")]
    [Tooltip("다음 필드 출구 프리팹")]
    public GameObject mapExitPrefab;

    [Tooltip("이전 필드 입구 프리팹 (선택)")]
    public GameObject mapEntrancePrefab;

    [Header("Dungeon")]
    [Tooltip("던전 입구 프리팹 (FieldDefinition용)")]
    public GameObject dungeonEntrancePrefab;

    [Header("Resource Prefabs")]
    [Tooltip("해초 프리팹들 (랜덤 스폰)")]
    public GameObject[] seaweedPrefabs;
    [Tooltip("해초 단일 프리팹 (배열 비었을 때 fallback)")]
    public GameObject seaweedPrefab;
    [Tooltip("산호 프리팹들 (랜덤 스폰)")]
    public GameObject[] coralPrefabs;
    [Tooltip("산호 단일 프리팹 (배열 비었을 때 fallback)")]
    public GameObject coralPrefab;
    [Tooltip("암석 프리팹들 (랜덤 스폰)")]
    public GameObject[] rockPrefabs;
    [Tooltip("암석 단일 프리팹 (배열 비었을 때 fallback)")]
    public GameObject rockPrefab;
    [Tooltip("모래 프리팹들 (랜덤 스폰)")]
    public GameObject[] sandPrefabs;
    [Tooltip("모래 단일 프리팹 (배열 비었을 때 fallback)")]
    public GameObject sandPrefab;
    [Tooltip("해면 프리팹들 (랜덤 스폰)")]
    public GameObject[] spongePrefabs;
    [Tooltip("해면 단일 프리팹 (배열 비었을 때 fallback)")]
    public GameObject spongePrefab;

    [Header("Runtime")]
    [Tooltip("생성된 맵 데이터 (읽기 전용)")]
    public MapData currentMap;

    [Header("Debug")]
    public bool logGeneration = true;

    private void AttachSpawnToField(GameObject go)
    {
        if (go == null) return;
        Transform parent = runtimeSpawnParent != null ? runtimeSpawnParent : (gridBoard != null ? gridBoard.transform : null);
        if (parent != null)
            go.transform.SetParent(parent, true);
    }

    // 스폰된 오브젝트 추적 (맵 재생성 시 정리용)
    private List<GameObject> spawnedObjects = new List<GameObject>();
    // 자원 스폰 중 동일 셀 중복 방지
    private readonly HashSet<Vector2Int> usedResourceCells = new HashSet<Vector2Int>();

    [SerializeField, Tooltip("같은 씬에서 필드 맵을 중복 생성하지 않도록")]
    private bool _fieldMapGeneratedOnce;

    private void Start()
    {
        TryGenerateInitialFieldMap();
    }

    /// <summary>
    /// 게임 시작 시 한 번만 맵 생성. 던전 복귀 후에는 FieldRoot 만 켜지므로 재호출되지 않음.
    /// </summary>
    public void TryGenerateInitialFieldMap()
    {
        if (_fieldMapGeneratedOnce)
            return;

        // 인스펙터에 예전 필드가 박혀 있어도 항상 진행도의 현재 필드 사용 (필드2 맵·시드가 바뀌도록)
        if (WorldProgressManager.Instance != null && WorldProgressManager.Instance.CurrentField != null)
        {
            fieldDefinition = WorldProgressManager.Instance.CurrentField;
            if (logGeneration)
                Debug.Log($"[FieldMapGenerator] 현재 진행 필드 적용: {fieldDefinition.fieldName} (index {WorldProgressManager.Instance.CurrentFieldIndex})");
        }
        else if (fieldDefinition == null && WorldProgressManager.Instance != null)
        {
            fieldDefinition = WorldProgressManager.Instance.CurrentField;
            if (logGeneration && fieldDefinition != null)
                Debug.Log($"[FieldMapGenerator] WorldProgressManager에서 필드 가져옴: {fieldDefinition.fieldName}");
        }

        if (GameManager.Instance != null)
            GameManager.Instance.SyncFieldProgressFromWorld();

        _fieldMapGeneratedOnce = true;
        GenerateMap();
    }

    /// <summary>
    /// 현재 필드 시드(GameManager)로 맵만 다시 생성. RerollCurrentFieldMapNewSeed(reloadScene:false) 와 함께 쓰임.
    /// </summary>
    public void ForceRegenerateFieldMap()
    {
        _fieldMapGeneratedOnce = false;
        TryGenerateInitialFieldMap();
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

        // 2. 맵 출구/입구 — 저장된 통로 우선, 입구 좌표가 있어야 플레이어 스폰(이전 맵에서 들어온 경우)이 맞음
        ResolveMapConnections(currentMap);

        // 3. 플레이어 스폰 위치
        currentMap.playerSpawnPos = FindPlayerSpawnPosition(currentMap);

        // 4. 던전 입구 위치
        FindDungeonEntrancePositions(currentMap);

        // 5. 적 스폰 위치 (카테고리별)
        FindEnemySpawnPositions(currentMap);

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
        ResolveMapConnections(currentMap);
        currentMap.playerSpawnPos = FindPlayerSpawnPosition(currentMap);
        FindDungeonEntrancePositions(currentMap);
        FindEnemySpawnPositions(currentMap);

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
        // 다른 필드에서 포털로 들어온 경우에만 입구에 스폰 (첫 필드는 가운데)
        if (gm != null && gm.playerData.hasPendingEntranceHint && map.entranceFromPrevMap != Vector2Int.zero)
            return map.entranceFromPrevMap;

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

    /// <summary>
    /// 저장된 출구/입구가 있으면 항상 그 통로를 쓰고(왕복 유지), 없으면 새로 찾은 뒤 디스크에 저장합니다.
    /// </summary>
    private void ResolveMapConnections(MapData map)
    {
        var gm = GameManager.Instance;
        if (gm != null && TryRestorePersistentPortals(map))
            return;

        FindMapConnectionPositions(map);

        if (gm == null) return;
        var save = gm.GetOrCreateFieldMapData(gm.GetCurrentMapKey());
        if (map.exitToNextMap != Vector2Int.zero && map.entranceFromPrevMap != Vector2Int.zero)
        {
            save.savedExitToNext = map.exitToNextMap;
            save.savedEntranceFromPrev = map.entranceFromPrevMap;
            save.hasSavedPortalCells = true;
            FieldLayoutDiskStore.Save(gm.GetCurrentMapKey(), save.mapSeed, save.savedExitToNext, save.savedEntranceFromPrev, true);
        }
    }

    private bool TryRestorePersistentPortals(MapData map)
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        var save = gm.GetOrCreateFieldMapData(gm.GetCurrentMapKey());
        if (!save.hasSavedPortalCells) return false;

        Vector2Int ex = save.savedExitToNext;
        Vector2Int en = save.savedEntranceFromPrev;
        if (ex == Vector2Int.zero || en == Vector2Int.zero) return false;
        if (!map.InBounds(ex.x, ex.y) || !map.InBounds(en.x, en.y))
            return false;

        // 벽으로 막혀 있어도 통로는 항상 열림
        map.SetTile(ex.x, ex.y, TileType.Road);
        map.SetTile(en.x, en.y, TileType.Road);
        map.exitToNextMap = ex;
        map.entranceFromPrevMap = en;
        CreateRoadArea(map, ex);
        CreateRoadArea(map, en);

        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 영구 통로 복원: 출구 {ex}, 입구 {en}");

        return true;
    }

    private void FindMapConnectionPositions(MapData map)
    {
        var gm = GameManager.Instance;
        bool fromPortal = gm != null && gm.playerData.hasPendingEntranceHint;

        if (fromPortal)
        {
            // 이전 맵에서 나온 방향의 *반대편*에 입구 — 출구는 그 반대편(다음으로 이어지는 쪽)
            EdgeSide enterFrom = gm.playerData.enterNewFieldFromEdge;
            float alignT = gm.playerData.entranceAlignT;

            map.entranceFromPrevMap = FindWalkableOnEdgeAligned(map, enterFrom, alignT);
            if (map.entranceFromPrevMap == Vector2Int.zero)
                map.entranceFromPrevMap = FindAnyWalkableNearEdge(map, enterFrom);

            EdgeSide exitSide = FieldTransitionUtil.Opposite(enterFrom);
            map.exitToNextMap = FindEdgePortalWalkable(map, exitSide);
            if (map.exitToNextMap == Vector2Int.zero)
                map.exitToNextMap = FindEdgeWalkablePosition(map, exitSide);
            if (map.exitToNextMap == Vector2Int.zero)
                map.exitToNextMap = FindAnyWalkableNearEdge(map, exitSide);
        }
        else
        {
            // 첫 입장: 출구/입구 기본(동→서) — 플레이어는 중앙 스폰, 입구는 장식·되돌아가기용
            map.exitToNextMap = FindEdgePortalWalkable(map, EdgeSide.Right);
            if (map.exitToNextMap == Vector2Int.zero)
                map.exitToNextMap = FindEdgeWalkablePosition(map, EdgeSide.Right);
            if (map.exitToNextMap == Vector2Int.zero)
                map.exitToNextMap = FindAnyWalkableNearEdge(map, EdgeSide.Right);

            map.entranceFromPrevMap = FindEdgePortalWalkable(map, EdgeSide.Left);
            if (map.entranceFromPrevMap == Vector2Int.zero)
                map.entranceFromPrevMap = FindEdgeWalkablePosition(map, EdgeSide.Left);
            if (map.entranceFromPrevMap == Vector2Int.zero)
                map.entranceFromPrevMap = FindAnyWalkableNearEdge(map, EdgeSide.Left);
        }

        if (map.exitToNextMap != Vector2Int.zero)
            CreateRoadArea(map, map.exitToNextMap);
        if (map.entranceFromPrevMap != Vector2Int.zero)
            CreateRoadArea(map, map.entranceFromPrevMap);

        if (logGeneration)
        {
            Debug.Log($"[FieldMapGenerator] 맵 연결 — 포털연동:{fromPortal}, 출구(다음): {map.exitToNextMap}, 입구(이전): {map.entranceFromPrevMap}");
            if (map.exitToNextMap == Vector2Int.zero && fieldDefinition != null && fieldDefinition.hasNextFieldExit)
                Debug.LogWarning("[FieldMapGenerator] 다음 필드로 나가는 출구 칸을 찾지 못했습니다.");
        }
    }

    /// <summary>alignT: 해당 변을 따라 0~1 (좌우 변=세로 위치, 상하 변=가로 위치)</summary>
    private Vector2Int FindWalkableOnEdgeAligned(MapData map, EdgeSide edge, float alignT)
    {
        alignT = Mathf.Clamp01(alignT);
        int w = map.width;
        int h = map.height;

        switch (edge)
        {
            case EdgeSide.Left:
            {
                int targetY = h > 3 ? Mathf.Clamp(Mathf.RoundToInt(1 + alignT * (h - 3)), 1, h - 2) : h / 2;
                for (int depth = 1; depth <= 10; depth++)
                {
                    int x = depth;
                    if (x >= w - 1) break;
                    for (int spread = 0; spread < h; spread++)
                    {
                        int yA = targetY + spread;
                        if (yA >= 1 && yA < h - 1 && map.IsWalkable(x, yA))
                            return new Vector2Int(x, yA);
                        if (spread == 0) continue;
                        int yB = targetY - spread;
                        if (yB >= 1 && yB < h - 1 && map.IsWalkable(x, yB))
                            return new Vector2Int(x, yB);
                    }
                }
                break;
            }
            case EdgeSide.Right:
            {
                int targetY = h > 3 ? Mathf.Clamp(Mathf.RoundToInt(1 + alignT * (h - 3)), 1, h - 2) : h / 2;
                for (int depth = 1; depth <= 10; depth++)
                {
                    int x = w - 1 - depth;
                    if (x < 1) break;
                    for (int spread = 0; spread < h; spread++)
                    {
                        int yA = targetY + spread;
                        if (yA >= 1 && yA < h - 1 && map.IsWalkable(x, yA))
                            return new Vector2Int(x, yA);
                        if (spread == 0) continue;
                        int yB = targetY - spread;
                        if (yB >= 1 && yB < h - 1 && map.IsWalkable(x, yB))
                            return new Vector2Int(x, yB);
                    }
                }
                break;
            }
            case EdgeSide.Bottom:
            {
                int targetX = w > 3 ? Mathf.Clamp(Mathf.RoundToInt(1 + alignT * (w - 3)), 1, w - 2) : w / 2;
                for (int depth = 1; depth <= 10; depth++)
                {
                    int y = depth;
                    if (y >= h - 1) break;
                    for (int spread = 0; spread < w; spread++)
                    {
                        int xA = targetX + spread;
                        if (xA >= 1 && xA < w - 1 && map.IsWalkable(xA, y))
                            return new Vector2Int(xA, y);
                        if (spread == 0) continue;
                        int xB = targetX - spread;
                        if (xB >= 1 && xB < w - 1 && map.IsWalkable(xB, y))
                            return new Vector2Int(xB, y);
                    }
                }
                break;
            }
            case EdgeSide.Top:
            {
                int targetX = w > 3 ? Mathf.Clamp(Mathf.RoundToInt(1 + alignT * (w - 3)), 1, w - 2) : w / 2;
                for (int depth = 1; depth <= 10; depth++)
                {
                    int y = h - 1 - depth;
                    if (y < 1) break;
                    for (int spread = 0; spread < w; spread++)
                    {
                        int xA = targetX + spread;
                        if (xA >= 1 && xA < w - 1 && map.IsWalkable(xA, y))
                            return new Vector2Int(xA, y);
                        if (spread == 0) continue;
                        int xB = targetX - spread;
                        if (xB >= 1 && xB < w - 1 && map.IsWalkable(xB, y))
                            return new Vector2Int(xB, y);
                    }
                }
                break;
            }
        }

        return Vector2Int.zero;
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
            AttachSpawnToField(enemy);

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

        usedResourceCells.Clear();

        int totalResources = 0;

        totalResources += SpawnResourceType(GetResourcePrefabPool(seaweedPrefabs, seaweedPrefab), fieldDefinition.seaweedCount, "Seaweed");
        totalResources += SpawnResourceType(GetResourcePrefabPool(coralPrefabs, coralPrefab), fieldDefinition.coralCount, "Coral");
        totalResources += SpawnResourceType(GetResourcePrefabPool(rockPrefabs, rockPrefab), fieldDefinition.rockCount, "Rock");
        totalResources += SpawnResourceType(GetResourcePrefabPool(sandPrefabs, sandPrefab), fieldDefinition.sandCount, "Sand");
        totalResources += SpawnResourceType(GetResourcePrefabPool(spongePrefabs, spongePrefab), fieldDefinition.spongeCount, "Sponge");

        if (logGeneration)
            Debug.Log($"[FieldMapGenerator] 자원 스폰 완료: 총 {totalResources}개");
    }

    /// <summary>
    /// 자원 타입별 스폰
    /// </summary>
    private int SpawnResourceType(GameObject[] prefabs, int count, string resourceName)
    {
        if (prefabs == null || prefabs.Length == 0 || count <= 0) return 0;

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(30, count * 20);
        while (spawned < count && attempts < maxAttempts)
        {
            attempts++;
            Vector2Int pos = FindRandomWalkablePosition(currentMap, currentMap.playerSpawnPos, 2f);
            if (pos == Vector2Int.zero) continue;
            if (usedResourceCells.Contains(pos)) continue;

            GameObject prefab = GetRandomPrefab(prefabs);
            if (prefab == null) continue;

            Vector3 worldPos = gridBoard.CellToWorld(pos);
            GameObject resource = Instantiate(prefab, worldPos, Quaternion.identity);
            resource.name = $"{resourceName}_{prefab.name}_{pos.x}_{pos.y}";
            AttachSpawnToField(resource);

            spawnedObjects.Add(resource);
            usedResourceCells.Add(pos);
            spawned++;
        }

        if (logGeneration && spawned > 0)
        {
            if (spawned < count)
                Debug.LogWarning($"[FieldMapGenerator] {resourceName} 스폰 부족: {spawned}/{count} (빈 셀 부족 가능)");
            else
                Debug.Log($"[FieldMapGenerator] {resourceName} 스폰: {spawned}개");
        }

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
            if (string.IsNullOrEmpty(dungeon.dungeonID))
            {
                if (logGeneration)
                    Debug.LogWarning($"[FieldMapGenerator] 던전 정의 '{dungeon.dungeonName}' 의 dungeonID 가 비어 있습니다. 입장이 안 될 수 있습니다.");
            }

            Vector2Int pos = currentMap.dungeonEntrances[i];
            Vector3 worldPos = gridBoard.CellToWorld(pos);

            GameObject entrance = Instantiate(entrancePrefab, worldPos, Quaternion.identity);
            entrance.name = $"DungeonEntrance_{dungeon.dungeonID}";
            AttachSpawnToField(entrance);

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
            AttachSpawnToField(entrance);

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
            AttachSpawnToField(enemy);
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
                    GameManager.Instance.playerData.hasPendingEntranceHint = false;
                    return;
                }
            }

            Vector3 spawnWorld = gridBoard.CellToWorld(currentMap.playerSpawnPos);
            player.position = spawnWorld;

            if (GameManager.Instance != null && GameManager.Instance.playerData.hasPendingEntranceHint)
                GameManager.Instance.playerData.hasPendingEntranceHint = false;

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
        // 다음 필드 출구 (nextField는 이동 로직용 — 없어도 프리팹·위치만 있으면 오브젝트는 깔아서 찾기 쉽게 함)
        if (fieldDefinition.hasNextFieldExit && mapExitPrefab != null)
        {
            if (currentMap.exitToNextMap != Vector2Int.zero)
            {
                Vector3 exitWorld = gridBoard.CellToWorld(currentMap.exitToNextMap);
                GameObject exitObj = Instantiate(mapExitPrefab, exitWorld, Quaternion.identity);
                exitObj.name = "MapExit_ToNext";
                AttachSpawnToField(exitObj);

                var trigger = exitObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = exitObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = true;
                EdgeSide ex = FieldTransitionUtil.InferClosestEdge(currentMap.exitToNextMap, currentMap.width, currentMap.height);
                trigger.ConfigurePortal(ex, currentMap.exitToNextMap, currentMap.width, currentMap.height);

                spawnedObjects.Add(exitObj);

                if (logGeneration)
                {
                    string dest = fieldDefinition.nextField != null ? fieldDefinition.nextField.fieldName : "(nextField 미지정 — GameManager 진행만)";
                    Debug.Log($"[FieldMapGenerator] 다음 필드 출구 생성: -> {dest} @ cell {currentMap.exitToNextMap}");
                }

                if (fieldDefinition.nextField == null && logGeneration)
                    Debug.LogWarning("[FieldMapGenerator] Field Definition의 Next Field 에셋을 지정해야 WorldProgress 기반 다음 필드 이동이 됩니다.");
            }
            else if (logGeneration)
                Debug.LogWarning("[FieldMapGenerator] 출구 셀을 찾지 못해 MapExit 프리팹을 스폰하지 못했습니다.");
        }
        else if (fieldDefinition != null && fieldDefinition.hasNextFieldExit && mapExitPrefab == null && logGeneration)
            Debug.LogWarning("[FieldMapGenerator] hasNextFieldExit 이지만 FieldMapGenerator의 Map Exit Prefab 이 비어 있습니다.");

        // 이전 필드 입구
        if (fieldDefinition.previousField != null && mapEntrancePrefab != null)
        {
            if (currentMap.entranceFromPrevMap != Vector2Int.zero)
            {
                Vector3 entranceWorld = gridBoard.CellToWorld(currentMap.entranceFromPrevMap);
                GameObject entranceObj = Instantiate(mapEntrancePrefab, entranceWorld, Quaternion.identity);
                entranceObj.name = "MapEntrance_FromPrev";
                AttachSpawnToField(entranceObj);

                var trigger = entranceObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = entranceObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = false;
                EdgeSide en = FieldTransitionUtil.InferClosestEdge(currentMap.entranceFromPrevMap, currentMap.width, currentMap.height);
                trigger.ConfigurePortal(en, currentMap.entranceFromPrevMap, currentMap.width, currentMap.height);

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
                AttachSpawnToField(exitObj);

                var trigger = exitObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = exitObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = true;
                EdgeSide ex = FieldTransitionUtil.InferClosestEdge(currentMap.exitToNextMap, currentMap.width, currentMap.height);
                trigger.ConfigurePortal(ex, currentMap.exitToNextMap, currentMap.width, currentMap.height);

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
                AttachSpawnToField(entranceObj);

                var trigger = entranceObj.GetComponent<MapExitTrigger>();
                if (trigger == null)
                    trigger = entranceObj.AddComponent<MapExitTrigger>();
                trigger.isNextMap = false;
                EdgeSide en = FieldTransitionUtil.InferClosestEdge(currentMap.entranceFromPrevMap, currentMap.width, currentMap.height);
                trigger.ConfigurePortal(en, currentMap.entranceFromPrevMap, currentMap.width, currentMap.height);

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

    /// <summary>
    /// 맵 둘레 안쪽 한 줄에서, 양옆(또는 위아래)이 벽인 한 칸 통로 — 가장자리 벽 사이 출구 느낌.
    /// </summary>
    private Vector2Int FindEdgePortalWalkable(MapData map, EdgeSide side)
    {
        switch (side)
        {
            case EdgeSide.Right:
                for (int y = 2; y < map.height - 2; y++)
                {
                    int x = map.width - 2;
                    if (!map.IsWalkable(x, y)) continue;
                    if (map.GetTile(x, y - 1) == TileType.Wall && map.GetTile(x, y + 1) == TileType.Wall)
                        return new Vector2Int(x, y);
                }
                break;
            case EdgeSide.Left:
                for (int y = 2; y < map.height - 2; y++)
                {
                    int x = 1;
                    if (!map.IsWalkable(x, y)) continue;
                    if (map.GetTile(x, y - 1) == TileType.Wall && map.GetTile(x, y + 1) == TileType.Wall)
                        return new Vector2Int(x, y);
                }
                break;
            case EdgeSide.Top:
                for (int x = 2; x < map.width - 2; x++)
                {
                    int y = map.height - 2;
                    if (!map.IsWalkable(x, y)) continue;
                    if (map.GetTile(x - 1, y) == TileType.Wall && map.GetTile(x + 1, y) == TileType.Wall)
                        return new Vector2Int(x, y);
                }
                break;
            case EdgeSide.Bottom:
                for (int x = 2; x < map.width - 2; x++)
                {
                    int y = 1;
                    if (!map.IsWalkable(x, y)) continue;
                    if (map.GetTile(x - 1, y) == TileType.Wall && map.GetTile(x + 1, y) == TileType.Wall)
                        return new Vector2Int(x, y);
                }
                break;
        }

        return Vector2Int.zero;
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

    /// <summary>
    /// 바깥에서 안쪽으로 여러 줄을 훑어 walkable 칸을 찾습니다.
    /// (기존 FindEdgeWalkablePosition은 한 줄만 봐서 그 열이 전부 벽이면 출구 좌표가 0이 됐음)
    /// </summary>
    private Vector2Int FindAnyWalkableNearEdge(MapData map, EdgeSide side)
    {
        int maxDepth = Mathf.Max(4, Mathf.Min(map.width, map.height) / 2);

        switch (side)
        {
            case EdgeSide.Right:
                for (int depth = 1; depth <= maxDepth; depth++)
                {
                    int x = map.width - 1 - depth;
                    if (x < 1) break;
                    for (int y = 1; y < map.height - 1; y++)
                    {
                        if (map.IsWalkable(x, y))
                            return new Vector2Int(x, y);
                    }
                }
                break;
            case EdgeSide.Left:
                for (int depth = 1; depth <= maxDepth; depth++)
                {
                    int x = depth;
                    if (x >= map.width - 1) break;
                    for (int y = 1; y < map.height - 1; y++)
                    {
                        if (map.IsWalkable(x, y))
                            return new Vector2Int(x, y);
                    }
                }
                break;
            case EdgeSide.Top:
                for (int depth = 1; depth <= maxDepth; depth++)
                {
                    int y = map.height - 1 - depth;
                    if (y < 1) break;
                    for (int x = 1; x < map.width - 1; x++)
                    {
                        if (map.IsWalkable(x, y))
                            return new Vector2Int(x, y);
                    }
                }
                break;
            case EdgeSide.Bottom:
                for (int depth = 1; depth <= maxDepth; depth++)
                {
                    int y = depth;
                    if (y >= map.height - 1) break;
                    for (int x = 1; x < map.width - 1; x++)
                    {
                        if (map.IsWalkable(x, y))
                            return new Vector2Int(x, y);
                    }
                }
                break;
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

    private GameObject[] GetResourcePrefabPool(GameObject[] prefabs, GameObject fallbackPrefab)
    {
        if (prefabs != null && prefabs.Length > 0)
            return prefabs;

        if (fallbackPrefab != null)
            return new[] { fallbackPrefab };

        return null;
    }

    private GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int target = Random.Range(0, validCount);
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;
            if (target == 0) return prefabs[i];
            target--;
        }

        return null;
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