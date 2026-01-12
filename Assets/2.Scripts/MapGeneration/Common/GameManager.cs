using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Current Progress")]
    public int currentChapter = 1;
    public int currentMapIndex = 0;

    [Header("Player Data (씬 전환 시 보존)")]
    public PlayerSaveData playerData = new PlayerSaveData();

    [Header("Chapter Data")]
    public int mapsPerChapter = 3;

    [Tooltip("클리어한 던전 ID 목록 (Inspector 직렬화 OK)")]
    public List<string> clearedDungeons = new List<string>();

    [Header("Scene Names")]
    public string[] fieldSceneNames = { "FieldMap1", "FieldMap2", "FieldMap3" };
    public string dungeonSceneName = "DungeonScene";

    [Header("Debug")]
    public bool logSceneTransitions = true;

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

    public void SavePlayerData()
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        playerData.hp = stats.hp;
        playerData.maxHP = stats.maxHP;
        playerData.fatigue = stats.fatigue;
        playerData.maxFatigue = stats.maxFatigue;
        playerData.hunger = stats.hunger;
        playerData.maxHunger = stats.maxHunger;

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 플레이어 데이터 저장: HP={playerData.hp}, Fatigue={playerData.fatigue}, Hunger={playerData.hunger}");
    }

    public void RestorePlayerData()
    {
        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        stats.hp = playerData.hp;
        stats.maxHP = playerData.maxHP;
        stats.fatigue = playerData.fatigue;
        stats.maxFatigue = playerData.maxFatigue;
        stats.hunger = playerData.hunger;
        stats.maxHunger = playerData.maxHunger;

        stats.ClampAll();

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 플레이어 데이터 복원: HP={stats.hp}, Fatigue={stats.fatigue}, Hunger={stats.hunger}");
    }

    public void LoadNextMap()
    {
        SavePlayerData();

        currentMapIndex++;

        if (currentMapIndex >= mapsPerChapter)
        {
            if (logSceneTransitions)
                Debug.Log($"[GameManager] 챕터 {currentChapter} 클리어!");

            currentChapter++;
            currentMapIndex = 0;
        }

        string nextScene = fieldSceneNames[Mathf.Min(currentMapIndex, fieldSceneNames.Length - 1)];

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 다음 맵 로드: {nextScene} (챕터 {currentChapter}, 맵 {currentMapIndex + 1})");

        SceneManager.LoadScene(nextScene);
    }

    public void LoadPreviousMap()
    {
        SavePlayerData();

        currentMapIndex--;
        if (currentMapIndex < 0)
        {
            currentMapIndex = 0;
            if (logSceneTransitions)
                Debug.LogWarning("[GameManager] 이미 첫 번째 맵입니다.");
            return;
        }

        string prevScene = fieldSceneNames[currentMapIndex];

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 이전 맵 로드: {prevScene}");

        SceneManager.LoadScene(prevScene);
    }

    public void EnterDungeon(string dungeonId)
    {
        SavePlayerData();

        playerData.currentDungeonId = dungeonId;
        playerData.returnToMapIndex = currentMapIndex;

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 던전 입장: {dungeonId}");

        SceneManager.LoadScene(dungeonSceneName);
    }

    public void ExitDungeonSuccess(string dungeonId)
    {
        if (!clearedDungeons.Contains(dungeonId))
            clearedDungeons.Add(dungeonId);

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 던전 클리어: {dungeonId}");

        ExitDungeon();
    }

    public void ExitDungeon()
    {
        SavePlayerData();

        int mapIndex = playerData.returnToMapIndex;
        string returnScene = fieldSceneNames[Mathf.Min(mapIndex, fieldSceneNames.Length - 1)];

        if (logSceneTransitions)
            Debug.Log($"[GameManager] 필드로 복귀: {returnScene}");

        SceneManager.LoadScene(returnScene);
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (logSceneTransitions)
            Debug.Log($"[GameManager] 씬 로드 완료: {scene.name}");

        RestorePlayerData();
    }
}
