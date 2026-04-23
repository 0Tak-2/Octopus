using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles player death, statistics, and relic rewards
/// </summary>
public class DeathManager : MonoBehaviour
{
    public static bool IsDeathInputLocked { get; private set; }
    public static void ClearDeathInputLock() => IsDeathInputLocked = false;

    public enum DeathContext
    {
        Unknown,
        Field,
        Dungeon,
    }

    public static DeathManager Instance { get; private set; }
    
    [Header("References")]
    public RelicManager relicManager;
    public PlayerStats playerStats;
    
    [Header("Death UI")]
    public GameObject deathScreenUI;
    [Tooltip("기존 deathScreenUI가 비어있거나 연결이 꼬여도, 최소 연출(검정 페이드 + YOU DIED)을 항상 표시")]
    public bool useSimpleDeathOverlay = true;
    [Range(0.05f, 2f)] public float simpleDeathFadeDuration = 0.45f;
    [Range(0f, 1f)] public float simpleDeathOverlayAlpha = 0.92f;
    public string simpleDeathText = "YOU DIED";
    [Header("Death Flow")]
    [Tooltip("사망 후 자동으로 타이틀(메인 메뉴)로 복귀")]
    public bool autoReturnToTitleOnDeath = true;
    [Range(0.5f, 10f)] public float autoReturnDelaySeconds = 4f;
    
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
    private bool _deathProcessed;
    private CanvasGroup _simpleOverlayGroup;
    private Image _simpleOverlayImage;
    private Text _simpleOverlayText;
    private bool _isReturningToTitle;
    
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
        _deathProcessed = false;
        _isReturningToTitle = false;
        IsDeathInputLocked = false;
        HideSimpleDeathOverlay();

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
    public bool TryHandlePlayerDeath(DeathContext context, string causeOfDeath = "Unknown")
    {
        if (_deathProcessed)
            return false;

        _deathProcessed = true;
        ProcessDeathInternal(context, causeOfDeath);
        return true;
    }

    public void ProcessDeath(string causeOfDeath = "Unknown")
    {
        TryHandlePlayerDeath(DeathContext.Unknown, causeOfDeath);
    }

    private void ProcessDeathInternal(DeathContext context, string causeOfDeath = "Unknown")
    {
        IsDeathInputLocked = true;

        // Finalize stats
        _currentRunStats.runEndTime = Time.time;
        _currentRunStats.totalPlayTime = _currentRunStats.runEndTime - _currentRunStats.runStartTime;
        _currentRunStats.causeOfDeath = causeOfDeath;
        
        if (showDebugLogs)
        {
            Debug.Log($"[DeathManager] Player died! Context: {context}, Cause: {causeOfDeath}");
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

        if (useSimpleDeathOverlay)
            StartCoroutine(ShowSimpleDeathOverlayRoutine());

        bool showDeathScreen = !autoReturnToTitleOnDeath;
        if (!showDeathScreen && deathScreenUI != null && deathScreenUI.activeSelf)
            deathScreenUI.SetActive(false);
        if (showDeathScreen && deathScreenUI != null)
        {
            deathScreenUI.SetActive(true);
        }

        if (autoReturnToTitleOnDeath)
            StartCoroutine(ReturnToTitleAfterDelayRoutine());
    }

    private System.Collections.IEnumerator ShowSimpleDeathOverlayRoutine()
    {
        EnsureSimpleDeathOverlay();
        if (_simpleOverlayGroup == null || _simpleOverlayImage == null || _simpleOverlayText == null)
            yield break;

        _simpleOverlayText.text = string.IsNullOrEmpty(simpleDeathText) ? "YOU DIED" : simpleDeathText;
        _simpleOverlayGroup.alpha = 0f;
        _simpleOverlayText.enabled = false;
        _simpleOverlayGroup.gameObject.SetActive(true);

        float duration = Mathf.Max(0.05f, simpleDeathFadeDuration);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            float a = Mathf.Lerp(0f, Mathf.Clamp01(simpleDeathOverlayAlpha), Mathf.Clamp01(t));
            _simpleOverlayImage.color = new Color(0f, 0f, 0f, a);
            _simpleOverlayGroup.alpha = Mathf.Clamp01(t);
            if (t >= 0.6f) _simpleOverlayText.enabled = true;
            yield return null;
        }

        _simpleOverlayImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(simpleDeathOverlayAlpha));
        _simpleOverlayGroup.alpha = 1f;
        _simpleOverlayText.enabled = true;
    }

    private void EnsureSimpleDeathOverlay()
    {
        if (_simpleOverlayGroup != null && _simpleOverlayImage != null && _simpleOverlayText != null)
            return;

        var root = new GameObject("SimpleDeathOverlay");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        _simpleOverlayGroup = root.AddComponent<CanvasGroup>();
        _simpleOverlayGroup.blocksRaycasts = false;
        _simpleOverlayGroup.interactable = false;

        var bg = new GameObject("BG");
        bg.transform.SetParent(root.transform, false);
        _simpleOverlayImage = bg.AddComponent<Image>();
        _simpleOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        _simpleOverlayImage.raycastTarget = false;
        var bgRt = _simpleOverlayImage.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var textGo = new GameObject("YouDiedText");
        textGo.transform.SetParent(root.transform, false);
        _simpleOverlayText = textGo.AddComponent<Text>();
        _simpleOverlayText.raycastTarget = false;
        _simpleOverlayText.alignment = TextAnchor.MiddleCenter;
        _simpleOverlayText.fontSize = 92;
        _simpleOverlayText.color = new Color(0.85f, 0.1f, 0.1f, 1f);
        _simpleOverlayText.fontStyle = FontStyle.Bold;
        _simpleOverlayText.text = "YOU DIED";
        _simpleOverlayText.enabled = false;
        // Unity 최신 버전에서는 Arial.ttf 대신 LegacyRuntime.ttf 사용.
        // (Arial.ttf 요청 시 ArgumentException 발생)
        _simpleOverlayText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var txtRt = _simpleOverlayText.rectTransform;
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = Vector2.zero;
        txtRt.offsetMax = Vector2.zero;

        DontDestroyOnLoad(root);
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
        if (_isReturningToTitle) return;

        // Clear equipped relics for new run
        if (relicManager != null)
            relicManager.ClearEquipped();

        IsDeathInputLocked = false;
        HideSimpleDeathOverlay();
        
        // Load character select
        SceneManager.LoadScene(characterSelectScene);
    }
    
    /// <summary>
    /// Called when player clicks "Main Menu" on death screen
    /// </summary>
    public void OnMainMenuClicked()
    {
        if (_isReturningToTitle) return;
        IsDeathInputLocked = false;
        HideSimpleDeathOverlay();
        SceneManager.LoadScene(mainMenuScene);
    }
    
    /// <summary>
    /// Restart immediately (for testing)
    /// </summary>
    public void RestartRun()
    {
        if (_isReturningToTitle) return;

        if (relicManager != null)
            relicManager.ClearEquipped();
        
        ResetRunStats();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void HideSimpleDeathOverlay()
    {
        if (_simpleOverlayGroup == null || _simpleOverlayImage == null || _simpleOverlayText == null)
            return;

        _simpleOverlayImage.color = new Color(0f, 0f, 0f, 0f);
        _simpleOverlayGroup.alpha = 0f;
        _simpleOverlayText.enabled = false;
        _simpleOverlayGroup.gameObject.SetActive(false);
    }

    private System.Collections.IEnumerator ReturnToTitleAfterDelayRoutine()
    {
        if (_isReturningToTitle)
            yield break;

        _isReturningToTitle = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, autoReturnDelaySeconds));

        DeleteCurrentRunData();
        HideSimpleDeathOverlay();
        SceneManager.LoadScene(mainMenuScene);
    }

    private void DeleteCurrentRunData()
    {
        if (showDebugLogs)
            Debug.Log("[DeathManager] Permadeath: 현재 런 데이터 삭제");

        FieldFreezeController.Clear();
        RunSlotSaveService.DeleteActiveSlot();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetGame();
        }

        var inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        if (inventory != null)
            inventory.ClearAllItemsAndSlots();
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
