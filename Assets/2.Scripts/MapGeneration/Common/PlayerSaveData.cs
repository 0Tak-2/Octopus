using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 스탯 저장 데이터
/// </summary>
[Serializable]
public class PlayerSaveData
{
    [Header("Stats")]
    public int hp = 100;
    public int maxHP = 100;      // 최종 maxHP (읽기용)
    public int baseMaxHP = 100;  // 기본 maxHP (저장/복원용)
    public int fatigue = 100;
    public int maxFatigue = 100;
    public int hunger = 100;
    public int maxHunger = 100;

    [Header("Position")]
    public Vector2Int lastFieldPosition;      // 필드에서 마지막 위치
    public Vector2Int dungeonEntrancePosition; // 던전 입구 위치 (나올 때 여기로)

    [Header("Field portal (다음 씬 입구 배치)")]
    [Tooltip("이전 맵에서 나온 방향·정렬로 다음 맵 입구를 잡을 때 사용")]
    public bool hasPendingEntranceHint;
    public EdgeSide enterNewFieldFromEdge;
    [Range(0f, 1f)] public float entranceAlignT = 0.5f;

    [Header("Dungeon")]
    public string currentDungeonId;
    public int returnToMapIndex;
}

/// <summary>
/// 인벤토리 아이템 저장 데이터 (직렬화 가능)
/// </summary>
[Serializable]
public class InventoryItemSave
{
    public int itemID;
    public string itemName;
    public int count;
    /// <summary>-1 = 구 세이브(슬롯 순서 없음), 0 이상 = 그리드 칸 인덱스</summary>
    public int slotIndex = -1;

    public InventoryItemSave() { }

    public InventoryItemSave(int id, string name, int cnt, int slot = -1)
    {
        itemID = id;
        itemName = name;
        count = cnt;
        slotIndex = slot;
    }
}

/// <summary>
/// 적 상태 저장 데이터
/// </summary>
[Serializable]
public class EnemyStateSave
{
    public string uniqueId;           // 고유 식별자 (위치 기반)
    public string definitionName;     // EnemyDefinition SO 이름
    public Vector2Int gridPosition;   // 그리드 위치
    public int currentHP;             // 현재 HP
    public bool isDead;               // 죽었는지 여부

    public EnemyStateSave() { }

    public EnemyStateSave(string id, string defName, Vector2Int pos, int hp, bool dead)
    {
        uniqueId = id;
        definitionName = defName;
        gridPosition = pos;
        currentHP = hp;
        isDead = dead;
    }
}

/// <summary>
/// 필드 맵 상태 저장 데이터
/// </summary>
[Serializable]
public class FieldMapSaveData
{
    public int mapSeed;                              // 맵 생성 시드
    public List<EnemyStateSave> enemies;             // 적 상태들
    public List<string> collectedItems;              // 수집한 아이템 ID들 (재스폰 방지)
    public List<string> destroyedObjects;            // 파괴된 오브젝트들

    [Tooltip("디스크·세션 공통: 한 번 정해진 출구/입구 통로 (왕복 유지)")]
    public bool hasSavedPortalCells;
    public Vector2Int savedExitToNext;
    public Vector2Int savedEntranceFromPrev;

    public FieldMapSaveData()
    {
        enemies = new List<EnemyStateSave>();
        collectedItems = new List<string>();
        destroyedObjects = new List<string>();
    }

    public FieldMapSaveData(int seed) : this()
    {
        mapSeed = seed;
    }
}

/// <summary>
/// 던전 상태 저장 데이터
/// </summary>
[Serializable]
public class DungeonSaveData
{
    public string dungeonId;
    public int currentFloor;
    public int maxFloors;                             // ✅ 총 층수 저장
    public List<int> floorSeeds;                      // ✅ 층별 시드 저장
    public List<DungeonFloorEnemies> floorEnemies;    // 층별 적 상태
    public bool isCleared;                            // ✅ 클리어 여부

    public DungeonSaveData()
    {
        floorSeeds = new List<int>();
        floorEnemies = new List<DungeonFloorEnemies>();
    }

    public DungeonSaveData(string id) : this()
    {
        dungeonId = id;
        currentFloor = 1;
        maxFloors = 0;
        isCleared = false;
    }

    /// <summary>
    /// 층별 시드 가져오기 (없으면 생성)
    /// </summary>
    public int GetOrCreateFloorSeed(int floor)
    {
        // 리스트 크기 확장
        while (floorSeeds.Count < floor)
        {
            floorSeeds.Add(UnityEngine.Random.Range(1, int.MaxValue));
        }
        return floorSeeds[floor - 1];
    }

    /// <summary>
    /// 층별 시드 설정
    /// </summary>
    public void SetFloorSeed(int floor, int seed)
    {
        while (floorSeeds.Count < floor)
        {
            floorSeeds.Add(0);
        }
        floorSeeds[floor - 1] = seed;
    }

    public List<EnemyStateSave> GetFloorEnemies(int floor)
    {
        var data = floorEnemies.Find(x => x.floor == floor);
        return data?.enemies ?? new List<EnemyStateSave>();
    }

    public void SetFloorEnemies(int floor, List<EnemyStateSave> enemies)
    {
        var existing = floorEnemies.Find(x => x.floor == floor);
        if (existing != null)
        {
            existing.enemies = enemies;
        }
        else
        {
            floorEnemies.Add(new DungeonFloorEnemies(floor, enemies));
        }
    }
}

/// <summary>
/// 던전 층별 적 데이터 (Dictionary 대신 직렬화 가능한 구조)
/// </summary>
[Serializable]
public class DungeonFloorEnemies
{
    public int floor;
    public List<EnemyStateSave> enemies;

    public DungeonFloorEnemies()
    {
        enemies = new List<EnemyStateSave>();
    }

    public DungeonFloorEnemies(int f, List<EnemyStateSave> e)
    {
        floor = f;
        enemies = e ?? new List<EnemyStateSave>();
    }
}