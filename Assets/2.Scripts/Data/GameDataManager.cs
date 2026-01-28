using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 씬 간 데이터 지속성 관리
/// </summary>
public class GameDataManager : MonoBehaviour
{
    public static GameDataManager Instance { get; private set; }

    [Header("씬 데이터")]
    public Dictionary<string, FieldMapData> fieldDataMap = new Dictionary<string, FieldMapData>(); // 필드별 데이터
    public string currentFieldName = ""; // 현재 필드 이름
    public Dictionary<Vector2Int, DungeonData> dungeonDataMap = new Dictionary<Vector2Int, DungeonData>();

    [Header("플레이어 데이터")]
    public PlayerData playerData;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 초기화
            if (playerData == null)
                playerData = new PlayerData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 현재 필드 설정
    /// </summary>
    public void SetCurrentField(string fieldName)
    {
        currentFieldName = fieldName;

        // 해당 필드 데이터가 없으면 생성
        if (!fieldDataMap.ContainsKey(fieldName))
        {
            fieldDataMap[fieldName] = new FieldMapData();
            Debug.Log($"[GameData] Created new field data for {fieldName}");
        }
    }

    /// <summary>
    /// 현재 필드 데이터 가져오기
    /// </summary>
    private FieldMapData GetCurrentFieldData()
    {
        if (string.IsNullOrEmpty(currentFieldName))
        {
            Debug.LogWarning("[GameData] Current field name not set!");
            return null;
        }

        if (!fieldDataMap.ContainsKey(currentFieldName))
        {
            fieldDataMap[currentFieldName] = new FieldMapData();
        }

        return fieldDataMap[currentFieldName];
    }

    /// <summary>
    /// 필드 맵 저장
    /// </summary>
    public void SaveFieldMap()
    {
        FieldMapData currentFieldData = GetCurrentFieldData();
        if (currentFieldData == null) return;

        // 던전 위치 저장 (DungeonEntrance가 있을 때만)
        /*
        DungeonEntrance[] dungeons = FindObjectsOfType<DungeonEntrance>();
        currentFieldData.dungeonPositions.Clear();
        foreach (var dungeon in dungeons)
        {
            Vector2Int pos = new Vector2Int(
                Mathf.RoundToInt(dungeon.transform.position.x),
                Mathf.RoundToInt(dungeon.transform.position.y)
            );
            currentFieldData.dungeonPositions.Add(pos);
        }
        */

        // 적 상태 저장 (Enemy 클래스가 있을 때만)
        /*
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        currentFieldData.enemyStates.Clear();
        foreach (var enemy in enemies)
        {
            EnemyState state = new EnemyState
            {
                position = enemy.transform.position,
                currentHealth = enemy.CurrentHealth,
                maxHealth = enemy.MaxHealth,
                enemyType = enemy.GetType().Name
            };
            currentFieldData.enemyStates.Add(state);
        }
        */

        // Harvestable 상태 저장
        Harvestable[] harvestables = FindObjectsOfType<Harvestable>();
        currentFieldData.harvestableStates.Clear();
        foreach (var harvestable in harvestables)
        {
            if (harvestable.isHarvested) continue; // 채집된 것은 저장 안 함

            HarvestableState state = new HarvestableState
            {
                position = harvestable.transform.position,
                itemID = harvestable.itemID,
                itemName = harvestable.itemName,
                requiresPickaxe = harvestable.requiresPickaxe,
                requiresShovel = harvestable.requiresShovel
            };
            currentFieldData.harvestableStates.Add(state);
        }

        Debug.Log($"[GameData] Saved field map: {harvestables.Length} harvestables");
    }

    /// <summary>
    /// 필드 맵 로드
    /// </summary>
    public void LoadFieldMap()
    {
        FieldMapData currentFieldData = GetCurrentFieldData();
        if (currentFieldData == null) return;

        Debug.Log($"[GameData] Loading field map '{currentFieldName}': {currentFieldData.dungeonPositions.Count} dungeons, {currentFieldData.enemyStates.Count} enemies");

        // 던전 생성 (DungeonSpawner에서 처리)
        // 적 생성 (FieldEnemySpawner에서 처리)
        // Harvestable 생성 (FieldResourceSpawner에서 처리)
    }

    /// <summary>
    /// 던전 데이터 저장
    /// </summary>
    public void SaveDungeon(Vector2Int dungeonPosition)
    {
        DungeonData data = new DungeonData();

        // 적 상태 저장 (Enemy 클래스가 있을 때만)
        /*
        Enemy[] enemies = FindObjectsOfType<Enemy>();
        data.enemyStates.Clear();
        foreach (var enemy in enemies)
        {
            EnemyState state = new EnemyState
            {
                position = enemy.transform.position,
                currentHealth = enemy.CurrentHealth,
                maxHealth = enemy.MaxHealth,
                enemyType = enemy.GetType().Name
            };
            data.enemyStates.Add(state);
        }
        */

        // 아이템 상태 저장
        DroppedItem[] items = FindObjectsOfType<DroppedItem>();
        data.droppedItems.Clear();
        foreach (var item in items)
        {
            DroppedItemState state = new DroppedItemState
            {
                position = item.transform.position,
                itemID = item.itemID,
                itemName = item.itemName
            };
            data.droppedItems.Add(state);
        }

        dungeonDataMap[dungeonPosition] = data;
        Debug.Log($"[GameData] Saved dungeon at {dungeonPosition}: {items.Length} items");
    }

    /// <summary>
    /// 던전 데이터 로드
    /// </summary>
    public DungeonData LoadDungeon(Vector2Int dungeonPosition)
    {
        if (dungeonDataMap.ContainsKey(dungeonPosition))
        {
            Debug.Log($"[GameData] Loading dungeon at {dungeonPosition}");
            return dungeonDataMap[dungeonPosition];
        }

        Debug.Log($"[GameData] No saved data for dungeon at {dungeonPosition}, creating new");
        return null;
    }

    /// <summary>
    /// 플레이어 데이터 저장
    /// </summary>
    public void SavePlayerData()
    {
        if (playerData == null)
            playerData = new PlayerData();

        // 위치 저장
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerData.position = player.transform.position;
        }

        // 인벤토리는 자동으로 InventoryManager가 관리
        // 도구는 자동으로 ToolManager가 관리

        Debug.Log($"[GameData] Saved player data at {playerData.position}");
    }

    /// <summary>
    /// 플레이어 데이터 로드
    /// </summary>
    public void LoadPlayerData()
    {
        if (playerData == null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null && playerData.position != Vector3.zero)
        {
            player.transform.position = playerData.position;
            Debug.Log($"[GameData] Loaded player data at {playerData.position}");
        }
    }
}

// ========== 데이터 구조 ==========

[System.Serializable]
public class FieldMapData
{
    public List<Vector2Int> dungeonPositions = new List<Vector2Int>();
    public List<EnemyState> enemyStates = new List<EnemyState>();
    public List<HarvestableState> harvestableStates = new List<HarvestableState>();
}

[System.Serializable]
public class DungeonData
{
    public List<EnemyState> enemyStates = new List<EnemyState>();
    public List<DroppedItemState> droppedItems = new List<DroppedItemState>();
    public bool isCleared = false;
}

[System.Serializable]
public class PlayerData
{
    public Vector3 position = Vector3.zero;
    // 인벤토리와 도구는 각자의 Manager가 DontDestroyOnLoad로 관리
}

[System.Serializable]
public class EnemyState
{
    public Vector3 position;
    public int currentHealth;
    public int maxHealth;
    public string enemyType;
}

[System.Serializable]
public class HarvestableState
{
    public Vector3 position;
    public int itemID;
    public string itemName;
    public bool requiresPickaxe;
    public bool requiresShovel;
}

[System.Serializable]
public class DroppedItemState
{
    public Vector3 position;
    public int itemID;
    public string itemName;
}