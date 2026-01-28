using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 관리 (데이터 저장/로드 포함)
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("씬 이름")]
    public string fieldSceneName = "FieldScene";
    public string dungeonSceneName = "DungeonScene";

    [Header("필드 정보")]
    public string currentFieldName = "Field_Start"; // 현재 필드 이름

    [Header("던전 정보")]
    public Vector2Int currentDungeonPosition; // 현재 들어간 던전 위치

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 던전으로 입장
    /// </summary>
    public void EnterDungeon(Vector2Int dungeonPosition)
    {
        Debug.Log($"[SceneTransition] Entering dungeon at {dungeonPosition}");

        // 현재 위치 저장
        currentDungeonPosition = dungeonPosition;

        // 필드 맵 저장
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SaveFieldMap();
            GameDataManager.Instance.SavePlayerData();
        }

        // 던전 씬 로드
        SceneManager.LoadScene(dungeonSceneName);
    }

    /// <summary>
    /// 다른 필드로 이동
    /// </summary>
    public void MoveToField(string targetFieldName)
    {
        Debug.Log($"[SceneTransition] Moving from '{currentFieldName}' to '{targetFieldName}'");

        // 현재 필드 저장
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SaveFieldMap();
            GameDataManager.Instance.SavePlayerData();
        }

        // 새 필드 설정
        currentFieldName = targetFieldName;

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SetCurrentField(targetFieldName);
        }

        // 필드 씬 로드
        SceneManager.LoadScene(fieldSceneName);
    }

    /// <summary>
    /// 필드로 복귀
    /// </summary>
    public void ExitDungeon()
    {
        Debug.Log($"[SceneTransition] Exiting dungeon at {currentDungeonPosition}");

        // 던전 데이터 저장
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.SaveDungeon(currentDungeonPosition);
            GameDataManager.Instance.SavePlayerData();
        }

        // 필드 씬 로드
        SceneManager.LoadScene(fieldSceneName);
    }

    /// <summary>
    /// 씬 로드 완료 시 호출 (UnityEvent)
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[SceneTransition] Scene loaded: {scene.name}");

        if (GameDataManager.Instance == null) return;

        // 필드 씬 로드 시
        if (scene.name == fieldSceneName)
        {
            // 현재 필드 설정
            GameDataManager.Instance.SetCurrentField(currentFieldName);

            // 데이터 로드
            GameDataManager.Instance.LoadFieldMap();
            GameDataManager.Instance.LoadPlayerData();
        }
        // 던전 씬 로드 시
        else if (scene.name == dungeonSceneName)
        {
            DungeonData dungeonData = GameDataManager.Instance.LoadDungeon(currentDungeonPosition);

            // 던전 데이터가 있으면 로드, 없으면 새로 생성
            if (dungeonData != null)
            {
                LoadDungeonState(dungeonData);
            }
            else
            {
                // 새 던전 생성 (DungeonGenerator가 처리)
            }

            GameDataManager.Instance.LoadPlayerData();
        }
    }

    /// <summary>
    /// 던전 상태 로드
    /// </summary>
    private void LoadDungeonState(DungeonData data)
    {
        // 적 생성
        // (DungeonEnemySpawner에서 처리)

        // 아이템 생성
        // (저장된 DroppedItem 재생성)

        Debug.Log($"[SceneTransition] Loaded dungeon state: {data.enemyStates.Count} enemies, {data.droppedItems.Count} items");
    }
}
