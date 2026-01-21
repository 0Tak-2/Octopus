using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던전 컨트롤러
/// 층 생성, 적 스폰, 클리어 판정
/// </summary>
public class DungeonController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GridBoard gridBoard;
    public Tilemap floorTilemap;
    public Tilemap wallTilemap;
    public MapRenderer mapRenderer;

    [Header("Dungeon Settings")]
    public DungeonDefinition dungeonDefinition;

    [Header("Grid Generation Settings")]
    [Tooltip("격자 너비 (방 개수)")]
    [Range(3, 6)] public int gridWidth = 4;
    [Tooltip("격자 높이 (방 개수)")]
    [Range(3, 6)] public int gridHeight = 4;

    [Header("Room Count")]
    [Tooltip("최소 방 개수")]
    [Range(5, 20)] public int minRooms = 8;
    [Tooltip("최대 방 개수")]
    [Range(5, 20)] public int maxRooms = 12;

    [Header("Room Size")]
    [Tooltip("최소 방 크기 (타일)")]
    [Range(3, 12)] public int minRoomSize = 5;
    [Tooltip("최대 방 크기 (타일)")]
    [Range(3, 15)] public int maxRoomSize = 8;

    [Header("Corridor Length")]
    [Tooltip("최소 복도 길이 (타일)")]
    [Range(2, 15)] public int minCorridorLength = 3;
    [Tooltip("최대 복도 길이 (타일)")]
    [Range(2, 20)] public int maxCorridorLength = 7;

    [Header("Corridor Width")]
    [Tooltip("복도 넓이 (타일) - 실제 복도 두께")]
    [Range(1, 5)] public int corridorWidth = 1;

    [Header("Prefabs")]
    public GameObject stairsPrefab;
    public GameObject exitPortalPrefab;
    public GameObject itemMarkerPrefab; // 임시 아이템 표시

    [Header("Debug")]
    public bool logGeneration = true;

    // 현재 던전 상태
    private int currentFloor = 1;
    private int maxFloors;
    private DungeonFloorData currentFloorData;
    private Dictionary<int, DungeonFloorData> allFloors = new Dictionary<int, DungeonFloorData>();

    // 스폰된 오브젝트 관리
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private GameObject bossInstance;
    private GameObject stairsInstance;
    private GameObject exitInstance;

    private void Start()
    {
        // Player 찾기 (DontDestroyOnLoad 때문에 씬에 없을 수 있음)
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // GridBoard 찾기
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();

        if (dungeonDefinition == null)
        {
            Debug.LogError("[DungeonController] DungeonDefinition이 없습니다!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[DungeonController] Player를 찾을 수 없습니다! Player 태그를 확인하세요.");
            return;
        }

        if (gridBoard == null)
        {
            Debug.LogError("[DungeonController] GridBoard를 찾을 수 없습니다!");
            return;
        }

        // 던전 층수 결정
        maxFloors = Random.Range(dungeonDefinition.minFloors, dungeonDefinition.maxFloors + 1);

        if (logGeneration)
            Debug.Log($"[DungeonController] {dungeonDefinition.dungeonName} 시작 (총 {maxFloors}층)");

        // 1층 생성
        GenerateFloor(1);

        // 플레이어 데이터 복원 (HP 등)
        GameManager.Instance?.RestorePlayerData();
    }

    /// <summary>
    /// 층 생성 및 렌더링
    /// </summary>
    private void GenerateFloor(int floorNumber)
    {
        if (logGeneration)
            Debug.Log($"[DungeonController] {floorNumber}층 생성 중...");

        // 이전 오브젝트 정리
        CleanupFloor();

        // 격자 기반 던전 생성
        int seed = System.DateTime.Now.Millisecond + floorNumber * 1000;
        currentFloorData = DungeonGridGenerator.GenerateFloor(floorNumber, gridWidth, gridHeight, seed,
            minRooms, maxRooms, minRoomSize, maxRoomSize, minCorridorLength, maxCorridorLength, corridorWidth);
        allFloors[floorNumber] = currentFloorData;
        currentFloor = floorNumber;

        // 엘리트 방 지정
        AssignEliteRoom();

        // 맵 렌더링
        RenderFloor(currentFloorData);

        // 플레이어 스폰
        SpawnPlayer(currentFloorData.startRoom);

        // 적 스폰
        SpawnEnemies();

        // 계단/출구 생성
        SpawnStairsAndExit();

        // 아이템 마커 생성
        SpawnItemMarkers();

        if (logGeneration)
            Debug.Log($"[DungeonController] {floorNumber}층 생성 완료 (방 {currentFloorData.rooms.Count}개)");
    }

    /// <summary>
    /// 엘리트 방 지정
    /// - 3층 던전: 2층에 반드시 1마리
    /// - 2층 던전: 1층에 50% 확률
    /// </summary>
    private void AssignEliteRoom()
    {
        bool shouldHaveElite = false;

        if (maxFloors == 3 && currentFloor == 2)
        {
            shouldHaveElite = true; // 3층 던전의 2층
        }
        else if (maxFloors == 2 && currentFloor == 1)
        {
            shouldHaveElite = Random.value < 0.5f; // 2층 던전의 1층, 50% 확률
        }

        if (shouldHaveElite)
        {
            // 일반 몹 방 중 하나를 엘리트 방으로 변경
            List<DungeonRoom> normalRooms = currentFloorData.rooms.FindAll(r => r.roomType == DungeonRoomType.Normal);
            if (normalRooms.Count > 0)
            {
                int eliteIndex = Random.Range(0, normalRooms.Count);
                normalRooms[eliteIndex].roomType = DungeonRoomType.Elite;

                if (logGeneration)
                    Debug.Log($"[DungeonController] 엘리트 방 지정: Room {normalRooms[eliteIndex].id}");
            }
        }
    }

    /// <summary>
    /// 맵 렌더링
    /// </summary>
    private void RenderFloor(DungeonFloorData floor)
    {
        if (mapRenderer != null)
        {
            // MapData로 변환
            MapData mapData = new MapData(floor.width, floor.height);
            for (int x = 0; x < floor.width; x++)
            {
                for (int y = 0; y < floor.height; y++)
                {
                    // 0=벽(Wall), 1=바닥(Empty), 2=복도(Road)
                    if (floor.tiles[x, y] == 0)
                        mapData.tiles[x, y] = TileType.Wall;
                    else if (floor.tiles[x, y] == 2)
                        mapData.tiles[x, y] = TileType.Road;
                    else
                        mapData.tiles[x, y] = TileType.Empty;
                }
            }

            mapRenderer.RenderMap(mapData);
        }

        // ✅ GridBoard 업데이트 (이동 가능 타일 설정)
        if (gridBoard != null)
        {
            // GridBoard 크기 업데이트
            gridBoard.width = floor.width;
            gridBoard.height = floor.height;

            // 막힌 타일 리스트 초기화
            gridBoard.blockedMoveCells.Clear();
            gridBoard.blockedVisionCells.Clear();

            // 벽 타일을 막힌 타일로 등록
            for (int x = 0; x < floor.width; x++)
            {
                for (int y = 0; y < floor.height; y++)
                {
                    if (floor.tiles[x, y] == 0) // 벽
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        gridBoard.blockedMoveCells.Add(cell);
                        gridBoard.blockedVisionCells.Add(cell);
                    }
                }
            }

            // GridBoard 갱신
            gridBoard.RefreshBlockedCells();

            if (logGeneration)
                Debug.Log($"[DungeonController] GridBoard 업데이트: {gridBoard.blockedMoveCells.Count}개 벽 타일");
        }
    }

    /// <summary>
    /// 플레이어 스폰
    /// </summary>
    private void SpawnPlayer(DungeonRoom startRoom)
    {
        if (player == null || gridBoard == null) return;

        // 시작 방 내부에서 이동 가능한 타일 찾기
        Vector2Int spawnPos = FindWalkablePositionInRoom(startRoom);

        Vector3 spawnWorld = gridBoard.CellToWorld(spawnPos);
        player.position = spawnWorld;

        // PlayerGridMover 동기화
        PlayerGridMover mover = player.GetComponent<PlayerGridMover>();
        if (mover != null)
        {
            mover.SetCurrentCell(spawnPos);
        }

        if (logGeneration)
            Debug.Log($"[DungeonController] 플레이어 스폰: {spawnPos}");
    }

    /// <summary>
    /// 방 내부에서 이동 가능한 위치 찾기
    /// </summary>
    private Vector2Int FindWalkablePositionInRoom(DungeonRoom room)
    {
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

        // 방 중심부터 시작해서 이동 가능한 타일 찾기
        for (int radius = 0; radius < 5; radius++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int x = room.x + dx;
                    int y = room.y + dy;

                    // 방 경계 내부이고 이동 가능한 타일
                    if (x >= minX && x < maxX && y >= minY && y < maxY)
                    {
                        if (currentFloorData.IsWalkable(x, y))
                        {
                            return new Vector2Int(x, y);
                        }
                    }
                }
            }
        }

        // 찾지 못하면 방 중심 반환 (폴백)
        return room.Center;
    }

    /// <summary>
    /// 적 스폰
    /// </summary>
    private void SpawnEnemies()
    {
        if (dungeonDefinition == null) return;

        int normalRoomCount = 0;
        int eliteRoomCount = 0;
        int bossRoomCount = 0;
        int itemRoomCount = 0;
        int stairsRoomCount = 0;

        foreach (var room in currentFloorData.rooms)
        {
            Debug.Log($"[DungeonController] Room {room.id}: Type = {room.roomType}");

            // Start와 Exit 방은 스킵
            if (room.roomType == DungeonRoomType.Start || room.roomType == DungeonRoomType.Exit)
            {
                continue;
            }

            // Boss 방
            if (room.roomType == DungeonRoomType.Boss)
            {
                bossRoomCount++;
                SpawnBoss(room);
            }
            // Elite 방
            else if (room.roomType == DungeonRoomType.Elite)
            {
                eliteRoomCount++;
                SpawnElite(room);
            }
            // Item 방 - 일반 몹 + 아이템
            else if (room.roomType == DungeonRoomType.Item)
            {
                itemRoomCount++;
                SpawnNormalEnemies(room);
            }
            // Stairs 방 - 일반 몹
            else if (room.roomType == DungeonRoomType.Stairs)
            {
                stairsRoomCount++;
                SpawnNormalEnemies(room);
            }
            // Normal 방
            else if (room.roomType == DungeonRoomType.Normal)
            {
                normalRoomCount++;
                SpawnNormalEnemies(room);
            }
        }

        Debug.Log($"[DungeonController] 방 타입 통계: Normal={normalRoomCount}, Elite={eliteRoomCount}, Boss={bossRoomCount}, Item={itemRoomCount}, Stairs={stairsRoomCount}");
        Debug.Log($"[DungeonController] 일반 몹 스폰 방: {normalRoomCount + itemRoomCount + stairsRoomCount}개");
    }

    /// <summary>
    /// 일반 몹 스폰
    /// </summary>
    private void SpawnNormalEnemies(DungeonRoom room)
    {
        if (dungeonDefinition.normalEnemyPrefabs == null || dungeonDefinition.normalEnemyPrefabs.Length == 0)
        {
            Debug.LogWarning($"[DungeonController] normalEnemyPrefabs가 비어있습니다!");
            return;
        }

        int enemyCount = Random.Range(dungeonDefinition.minEnemiesPerRoom, dungeonDefinition.maxEnemiesPerRoom + 1);

        Debug.Log($"[DungeonController] Room {room.id}에 일반 몹 {enemyCount}마리 스폰");

        for (int i = 0; i < enemyCount; i++)
        {
            // 랜덤 적 선택
            GameObject enemyPrefab = dungeonDefinition.normalEnemyPrefabs[Random.Range(0, dungeonDefinition.normalEnemyPrefabs.Length)];

            // 방 내부 랜덤 위치
            Vector2Int spawnPos = GetRandomPositionInRoom(room);
            Vector3 worldPos = gridBoard.CellToWorld(spawnPos);

            GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            enemy.name = $"Enemy_{room.id}_{i}";
            spawnedEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// 엘리트 스폰
    /// </summary>
    private void SpawnElite(DungeonRoom room)
    {
        if (dungeonDefinition.eliteEnemyPrefab == null) return;

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        GameObject elite = Instantiate(dungeonDefinition.eliteEnemyPrefab, worldPos, Quaternion.identity);
        elite.name = $"Elite_{room.id}";
        spawnedEnemies.Add(elite);

        if (logGeneration)
            Debug.Log($"[DungeonController] 엘리트 스폰: Room {room.id}");
    }

    /// <summary>
    /// 보스 스폰
    /// </summary>
    private void SpawnBoss(DungeonRoom room)
    {
        if (dungeonDefinition.bossPrefab == null) return;

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        bossInstance = Instantiate(dungeonDefinition.bossPrefab, worldPos, Quaternion.identity);
        bossInstance.name = $"Boss_Floor{currentFloor}";

        if (logGeneration)
            Debug.Log($"[DungeonController] 보스 스폰: Room {room.id}");
    }

    /// <summary>
    /// 계단/출구 생성
    /// </summary>
    private void SpawnStairsAndExit()
    {
        // 계단 (다음 층으로, 마지막 층 제외)
        if (currentFloor < maxFloors && currentFloorData.stairsRoom != null && stairsPrefab != null)
        {
            Vector3 stairsPos = gridBoard.CellToWorld(currentFloorData.stairsRoom.Center);
            stairsInstance = Instantiate(stairsPrefab, stairsPos, Quaternion.identity);

            var stairsInteract = stairsInstance.GetComponent<DungeonStairsInteract>();
            if (stairsInteract == null)
                stairsInteract = stairsInstance.AddComponent<DungeonStairsInteract>();
            stairsInteract.dungeonController = this;
        }

        // 출구 (필드 복귀, 모든 층에 생성)
        if (currentFloorData.exitRoom != null && exitPortalPrefab != null)
        {
            Vector3 exitPos = gridBoard.CellToWorld(currentFloorData.exitRoom.Center);
            exitInstance = Instantiate(exitPortalPrefab, exitPos, Quaternion.identity);

            var exitInteract = exitInstance.GetComponent<DungeonExitPortal>();
            if (exitInteract == null)
                exitInteract = exitInstance.AddComponent<DungeonExitPortal>();

            if (logGeneration)
                Debug.Log($"[DungeonController] 출구 생성: {currentFloor}층");
        }
    }

    /// <summary>
    /// 아이템 마커 생성 (임시)
    /// </summary>
    private void SpawnItemMarkers()
    {
        if (itemMarkerPrefab == null) return;

        foreach (var room in currentFloorData.rooms)
        {
            if (room.roomType == DungeonRoomType.Item)
            {
                Vector3 markerPos = gridBoard.CellToWorld(room.Center);
                GameObject marker = Instantiate(itemMarkerPrefab, markerPos, Quaternion.identity);
                marker.name = $"ItemMarker_{room.id}";
                spawnedEnemies.Add(marker); // 정리용으로 리스트에 추가
            }
        }
    }

    /// <summary>
    /// 방 내부 랜덤 위치
    /// </summary>
    private Vector2Int GetRandomPositionInRoom(DungeonRoom room)
    {
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

        // 가장자리 피하기
        int x = Random.Range(minX + 1, maxX - 1);
        int y = Random.Range(minY + 1, maxY - 1);

        return new Vector2Int(x, y);
    }

    /// <summary>
    /// 이전 층 오브젝트 정리
    /// </summary>
    private void CleanupFloor()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        spawnedEnemies.Clear();

        if (bossInstance != null) Destroy(bossInstance);
        if (stairsInstance != null) Destroy(stairsInstance);
        if (exitInstance != null) Destroy(exitInstance);
    }

    /// <summary>
    /// 다음 층으로 이동 (외부에서 호출)
    /// </summary>
    public void GoToNextFloor()
    {
        if (currentFloor >= maxFloors)
        {
            Debug.LogWarning("[DungeonController] 이미 마지막 층입니다!");
            return;
        }

        GenerateFloor(currentFloor + 1);
    }

    /// <summary>
    /// 보스 처치 확인 (Update에서 체크)
    /// </summary>
    private void Update()
    {
        // 보스가 죽었고, 마지막 층이면 클리어
        if (bossInstance == null && currentFloor == maxFloors && !IsDungeonCleared())
        {
            OnDungeonCleared();
        }
    }

    private bool dungeonCleared = false;

    private bool IsDungeonCleared()
    {
        return dungeonCleared;
    }

    /// <summary>
    /// 던전 클리어
    /// </summary>
    private void OnDungeonCleared()
    {
        dungeonCleared = true;

        if (logGeneration)
            Debug.Log($"[DungeonController] 던전 클리어! {dungeonDefinition.dungeonName}");

        // 승리 UI 표시 (TODO)
        // 보상 지급 (TODO)

        // 3초 후 자동 복귀
        Invoke(nameof(ReturnToField), 3f);
    }

    /// <summary>
    /// 필드로 복귀
    /// </summary>
    public void ReturnToField()
    {
        string dungeonId = GameManager.Instance.playerData.currentDungeonId;
        GameManager.Instance.ExitDungeonSuccess(dungeonId);
    }
}