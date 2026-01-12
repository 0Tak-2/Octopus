using UnityEngine;

/// <summary>
/// 필드맵 생성 설정 (ScriptableObject)
/// Inspector에서 조절 가능
/// </summary>
[CreateAssetMenu(menuName = "MapGeneration/Field Map Config", fileName = "FieldMapConfig")]
public class FieldMapConfig : ScriptableObject
{
    [Header("Map Size")]
    [Tooltip("맵 너비")]
    public int width = 20;
    
    [Tooltip("맵 높이")]
    public int height = 20;
    
    [Header("Cellular Automata Settings")]
    [Tooltip("초기 벽 확률 (0.0 ~ 1.0)")]
    [Range(0f, 1f)] public float initialWallProbability = 0.45f;
    
    [Tooltip("CA 반복 횟수 (많을수록 부드러움)")]
    [Range(1, 10)] public int iterations = 4;
    
    [Tooltip("벽이 되기 위한 이웃 벽 개수 (8개 중)")]
    [Range(1, 8)] public int wallThreshold = 5;
    
    [Header("Dungeon Entrances")]
    [Tooltip("던전 입구 최소 개수")]
    [Range(1, 3)] public int minDungeonEntrances = 1;
    
    [Tooltip("던전 입구 최대 개수")]
    [Range(1, 3)] public int maxDungeonEntrances = 2;
    
    [Tooltip("던전 입구 프리팹")]
    public GameObject dungeonEntrancePrefab;
    
    [Header("Enemy Spawns")]
    [Tooltip("적 스폰 개수")]
    [Range(0, 20)] public int enemySpawnCount = 5;
    
    [Tooltip("적 프리팹")]
    public GameObject enemyPrefab;
    
    [Header("Generation")]
    [Tooltip("랜덤 시드 (0 = 완전 랜덤)")]
    public int seed = 0;
}
