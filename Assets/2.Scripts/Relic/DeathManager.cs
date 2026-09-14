using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles player death, statistics, and relic rewards
/// </summary>
public class DeathManager : MonoBehaviour
{
    /// <summary>플레이어 입력 전면 차단 플래그. 사망 연출 외에 런 종료 선택창에서도 쓴다.</summary>
    public static bool IsDeathInputLocked { get; private set; }
    public static void ClearDeathInputLock() => IsDeathInputLocked = false;
    public static void LockInput() => IsDeathInputLocked = true;

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
    [Tooltip("탈출로 런을 성공적으로 끝냈을 때 표시할 문구. 이 오버레이는 레거시 내장 폰트라 한글이 깨지므로 영문으로 둔다.")]
    public string simpleVictoryText = "SURVIVED";
    [Header("Death Flow")]
    [Tooltip("사망 후 자동으로 타이틀(메인 메뉴)로 복귀")]
    public bool autoReturnToTitleOnDeath = true;
    [Range(0.5f, 10f)] public float autoReturnDelaySeconds = 4f;

    [Tooltip("런 종료 시 통계 화면을 띄운다. 켜면 자동 복귀 대신 플레이어가 버튼을 눌러 나간다. " +
             "씬에 DeathScreenUI 가 없으면 런타임에 만들어 쓴다.")]
    public bool showRunEndStats = true;

    [Header("Run Success")]
    [Tooltip("탈출 성공 시 유물 보상 확률에 더해지는 보너스. 죽는 것보다 살아 나오는 게 이득이어야 한다.")]
    [Range(0f, 1f)] public float successRewardBonus = 0.25f;
    
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
    
    // 통계의 실제 소유자는 GameManager 다. 이 매니저는 씬 스코프라 챕터를 넘어가면 새로 생기고,
    // 자체 필드에 들고 있으면 그때마다 처치 수·플레이 타임이 0이 된다.
    // GameManager 가 없을 때만(타이틀 등) 로컬 폴백을 쓴다.
    private RunStatistics _fallbackRunStats = new RunStatistics();

    private RunStatistics _currentRunStats
    {
        get
        {
            var gm = GameManager.Instance;
            if (gm == null) return _fallbackRunStats;
            if (gm.runStats == null) gm.runStats = new RunStatistics();
            return gm.runStats;
        }
    }
    
    // Last death's reward
    private RelicDefinition _lastRewardedRelic;
    // 죽음과 탈출이 공유하는 가드. 보스가 죽는 프레임에 플레이어도 죽으면 두 종료 연출이 겹친다.
    private bool _runEnded;
    private CanvasGroup _simpleOverlayGroup;
    private Image _simpleOverlayImage;
    private Text _simpleOverlayText;
    private bool _isReturningToTitle;
    private DeathScreenUI _statsScreen;
    
    /// <summary>이미 런이 끝났는가(사망이든 탈출이든). 종료 연출이 겹치는 것을 막는 데 쓴다.</summary>
    public bool RunEnded => _runEnded;

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

        EnsureRunEndStatsScreen();

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
        // 통계 자체는 여기서 지우지 않는다. 런 시작 신호는 GameManager.ResetGame() 이다.
        // 예전엔 여기서 매번 새로 만들어서, 챕터를 넘어가면 통계가 통째로 날아갔다.
        _runEnded = false;
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
        if (_runEnded)
            return false;

        _runEnded = true;
        EndRun(false, context, causeOfDeath);
        return true;
    }

    public void ProcessDeath(string causeOfDeath = "Unknown")
    {
        TryHandlePlayerDeath(DeathContext.Unknown, causeOfDeath);
    }

    /// <summary>
    /// 챕터 보스를 잡고 탈출을 선택했을 때. 런이 성공으로 끝난다.
    /// 죽음과 같은 종료 경로를 타되 보상이 더 좋고 문구가 다르다.
    /// </summary>
    public bool TryHandleRunSuccess(string reason = "Extracted")
    {
        if (_runEnded)
            return false;

        _runEnded = true;
        EndRun(true, DeathContext.Dungeon, reason);
        return true;
    }

    private void EndRun(bool success, DeathContext context, string cause)
    {
        IsDeathInputLocked = true;

        // Finalize stats
        // totalPlayTime 은 씬을 넘나들며 누적된 값이다. 여기서 마지막 구간만 적립한다.
        // (예전처럼 runEndTime - runStartTime 으로 덮어쓰면 마지막 챕터 시간만 남는다)
        GameManager.Instance?.AccumulatePlayTime();
        _currentRunStats.runEndTime = Time.time;
        _currentRunStats.causeOfDeath = cause;
        _currentRunStats.victory = success;

        if (showDebugLogs)
        {
            Debug.Log($"[DeathManager] 런 종료 ({(success ? "탈출 성공" : "사망")})! Context: {context}, Cause: {cause}");
            Debug.Log($"  - Kills: {_currentRunStats.totalKills}");
            Debug.Log($"  - Play time: {_currentRunStats.totalPlayTime:F1}s");
            Debug.Log($"  - Highest field: {_currentRunStats.highestFieldReached}");
        }

        // Award relic
        _lastRewardedRelic = DetermineRelicReward(success);

        if (_lastRewardedRelic != null && relicManager != null)
        {
            relicManager.AddRelic(_lastRewardedRelic);

            if (showDebugLogs)
                Debug.Log($"[DeathManager] Awarded relic: {_lastRewardedRelic.relicName} ({_lastRewardedRelic.rarity})");
        }

        // Fire event
        OnPlayerDeath?.Invoke(_currentRunStats, _lastRewardedRelic);

        if (useSimpleDeathOverlay)
            StartCoroutine(ShowSimpleRunEndOverlayRoutine(success));

        // 통계 화면을 쓰면 자동 복귀를 하지 않는다. 플레이어가 결과를 다 보고 버튼으로 나간다.
        if (showRunEndStats && _statsScreen != null)
            return;

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

    /// <summary>
    /// 씬에 DeathScreenUI 가 없으면 런타임 생성. 통계 화면은 사망/생환이 공유한다.
    /// EndRun 의 OnPlayerDeath 이벤트보다 먼저 구독돼 있어야 하므로 Start 에서 만든다.
    /// </summary>
    private void EnsureRunEndStatsScreen()
    {
        if (!showRunEndStats) return;
        if (_statsScreen != null) return;

        _statsScreen = FindObjectOfType<DeathScreenUI>(true);
        if (_statsScreen == null)
            _statsScreen = DeathScreenUI.CreateRuntime();

        // 씬이 바뀌면 이 매니저는 새로 생기므로 화면을 다시 붙여준다.
        _statsScreen.BindTo(this);
    }

    /// <summary>
    /// 통계 화면의 버튼에서 호출. 런 데이터를 지우고 타이틀로 돌아간다.
    /// (기존 OnContinueClicked 는 빌드에 없는 "CharacterSelect" 씬을 로드해 터졌다.)
    /// </summary>
    public void ReturnToTitleNow()
    {
        if (_isReturningToTitle) return;
        _isReturningToTitle = true;

        if (relicManager != null)
            relicManager.ClearEquipped();

        DeleteCurrentRunData();
        HideSimpleDeathOverlay();
        IsDeathInputLocked = false;
        SceneManager.LoadScene(mainMenuScene);
    }

    private System.Collections.IEnumerator ShowSimpleRunEndOverlayRoutine(bool success)
    {
        EnsureSimpleDeathOverlay();
        if (_simpleOverlayGroup == null || _simpleOverlayImage == null || _simpleOverlayText == null)
            yield break;

        string fallback = success ? "SURVIVED" : "YOU DIED";
        string configured = success ? simpleVictoryText : simpleDeathText;
        _simpleOverlayText.text = string.IsNullOrEmpty(configured) ? fallback : configured;
        _simpleOverlayText.color = success
            ? new Color(0.95f, 0.82f, 0.25f, 1f)
            : new Color(0.85f, 0.1f, 0.1f, 1f);
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
    private RelicDefinition DetermineRelicReward(bool success)
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

        // 살아서 나오는 쪽이 죽는 쪽보다 확실히 이득이어야 탈출 선택에 의미가 생긴다.
        if (success)
            bonusChance += successRewardBonus;
        
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
        // characterSelectScene("CharacterSelect")은 빌드 설정에 없는 씬이라 로드하면 터진다.
        // 런이 끝난 뒤 갈 곳은 타이틀뿐이다.
        ReturnToTitleNow();
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

    [Header("Outcome")]
    [Tooltip("탈출로 런을 성공적으로 끝냈으면 true, 죽었으면 false")]
    public bool victory = false;
}
