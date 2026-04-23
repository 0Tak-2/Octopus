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
        if (transform.parent != null) transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        if (logSceneTransitions)
            Debug.Log("[GameManager] 초기화 완료 (DontDestroyOnLoad)");

        // 타이틀의 "이어하기"에서 선택된 슬롯 데이터가 있으면 여기서 주입.
        RunSlotSaveService.TryConsumePendingInto(this);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (logSceneTransitions)
            Debug.Log($"[GameManager] 씬 로드 완료: {scene.name}");

        // WorldProgressManager와 맵 인덱스 동기화 (필드 세이브 키 C*_M* 일관성)
        var wpm = WorldProgressManager.Instance;
        if (wpm != null && (scene.name == fieldSceneName || scene.name.Contains("Field") || scene.name.Contains("Map")))
            SyncFieldProgressFromWorld();

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

        inventory.RebuildSlotsFromDictionaryIfEmpty();

        for (int s = 0; s < inventory.maxInventorySize; s++)
        {
            int id = inventory.GetSlotItemId(s);
            if (id == 0) continue;
            if (!inventory.items.TryGetValue(id, out var invItem)) continue;
            inventoryData.Add(new InventoryItemSave(
                invItem.itemID,
                invItem.itemName,
                invItem.count,
                s
            ));
        }

        if (logDataOperations)
            Debug.Log($"[GameManager] 인벤토리 저장: {inventoryData.Count}개 아이템");
    }

    public void RestoreInventory()
    {
        InventoryManager inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        if (inventory == null) return;

        inventory.ClearAllItemsAndSlots();

        foreach (var item in inventoryData)
        {
            inventory.items[item.itemID] = new InventoryItem
            {
                itemID = item.itemID,
                itemName = item.itemName,
                count = item.count,
                icon = null,
                description = ""
            };

            if (ItemDatabase.Instance != null)
            {
                var itemData = ItemDatabase.Instance.GetItemData(item.itemID);
                if (itemData != null)
                {
                    inventory.items[item.itemID].icon = itemData.icon;
                    inventory.items[item.itemID].description = itemData.description;
                }
            }

            if (item.slotIndex >= 0 && item.slotIndex < inventory.maxInventorySize)
                inventory.ForceAssignSlot(item.slotIndex, item.itemID);
        }

        // 구 세이브(slotIndex 없음) 또는 누락 시 칸 배치
        inventory.RebuildSlotsFromDictionaryIfEmpty();

        if (logDataOperations)
            Debug.Log($"[GameManager] 인벤토리 복원: {inventory.items.Count}개 아이템");
    }

    // =========================================================
    // 필드 맵 상태 저장/복원 (NEW!)
    // =========================================================
    public string GetCurrentMapKey()
    {
        var wpm = WorldProgressManager.Instance;
        if (wpm != null && wpm.CurrentField != null)
        {
            string id = string.IsNullOrEmpty(wpm.CurrentField.fieldID)
                ? wpm.CurrentFieldIndex.ToString()
                : wpm.CurrentField.fieldID;
            return $"Field_{wpm.CurrentFieldIndex}_{id}";
        }

        return $"C{currentChapter}_M{currentMapIndex}";
    }

    /// <summary>
    /// 포털 상호작용 직후 다음 씬에서 입구를 반대편 가장자리에 맞추기 위한 힌트
    /// </summary>
    public void RegisterFieldExitForNextScene(EdgeSide exitedThrough, Vector2Int exitCell, int mapWidth, int mapHeight)
    {
        float alignT = FieldTransitionUtil.ComputeAlignT(exitedThrough, exitCell, mapWidth, mapHeight);
        playerData.hasPendingEntranceHint = true;
        playerData.enterNewFieldFromEdge = FieldTransitionUtil.Opposite(exitedThrough);
        playerData.entranceAlignT = alignT;

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 다음 필드 입구 힌트: 들어오는 변={playerData.enterNewFieldFromEdge}, alignT={alignT:F2} (나간 변={exitedThrough})");
    }

    /// <summary>WorldProgressManager와 currentMapIndex·챕터 동기화</summary>
    public void SyncFieldProgressFromWorld()
    {
        var wpm = WorldProgressManager.Instance;
        if (wpm == null) return;
        currentMapIndex = wpm.CurrentFieldIndex;
        currentChapter = 1;
    }

    public FieldMapSaveData GetOrCreateFieldMapData(string mapKey)
    {
        var entry = fieldMapDataList.Find(x => x.mapKey == mapKey);
        if (entry != null)
            return entry.data;

        // 디스크에 있으면 같은 시드·통로로 복원 (왕복 통로 유지)
        if (FieldLayoutDiskStore.TryLoad(mapKey, out int diskSeed, out Vector2Int diskEx, out Vector2Int diskEn, out bool diskPortals))
        {
            var fromDisk = new FieldMapSaveData(diskSeed);
            if (diskPortals && diskEx != Vector2Int.zero && diskEn != Vector2Int.zero)
            {
                fromDisk.savedExitToNext = diskEx;
                fromDisk.savedEntranceFromPrev = diskEn;
                fromDisk.hasSavedPortalCells = true;
            }

            fieldMapDataList.Add(new FieldMapEntry(mapKey, fromDisk));

            if (logDataOperations)
                Debug.Log($"[GameManager] 디스크에서 필드 맵 데이터 복원: {mapKey}, Seed={diskSeed}, 통로={fromDisk.hasSavedPortalCells}");

            return fromDisk;
        }

        var newData = new FieldMapSaveData(Random.Range(1, int.MaxValue));
        fieldMapDataList.Add(new FieldMapEntry(mapKey, newData));
        FieldLayoutDiskStore.Save(mapKey, newData.mapSeed, Vector2Int.zero, Vector2Int.zero, false);

        if (logDataOperations)
            Debug.Log($"[GameManager] 새 맵 데이터 생성: {mapKey}, Seed={newData.mapSeed}");

        return newData;
    }

    public void SaveFieldState()
    {
        string mapKey = GetCurrentMapKey();
        var mapData = GetOrCreateFieldMapData(mapKey);

        // 적 상태 저장 (필드 루트만 — 던전 적 제외)
        mapData.enemies.Clear();
        GridBoard gridBoard = GetFieldGridBoardForState();

        foreach (var enemy in GetFieldEnemyInstances())
        {
            if (enemy.definition == null) continue;

            Vector2Int pos = gridBoard != null
                ? gridBoard.WorldToCell(enemy.transform.position)
                : Vector2Int.zero;

            int instId = enemy.gameObject.GetInstanceID();
            mapData.enemies.Add(new EnemyStateSave(
                $"Enemy_{pos.x}_{pos.y}",
                enemy.definition.name,
                pos,
                enemy.currentHP,
                enemy.currentHP <= 0 || !enemy.gameObject.activeSelf,
                instId
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
        GridBoard gridBoard = GetFieldGridBoardForState();

        var byInstanceId = new Dictionary<int, EnemyStateSave>();
        foreach (var s in mapData.enemies)
        {
            if (s.unityInstanceId != 0)
                byInstanceId[s.unityInstanceId] = s;
        }

        // 적 상태 복원: 인스턴스 ID 우선 (이동한 적도 동일 개체로 매칭), 없으면 레거시 위치 ID
        foreach (var enemy in GetFieldEnemyInstances())
        {
            if (enemy.definition == null) continue;

            int oid = enemy.gameObject.GetInstanceID();
            EnemyStateSave saved = null;
            if (!byInstanceId.TryGetValue(oid, out saved))
            {
                Vector2Int pos = gridBoard != null
                    ? gridBoard.WorldToCell(enemy.transform.position)
                    : Vector2Int.zero;
                string enemyId = $"Enemy_{pos.x}_{pos.y}";
                saved = mapData.enemies.Find(x => x.uniqueId == enemyId && x.unityInstanceId == 0);
            }

            if (saved == null)
                continue;

            if (saved.isDead)
            {
                enemy.gameObject.SetActive(false);
                continue;
            }

            enemy.gameObject.SetActive(true);
            enemy.currentHP = saved.currentHP;
            enemy.Clamp();

            if (gridBoard != null && gridBoard.InBounds(saved.gridPosition))
            {
                enemy.transform.position = gridBoard.CellToWorld(saved.gridPosition);
                var occ = GridOccupancyRegistry.Instance;
                if (occ != null)
                {
                    occ.Release(enemy.transform);
                    occ.TryOccupy(enemy.transform, saved.gridPosition);
                }
            }
        }

        // 플레이어 위치 복원 (던전에서 돌아왔을 때 → 던전 입구 위치로)
        // ✅ FieldMapGenerator.SpawnPlayer()가 이미 처리했으므로 여기서는 안 함

        if (logDataOperations)
            Debug.Log($"[GameManager] 필드 상태 복원: {mapKey}");
    }

    /// <summary>필드 모드 저장/복원 시 사용할 GridBoard (던전 격자와 혼동 방지)</summary>
    private static GridBoard GetFieldGridBoardForState()
    {
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            return FieldMapGenerator.ResolveGameplayGrid(ModeManager.Instance.fieldRoot);

        return FieldMapGenerator.ResolveGameplayGrid(null);
    }

    private static EnemyInstance[] GetFieldEnemyInstances()
    {
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            return ModeManager.Instance.fieldRoot.GetComponentsInChildren<EnemyInstance>(true);
        return Object.FindObjectsOfType<EnemyInstance>(true);
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

        // 필드에 있을 때만 필드 적/맵 상태 저장 (던전 단일 씬에서는 이름만으로는 구분 안 됨)
        if (ShouldSaveFieldState())
            SaveFieldState();

        // 기본 정책: 현재 활성 슬롯에 항상 최신 런 상태를 저장.
        RunSlotSaveService.SaveActiveRun(this);
    }

    /// <summary>필드 모드일 때만 SaveFieldState 호출해야 함 (GridBoard가 필드 기준일 때)</summary>
    public bool ShouldSaveFieldState()
    {
        if (ModeManager.Instance != null)
            return ModeManager.Instance.CurrentMode == ModeManager.GameplayMode.Field;

        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName == fieldSceneName || sceneName.Contains("Field") || sceneName.Contains("Map");
    }

    public void EnterDungeon(string dungeonId)
    {
        ModeManager mm = ModeManager.Instance ?? FindObjectOfType<ModeManager>();
        if (mm != null)
        {
            mm.EnterDungeon(dungeonId);
            return;
        }

        Debug.LogError("[GameManager] ModeManager 가 씬에 없습니다. NewMapScene에 ModeManager를 배치하세요.");
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
        ModeManager mm = ModeManager.Instance ?? FindObjectOfType<ModeManager>();
        if (mm != null)
        {
            mm.ExitToField();
            return;
        }

        Debug.LogError("[GameManager] ModeManager 가 씬에 없습니다.");
    }

    /// <summary>필드마다 같은 dungeonID 를 쓸 수 있으므로 GetCurrentMapKey() 와 묶어서 저장</summary>
    public string BuildDungeonClearanceKey(string dungeonId)
    {
        if (string.IsNullOrEmpty(dungeonId)) return "";
        return $"{GetCurrentMapKey()}|{dungeonId}";
    }

    public bool IsDungeonClearedOnCurrentField(string dungeonId)
    {
        if (string.IsNullOrEmpty(dungeonId)) return false;
        return clearedDungeons.Contains(BuildDungeonClearanceKey(dungeonId));
    }

    public void RegisterDungeonClearedOnCurrentField(string dungeonId)
    {
        string key = BuildDungeonClearanceKey(dungeonId);
        if (string.IsNullOrEmpty(key)) return;
        if (!clearedDungeons.Contains(key))
            clearedDungeons.Add(key);
    }

    public void ExitDungeonSuccess(string dungeonId)
    {
        RegisterDungeonClearedOnCurrentField(dungeonId);

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 던전 클리어: {dungeonId} (키={BuildDungeonClearanceKey(dungeonId)})");

        ExitDungeon();
    }

    public void LoadNextMap()
    {
        SaveAllCurrentState();

        var wpm = WorldProgressManager.Instance;
        if (wpm != null)
        {
            FieldDefinition cur = wpm.CurrentField;
            if (cur != null && cur.hasNextFieldExit && cur.nextField != null)
            {
                if (wpm.TryGoToFieldByAsset(cur.nextField))
                    return;
            }
        }

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

        var wpm = WorldProgressManager.Instance;
        if (wpm != null)
        {
            FieldDefinition cur = wpm.CurrentField;
            if (cur != null && cur.previousField != null)
            {
                if (wpm.TryGoToFieldByAsset(cur.previousField))
                    return;
            }
        }

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
    // 필드 맵 새 시드 (같은 필드 정의로 레이아웃만 다시)
    // =========================================================

    /// <summary>
    /// <b>현재 진행 중인 필드</b>만 새 랜덤 시드로 다시 생성합니다. 저장된 통로(FieldLayout PlayerPrefs)도 지웁니다.
    /// 기본은 씬 재로드(권장). reloadScene=false면 플레이 중 같은 씬에서 FieldMapGenerator만 다시 돌립니다.
    /// </summary>
    public void RerollCurrentFieldMapNewSeed(bool reloadScene = true)
    {
        SyncFieldProgressFromWorld();
        string key = GetCurrentMapKey();
        FieldLayoutDiskStore.ClearKey(key);
        fieldMapDataList.RemoveAll(e => e.mapKey == key);
        GetOrCreateFieldMapData(key);

        string sn = SceneManager.GetActiveScene().name;
        FogOfWarRenderer.ClearExploredCacheForField(sn);

        if (logDataOperations)
            Debug.Log($"[GameManager] 필드 맵 새 시드: {key} → Seed={GetFieldMapSeed()}");

        if (reloadScene)
        {
            SceneManager.LoadScene(sn);
            return;
        }

        var gen = FindObjectOfType<FieldMapGenerator>();
        if (gen != null)
            gen.ForceRegenerateFieldMap();
        else
            Debug.LogWarning("[GameManager] FieldMapGenerator 없음. 씬 재로드(RerollCurrentFieldMapNewSeed(true))를 쓰세요.");
    }

#if UNITY_EDITOR
    [ContextMenu("Dev: Reroll current field seed + reload scene")]
    private void DevRerollCurrentFieldSeed()
    {
        if (!Application.isPlaying) return;
        RerollCurrentFieldMapNewSeed(true);
    }
#endif

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

        var wpm = WorldProgressManager.Instance;
        if (wpm != null)
        {
            wpm.ResetProgress();
            if (wpm.allFields != null)
            {
                for (int i = 0; i < wpm.allFields.Length; i++)
                {
                    var f = wpm.allFields[i];
                    if (f == null) continue;
                    string id = string.IsNullOrEmpty(f.fieldID) ? i.ToString() : f.fieldID;
                    FieldLayoutDiskStore.ClearKey($"Field_{i}_{id}");
                }
            }
        }

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