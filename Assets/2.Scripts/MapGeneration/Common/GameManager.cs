using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// 중앙 집중식 게임 상태 관리자
/// 씬 전환 시에도 모든 중요 데이터를 보존합니다.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // =========================================================
    // 진행 상황
    // =========================================================
    [Header("Current Progress")]
    public int currentChapter = 1;
    public int currentMapIndex = 0;

    [Header("Chapter Data")]
    public int mapsPerChapter = 3;

    [Tooltip("클리어한 던전 ID 목록")]
    public List<string> clearedDungeons = new List<string>();

    // =========================================================
    // 플레이어 데이터
    // =========================================================
    [Header("Player Data")]
    public PlayerSaveData playerData = new PlayerSaveData();

    // =========================================================
    // 인벤토리 데이터 (NEW!)
    // =========================================================
    [Header("Inventory Data")]
    public List<InventoryItemSave> inventoryData = new List<InventoryItemSave>();

    // =========================================================
    // 필드 맵 데이터 (NEW!)
    // =========================================================
    [Header("Field Map Data")]
    [Tooltip("챕터_맵인덱스 → 맵 데이터")]
    public List<FieldMapEntry> fieldMapDataList = new List<FieldMapEntry>();

    // 현재 필드에서 온 건지 여부 (복원 시 필요)
    private bool _comingFromField = false;
    private string _lastFieldMapKey = "";

    // =========================================================
    // 던전 데이터 (여러 던전 지원!)
    // =========================================================
    [Header("Dungeon Data")]
    public DungeonSaveData currentDungeonData;

    [Tooltip("던전ID → 던전 데이터 (여러 던전 저장)")]
    public List<DungeonDataEntry> allDungeonData = new List<DungeonDataEntry>();

    // =========================================================
    // 씬 설정
    // =========================================================
    [Header("Scene Names")]
    public string fieldSceneName = "NewMapScene";
    public string dungeonSceneName = "DungeonScene";

    [Header("Debug")]
    public bool logSceneTransitions = true;
    public bool logDataOperations = true;

    // =========================================================
    // Unity Lifecycle
    // =========================================================
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (logSceneTransitions)
            Debug.Log("[GameManager] 초기화 완료 (DontDestroyOnLoad)");
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (logSceneTransitions)
            Debug.Log($"[GameManager] 씬 로드 완료: {scene.name}");

        // 플레이어 스탯은 항상 복원
        RestorePlayerStats();

        // 인벤토리 복원
        RestoreInventory();

        // 필드 씬이면 필드 상태 복원
        if (scene.name == fieldSceneName || scene.name.Contains("Field") || scene.name.Contains("Map"))
        {
            // 약간의 딜레이 후 복원 (씬 초기화 대기)
            StartCoroutine(RestoreFieldStateDelayed());
        }
    }

    private System.Collections.IEnumerator RestoreFieldStateDelayed()
    {
        // 씬 오브젝트들이 Start() 실행될 때까지 대기
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        RestoreFieldState();
    }

    // =========================================================
    // 플레이어 스탯 저장/복원
    // =========================================================
    public void SavePlayerStats()
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        playerData.hp = stats.hp;
        playerData.maxHP = stats.maxHP;  // 읽기는 OK (프로퍼티)
        playerData.baseMaxHP = stats.baseMaxHP;  // 기본값 저장
        playerData.fatigue = stats.fatigue;
        playerData.maxFatigue = stats.maxFatigue;
        playerData.hunger = stats.hunger;
        playerData.maxHunger = stats.maxHunger;

        // 플레이어 위치 저장
        GridBoard gridBoard = FindObjectOfType<GridBoard>();
        if (gridBoard != null)
        {
            playerData.lastFieldPosition = gridBoard.WorldToCell(stats.transform.position);
        }

        if (logDataOperations)
            Debug.Log($"[GameManager] 플레이어 스탯 저장: HP={playerData.hp}, 위치={playerData.lastFieldPosition}");
    }

    public void RestorePlayerStats()
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        stats.hp = playerData.hp;
        stats.baseMaxHP = playerData.baseMaxHP;  // baseMaxHP에 할당
        stats.fatigue = playerData.fatigue;
        stats.maxFatigue = playerData.maxFatigue;
        stats.hunger = playerData.hunger;
        stats.maxHunger = playerData.maxHunger;
        stats.ClampAll();

        if (logDataOperations)
            Debug.Log($"[GameManager] 플레이어 스탯 복원: HP={stats.hp}");
    }

    // =========================================================
    // 인벤토리 저장/복원 (NEW!)
    // =========================================================
    public void SaveInventory()
    {
        InventoryManager inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        if (inventory == null) return;

        inventoryData.Clear();

        foreach (var kvp in inventory.items)
        {
            inventoryData.Add(new InventoryItemSave(
                kvp.Value.itemID,
                kvp.Value.itemName,
                kvp.Value.count
            ));
        }

        if (logDataOperations)
            Debug.Log($"[GameManager] 인벤토리 저장: {inventoryData.Count}개 아이템");
    }

    public void RestoreInventory()
    {
        InventoryManager inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        if (inventory == null) return;

        // 기존 인벤토리 클리어
        inventory.items.Clear();

        // 저장된 데이터로 복원
        foreach (var item in inventoryData)
        {
            inventory.items[item.itemID] = new InventoryItem
            {
                itemID = item.itemID,
                itemName = item.itemName,
                count = item.count,
                icon = null,  // ItemDatabase에서 다시 로드해야 함
                description = ""
            };

            // ItemDatabase에서 아이콘/설명 복원
            if (ItemDatabase.Instance != null)
            {
                var itemData = ItemDatabase.Instance.GetItemData(item.itemID);
                if (itemData != null)
                {
                    inventory.items[item.itemID].icon = itemData.icon;
                    inventory.items[item.itemID].description = itemData.description;
                }
            }
        }

        if (logDataOperations)
            Debug.Log($"[GameManager] 인벤토리 복원: {inventory.items.Count}개 아이템");
    }

    // =========================================================
    // 필드 맵 상태 저장/복원 (NEW!)
    // =========================================================
    public string GetCurrentMapKey()
    {
        return $"C{currentChapter}_M{currentMapIndex}";
    }

    public FieldMapSaveData GetOrCreateFieldMapData(string mapKey)
    {
        var entry = fieldMapDataList.Find(x => x.mapKey == mapKey);
        if (entry != null)
            return entry.data;

        // 새로 생성 (랜덤 시드)
        var newData = new FieldMapSaveData(Random.Range(1, int.MaxValue));
        fieldMapDataList.Add(new FieldMapEntry(mapKey, newData));

        if (logDataOperations)
            Debug.Log($"[GameManager] 새 맵 데이터 생성: {mapKey}, Seed={newData.mapSeed}");

        return newData;
    }

    public void SaveFieldState()
    {
        string mapKey = GetCurrentMapKey();
        var mapData = GetOrCreateFieldMapData(mapKey);

        // 적 상태 저장
        mapData.enemies.Clear();
        GridBoard gridBoard = FindObjectOfType<GridBoard>();

        EnemyInstance[] enemies = FindObjectsOfType<EnemyInstance>(true); // 비활성화된 것도 포함
        foreach (var enemy in enemies)
        {
            if (enemy.definition == null) continue;

            Vector2Int pos = gridBoard != null
                ? gridBoard.WorldToCell(enemy.transform.position)
                : Vector2Int.zero;

            mapData.enemies.Add(new EnemyStateSave(
                $"Enemy_{pos.x}_{pos.y}",
                enemy.definition.name,
                pos,
                enemy.currentHP,
                enemy.currentHP <= 0 || !enemy.gameObject.activeSelf
            ));
        }

        _lastFieldMapKey = mapKey;

        if (logDataOperations)
            Debug.Log($"[GameManager] 필드 상태 저장: {mapKey}, 적 {mapData.enemies.Count}마리");
    }

    public void RestoreFieldState()
    {
        string mapKey = GetCurrentMapKey();
        var entry = fieldMapDataList.Find(x => x.mapKey == mapKey);

        if (entry == null)
        {
            if (logDataOperations)
                Debug.Log($"[GameManager] 필드 복원할 데이터 없음 (새 맵): {mapKey}");
            return;
        }

        var mapData = entry.data;
        GridBoard gridBoard = FindObjectOfType<GridBoard>();

        // 적 상태 복원
        EnemyInstance[] enemies = FindObjectsOfType<EnemyInstance>(true);
        foreach (var enemy in enemies)
        {
            if (enemy.definition == null) continue;

            Vector2Int pos = gridBoard != null
                ? gridBoard.WorldToCell(enemy.transform.position)
                : Vector2Int.zero;

            string enemyId = $"Enemy_{pos.x}_{pos.y}";
            var savedState = mapData.enemies.Find(x => x.uniqueId == enemyId);

            if (savedState != null)
            {
                if (savedState.isDead)
                {
                    // 죽은 적은 비활성화
                    enemy.gameObject.SetActive(false);
                }
                else
                {
                    // HP 복원
                    enemy.currentHP = savedState.currentHP;
                }
            }
        }

        // 플레이어 위치 복원 (던전에서 돌아왔을 때 → 던전 입구 위치로)
        // ✅ FieldMapGenerator.SpawnPlayer()가 이미 처리했으므로 여기서는 안 함
        // if (_comingFromField) { ... }

        _comingFromField = false;

        if (logDataOperations)
            Debug.Log($"[GameManager] 필드 상태 복원: {mapKey}");
    }

    /// <summary>
    /// 필드 맵의 시드를 가져옴 (FieldMapGenerator에서 호출)
    /// </summary>
    public int GetFieldMapSeed()
    {
        string mapKey = GetCurrentMapKey();
        var mapData = GetOrCreateFieldMapData(mapKey);
        return mapData.mapSeed;
    }

    // =========================================================
    // 씬 전환
    // =========================================================

    /// <summary>
    /// 모든 현재 상태를 저장
    /// </summary>
    public void SaveAllCurrentState()
    {
        SavePlayerStats();
        SaveInventory();

        // 필드에 있으면 필드 상태도 저장
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName == fieldSceneName || sceneName.Contains("Field") || sceneName.Contains("Map"))
        {
            SaveFieldState();
        }
    }

    public void EnterDungeon(string dungeonId)
    {
        // 모든 상태 저장
        SaveAllCurrentState();

        playerData.currentDungeonId = dungeonId;
        playerData.returnToMapIndex = currentMapIndex;
        _comingFromField = true;

        // ✅ 저장된 던전 데이터가 있으면 로드, 없으면 새로 생성
        currentDungeonData = GetOrCreateDungeonData(dungeonId);

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 던전 입장: {dungeonId}, 저장된 층: {currentDungeonData.currentFloor}");

        SceneManager.LoadScene(dungeonSceneName);
    }

    /// <summary>
    /// 던전 데이터 가져오기 (없으면 생성)
    /// </summary>
    public DungeonSaveData GetOrCreateDungeonData(string dungeonId)
    {
        var entry = allDungeonData.Find(x => x.dungeonId == dungeonId);
        if (entry != null)
        {
            if (logDataOperations)
                Debug.Log($"[GameManager] 기존 던전 데이터 로드: {dungeonId}");
            return entry.data;
        }

        // 새로 생성
        var newData = new DungeonSaveData(dungeonId);
        allDungeonData.Add(new DungeonDataEntry(dungeonId, newData));

        if (logDataOperations)
            Debug.Log($"[GameManager] 새 던전 데이터 생성: {dungeonId}");

        return newData;
    }

    /// <summary>
    /// 던전 데이터 저장
    /// </summary>
    public void SaveDungeonData(DungeonSaveData data)
    {
        if (data == null) return;

        var entry = allDungeonData.Find(x => x.dungeonId == data.dungeonId);
        if (entry != null)
        {
            entry.data = data;
        }
        else
        {
            allDungeonData.Add(new DungeonDataEntry(data.dungeonId, data));
        }

        if (logDataOperations)
            Debug.Log($"[GameManager] 던전 데이터 저장: {data.dungeonId}, 층: {data.currentFloor}");
    }

    public void ExitDungeon()
    {
        SavePlayerStats();
        SaveInventory();

        // ✅ 현재 던전 데이터 저장
        if (currentDungeonData != null)
        {
            SaveDungeonData(currentDungeonData);
        }

        _comingFromField = true;

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 필드로 복귀");

        SceneManager.LoadScene(fieldSceneName);
    }

    public void ExitDungeonSuccess(string dungeonId)
    {
        if (!clearedDungeons.Contains(dungeonId))
            clearedDungeons.Add(dungeonId);

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 던전 클리어: {dungeonId}");

        ExitDungeon();
    }

    public void LoadNextMap()
    {
        SaveAllCurrentState();

        currentMapIndex++;

        if (currentMapIndex >= mapsPerChapter)
        {
            if (logSceneTransitions)
                Debug.Log($"[GameManager] 챕터 {currentChapter} 클리어!");

            currentChapter++;
            currentMapIndex = 0;
        }

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 다음 맵 로드 (챕터 {currentChapter}, 맵 {currentMapIndex + 1})");

        SceneManager.LoadScene(fieldSceneName);
    }

    public void LoadPreviousMap()
    {
        SaveAllCurrentState();

        currentMapIndex--;
        if (currentMapIndex < 0)
        {
            currentMapIndex = 0;
            if (logSceneTransitions)
                Debug.LogWarning("[GameManager] 이미 첫 번째 맵입니다.");
            return;
        }

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 이전 맵 로드");

        SceneManager.LoadScene(fieldSceneName);
    }

    // =========================================================
    // 게임 리셋
    // =========================================================
    public void ResetGame()
    {
        currentChapter = 1;
        currentMapIndex = 0;
        clearedDungeons.Clear();
        playerData = new PlayerSaveData();
        inventoryData.Clear();
        fieldMapDataList.Clear();
        currentDungeonData = null;

        if (logDataOperations)
            Debug.Log("[GameManager] 게임 리셋 완료");
    }
}

/// <summary>
/// 필드 맵 데이터 엔트리 (직렬화 가능한 Dictionary 대체)
/// </summary>
[System.Serializable]
public class FieldMapEntry
{
    public string mapKey;
    public FieldMapSaveData data;

    public FieldMapEntry() { }

    public FieldMapEntry(string key, FieldMapSaveData d)
    {
        mapKey = key;
        data = d;
    }
}

/// <summary>
/// 던전 데이터 엔트리 (직렬화 가능한 Dictionary 대체)
/// </summary>
[System.Serializable]
public class DungeonDataEntry
{
    public string dungeonId;
    public DungeonSaveData data;

    public DungeonDataEntry() { }

    public DungeonDataEntry(string id, DungeonSaveData d)
    {
        dungeonId = id;
        data = d;
    }
}