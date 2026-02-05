using UnityEngine;

/// <summary>
/// Dungeon definition ScriptableObject
/// Create: Assets > Create > Dungeon > Dungeon Definition
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Dungeon Definition", fileName = "DungeonDefinition")]
public class DungeonDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string dungeonID;
    public string dungeonName = "Ocean Cave";
    
    [TextArea(2, 3)]
    public string description = "A dark cave beneath the sea";
    
    [Header("Floor Settings")]
    [Tooltip("Minimum number of floors")]
    [Range(1, 5)] public int minFloors = 2;
    
    [Tooltip("Maximum number of floors")]
    [Range(1, 5)] public int maxFloors = 3;
    
    [Header("Floor Size")]
    [Tooltip("Floor width in tiles")]
    [Range(15, 40)] public int floorWidth = 25;
    
    [Tooltip("Floor height in tiles")]
    [Range(15, 40)] public int floorHeight = 25;
    
    [Header("Enemy Pool - Normal")]
    [Tooltip("Normal enemy prefabs for this dungeon")]
    public GameObject[] normalEnemyPrefabs;
    
    [Header("Enemy Pool - Elite")]
    [Tooltip("Elite enemy prefab")]
    public GameObject eliteEnemyPrefab;
    
    [Tooltip("Elite spawn chance per room (0~1)")]
    [Range(0f, 1f)] public float eliteSpawnChance = 0.2f;
    
    [Header("Enemy Pool - Boss")]
    [Tooltip("Boss prefab (spawns on final floor)")]
    public GameObject bossPrefab;
    
    [Tooltip("Is this a boss dungeon? (guaranteed boss on last floor)")]
    public bool isBossDungeon = false;
    
    [Header("Spawn Settings")]
    [Tooltip("Minimum enemies per room")]
    [Range(0, 5)] public int minEnemiesPerRoom = 1;
    
    [Tooltip("Maximum enemies per room")]
    [Range(1, 8)] public int maxEnemiesPerRoom = 3;
    
    [Tooltip("Enemy count increases per floor")]
    public bool scaleEnemiesPerFloor = true;
    
    [Header("Loot Settings")]
    [Tooltip("Resource spawn multiplier (1 = normal)")]
    [Range(0.5f, 3f)] public float resourceMultiplier = 1f;
    
    [Tooltip("Chest spawn chance per room")]
    [Range(0f, 1f)] public float chestSpawnChance = 0.15f;
    
    [Header("Difficulty")]
    [Tooltip("Difficulty rating (affects enemy stats?)")]
    [Range(1, 10)] public int difficultyRating = 1;
    
    [Tooltip("Enemy damage multiplier")]
    [Range(0.5f, 2f)] public float enemyDamageMultiplier = 1f;
    
    [Tooltip("Enemy HP multiplier")]
    [Range(0.5f, 2f)] public float enemyHPMultiplier = 1f;
    
    [Header("Visuals")]
    [Tooltip("Dungeon ambient tint color")]
    public Color dungeonTint = Color.white;
    
    [Tooltip("Dungeon background color")]
    public Color backgroundColor = new Color(0.05f, 0.05f, 0.1f);
    
    [Header("Audio (Optional)")]
    public AudioClip dungeonMusic;
    public AudioClip bossMusic;
    
    /// <summary>
    /// Get random floor count
    /// </summary>
    public int GetRandomFloorCount()
    {
        return Random.Range(minFloors, maxFloors + 1);
    }
    
    /// <summary>
    /// Get enemy count for a specific floor
    /// </summary>
    public int GetEnemyCountForFloor(int floorIndex, int totalFloors)
    {
        int baseCount = Random.Range(minEnemiesPerRoom, maxEnemiesPerRoom + 1);
        
        if (scaleEnemiesPerFloor)
        {
            // Increase enemies on deeper floors
            float multiplier = 1f + (floorIndex * 0.2f);
            baseCount = Mathf.RoundToInt(baseCount * multiplier);
        }
        
        return baseCount;
    }
    
    /// <summary>
    /// Check if should spawn elite on this floor
    /// </summary>
    public bool ShouldSpawnElite()
    {
        return eliteEnemyPrefab != null && Random.value <= eliteSpawnChance;
    }
    
    /// <summary>
    /// Check if should spawn boss (only on last floor of boss dungeon)
    /// </summary>
    public bool ShouldSpawnBoss(int currentFloor, int totalFloors)
    {
        if (!isBossDungeon) return false;
        if (bossPrefab == null) return false;
        return currentFloor >= totalFloors - 1; // Last floor (0-indexed)
    }
}
