using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages world progression between fields and dungeons
/// Tracks which fields/dungeons have been cleared
/// </summary>
public class WorldProgressManager : MonoBehaviour
{
    public static WorldProgressManager Instance { get; private set; }
    
    [Header("Field Definitions (in order)")]
    [Tooltip("진행 순서대로. 인덱스 0 = 게임 시작 시 첫 필드(필드1).")]
    public FieldDefinition[] allFields;
    
    [Header("Current State")]
    [Tooltip("0 = allFields[0] 에서 시작. PlayerPrefs로 저장됩니다.")]
    [SerializeField] private int currentFieldIndex = 0;
    [SerializeField] private string currentDungeonID = "";
    [SerializeField] private int currentDungeonFloor = 0;
    
    [Header("Scene Names")]
    public string fieldSceneName = "GameScene";
    public string dungeonSceneName = "DungeonScene";
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Progress tracking
    private bool[] _clearedFields;
    private string[] _clearedDungeons;
    
    // Events
    public event System.Action<FieldDefinition> OnFieldChanged;
    public event System.Action<DungeonDefinition, int> OnDungeonFloorChanged;
    public event System.Action OnBossDefeated;
    
    // Properties
    public FieldDefinition CurrentField => 
        (currentFieldIndex >= 0 && currentFieldIndex < allFields.Length) ? allFields[currentFieldIndex] : null;
    
    public int CurrentFieldIndex => currentFieldIndex;
    public string CurrentDungeonID => currentDungeonID;
    public int CurrentDungeonFloor => currentDungeonFloor;
    public bool IsInDungeon => !string.IsNullOrEmpty(currentDungeonID);
    
    private const string SAVE_KEY_FIELD = "WorldProgress_Field";
    private const string SAVE_KEY_CLEARED = "WorldProgress_Cleared_";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        InitializeProgress();
        LoadProgress();
    }
    
    private void InitializeProgress()
    {
        if (allFields == null || allFields.Length == 0)
        {
            Debug.LogWarning("[WorldProgress] No fields assigned!");
            return;
        }
        
        _clearedFields = new bool[allFields.Length];
        _clearedDungeons = new string[50]; // Max 50 dungeons tracked
    }
    
    // ============================================
    // Field Navigation
    // ============================================
    
    /// <summary>
    /// Go to specific field by index
    /// </summary>
    public void GoToField(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= allFields.Length)
        {
            Debug.LogError($"[WorldProgress] Invalid field index: {fieldIndex}");
            return;
        }
        
        currentFieldIndex = fieldIndex;
        currentDungeonID = "";
        currentDungeonFloor = 0;
        
        SaveProgress();
        
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Going to Field {fieldIndex + 1}: {allFields[fieldIndex].fieldName}");
        
        OnFieldChanged?.Invoke(allFields[fieldIndex]);
        SceneManager.LoadScene(ResolveFieldSceneName());
    }
    
    /// <summary>
    /// Go to next field
    /// </summary>
    public void GoToNextField()
    {
        if (currentFieldIndex < allFields.Length - 1)
        {
            GoToField(currentFieldIndex + 1);
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[WorldProgress] Already at last field!");
        }
    }
    
    /// <summary>
    /// Go to previous field
    /// </summary>
    public void GoToPreviousField()
    {
        if (currentFieldIndex > 0)
        {
            GoToField(currentFieldIndex - 1);
        }
        else
        {
            if (showDebugLogs)
                Debug.Log("[WorldProgress] Already at first field!");
        }
    }

    /// <summary>
    /// FieldDefinition.nextField / previousField로 이동할 때 사용.
    /// allFields 배열에 해당 에셋이 포함되어 있어야 합니다 (순서 = 진행 순서).
    /// </summary>
    public bool TryGoToFieldByAsset(FieldDefinition def)
    {
        if (def == null || allFields == null)
            return false;

        for (int i = 0; i < allFields.Length; i++)
        {
            if (allFields[i] == def)
            {
                GoToField(i);
                return true;
            }
        }

        if (showDebugLogs)
            Debug.LogWarning($"[WorldProgress] '{def.fieldName}' 에셋이 allFields 목록에 없습니다. 목록에 추가하세요.");

        return false;
    }
    
    // ============================================
    // Dungeon Navigation
    // ============================================
    
    /// <summary>
    /// Enter a dungeon
    /// </summary>
    public void EnterDungeon(DungeonDefinition dungeon)
    {
        if (dungeon == null)
        {
            Debug.LogError("[WorldProgress] Dungeon is null!");
            return;
        }
        
        currentDungeonID = dungeon.dungeonID;
        currentDungeonFloor = 0;
        
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Entering dungeon: {dungeon.dungeonName}");
        
        OnDungeonFloorChanged?.Invoke(dungeon, 0);
        SceneManager.LoadScene(dungeonSceneName);
    }
    
    /// <summary>
    /// Go to next dungeon floor
    /// </summary>
    public void GoToNextFloor()
    {
        currentDungeonFloor++;
        
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Going to floor {currentDungeonFloor + 1}");
        
        // Reload dungeon scene to generate new floor
        SceneManager.LoadScene(dungeonSceneName);
    }
    
    /// <summary>
    /// Exit dungeon back to field
    /// </summary>
    public void ExitDungeon()
    {
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Exiting dungeon, returning to Field {currentFieldIndex + 1}");
        
        currentDungeonID = "";
        currentDungeonFloor = 0;
        
        SceneManager.LoadScene(ResolveFieldSceneName());
    }

    private string ResolveFieldSceneName()
    {
        if (GameManager.Instance != null && !string.IsNullOrEmpty(GameManager.Instance.fieldSceneName))
            return GameManager.Instance.fieldSceneName;
        return fieldSceneName;
    }
    
    // ============================================
    // Progress Tracking
    // ============================================
    
    /// <summary>
    /// Mark current field as cleared
    /// </summary>
    public void MarkFieldCleared()
    {
        if (currentFieldIndex >= 0 && currentFieldIndex < _clearedFields.Length)
        {
            _clearedFields[currentFieldIndex] = true;
            SaveProgress();
            
            if (showDebugLogs)
                Debug.Log($"[WorldProgress] Field {currentFieldIndex + 1} cleared!");
        }
    }
    
    /// <summary>
    /// Mark dungeon as cleared
    /// </summary>
    public void MarkDungeonCleared(string dungeonID)
    {
        // Add to cleared list
        for (int i = 0; i < _clearedDungeons.Length; i++)
        {
            if (string.IsNullOrEmpty(_clearedDungeons[i]))
            {
                _clearedDungeons[i] = dungeonID;
                break;
            }
        }
        
        PlayerPrefs.SetInt(SAVE_KEY_CLEARED + dungeonID, 1);
        SaveProgress();
        
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Dungeon {dungeonID} cleared!");
    }
    
    /// <summary>
    /// Called when boss is defeated
    /// </summary>
    public void OnBossKilled()
    {
        MarkFieldCleared();
        
        if (showDebugLogs)
            Debug.Log("[WorldProgress] Boss defeated!");
        
        OnBossDefeated?.Invoke();
        
        // If this is the final boss, trigger game win
        if (currentFieldIndex >= allFields.Length - 1)
        {
            if (showDebugLogs)
                Debug.Log("[WorldProgress] GAME COMPLETE!");
            // TODO: Show victory screen
        }
    }
    
    /// <summary>
    /// Check if a field has been cleared
    /// </summary>
    public bool IsFieldCleared(int fieldIndex)
    {
        if (fieldIndex < 0 || fieldIndex >= _clearedFields.Length)
            return false;
        return _clearedFields[fieldIndex];
    }
    
    /// <summary>
    /// Check if a dungeon has been cleared
    /// </summary>
    public bool IsDungeonCleared(string dungeonID)
    {
        return PlayerPrefs.GetInt(SAVE_KEY_CLEARED + dungeonID, 0) == 1;
    }
    
    // ============================================
    // Save/Load
    // ============================================
    
    private void SaveProgress()
    {
        PlayerPrefs.SetInt(SAVE_KEY_FIELD, currentFieldIndex);
        
        for (int i = 0; i < _clearedFields.Length; i++)
        {
            PlayerPrefs.SetInt(SAVE_KEY_CLEARED + "Field_" + i, _clearedFields[i] ? 1 : 0);
        }
        
        PlayerPrefs.Save();
    }
    
    private void LoadProgress()
    {
        currentFieldIndex = PlayerPrefs.GetInt(SAVE_KEY_FIELD, 0);
        
        for (int i = 0; i < _clearedFields.Length; i++)
        {
            _clearedFields[i] = PlayerPrefs.GetInt(SAVE_KEY_CLEARED + "Field_" + i, 0) == 1;
        }
        
        if (showDebugLogs)
            Debug.Log($"[WorldProgress] Loaded - Current Field: {currentFieldIndex + 1}");
    }
    
    /// <summary>
    /// Reset all progress (for new game)
    /// </summary>
    public void ResetProgress()
    {
        currentFieldIndex = 0;
        currentDungeonID = "";
        currentDungeonFloor = 0;
        
        for (int i = 0; i < _clearedFields.Length; i++)
        {
            _clearedFields[i] = false;
            PlayerPrefs.DeleteKey(SAVE_KEY_CLEARED + "Field_" + i);
        }
        
        PlayerPrefs.DeleteKey(SAVE_KEY_FIELD);
        PlayerPrefs.Save();
        
        if (showDebugLogs)
            Debug.Log("[WorldProgress] Progress reset!");
    }
    
    // ============================================
    // Utility
    // ============================================
    
    /// <summary>
    /// Get dungeon definition by ID from current field
    /// </summary>
    public DungeonDefinition GetDungeonByID(string dungeonID)
    {
        if (CurrentField == null || CurrentField.dungeons == null)
            return null;
        
        foreach (var dungeon in CurrentField.dungeons)
        {
            if (dungeon != null && dungeon.dungeonID == dungeonID)
                return dungeon;
        }
        
        return null;
    }
    
    /// <summary>
    /// Get current dungeon definition
    /// </summary>
    public DungeonDefinition GetCurrentDungeon()
    {
        if (string.IsNullOrEmpty(currentDungeonID))
            return null;
        
        return GetDungeonByID(currentDungeonID);
    }
}
