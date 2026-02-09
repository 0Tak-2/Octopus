using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 던전 컨트롤러 (층 저장 기능 추가)
/// 층 생성, 적 스폰, 클리어 판정, 층 상태 저장/복원
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
    public GameObject itemMarkerPrefab;

    [Header("Debug")]
    public bool logGeneration = true;

    // 현재 던전 상태
    private int currentFloor = 1;
    private bool _cameFromBelow = true; // true=아래에서 올라옴, false=위에서 내려옴
    private int maxFloors;
    private DungeonFloorData currentFloorData;

    // ✅ 층별 데이터 저장 (시드 포함)
    private Dictionary<int, DungeonFloorData> allFloors = new Dictionary<int, DungeonFloorData>();
    private Dictionary<int, int> floorSeeds = new Dictionary<int, int>(); // 층별 시드 저장

    // 스폰된 오브젝트 관리
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private GameObject bossInstance;
    private GameObject stairsInstance;
    private GameObject exitInstance;

    // 던전 ID (GameManager에서 가져옴)
    private string dungeonId;

    private void Start()
    {
        // Player 찾기
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        // GridBoard 찾기
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();

        // WorldProgressManager에서 현재 던전 자동 가져오기
        if (dungeonDefinition == null && WorldProgressManager.Instance != null)
        {
            var currentDungeon = WorldProgressManager.Instance.GetCurrentDungeon();
            if (currentDungeon != null)
            {
                dungeonDefinition = currentDungeon;
                Debug.Log($"[DungeonController] WorldProgressManager에서 던전 가져옴: {dungeonDefinition.dungeonName}");
            }
        }

        // GameManager에서 던전 ID로 찾기 (fallback)
        if (dungeonDefinition == null && GameManager.Instance != null)
        {
            string dId = GameManager.Instance.playerData.currentDungeonId;
            if (!string.IsNullOrEmpty(dId) && WorldProgressManager.Instance != null)
            {
                var dungeon = WorldProgressManager.Instance.GetDungeonByID(dId);
                if (dungeon != null)
                {
                    dungeonDefinition = dungeon;
                    Debug.Log($"[DungeonController] GameManager ID로 던전 찾음: {dungeonDefinition.dungeonName}");
                }
            }
        }

        // WorldProgressManager에도 현재 던전 동기화
        if (dungeonDefinition != null && WorldProgressManager.Instance != null)
        {
            if (string.IsNullOrEmpty(WorldProgressManager.Instance.CurrentDungeonID))
            {
                WorldProgressManager.Instance.EnterDungeon(dungeonDefinition);
            }
        }

        if (dungeonDefinition == null)
        {
            Debug.LogError("[DungeonController] DungeonDefinition이 없습니다!");
            return;
        }

        if (player == null)
        {
            Debug.LogError("[DungeonController] Player를 찾을 수 없습니다!");
            return;
        }

        if (gridBoard == null)
        {
            Debug.LogError("[DungeonController] GridBoard를 찾을 수 없습니다!");
            return;
        }

        // ✅ GameManager에서 던전 정보 가져오기
        if (GameManager.Instance != null)
        {
            dungeonId = GameManager.Instance.playerData.currentDungeonId;

            // 저장된 던전 데이터 로드
            var savedData = GameManager.Instance.currentDungeonData;
            if (savedData != null && savedData.dungeonId == dungeonId)
            {
                currentFloor = savedData.currentFloor;
                maxFloors = savedData.maxFloors;

                // ✅ 저장된 시드 복원
                for (int i = 0; i < savedData.floorSeeds.Count; i++)
                {
                    floorSeeds[i + 1] = savedData.floorSeeds[i];
                }

                if (logGeneration)
                    Debug.Log($"[DungeonController] 저장된 던전 복원: {dungeonId}, {currentFloor}/{maxFloors}층, 시드 {savedData.floorSeeds.Count}개");
            }
        }

        // 던전 층수 결정 (처음 입장 시에만)
        if (maxFloors == 0)
        {
            maxFloors = Random.Range(dungeonDefinition.minFloors, dungeonDefinition.maxFloors + 1);

            // ✅ GameManager에 maxFloors 저장
            if (GameManager.Instance?.currentDungeonData != null)
            {
                GameManager.Instance.currentDungeonData.maxFloors = maxFloors;
            }
        }

        if (logGeneration)
            Debug.Log($"[DungeonController] {dungeonDefinition.dungeonName} 시작 (총 {maxFloors}층)");

        // 항상 1층부터 새로 생성 (로그라이크 특성: 던전 재입장 시 초기화)
        currentFloor = 1;
        allFloors.Clear();
        floorSeeds.Clear();
        GenerateFloor(1);

        // 플레이어 데이터 복원
        GameManager.Instance?.RestorePlayerStats();
    }

    /// <summary>
    /// 층 로드 또는 생성
    /// </summary>
    private void LoadOrGenerateFloor(int floorNumber)
    {
        // ✅ 이미 생성된 층이 있으면 복원
        if (allFloors.ContainsKey(floorNumber))
        {
            if (logGeneration)
                Debug.Log($"[DungeonController] {floorNumber}층 복원 중...");

            RestoreFloor(floorNumber);
        }
        else
        {
            // 새로 생성
            GenerateFloor(floorNumber);
        }
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

        // ✅ 시드 - GameManager에 저장된 게 있으면 사용, 없으면 새로 생성
        int seed;
        if (floorSeeds.ContainsKey(floorNumber))
        {
            seed = floorSeeds[floorNumber];
        }
        else if (GameManager.Instance?.currentDungeonData != null &&
                 GameManager.Instance.currentDungeonData.floorSeeds.Count >= floorNumber)
        {
            // GameManager에서 복원
            seed = GameManager.Instance.currentDungeonData.floorSeeds[floorNumber - 1];
            floorSeeds[floorNumber] = seed;
        }
        else
        {
            // 새로 생성
            seed = System.DateTime.Now.Millisecond + floorNumber * 1000 + Random.Range(0, 10000);
            floorSeeds[floorNumber] = seed;

            // ✅ GameManager에 저장
            if (GameManager.Instance?.currentDungeonData != null)
            {
                GameManager.Instance.currentDungeonData.SetFloorSeed(floorNumber, seed);
            }
        }

        if (logGeneration)
            Debug.Log($"[DungeonController] {floorNumber}층 시드: {seed}");

        // 격자 기반 던전 생성
        currentFloorData = DungeonGridGenerator.GenerateFloor(floorNumber, gridWidth, gridHeight, seed,
            minRooms, maxRooms, minRoomSize, maxRoomSize, minCorridorLength, maxCorridorLength, corridorWidth);
        allFloors[floorNumber] = currentFloorData;
        currentFloor = floorNumber;

        // 엘리트 방 지정
        AssignEliteRoom();

        // 맵 렌더링
        RenderFloor(currentFloorData);

        // 플레이어 스폰 (이동 방향에 따라 스폰 위치 결정)
        DungeonRoom spawnRoom = _cameFromBelow ? currentFloorData.startRoom : (currentFloorData.stairsRoom ?? currentFloorData.startRoom);
        SpawnPlayer(spawnRoom);

        // 적 스폰 (저장된 상태가 있으면 그걸로)
        SpawnEnemiesWithSavedState();

        // 계단/출구 생성
        SpawnStairsAndExit();

        // 아이템 마커 생성
        SpawnItemMarkers();

        // ✅ GameManager에 현재 층 저장
        SaveCurrentFloorToGameManager();

        if (logGeneration)
            Debug.Log($"[DungeonController] {floorNumber}층 생성 완료 (방 {currentFloorData.rooms.Count}개)");
    }

    /// <summary>
    /// 저장된 층 복원
    /// </summary>
    private void RestoreFloor(int floorNumber)
    {
        // 이전 오브젝트 정리
        CleanupFloor();

        currentFloorData = allFloors[floorNumber];
        currentFloor = floorNumber;

        // 맵 렌더링
        RenderFloor(currentFloorData);

        // 플레이어 스폰 (이동 방향에 따라 스폰 위치 결정)
        DungeonRoom restoreSpawnRoom = _cameFromBelow ? currentFloorData.startRoom : (currentFloorData.stairsRoom ?? currentFloorData.startRoom);
        SpawnPlayer(restoreSpawnRoom);

        // 적 스폰 (저장된 상태로)
        SpawnEnemiesWithSavedState();

        // 계단/출구 생성
        SpawnStairsAndExit();

        // 아이템 마커 생성
        SpawnItemMarkers();

        if (logGeneration)
            Debug.Log($"[DungeonController] {floorNumber}층 복원 완료");
    }

    /// <summary>
    /// 적 스폰 (저장된 상태 반영)
    /// </summary>
    private void SpawnEnemiesWithSavedState()
    {
        if (dungeonDefinition == null) return;

        // GameManager에서 저장된 적 상태 가져오기
        List<EnemyStateSave> savedEnemies = null;
        bool hasSavedData = false;

        if (GameManager.Instance?.currentDungeonData != null)
        {
            savedEnemies = GameManager.Instance.currentDungeonData.GetFloorEnemies(currentFloor);
            hasSavedData = savedEnemies != null && savedEnemies.Count > 0;

            if (logGeneration)
                Debug.Log($"[DungeonController] {currentFloor}층 저장된 적 데이터: {(hasSavedData ? savedEnemies.Count + "마리" : "없음")}");
        }

        // ✅ 저장된 데이터가 있으면 저장된 적들만 스폰 (방 단위가 아닌 전체)
        if (hasSavedData)
        {
            SpawnAllEnemiesFromSavedData(savedEnemies);
        }
        else
        {
            // 저장된 데이터 없음 - 새로 생성
            SpawnAllEnemiesNew();
        }
    }

    /// <summary>
    /// 저장된 데이터로 모든 적 스폰
    /// </summary>
    private void SpawnAllEnemiesFromSavedData(List<EnemyStateSave> savedEnemies)
    {
        foreach (var saved in savedEnemies)
        {
            // 죽은 적은 스폰하지 않음
            if (saved.isDead)
            {
                if (logGeneration)
                    Debug.Log($"[DungeonController] 적 {saved.uniqueId} 죽음 상태, 스폰 안 함");
                continue;
            }

            // 보스
            if (saved.uniqueId.StartsWith("Boss_"))
            {
                if (dungeonDefinition.bossPrefab == null) continue;

                Vector3 worldPos = gridBoard.CellToWorld(saved.gridPosition);
                bossInstance = Instantiate(dungeonDefinition.bossPrefab, worldPos, Quaternion.identity);
                bossInstance.name = saved.uniqueId;
                MarkBossSpawned();

                var boss = bossInstance.GetComponent<EnemyInstance>();
                if (boss != null)
                    boss.currentHP = saved.currentHP;

                if (logGeneration)
                    Debug.Log($"[DungeonController] 보스 복원: HP={saved.currentHP}");
                continue;
            }

            // 엘리트
            if (saved.uniqueId.StartsWith("Elite_"))
            {
                if (dungeonDefinition.eliteEnemyPrefab == null) continue;

                Vector3 worldPos = gridBoard.CellToWorld(saved.gridPosition);
                GameObject elite = Instantiate(dungeonDefinition.eliteEnemyPrefab, worldPos, Quaternion.identity);
                elite.name = saved.uniqueId;
                spawnedEnemies.Add(elite);

                var enemy = elite.GetComponent<EnemyInstance>();
                if (enemy != null)
                    enemy.currentHP = saved.currentHP;

                if (logGeneration)
                    Debug.Log($"[DungeonController] 엘리트 복원: HP={saved.currentHP}");
                continue;
            }

            // 일반 적
            if (saved.uniqueId.StartsWith("Enemy_"))
            {
                // 저장된 Definition 이름으로 올바른 프리팹 찾기
                GameObject enemyPrefab = FindEnemyPrefabByDefinitionName(saved.definitionName);
                if (enemyPrefab == null)
                {
                    Debug.LogWarning($"[DungeonController] 프리팹 못 찾음: {saved.definitionName}");
                    continue;
                }

                Vector3 worldPos = gridBoard.CellToWorld(saved.gridPosition);
                GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
                enemy.name = saved.uniqueId;
                spawnedEnemies.Add(enemy);

                var enemyInstance = enemy.GetComponent<EnemyInstance>();
                if (enemyInstance != null)
                    enemyInstance.currentHP = saved.currentHP;

                if (logGeneration)
                    Debug.Log($"[DungeonController] 적 복원: {saved.uniqueId}, Def={saved.definitionName}, HP={saved.currentHP}");
            }
        }
    }

    /// <summary>
    /// 새로 모든 적 생성
    /// </summary>
    private void SpawnAllEnemiesNew()
    {
        foreach (var room in currentFloorData.rooms)
        {
            if (room.roomType == DungeonRoomType.Start || room.roomType == DungeonRoomType.Exit)
                continue;

            if (room.roomType == DungeonRoomType.Boss)
            {
                SpawnBoss(room);
            }
            else if (room.roomType == DungeonRoomType.Elite)
            {
                SpawnElite(room);
            }
            else if (room.roomType == DungeonRoomType.Normal ||
                     room.roomType == DungeonRoomType.Item ||
                     room.roomType == DungeonRoomType.Stairs)
            {
                SpawnNormalEnemies(room);
            }
        }
    }

    private void SpawnBossWithState(DungeonRoom room, List<EnemyStateSave> savedEnemies)
    {
        if (dungeonDefinition.bossPrefab == null) return;

        string enemyId = $"Boss_Floor{currentFloor}";
        var savedState = savedEnemies?.Find(x => x.uniqueId == enemyId);

        // 죽었으면 스폰 안 함
        if (savedState != null && savedState.isDead)
        {
            if (logGeneration)
                Debug.Log($"[DungeonController] 보스 이미 죽음, 스폰 안 함");
            return;
        }

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        bossInstance = Instantiate(dungeonDefinition.bossPrefab, worldPos, Quaternion.identity);
        bossInstance.name = enemyId;

        // HP 복원
        if (savedState != null)
        {
            var enemy = bossInstance.GetComponent<EnemyInstance>();
            if (enemy != null)
                enemy.currentHP = savedState.currentHP;
        }
    }

    private void SpawnEliteWithState(DungeonRoom room, List<EnemyStateSave> savedEnemies)
    {
        if (dungeonDefinition.eliteEnemyPrefab == null) return;

        string enemyId = $"Elite_{room.id}";
        var savedState = savedEnemies?.Find(x => x.uniqueId == enemyId);

        if (savedState != null && savedState.isDead)
            return;

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        GameObject elite = Instantiate(dungeonDefinition.eliteEnemyPrefab, worldPos, Quaternion.identity);
        elite.name = enemyId;
        spawnedEnemies.Add(elite);

        if (savedState != null)
        {
            var enemy = elite.GetComponent<EnemyInstance>();
            if (enemy != null)
                enemy.currentHP = savedState.currentHP;
        }
    }

    private void SpawnNormalEnemiesWithState(DungeonRoom room, List<EnemyStateSave> savedEnemies)
    {
        if (dungeonDefinition.normalEnemyPrefabs == null || dungeonDefinition.normalEnemyPrefabs.Length == 0)
            return;

        // 저장된 적이 있으면 그 개수와 위치로 스폰
        var roomEnemies = savedEnemies?.FindAll(x => x.uniqueId.StartsWith($"Enemy_{room.id}_"));

        if (roomEnemies != null && roomEnemies.Count > 0)
        {
            // 저장된 적 복원
            foreach (var saved in roomEnemies)
            {
                // ✅ 죽은 적은 스폰하지 않음
                if (saved.isDead)
                {
                    if (logGeneration)
                        Debug.Log($"[DungeonController] 적 {saved.uniqueId} 이미 죽음, 스폰 안 함");
                    continue;
                }

                // ✅ 저장된 Definition 이름으로 올바른 프리팹 찾기
                GameObject enemyPrefab = FindEnemyPrefabByDefinitionName(saved.definitionName);
                if (enemyPrefab == null)
                {
                    Debug.LogWarning($"[DungeonController] 프리팹 못 찾음: {saved.definitionName}, 기본 프리팹 사용");
                    enemyPrefab = dungeonDefinition.normalEnemyPrefabs[0];
                }

                Vector3 worldPos = gridBoard.CellToWorld(saved.gridPosition);

                GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
                enemy.name = saved.uniqueId;
                spawnedEnemies.Add(enemy);

                var enemyInstance = enemy.GetComponent<EnemyInstance>();
                if (enemyInstance != null)
                {
                    enemyInstance.currentHP = saved.currentHP;
                    if (logGeneration)
                        Debug.Log($"[DungeonController] 적 복원: {saved.uniqueId}, HP={saved.currentHP}, Def={saved.definitionName}");
                }
            }
        }
        else
        {
            // 새로 생성
            int enemyCount = Random.Range(dungeonDefinition.minEnemiesPerRoom, dungeonDefinition.maxEnemiesPerRoom + 1);

            for (int i = 0; i < enemyCount; i++)
            {
                GameObject enemyPrefab = dungeonDefinition.normalEnemyPrefabs[Random.Range(0, dungeonDefinition.normalEnemyPrefabs.Length)];
                Vector2Int spawnPos = GetRandomPositionInRoom(room);
                Vector3 worldPos = gridBoard.CellToWorld(spawnPos);

                GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
                enemy.name = $"Enemy_{room.id}_{i}";
                spawnedEnemies.Add(enemy);
            }
        }
    }

    /// <summary>
    /// Definition 이름으로 프리팹 찾기
    /// </summary>
    private GameObject FindEnemyPrefabByDefinitionName(string definitionName)
    {
        if (string.IsNullOrEmpty(definitionName)) return null;

        // normalEnemyPrefabs에서 찾기
        foreach (var prefab in dungeonDefinition.normalEnemyPrefabs)
        {
            if (prefab == null) continue;

            var enemyInstance = prefab.GetComponent<EnemyInstance>();
            if (enemyInstance != null && enemyInstance.definition != null)
            {
                if (enemyInstance.definition.name == definitionName)
                    return prefab;
            }
        }

        // eliteEnemyPrefab 체크
        if (dungeonDefinition.eliteEnemyPrefab != null)
        {
            var elite = dungeonDefinition.eliteEnemyPrefab.GetComponent<EnemyInstance>();
            if (elite != null && elite.definition != null && elite.definition.name == definitionName)
                return dungeonDefinition.eliteEnemyPrefab;
        }

        // bossPrefab 체크
        if (dungeonDefinition.bossPrefab != null)
        {
            var boss = dungeonDefinition.bossPrefab.GetComponent<EnemyInstance>();
            if (boss != null && boss.definition != null && boss.definition.name == definitionName)
                return dungeonDefinition.bossPrefab;
        }

        return null;
    }

    /// <summary>
    /// 현재 층 상태를 GameManager에 저장
    /// </summary>
    public void SaveCurrentFloorToGameManager()
    {
        if (GameManager.Instance == null) return;

        var dungeonData = GameManager.Instance.currentDungeonData;
        if (dungeonData == null)
        {
            dungeonData = new DungeonSaveData(dungeonId);
            GameManager.Instance.currentDungeonData = dungeonData;
        }

        dungeonData.currentFloor = currentFloor;

        // 적 상태 저장
        List<EnemyStateSave> enemyStates = new List<EnemyStateSave>();
        int deadCount = 0;
        int aliveCount = 0;

        // 보스
        if (bossInstance != null)
        {
            var boss = bossInstance.GetComponent<EnemyInstance>();
            if (boss != null)
            {
                bool isDead = boss.currentHP <= 0 || !bossInstance.activeSelf;
                enemyStates.Add(new EnemyStateSave(
                    bossInstance.name,
                    boss.definition?.name ?? "Boss",
                    gridBoard.WorldToCell(bossInstance.transform.position),
                    boss.currentHP,
                    isDead
                ));

                if (isDead) deadCount++; else aliveCount++;
            }
        }

        // 일반 적들 (비활성화된 것도 포함!)
        foreach (var enemyObj in spawnedEnemies)
        {
            if (enemyObj == null) continue;

            var enemy = enemyObj.GetComponent<EnemyInstance>();
            if (enemy != null && enemy.definition != null)
            {
                bool isDead = enemy.currentHP <= 0 || !enemyObj.activeSelf;

                enemyStates.Add(new EnemyStateSave(
                    enemyObj.name,
                    enemy.definition.name,
                    gridBoard.WorldToCell(enemyObj.transform.position),
                    enemy.currentHP,
                    isDead
                ));

                if (isDead) deadCount++; else aliveCount++;

                if (logGeneration && isDead)
                    Debug.Log($"[DungeonController] 죽은 적 저장: {enemyObj.name}, HP={enemy.currentHP}, Active={enemyObj.activeSelf}");
            }
        }

        dungeonData.SetFloorEnemies(currentFloor, enemyStates);

        // ✅ GameManager에 던전 데이터도 저장
        GameManager.Instance.SaveDungeonData(dungeonData);

        if (logGeneration)
            Debug.Log($"[DungeonController] {currentFloor}층 상태 저장: 총 {enemyStates.Count}마리 (살아있음: {aliveCount}, 죽음: {deadCount})");
    }

    /// <summary>
    /// 다음 층으로 이동
    /// </summary>
    public void GoToNextFloor()
    {
        if (currentFloor >= maxFloors)
        {
            Debug.LogWarning("[DungeonController] 이미 마지막 층입니다!");
            return;
        }

        // ✅ 현재 층 상태 저장
        SaveCurrentFloorToGameManager();

        // 다음 층으로
        _cameFromBelow = true;
        LoadOrGenerateFloor(currentFloor + 1);
    }

    /// <summary>
    /// 이전 층으로 이동 (추가 기능)
    /// </summary>
    public void GoToPreviousFloor()
    {
        if (currentFloor <= 1)
        {
            Debug.LogWarning("[DungeonController] 이미 1층입니다!");
            return;
        }

        // 현재 층 상태 저장
        SaveCurrentFloorToGameManager();

        // 이전 층으로
        _cameFromBelow = false;
        LoadOrGenerateFloor(currentFloor - 1);
    }

    // =========================================================
    // 기존 메서드들 (수정 없음)
    // =========================================================

    private void AssignEliteRoom()
    {
        bool shouldHaveElite = false;

        if (maxFloors == 3 && currentFloor == 2)
            shouldHaveElite = true;
        else if (maxFloors == 2 && currentFloor == 1)
            shouldHaveElite = Random.value < 0.5f;

        if (shouldHaveElite)
        {
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

    private void RenderFloor(DungeonFloorData floor)
    {
        if (mapRenderer != null)
        {
            MapData mapData = new MapData(floor.width, floor.height);
            for (int x = 0; x < floor.width; x++)
            {
                for (int y = 0; y < floor.height; y++)
                {
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

        if (gridBoard != null)
        {
            gridBoard.width = floor.width;
            gridBoard.height = floor.height;
            gridBoard.blockedMoveCells.Clear();
            gridBoard.blockedVisionCells.Clear();

            for (int x = 0; x < floor.width; x++)
            {
                for (int y = 0; y < floor.height; y++)
                {
                    if (floor.tiles[x, y] == 0)
                    {
                        Vector2Int cell = new Vector2Int(x, y);
                        gridBoard.blockedMoveCells.Add(cell);
                        gridBoard.blockedVisionCells.Add(cell);
                    }
                }
            }

            gridBoard.RefreshBlockedCells();
        }
    }

    private void SpawnPlayer(DungeonRoom room)
    {
        if (player == null || gridBoard == null || room == null) return;

        Vector2Int spawnPos = FindWalkablePositionInRoom(room);
        Vector3 spawnWorld = gridBoard.CellToWorld(spawnPos);
        player.position = spawnWorld;

        // PlayerGridMover 동기화 (이게 없으면 이동 불가!)
        var mover = player.GetComponent<PlayerGridMover>();
        if (mover != null)
        {
            mover.SetCurrentCell(spawnPos);

            // GridBoard 참조도 갱신 (층 전환 시 필수)
            if (mover.grid != gridBoard)
                mover.grid = gridBoard;
        }

        // GridOccupancyRegistry 초기화 (층 전환 시 이전 점유 정보 정리)
        var occupancy = GridOccupancyRegistry.Instance;
        if (occupancy != null)
        {
            occupancy.Release(player);
            occupancy.TryOccupy(player, spawnPos);
        }

        if (logGeneration)
            Debug.Log($"[DungeonController] 플레이어 스폰: {spawnPos} (CurrentCell 동기화 완료)");
    }

    private Vector2Int FindWalkablePositionInRoom(DungeonRoom room)
    {
        // 1. 방 중심이 걸어다닐 수 있으면 중심 사용
        if (currentFloorData.IsWalkable(room.Center.x, room.Center.y))
            return room.Center;

        // 2. 방 내부에서 걸어다닐 수 있는 타일 찾기
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);

        for (int x = minX + 1; x < maxX - 1; x++)
        {
            for (int y = minY + 1; y < maxY - 1; y++)
            {
                if (currentFloorData.IsWalkable(x, y))
                    return new Vector2Int(x, y);
            }
        }

        // 3. 방 범위 확장해서 찾기 (복도 포함)
        for (int x = minX - 2; x < maxX + 2; x++)
        {
            for (int y = minY - 2; y < maxY + 2; y++)
            {
                if (currentFloorData.IsWalkable(x, y))
                    return new Vector2Int(x, y);
            }
        }

        // 4. 전체 맵에서 아무 walkable 타일
        for (int x = 1; x < currentFloorData.width - 1; x++)
        {
            for (int y = 1; y < currentFloorData.height - 1; y++)
            {
                if (currentFloorData.IsWalkable(x, y))
                    return new Vector2Int(x, y);
            }
        }

        Debug.LogError("[DungeonController] walkable 타일을 찾을 수 없습니다!");
        return room.Center;
    }

    private void SpawnEnemies()
    {
        if (dungeonDefinition == null) return;

        foreach (var room in currentFloorData.rooms)
        {
            if (room.roomType == DungeonRoomType.Start || room.roomType == DungeonRoomType.Exit)
                continue;

            if (room.roomType == DungeonRoomType.Boss)
                SpawnBoss(room);
            else if (room.roomType == DungeonRoomType.Elite)
                SpawnElite(room);
            else if (room.roomType == DungeonRoomType.Normal ||
                     room.roomType == DungeonRoomType.Item ||
                     room.roomType == DungeonRoomType.Stairs)
                SpawnNormalEnemies(room);
        }
    }

    private void SpawnNormalEnemies(DungeonRoom room)
    {
        if (dungeonDefinition.normalEnemyPrefabs == null || dungeonDefinition.normalEnemyPrefabs.Length == 0)
            return;

        int enemyCount = Random.Range(dungeonDefinition.minEnemiesPerRoom, dungeonDefinition.maxEnemiesPerRoom + 1);

        for (int i = 0; i < enemyCount; i++)
        {
            GameObject enemyPrefab = dungeonDefinition.normalEnemyPrefabs[Random.Range(0, dungeonDefinition.normalEnemyPrefabs.Length)];
            Vector2Int spawnPos = GetRandomPositionInRoom(room);
            Vector3 worldPos = gridBoard.CellToWorld(spawnPos);

            GameObject enemy = Instantiate(enemyPrefab, worldPos, Quaternion.identity);
            enemy.name = $"Enemy_{room.id}_{i}";
            spawnedEnemies.Add(enemy);
        }
    }

    private void SpawnElite(DungeonRoom room)
    {
        if (dungeonDefinition.eliteEnemyPrefab == null) return;

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        GameObject elite = Instantiate(dungeonDefinition.eliteEnemyPrefab, worldPos, Quaternion.identity);
        elite.name = $"Elite_{room.id}";
        spawnedEnemies.Add(elite);
    }

    private void SpawnBoss(DungeonRoom room)
    {
        if (dungeonDefinition.bossPrefab == null) return;

        Vector3 worldPos = gridBoard.CellToWorld(room.Center);
        bossInstance = Instantiate(dungeonDefinition.bossPrefab, worldPos, Quaternion.identity);
        bossInstance.name = $"Boss_Floor{currentFloor}";
        MarkBossSpawned();
    }

    // 위층 계단 오브젝트 추적
    private GameObject upStairsInstance;

    private void SpawnStairsAndExit()
    {
        // === 1층에만 Exit Portal (필드 탈출) ===
        if (currentFloor == 1 && currentFloorData.startRoom != null && exitPortalPrefab != null)
        {
            Vector3 exitPos = gridBoard.CellToWorld(currentFloorData.startRoom.Center);
            exitInstance = Instantiate(exitPortalPrefab, exitPos, Quaternion.identity);

            var exitInteract = exitInstance.GetComponent<DungeonExitPortal>();
            if (exitInteract == null)
                exitInteract = exitInstance.AddComponent<DungeonExitPortal>();

            if (logGeneration)
                Debug.Log($"[DungeonController] Exit Portal 생성 (1층): Start Room {currentFloorData.startRoom.Center}");
        }

        // === 2층 이상: 위층 계단 (이전 층으로) ===
        if (currentFloor > 1 && currentFloorData.startRoom != null && stairsPrefab != null)
        {
            Vector3 upPos = gridBoard.CellToWorld(currentFloorData.startRoom.Center);
            upStairsInstance = Instantiate(stairsPrefab, upPos, Quaternion.identity);
            upStairsInstance.name = "Stairs_Up";

            // DungeonStairsInteract 대신 전용 컴포넌트 사용
            var upInteract = upStairsInstance.GetComponent<DungeonStairsInteract>();
            if (upInteract == null)
                upInteract = upStairsInstance.AddComponent<DungeonStairsInteract>();
            upInteract.dungeonController = this;
            upInteract.goUp = true; // 위층으로

            if (logGeneration)
                Debug.Log($"[DungeonController] 위층 계단 생성 ({currentFloor}층): Start Room {currentFloorData.startRoom.Center}");
        }

        // === 다음 층 계단 (마지막 층 아닐 때만) ===
        if (currentFloor < maxFloors && currentFloorData.stairsRoom != null && stairsPrefab != null)
        {
            Vector3 stairsPos = gridBoard.CellToWorld(currentFloorData.stairsRoom.Center);
            stairsInstance = Instantiate(stairsPrefab, stairsPos, Quaternion.identity);
            stairsInstance.name = "Stairs_Down";

            var stairsInteract = stairsInstance.GetComponent<DungeonStairsInteract>();
            if (stairsInteract == null)
                stairsInteract = stairsInstance.AddComponent<DungeonStairsInteract>();
            stairsInteract.dungeonController = this;
            stairsInteract.goUp = false; // 아래층으로

            if (logGeneration)
                Debug.Log($"[DungeonController] 다음 층 계단 생성 ({currentFloor}층): Stairs Room {currentFloorData.stairsRoom.Center}");
        }
    }

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
                spawnedEnemies.Add(marker);
            }
        }
    }

    private Vector2Int GetRandomPositionInRoom(DungeonRoom room)
    {
        room.GetBounds(out int minX, out int minY, out int maxX, out int maxY);
        int x = Random.Range(minX + 1, maxX - 1);
        int y = Random.Range(minY + 1, maxY - 1);
        return new Vector2Int(x, y);
    }

    private void CleanupFloor()
    {
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        spawnedEnemies.Clear();

        if (bossInstance != null) Destroy(bossInstance);
        if (stairsInstance != null) Destroy(stairsInstance);
        if (upStairsInstance != null) Destroy(upStairsInstance);
        if (exitInstance != null) Destroy(exitInstance);
    }

    // 보스 처치 확인용 (Update에서 매 프레임 체크)
    private bool bossWasSpawned = false;

    private void Update()
    {
        // 보스 던전에서만 자동 클리어 판정
        // 보스가 스폰된 적 있고(bossWasSpawned), 지금은 null이면 보스를 처치한 것
        if (bossWasSpawned && bossInstance == null && !IsDungeonCleared())
        {
            OnDungeonCleared();
        }
    }

    /// <summary>
    /// 보스 스폰 시 호출 (SpawnBoss 내부에서)
    /// </summary>
    private void MarkBossSpawned()
    {
        bossWasSpawned = true;
    }

    private bool dungeonCleared = false;

    private bool IsDungeonCleared()
    {
        return dungeonCleared;
    }

    private void OnDungeonCleared()
    {
        dungeonCleared = true;

        if (logGeneration)
            Debug.Log($"[DungeonController] 던전 클리어! {dungeonDefinition.dungeonName}");

        Invoke(nameof(ReturnToField), 3f);
    }

    public void ReturnToField()
    {
        // 나가기 전 상태 저장
        SaveCurrentFloorToGameManager();

        string dId = GameManager.Instance?.playerData.currentDungeonId ?? dungeonId;

        // 보스 던전이고 보스를 처치했을 때만 클리어 등록
        if (dungeonDefinition != null && dungeonDefinition.isBossDungeon && bossWasSpawned)
        {
            GameManager.Instance?.ExitDungeonSuccess(dId);
        }
        else
        {
            // 일반 던전: 클리어 등록 없이 나가기 (재입장 가능)
            GameManager.Instance?.ExitDungeon();
        }
    }
}