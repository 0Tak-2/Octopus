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

    [Tooltip("현재 런 캐릭터 이름 (슬롯 세이브와 동기화)")]
    public string runCharacterName = "";

    // =========================================================
    // 런 통계
    // =========================================================
    // DeathManager 가 아니라 여기가 통계의 집이다.
    // DeathManager 는 씬 스코프라 챕터를 넘어갈 때(씬 리로드) Start()->ResetRunStats() 로
    // 처치 수·플레이 타임이 0이 됐다. 그러면 "깊이 갈수록 보상이 커진다"가 성립하지 않는다.
    // GameManager 는 DontDestroyOnLoad 이고 슬롯 세이브의 소스이므로 여기 두면
    // 챕터 이동과 이어하기 양쪽에서 통계가 유지된다.
    [Header("Run Statistics")]
    public RunStatistics runStats = new RunStatistics();

    // 이번 씬 세션에서 플레이 타임을 재기 시작한 시각 (Time.time 기준)
    private float _playTimeAnchor;

    /// <summary>
    /// 지금까지 흐른 시간을 통계에 적립하고 기준점을 다시 잡는다.
    /// Time.time 은 앱 재시작 시 0으로 돌아가므로 경과분을 누적해 둬야 이어하기에서도 맞는다.
    /// </summary>
    public void AccumulatePlayTime()
    {
        float now = Time.time;
        if (now > _playTimeAnchor)
            runStats.totalPlayTime += now - _playTimeAnchor;
        _playTimeAnchor = now;
    }

    /// <summary>씬이 새로 로드됐을 때 기준점만 갱신 (누적치는 유지).</summary>
    public void ResetPlayTimeAnchor() => _playTimeAnchor = Time.time;

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

    [Header("Auto Save")]
    [Tooltip("자동 저장 사용. 끄면 Esc 메뉴와 맵 이동 때만 저장된다.")]
    public bool enableAutoSave = true;

    [Tooltip("필드에서 이 턴 수만큼 지날 때마다 저장. 너무 작으면 디스크 쓰기가 잦아진다.")]
    [Min(1)] public int autoSaveIntervalTurns = 10;

    private int _turnsSinceAutoSave;
    private FieldTimeManager _boundFieldTime;

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
    }

    private void Start()
    {
        // OnSceneLoaded 는 '첫 씬'에서는 안 불릴 수 있다.
        // (에디터에서 게임 씬을 열어둔 채 Play 를 누르면 OnEnable 구독보다 이벤트가 먼저 지나간다)
        // 그래서 여기서도 한 번 보장한다. 둘 다 중복 생성은 막혀 있다.
        if (IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            EnsureGameSessionMenu();
            EnsureWorldMap();
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindAutoSave();
    }

    // =========================================================
    // 자동 저장
    // =========================================================
    // 예전엔 Esc 메뉴와 맵 이동 때만 저장돼서, 그 사이에 게임이 꺼지면 진행이 통째로 날아갔다.

    private void BindAutoSave()
    {
        UnbindAutoSave();
        if (!enableAutoSave) return;

        _boundFieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (_boundFieldTime != null)
            _boundFieldTime.OnTimeAdvanced += HandleTurnForAutoSave;

        _turnsSinceAutoSave = 0;
    }

    private void UnbindAutoSave()
    {
        if (_boundFieldTime != null)
            _boundFieldTime.OnTimeAdvanced -= HandleTurnForAutoSave;
        _boundFieldTime = null;
    }

    private void HandleTurnForAutoSave(int delta, int newTotalTime)
    {
        if (!enableAutoSave) return;

        _turnsSinceAutoSave += Mathf.Max(1, delta);
        if (_turnsSinceAutoSave < autoSaveIntervalTurns) return;

        _turnsSinceAutoSave = 0;
        TryAutoSave("턴 경과");
    }

    /// <summary>
    /// 자동 저장. 런이 끝난 뒤에는 절대 저장하지 않는다.
    /// 사망 시 DeleteCurrentRunData 로 슬롯을 지우는데, 그 뒤에 저장이 돌면
    /// 지워진 캐릭터가 되살아난다.
    /// </summary>
    public void TryAutoSave(string reason)
    {
        if (!enableAutoSave) return;

        var dm = DeathManager.Instance;
        if (dm != null && dm.RunEnded) return;

        SaveAllCurrentState();

        if (logDataOperations)
            Debug.Log($"[GameManager] 자동 저장 ({reason})");
    }

    private void OnApplicationQuit()
    {
        TryAutoSave("게임 종료");
    }

    private void OnApplicationPause(bool paused)
    {
        // 모바일·백그라운드 전환 대비. 에디터에서는 플레이 중단 시에도 불린다.
        if (paused) TryAutoSave("일시정지");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (logSceneTransitions)
            Debug.Log($"[GameManager] 씬 로드 완료: {scene.name}");

        if (IsGameplayScene(scene.name))
        {
            RunSlotSaveService.ApplySessionStart(this);
            EnsureGameSessionMenu();
            EnsureWorldMap();
        }

        // WorldProgressManager와 맵 인덱스 동기화 (필드 세이브 키 C*_M* 일관성)
        var wpm = WorldProgressManager.Instance;
        if (wpm != null && (scene.name == fieldSceneName || scene.name.Contains("Field") || scene.name.Contains("Map")))
            SyncFieldProgressFromWorld();

        // 플레이어 스탯은 항상 복원
        RestorePlayerStats();

        // 인벤토리 복원
        RestoreInventory();

        // 필드 씬이면 필드 상태 복원
        if (IsGameplayScene(scene.name))
        {
            // 약간의 딜레이 후 복원 (씬 초기화 대기)
            StartCoroutine(RestoreFieldStateDelayed());

            // FieldTimeManager 는 씬마다 새로 생기므로 매번 다시 붙인다.
            BindAutoSave();
        }
        else
        {
            UnbindAutoSave();
        }
    }

    private bool IsGameplayScene(string sceneName)
    {
        return sceneName == fieldSceneName || sceneName.Contains("Field") || sceneName.Contains("Map");
    }

    /// <summary>전체 지도(M)를 씬에 붙인다. 씬 편집 없이 쓰도록 런타임 생성한다.</summary>
    private static void EnsureWorldMap()
    {
        if (FindObjectOfType<WorldMapUI>() != null)
            return;

        new GameObject("WorldMapUI").AddComponent<WorldMapUI>();
    }

    private static void EnsureGameSessionMenu()
    {
        if (FindObjectOfType<GameSessionMenu>() != null)
            return;
        new GameObject("GameSessionMenu").AddComponent<GameSessionMenu>();
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

        // 플레이어 위치 저장 (필드에 있을 때만 — 던전 좌표를 필드 좌표로 저장하면 안 된다)
        GridBoard gridBoard = FindObjectOfType<GridBoard>();
        bool inField = ModeManager.Instance == null
                       || ModeManager.Instance.CurrentMode == ModeManager.GameplayMode.Field;

        if (gridBoard != null && inField)
        {
            playerData.lastFieldPosition = gridBoard.WorldToCell(stats.transform.position);
            // 어느 맵의 좌표인지 함께 남긴다. 챕터가 바뀌면 이 좌표를 쓰면 안 된다.
            playerData.lastFieldMapKey = GetCurrentMapKey();
        }

        SaveGearState();

        if (logDataOperations)
            Debug.Log($"[GameManager] 플레이어 스탯 저장: HP={playerData.hp}, 위치={playerData.lastFieldPosition}");
    }

    /// <summary>
    /// 장비·컬러모듈·도구 저장.
    /// 여태 저장 구조에 없어서, 나갔다 들어오면 Player 프리팹 기본값으로 되돌아갔다.
    /// </summary>
    private void SaveGearState()
    {
        var equip = EquipmentManager.Instance;
        if (equip != null)
            playerData.equippedEquipmentNames = equip.GetEquippedNames();

        var slots = ColorModuleSlots.Instance;
        if (slots != null)
            playerData.equippedColorModuleNames = slots.GetEquippedNames();

        var modInv = ColorModuleInventory.Instance;
        if (modInv != null)
        {
            playerData.ownedColorModuleNames = new List<string>();
            foreach (var m in modInv.OwnedModules)
                if (m?.definition != null)
                    playerData.ownedColorModuleNames.Add(m.definition.name);
        }

        var tools = ToolManager.Instance;
        if (tools != null)
        {
            playerData.equippedPickaxeID = tools.equippedPickaxeID;
            playerData.equippedShovelID = tools.equippedShovelID;
        }
    }

    /// <summary>장비·컬러모듈·도구 복원. RestorePlayerStats 에서 호출한다.</summary>
    private void RestoreGearState()
    {
        // 보유 모듈을 먼저 채워야 장착 복원이 참조할 대상이 생긴다.
        var modInv = ColorModuleInventory.Instance;
        if (modInv != null && playerData.ownedColorModuleNames != null)
        {
            foreach (var name in playerData.ownedColorModuleNames)
            {
                var def = ColorModuleLookup.FindByName(name);
                if (def != null && !modInv.HasModule(def))
                    modInv.AddModule(def);
            }
        }

        var equip = EquipmentManager.Instance;
        if (equip != null)
            equip.RestoreEquippedByNames(playerData.equippedEquipmentNames);

        var slots = ColorModuleSlots.Instance;
        if (slots != null)
            slots.RestoreEquippedByNames(playerData.equippedColorModuleNames);

        var tools = ToolManager.Instance;
        if (tools != null)
        {
            if (playerData.equippedPickaxeID >= 0) tools.EquipPickaxe(playerData.equippedPickaxeID);
            if (playerData.equippedShovelID >= 0) tools.EquipShovel(playerData.equippedShovelID);
        }
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

        // 장비가 붙어야 maxHP 보너스가 반영되므로 스탯 복원 뒤에 이어서 한다.
        RestoreGearState();
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

    // =========================================================
    // 채집물 영속화
    // =========================================================
    // FieldMapSaveData.destroyedObjects 는 선언만 돼 있고 읽고 쓰는 곳이 없었다.
    // 그래서 맵을 나갔다 들어올 때마다 해초·산호·암석이 전부 되살아나 무한 파밍이 가능했고,
    // 배고픔 같은 생존 압박이 의미를 잃었다.
    //
    // 맵은 시드로 결정론적으로 재생성되므로 '격자 셀'이 안정적인 식별자가 된다.

    private static string CellKey(Vector2Int cell) => $"{cell.x},{cell.y}";

    /// <summary>채집/파괴된 필드 오브젝트의 셀을 현재 맵에 기록한다.</summary>
    public void MarkFieldObjectDestroyed(Vector2Int cell)
    {
        var mapData = GetOrCreateFieldMapData(GetCurrentMapKey());
        if (mapData == null) return;

        string key = CellKey(cell);
        if (!mapData.destroyedObjects.Contains(key))
            mapData.destroyedObjects.Add(key);
    }

    /// <summary>이 셀의 오브젝트가 이미 채집/파괴됐는가. 스폰할 때 걸러내는 용도.</summary>
    public bool IsFieldObjectDestroyed(Vector2Int cell)
    {
        var entry = fieldMapDataList.Find(x => x.mapKey == GetCurrentMapKey());
        return entry?.data != null && entry.data.destroyedObjects.Contains(CellKey(cell));
    }

    public void SaveFieldState()
    {
        string mapKey = GetCurrentMapKey();
        var mapData = GetOrCreateFieldMapData(mapKey);

        // 적 상태 저장 (필드 루트만 — 던전 적 제외)
        //
        // 통째로 비우면 안 된다. 집중전투에서 죽은 적은 Destroy 되어 씬에서 사라지므로
        // 아래 순회에 잡히지 않는다. 비우고 새로 쓰면 그 '죽었다'는 기록이 날아가고,
        // 다음에 들어올 때 다시 살아난다.
        // 살아있는(또는 비활성인) 적은 갱신하고, 사라진 적의 기록은 남긴다.
        var previousByID = new Dictionary<string, EnemyStateSave>();
        foreach (var s in mapData.enemies)
            if (s != null && !string.IsNullOrEmpty(s.uniqueId))
                previousByID[s.uniqueId] = s;

        mapData.enemies.Clear();
        var writtenIDs = new HashSet<string>();
        GridBoard gridBoard = GetFieldGridBoardForState();

        foreach (var enemy in GetFieldEnemyInstances())
        {
            if (enemy.definition == null) continue;

            Vector2Int pos = gridBoard != null
                ? gridBoard.WorldToCell(enemy.transform.position)
                : Vector2Int.zero;

            // 스폰 순번을 키로 쓴다. 위치는 적이 배회하면서 바뀌므로 식별자가 될 수 없고,
            // GetInstanceID 는 실행할 때마다 달라져 이어하기에서 매칭이 전부 실패한다.
            string uniqueId = enemy.fieldSpawnIndex >= 0
                ? $"FieldEnemy_{enemy.fieldSpawnIndex}"
                : $"Enemy_{pos.x}_{pos.y}";

            int instId = enemy.gameObject.GetInstanceID();
            mapData.enemies.Add(new EnemyStateSave(
                uniqueId,
                enemy.definition.name,
                pos,
                enemy.currentHP,
                enemy.currentHP <= 0 || !enemy.gameObject.activeSelf,
                instId
            ));
            writtenIDs.Add(uniqueId);
        }

        // 씬에서 사라진 적(= 집중전투에서 처치되어 Destroy 된 적)의 기록을 되살린다.
        // 이게 없으면 잡은 적이 다음 입장 때 부활한다.
        foreach (var kv in previousByID)
        {
            if (writtenIDs.Contains(kv.Key)) continue;

            var old = kv.Value;
            old.isDead = true;      // 사라졌다 = 처치됐다
            old.currentHP = 0;
            mapData.enemies.Add(old);
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

            EnemyStateSave saved = null;

            // 1순위: 스폰 순번. 세션을 넘어서도 유지되는 유일한 식별자다.
            if (enemy.fieldSpawnIndex >= 0)
            {
                string spawnId = $"FieldEnemy_{enemy.fieldSpawnIndex}";
                saved = mapData.enemies.Find(x => x.uniqueId == spawnId);
            }

            // 2순위: 같은 세션 안에서의 인스턴스 ID (던전 왕복 등)
            if (saved == null)
            {
                int oid = enemy.gameObject.GetInstanceID();
                byInstanceId.TryGetValue(oid, out saved);
            }

            // 3순위: 옛 세이브 호환 — 위치 기반 ID
            if (saved == null)
            {
                Vector2Int pos = gridBoard != null
                    ? gridBoard.WorldToCell(enemy.transform.position)
                    : Vector2Int.zero;
                string enemyId = $"Enemy_{pos.x}_{pos.y}";
                saved = mapData.enemies.Find(x => x.uniqueId == enemyId);
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
        runCharacterName = "";
        // 런이 새로 시작될 때만 통계를 지운다. (이어하기면 직후 스냅샷이 덮어쓴다)
        runStats = new RunStatistics();
        ResetPlayTimeAnchor();
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