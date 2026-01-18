using UnityEngine;

/// <summary>
/// 던전 정의 (ScriptableObject)
/// Inspector에서 던전별 몹 풀, 난이도 설정
/// </summary>
[CreateAssetMenu(menuName = "Dungeon/Dungeon Definition", fileName = "DungeonDefinition")]
public class DungeonDefinition : ScriptableObject
{
    [Header("Dungeon Info")]
    [Tooltip("던전 이름")]
    public string dungeonName = "숲의 던전";
    
    [Tooltip("최소 층수")]
    [Range(2, 3)] public int minFloors = 2;
    
    [Tooltip("최대 층수")]
    [Range(2, 3)] public int maxFloors = 3;
    
    [Header("Enemy Pool")]
    [Tooltip("이 던전에서 나올 일반 적들 (랜덤 선택)")]
    public GameObject[] normalEnemyPrefabs;
    
    [Tooltip("이 던전의 엘리트 적 (Enemy3 사용 권장)")]
    public GameObject eliteEnemyPrefab;
    
    [Tooltip("이 던전의 보스 (Enemy4 사용 권장)")]
    public GameObject bossPrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("방당 최소 적 수")]
    [Range(1, 5)] public int minEnemiesPerRoom = 1;
    
    [Tooltip("방당 최대 적 수")]
    [Range(1, 5)] public int maxEnemiesPerRoom = 3;
    
    [Header("Visuals")]
    [Tooltip("던전 배경색 (선택)")]
    public Color dungeonTint = Color.white;
}
