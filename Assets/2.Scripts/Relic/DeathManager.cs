using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles player death, statistics, and relic rewards
/// </summary>
public class DeathManager : MonoBehaviour
{
    public static DeathManager Instance { get; private set; }
    
    [Header("References")]
    public RelicManager relicManager;
    public PlayerStats playerStats;
    
    [Header("Death UI")]
    public GameObject deathScreenUI;
    
    [Header("Scene Names")]
    public string characterSelectScene = "CharacterSelect";
    public string mainMenuScene = "MainMenu";
    
    [Header("Relic Drop Chances")]
    [Range(0f, 1f)] public float commonChance = 0.60f;
    [Range(0f, 1f)] public float rareChance = 0.25f;
    [Range(0f, 1f)] public float epicChance = 0.12f;
    [Range(0f, 1f)] public float legendaryChance = 0.03f;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Run statistics
    private RunStatistics _currentRunStats = new RunStatistics();
    
    // Last death's reward
    private RelicDefinition _lastRewardedRelic;
    
    public RunStatistics CurrentStats => _currentRunStats;
    public RelicDefinition LastRewardedRelic => _lastRewardedRelic;
    
    // Events
    public event System.Action<RunStatistics, RelicDefinition> OnPlayerDeath;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance ?? FindObjectOfType<RelicManager>();
        
        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();
        
        if (deathScreenUI != null)
            deathScreenUI.SetActive(false);
        
        // Start new run stats
        ResetRunStats();
    }
    
    // ============================================
    // Statistics Tracking
    // ============================================
    
    /// <summary>
    /// Reset statistics for new run
    /// </summary>
    public void ResetRunStats()
    {
        _currentRunStats = new RunStatistics();
        _currentRunStats.runStartTime = Time.time;

        if (CharacterLevelProgression.Instance != null)
            CharacterLevelProgression.Instance.ResetRunProgress();
        
        if (showDebugLogs)
            Debug.Log("[DeathManager] Run statistics reset");
    }
    
    /// <summary>
    /// Record an enemy kill
    /// </summary>
    public void RecordKill(string enemyName, bool isBoss = false, bool isElite = false)
    {
        _currentRunStats.totalKills++;
        
        if (isBoss) _currentRunStats.bossKills++;
        if (isElite) _currentRunStats.eliteKills++;
        
        if (showDebugLogs)
            Debug.Log($"[DeathManager] Kill recorded: {enemyName}");
    }
    
    /// <summary>
    /// Record damage taken
    /// </summary>
    public void RecordDamageTaken(int damage)
    {
        _currentRunStats.totalDamageTaken += damage;
    }
    
    /// <summary>
    /// Record damage dealt
    /// </summary>
    public void RecordDamageDealt(int damage)
    {
        _currentRunStats.totalDamageDealt += damage;
    }
    
    /// <summary>
    /// Record a close call (near death)
    /// </summary>
    public void RecordCloseCall()
    {
        _currentRunStats.closeCalls++;
    }
    
    /// <summary>
    /// Record rest/sleep
    /// </summary>
    public void RecordRest()
    {
        _currentRunStats.timesRested++;
    }
    
    /// <summary>
    /// Record eating
    /// </summary>
    public void RecordMeal()
    {
        _currentRunStats.mealsEaten++;
    }
    
    /// <summary>
    /// Record item crafted
    /// </summary>
    public void RecordCraft()
    {
        _currentRunStats.itemsCrafted++;
    }
    
    /// <summary>
    /// Record field/dungeon progress
    /// </summary>
    public void RecordFieldReached(int fieldNumber)
    {
        if (fieldNumber > _currentRunStats.highestFieldReached)
            _currentRunStats.highestFieldReached = fieldNumber;
    }
    
    public void RecordDungeonCleared()
    {
        _currentRunStats.dungeonsCleared++;
    }
    
    // ============================================
    // Death Processing
    // ============================================
    
    /// <summary>
    /// Called when player dies
    /// </summary>
    public void ProcessDeath(string causeOfDeath = "Unknown")
    {
        // Finalize stats
        _currentRunStats.runEndTime = Time.time;
        _currentRunStats.totalPlayTime = _currentRunStats.runEndTime - _currentRunStats.runStartTime;
        _currentRunStats.causeOfDeath = causeOfDeath;
        
        if (showDebugLogs)
        {
            Debug.Log($"[DeathManager] Player died! Cause: {causeOfDeath}");
            Debug.Log($"  - Kills: {_currentRunStats.totalKills}");
            Debug.Log($"  - Play time: {_currentRunStats.totalPlayTime:F1}s");
            Debug.Log($"  - Highest field: {_currentRunStats.highestFieldReached}");
        }
        
        // Award relic
        _lastRewardedRelic = DetermineRelicReward();
        
        if (_lastRewardedRelic != null && relicManager != null)
        {
            relicManager.AddRelic(_lastRewardedRelic);
            
            if (showDebugLogs)
                Debug.Log($"[DeathManager] Awarded relic: {_lastRewardedRelic.relicName} ({_lastRewardedRelic.rarity})");
        }
        
        // Fire event
        OnPlayerDeath?.Invoke(_currentRunStats, _lastRewardedRelic);
        
        // Show death screen
        if (deathScreenUI != null)
        {
            deathScreenUI.SetActive(true);
        }
    }
    
    /// <summary>
    /// Determine relic reward based on performance
    /// </summary>
    private RelicDefinition DetermineRelicReward()
    {
        if (relicManager == null) return null;
        
        // Adjust chances based on performance
        float bonusChance = 0f;
        
        // Boss kills boost rare+ chance
        bonusChance += _currentRunStats.bossKills * 0.05f;
        
        // Field progress boosts chance
        bonusChance += _currentRunStats.highestFieldReached * 0.02f;
        
        // Dungeon clears boost chance
        bonusChance += _currentRunStats.dungeonsCleared * 0.01f;
        
        float adjustedRare = Mathf.Min(rareChance + bonusChance * 0.5f, 0.5f);
        float adjustedEpic = Mathf.Min(epicChance + bonusChance * 0.3f, 0.3f);
        float adjustedLegendary = Mathf.Min(legendaryChance + bonusChance * 0.1f, 0.15f);
        float adjustedCommon = 1f - adjustedRare - adjustedEpic - adjustedLegendary;
        
        return relicManager.GetRandomRelic(adjustedCommon, adjustedRare, adjustedEpic, adjustedLegendary);
    }
    
    // ============================================
    // UI Callbacks
    // ============================================
    
    /// <summary>
    /// Called when player clicks "Continue" on death screen
    /// </summary>
    public void OnContinueClicked()
    {
        // Clear equipped relics for new run
        if (relicManager != null)
            relicManager.ClearEquipped();
        
        // Load character select
        SceneManager.LoadScene(characterSelectScene);
    }
    
    /// <summary>
    /// Called when player clicks "Main Menu" on death screen
    /// </summary>
    public void OnMainMenuClicked()
    {
        SceneManager.LoadScene(mainMenuScene);
    }
    
    /// <summary>
    /// Restart immediately (for testing)
    /// </summary>
    public void RestartRun()
    {
        if (relicManager != null)
            relicManager.ClearEquipped();
        
        ResetRunStats();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}

/// <summary>
/// Statistics for a single run
/// </summary>
[System.Serializable]
public class RunStatistics
{
    [Header("Combat")]
    public int totalKills = 0;
    public int eliteKills = 0;
    public int bossKills = 0;
    public int totalDamageTaken = 0;
    public int totalDamageDealt = 0;
    public int closeCalls = 0;
    
    [Header("Survival")]
    public int timesRested = 0;
    public int mealsEaten = 0;
    public int itemsCrafted = 0;
    
    [Header("Progress")]
    public int highestFieldReached = 1;
    public int dungeonsCleared = 0;
    
    [Header("Time")]
    public float runStartTime = 0f;
    public float runEndTime = 0f;
    public float totalPlayTime = 0f;
    
    [Header("Death")]
    public string causeOfDeath = "";
}
