using UnityEngine;

/// <summary>
/// Field definition ScriptableObject
/// Defines a field area with its dungeons, enemies, and resources
/// Create: Assets > Create > MapGeneration > Field Definition
/// </summary>
[CreateAssetMenu(menuName = "MapGeneration/Field Definition", fileName = "NewFieldDefinition")]
public class FieldDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string fieldID;
    public string fieldName = "Field 1";

    [TextArea(2, 3)]
    public string description = "Starting area";

    [Header("Field Index (for progression)")]
    [Tooltip("1 = first field, 2 = second, 3 = boss field")]
    [Range(1, 10)] public int fieldIndex = 1;

    [Header("Map Size")]
    [Tooltip("Field width in tiles")]
    [Range(20, 50)] public int width = 30;

    [Tooltip("Field height in tiles")]
    [Range(20, 50)] public int height = 30;

    [Header("Map Generation")]
    [Tooltip("Initial wall probability for cellular automata")]
    [Range(0.3f, 0.6f)] public float wallProbability = 0.45f;

    [Tooltip("CA iterations")]
    [Range(2, 8)] public int caIterations = 4;

    [Tooltip("�ܰ� �� �β� (��� ����). 1~3 ����")]
    [Range(1, 4)] public int borderThickness = 2;

    [Tooltip("�� ������ �н� Ƚ��. �������� �Ų����쳪 �����ο���")]
    [Range(0, 4)] public int smoothPasses = 2;

    [Header("Dungeon Entrances")]
    [Tooltip("Dungeons accessible from this field")]
    public DungeonDefinition[] dungeons;

    [Header("Enemy Spawns - Normal")]
    [Tooltip("Normal enemy prefabs for this field")]
    public GameObject[] normalEnemyPrefabs;

    [Tooltip("Number of normal enemies to spawn")]
    [Range(0, 20)] public int normalEnemyCount = 5;

    [Header("Enemy Spawns - Elite")]
    [Tooltip("Elite enemy prefabs (Field 2+)")]
    public GameObject[] eliteEnemyPrefabs;

    [Tooltip("Number of elite enemies")]
    [Range(0, 5)] public int eliteEnemyCount = 0;

    [Header("Enemy Spawns - Neutral/Passive")]
    [Tooltip("Neutral enemy prefabs (AI5)")]
    public GameObject[] neutralEnemyPrefabs;

    [Tooltip("Number of neutral enemies")]
    [Range(0, 10)] public int neutralEnemyCount = 3;

    [Tooltip("Passive enemy prefabs (AI6)")]
    public GameObject[] passiveEnemyPrefabs;

    [Tooltip("Number of passive enemies")]
    [Range(0, 10)] public int passiveEnemyCount = 2;

    [Header("Resource Spawns")]
    [Tooltip("Seaweed spawn count")]
    [Range(0, 30)] public int seaweedCount = 15;

    [Tooltip("Coral spawn count")]
    [Range(0, 20)] public int coralCount = 10;

    [Tooltip("Rock spawn count (requires pickaxe)")]
    [Range(0, 15)] public int rockCount = 8;

    [Tooltip("Sand spawn count (requires shovel)")]
    [Range(0, 15)] public int sandCount = 5;

    [Tooltip("Sponge spawn count (spawns near rocks)")]
    [Range(0, 10)] public int spongeCount = 3;

    [Header("Exits")]
    [Tooltip("Has exit to next field?")]
    public bool hasNextFieldExit = true;

    [Tooltip("���� �ʵ� ����. WorldProgressManager�� allFields���� ���� ������ ����ؾ� �̵��� �����մϴ�.")]
    public FieldDefinition nextField;

    [Tooltip("���� �ʵ� ���� (�ǵ��ư��� �ⱸ). allFields�� ��� �ʿ�.")]
    public FieldDefinition previousField;

    [Header("Visuals")]
    [Tooltip("Field ambient color tint")]
    public Color ambientTint = Color.white;

    [Tooltip("Field background color")]
    public Color backgroundColor = new Color(0.1f, 0.2f, 0.3f);

    [Header("Audio (Optional)")]
    public AudioClip ambientMusic;
    public AudioClip ambientSFX;

    /// <summary>
    /// Check if this is the boss field (last field)
    /// </summary>
    public bool IsBossField => nextField == null && hasNextFieldExit == false;

    /// <summary>
    /// Get total enemy count for this field
    /// </summary>
    public int TotalEnemyCount => normalEnemyCount + eliteEnemyCount + neutralEnemyCount + passiveEnemyCount;
}