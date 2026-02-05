using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class FocusedCombatManager : MonoBehaviour
{
    public static FocusedCombatManager Instance { get; private set; }

    [Header("HUD")]
    public CombatHUDController hud;
    public CombatUnitToken PlayerToken => _playerToken;
    public CombatUnitToken EnemyToken => _enemyToken;

    [Header("Roots")]
    public GameObject fieldRoot;
    public GameObject combatUIRoot;

    [Header("Fade")]
    public ScreenFader fader;
    public float fadeOutDuration = 0.25f;
    public float fadeInDuration = 0.25f;

    [Header("Combat Board")]
    public CombatBoardUI boardUI;

    [Header("Unit Token Prefabs (UI)")]
    public CombatUnitToken playerTokenPrefab;
    public CombatUnitToken enemyTokenPrefab; // fallback (definition 없을 때만)

    [Header("Player Hint (UI)")]
    public TMP_Text playerHintPrefab;
    public Vector2 playerHintOffset = new Vector2(0f, 28f);
    private TMP_Text _playerHintInstance;

    [Header("Map Sizes")]
    public Vector2Int[] mapSizes = new Vector2Int[]
    {
        new Vector2Int(9,9),
        new Vector2Int(11,7),
        new Vector2Int(7,11)
    };

    [Header("Generation")]
    public CombatSpawnPlanner spawnPlanner;
    public CombatTerrainGenerator terrainGenerator;
    public bool fallbackToEmptyOnFail = true;

    public CombatGridData gridData { get; private set; }

    [Header("Input Lock Targets (disable during focused combat)")]
    public MonoBehaviour[] disableDuringCombat;

    [Header("Turn / AP")]
    public int startAP = 2;
    public int maxAP = 5;

    [Header("Movement")]
    public int moveRange = 2;
    public float stepDelay = 0.15f;

    [Header("UI (Left Panel)")]
    public TMP_Text apText;
    public TMP_Text hpText;

    [Header("Turn End UI")]
    public Button endTurnButton;
    public KeyCode endTurnKey = KeyCode.Space;
    public bool autoEndTurnWhenAPZero = true;

    [Header("Escape Tile")]
    public bool enableEscapeTile = true;
    public KeyCode escapeKey = KeyCode.Z;
    [Min(0)] public int escapeApCost = 1;
    [Range(0f, 1f)] public float escapeSuccessChance = 0.5f;
    [Tooltip("도망 프롬프트 텍스트")]
    public string escapePrompt = "도망치기";

    [Header("Action Controller")]
    public PlayerCombatActionController actionController;

    [Header("HP (fallback)")]
    public int playerMaxHP = 10;
    public int enemyFallbackMaxHP = 10;

    [Header("Enemy fallback combat (when definition missing)")]
    [Min(1)] public int enemyFallbackMoveRange = 2;
    [Min(1)] public int enemyFallbackAttackRange = 1;
    [Min(0)] public int enemyFallbackAttackDamage = 1;
    public bool enemyFallbackUseDashAttackMotion = true;
    [Range(0f, 0.95f)] public float enemyFallbackBaseEvasion = 0f;

    [Header("Controllers (auto find / add)")]
    public CombatVfxController vfx;
    public CombatTerrainEffectSystem terrainFx;
    public EnemyAIController enemyAI;

    // ✅ Boss AI (AI4)
    public BossAIController bossAI;

    [Header("Grid Blocker (legacy)")]
    public MonoBehaviour blockerBehaviour;
    private ICombatGridBlocker _blocker;

    [Header("Field Sync")]
    [Tooltip("집중전투에서 적이 죽으면 필드의 해당 적 오브젝트를 제거할지")]
    public bool destroyFieldEnemyOnDefeat = true;

    [Header("Time System (Field Integration)")]
    [Tooltip("집중전투에서 턴마다 Time을 소비할지")]
    public bool consumeTimePerTurn = true;
    [Tooltip("플레이어 턴 종료 시 Time 소비량")]
    [Min(0)] public int timePerPlayerTurn = 1;
    [Tooltip("적 턴 종료 시 Time 소비량")]
    [Min(0)] public int timePerEnemyTurn = 1;

    // ===== Runtime =====
    private CombatUnitToken _playerToken;
    private CombatUnitToken _enemyToken;

    private EnemyDefinition _pendingEnemyDef;
    public EnemyDefinition CurrentEnemyDef => _pendingEnemyDef;

    private EncounterContext _pendingContext;

    private int _effectivePlayerMaxHP;
    
    // ✅ Phase 1: 상태이상 & 스탯 참조
    private StatusEffectManager _playerStatusEffects;
    private StatusEffectManager _enemyStatusEffects;
    private PlayerStats _playerStats;
    
    public StatusEffectManager PlayerStatusEffects => _playerStatusEffects;
    public StatusEffectManager EnemyStatusEffects => _enemyStatusEffects;
    public PlayerStats PlayerStatsRef => _playerStats;

    public CombatState State { get; private set; } = new CombatState();
    public CombatMovementService Movement { get; private set; }

    public bool IsInFocusedCombat => State.isInCombat;
    public bool IsBusyForInput => State.isBusy;

    private bool _hasEscapeTile = false;
    private Vector2Int _escapeCell;

    // ✅ Player stun (skip next player turns)
    private int _playerStunTurns = 0;

    // ✅ stun skip pacing
    [Header("Stun Skip UX")]
    public float stunSkipDelay = 0.35f;

    [Header("Field Time Manager")]
    [Tooltip("집중전투에서 Time 소비를 위한 참조")]
    public FieldTimeManager fieldTimeManager;

    // ✅ Transition guard (fade / enter / exit)
    private bool _isTransitioning = false;

    public int PlayerRangeBonus
    {
        get
        {
            if (_playerToken == null) return 0;
            CombatUnitStats s = _playerToken.GetComponent<CombatUnitStats>();
            return (s != null) ? s.rangeBonus : 0;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (combatUIRoot != null) combatUIRoot.SetActive(false);
        if (fader != null) fader.ForceClear();

        ResolveBlocker();

        if (actionController == null)
            actionController = GetComponent<PlayerCombatActionController>();
        if (actionController != null && actionController.combat == null)
            actionController.combat = this;

        if (vfx == null) vfx = GetComponent<CombatVfxController>();
        if (vfx == null) vfx = gameObject.AddComponent<CombatVfxController>();

        if (terrainFx == null) terrainFx = GetComponent<CombatTerrainEffectSystem>();
        if (terrainFx == null) terrainFx = gameObject.AddComponent<CombatTerrainEffectSystem>();

        if (enemyAI == null) enemyAI = GetComponent<EnemyAIController>();
        if (enemyAI == null) enemyAI = gameObject.AddComponent<EnemyAIController>();
        enemyAI.Bind(this);

        if (bossAI == null) bossAI = GetComponent<BossAIController>();
        if (bossAI == null) bossAI = gameObject.AddComponent<BossAIController>();
        bossAI.Bind(this);

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(RequestEndTurn);
            endTurnButton.onClick.AddListener(RequestEndTurn);
        }

        // ✅ FieldTimeManager 찾기
        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();

        _effectivePlayerMaxHP = Mathf.Max(1, playerMaxHP);
    }

    private void Update()
    {
        if (_isTransitioning) return;
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (State.isBusy) return;

        if (Input.GetKeyDown(endTurnKey))
            RequestEndTurn();

        HandleEscapeInput();
    }

    private void HandleEscapeInput()
    {
        if (!enableEscapeTile) return;
        if (!_hasEscapeTile) return;
        if (_isTransitioning) return;
        if (State.isBusy) return;

        if (actionController != null && actionController.IsInActionMode)
            return;

        if (State.playerCell != _escapeCell)
            return;

        if (State.currentAP >= escapeApCost)
            ShowPlayerHint($"{escapePrompt}\n({escapeKey})");
        else
            ShowPlayerHint($"{escapePrompt}\n({escapeKey})\n(AP 부족)");

        if (Input.GetKeyDown(escapeKey))
        {
            if (State.currentAP < escapeApCost) return;
            StartCoroutine(EscapeAttemptRoutine());
        }
    }

    private IEnumerator EscapeAttemptRoutine()
    {
        HidePlayerHint();
        State.isBusy = true;

        // ✅ AP 차감 통일 (코루틴 중 자동 턴종료 금지)
        if (escapeApCost > 0)
            SpendAP(escapeApCost, allowAutoEndTurn: false, refreshUI: true);

        bool success = (Random.value < escapeSuccessChance);

        if (vfx != null)
        {
            if (success) vfx.ShowPopup(_playerToken, "도망에 성공했다!");
            else vfx.ShowPopup(_playerToken, "도망에 실패했다!");
        }

        yield return new WaitForSeconds(0.5f);

        State.isBusy = false;

        if (!State.isInCombat) yield break;

        if (success) ExitFocusedCombat();
        else BeginEnemyTurn();
    }

    private void ResolveBlocker()
    {
        _blocker = null;
        if (blockerBehaviour == null) return;

        _blocker = blockerBehaviour as ICombatGridBlocker;
        if (_blocker == null)
            _blocker = blockerBehaviour.GetComponent<ICombatGridBlocker>();
    }

    // ==========================
    // Entry
    // ==========================
    public void EnterFocusedCombat()
    {
        if (State.isInCombat) return;
        StartCoroutine(EnterRoutine());
    }

    public void EnterFocusedCombat(EncounterContext ctx)
    {
        if (State.isInCombat) return;
        if (ctx == null || ctx.enemy == null || ctx.enemy.definition == null) return;

        _pendingContext = ctx;
        _pendingEnemyDef = ctx.enemy.definition;

        StartCoroutine(EnterRoutine());
    }

    public void EnterFocusedCombat(EnemyDefinition def)
    {
        _pendingContext = null;
        _pendingEnemyDef = def;
        EnterFocusedCombat();
    }

    public void ExitFocusedCombat()
    {
        if (!State.isInCombat) return;
        StartCoroutine(ExitRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        _isTransitioning = true;
        HidePlayerHint();

        State.isInCombat = true;

        if (fader == null)
        {
            DoEnter();
            yield break;
        }

        yield return StartCoroutine(fader.FadeOutIn(DoEnter, fadeOutDuration, fadeInDuration));
    }

    private IEnumerator ExitRoutine()
    {
        _isTransitioning = true;
        HidePlayerHint();

        if (fader == null)
        {
            DoExit();
            yield break;
        }

        yield return StartCoroutine(fader.FadeOutIn(DoExit, fadeOutDuration, fadeInDuration));
    }

    private void DoEnter()
    {
        SetDisableDuringCombat(true);

        if (fieldRoot != null) fieldRoot.SetActive(false);
        if (combatUIRoot != null) combatUIRoot.SetActive(true);

        ResolveBlocker();

        Vector2Int size = mapSizes[Random.Range(0, mapSizes.Length)];

        if (boardUI != null)
        {
            boardUI.Build(size.x, size.y);
            boardUI.OnTileClicked -= HandleTileClicked;
            boardUI.OnTileClicked += HandleTileClicked;
        }

        if (spawnPlanner != null)
            spawnPlanner.PickEdgeSpawns(size.x, size.y, out State.playerCell, out State.enemyCell);
        else
        {
            int cx = (size.x - 1) / 2;
            State.playerCell = new Vector2Int(cx, 0);
            State.enemyCell = new Vector2Int(cx, size.y - 1);
        }

        gridData = null;
        if (terrainGenerator != null)
        {
            bool ok = terrainGenerator.Generate(size.x, size.y, State.playerCell, State.enemyCell, out CombatGridData gen);
            if (ok) gridData = gen;
            else if (fallbackToEmptyOnFail) gridData = new CombatGridData(size.x, size.y);
        }
        else gridData = new CombatGridData(size.x, size.y);

        if (boardUI != null && gridData != null)
            boardUI.ApplyTerrainVisual(gridData);

        if (boardUI != null)
        {
            if (playerTokenPrefab != null)
                _playerToken = boardUI.PlaceToken(playerTokenPrefab, State.playerCell.x, State.playerCell.y);

            CombatUnitToken enemyPrefabToUse = enemyTokenPrefab;
            if (_pendingEnemyDef != null && _pendingEnemyDef.tokenPrefab != null)
                enemyPrefabToUse = _pendingEnemyDef.tokenPrefab;

            if (enemyPrefabToUse != null)
                _enemyToken = boardUI.PlaceToken(enemyPrefabToUse, State.enemyCell.x, State.enemyCell.y);
        }

        vfx.Bind(boardUI);

        if (_pendingContext != null && _pendingContext.playerStats != null)
        {
            _effectivePlayerMaxHP = Mathf.Max(1, _pendingContext.playerStats.maxHP);
            State.playerHP = Mathf.Clamp(_pendingContext.playerStats.hp, 0, _effectivePlayerMaxHP);
        }
        else
        {
            _effectivePlayerMaxHP = Mathf.Max(1, playerMaxHP);
            State.playerHP = _effectivePlayerMaxHP;
        }

        if (_pendingContext != null && _pendingContext.enemy != null)
        {
            int maxEnemy = Mathf.Max(1, GetEnemyMaxHP());
            int ehp = Mathf.Clamp(_pendingContext.enemy.currentHP, 0, maxEnemy);
            if (ehp <= 0) ehp = maxEnemy;
            State.enemyHP = ehp;
        }
        else
        {
            State.enemyHP = Mathf.Max(1, GetEnemyMaxHP());
        }

        State.startAP = startAP;
        State.maxAP = maxAP;
        State.currentAP = Mathf.Clamp(startAP, 0, maxAP);
        State.freeMovesUsedThisTurn = 0;

        Movement = new CombatMovementService(
            moveRange: moveRange,
            isBlocked: IsBlocked,
            inBounds: (x, y) => boardUI != null && boardUI.InBounds(x, y)
        );

        terrainFx.Apply(gridData, State.playerCell, _playerToken);
        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        BuildEscapeTile();

        _playerStunTurns = 0;
        
        // ✅ Phase 1: 상태이상 매니저 초기화
        InitializeStatusEffects();
        
        // ✅ Phase 1: 플레이어 스탯 참조 캐싱
        if (_pendingContext != null && _pendingContext.playerStats != null)
            _playerStats = _pendingContext.playerStats;
        else
            _playerStats = FindObjectOfType<PlayerStats>();

        if (bossAI != null) bossAI.ResetBossState();

        BeginPlayerTurn(initialStart: true);
        HidePlayerHint();

        if (hud != null)
            hud.Bind(this);

        RefreshUI();

        _isTransitioning = false;
    }

    private void BuildEscapeTile()
    {
        _hasEscapeTile = false;

        if (!enableEscapeTile) return;
        if (boardUI == null || gridData == null) return;

        boardUI.ClearSpecials();

        const int MAX_TRY = 500;
        for (int i = 0; i < MAX_TRY; i++)
        {
            int x = Random.Range(0, gridData.width);
            int y = Random.Range(0, gridData.height);

            Vector2Int c = new Vector2Int(x, y);

            if (c == State.playerCell) continue;
            if (c == State.enemyCell) continue;
            if (IsBlocked(c)) continue;

            _escapeCell = c;
            _hasEscapeTile = true;
            boardUI.SetEscapeTile(_escapeCell.x, _escapeCell.y, true);
            break;
        }
    }

    private void DoExit()
    {
        HidePlayerHint();

        if (_pendingContext != null)
        {
            if (_pendingContext.playerStats != null)
            {
                _pendingContext.playerStats.hp = Mathf.Clamp(State.playerHP, 0, _pendingContext.playerStats.maxHP);
                _pendingContext.playerStats.ClampAll();
            }

            if (_pendingContext.enemy != null && _pendingContext.enemy.definition != null)
            {
                int maxEnemy = Mathf.Max(1, _pendingContext.enemy.definition.maxHP);
                _pendingContext.enemy.currentHP = Mathf.Clamp(State.enemyHP, 0, maxEnemy);

                if (destroyFieldEnemyOnDefeat && _pendingContext.enemy.currentHP <= 0)
                {
                    Destroy(_pendingContext.enemy.gameObject);
                }
            }
        }

        if (boardUI != null)
        {
            boardUI.OnTileClicked -= HandleTileClicked;
            boardUI.ClearHighlights();
            boardUI.ClearSpecials();
        }

        if (actionController != null)
            actionController.CancelAction();

        if (combatUIRoot != null) combatUIRoot.SetActive(false);
        if (fieldRoot != null) fieldRoot.SetActive(true);

        SetDisableDuringCombat(false);

        State.isInCombat = false;
        State.isBusy = false;

        gridData = null;
        _hasEscapeTile = false;
        
        // ✅ Phase 1: 상태이상 정리
        CleanupStatusEffects();

        _pendingEnemyDef = null;
        _pendingContext = null;
        _playerStats = null;

        _effectivePlayerMaxHP = Mathf.Max(1, playerMaxHP);

        if (fader != null)
            fader.ForceClear();

        _isTransitioning = false;
    }

    // ==========================
    // Turn Rules
    // ==========================
    public void RequestEndTurn()
    {
        if (_isTransitioning) return;
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (State.isBusy) return;

        if (actionController != null && actionController.IsInActionMode)
            actionController.CancelAction();

        // ✅ 플레이어 턴 종료 시 Time 소비
        ConsumeTimeForPlayerTurn();

        BeginEnemyTurn();
    }

    // ✅ apply player stun from boss (popup safe)
    public void ApplyPlayerStun(int turns)
    {
        _playerStunTurns = Mathf.Max(_playerStunTurns, Mathf.Max(0, turns));

        if (vfx != null)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null)
                vfx.popupPrefab = playerHintPrefab;

            vfx.ShowPopup(_playerToken, "기절!");
        }
    }

    private void BeginPlayerTurn(bool initialStart)
    {
        State.isPlayerTurn = true;
        State.isBusy = false;

        if (!initialStart)
            State.currentAP = Mathf.Min(State.currentAP + 1, State.maxAP);

        if (hud != null) hud.RefreshAll();

        State.ResetTurn();
        
        // ✅ Phase 1: 플레이어 턴 시작 시 스턴 적용자 추적 초기화
        if (_playerStatusEffects != null)
            _playerStatusEffects.OnTurnStart();

        // ✅ STUN: delay + then give enemy turn (so it doesn't look like double-hit instant)
        if (_playerStunTurns > 0)
        {
            _playerStunTurns--;

            if (actionController != null && actionController.IsInActionMode)
                actionController.CancelAction();

            if (boardUI != null) boardUI.ClearHighlights();
            HidePlayerHint();

            StartCoroutine(StunSkipToEnemyTurnRoutine());
            return;
        }
        
        // ✅ Phase 1: 플레이어가 스턴 상태이상이면 AP 감소
        if (_playerStatusEffects != null && _playerStatusEffects.IsStunned)
        {
            int apReduction = StatusEffectConstants.STUN_AP_REDUCTION;
            if (State.currentAP >= apReduction)
            {
                State.currentAP -= apReduction;
                if (vfx != null) vfx.ShowPopup(_playerToken, $"스턴! AP -{apReduction}");
            }
            else
            {
                // AP가 부족하면 턴 스킵
                if (vfx != null) vfx.ShowPopup(_playerToken, "기절!");
                StartCoroutine(StunSkipToEnemyTurnRoutine());
                return;
            }
        }

        terrainFx.Apply(gridData, State.playerCell, _playerToken);
        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        RefreshUI();
        RefreshMoveHighlights();
    }

    private IEnumerator StunSkipToEnemyTurnRoutine()
    {
        State.isBusy = true;
        RefreshUI();

        if (stunSkipDelay > 0f)
            yield return new WaitForSeconds(stunSkipDelay);

        State.isBusy = false;

        if (!State.isInCombat) yield break;
        BeginEnemyTurn();
    }

    private void BeginEnemyTurn()
    {
        State.isPlayerTurn = false;
        State.isBusy = false;
        
        // ✅ Phase 1: 플레이어 턴 종료 시 상태이상 처리 (출혈 피해 등)
        ProcessTurnEndStatusEffects(isPlayerTurn: true);
        
        // ✅ Phase 1: 적 턴 시작 시 스턴 적용자 추적 초기화
        if (_enemyStatusEffects != null)
            _enemyStatusEffects.OnTurnStart();

        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);
        terrainFx.Apply(gridData, State.playerCell, _playerToken);

        RefreshUI();
        if (boardUI != null) boardUI.ClearHighlights();

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        // ✅ Phase 1: 적이 스턴 상태면 턴 스킵
        if (_enemyStatusEffects != null && _enemyStatusEffects.IsStunned)
        {
            if (vfx != null) vfx.ShowPopup(_enemyToken, "기절!");
            yield return new WaitForSeconds(0.5f);
            
            // 적 턴 종료 시 상태이상 처리
            ProcessTurnEndStatusEffects(isPlayerTurn: false);
            ConsumeTimeForEnemyTurn();
            
            if (!State.isInCombat) yield break;
            BeginPlayerTurn(initialStart: false);
            yield break;
        }
        
        EnemyAIType type = GetEnemyAIType();

        if (type == EnemyAIType.AI4_Boss)
        {
            if (bossAI != null)
                yield return StartCoroutine(bossAI.TakeTurn());
        }
        else
        {
            if (enemyAI != null)
                yield return StartCoroutine(enemyAI.TakeTurn());
        }
        
        // ✅ Phase 1: 적 턴 종료 시 상태이상 처리 (출혈 피해 등)
        ProcessTurnEndStatusEffects(isPlayerTurn: false);

        // ✅ 적 턴 종료 시 Time 소비
        ConsumeTimeForEnemyTurn();

        if (!State.isInCombat) yield break;
        BeginPlayerTurn(initialStart: false);
    }

    // ==========================
    // Tile Click Routing
    // ==========================
    private void HandleTileClicked(int x, int y)
    {
        if (actionController != null && actionController.IsInActionMode)
        {
            actionController.OnTileClicked(x, y);
            return;
        }

        HandleMoveTileClicked(x, y);
    }

    // ==========================
    // Player Move
    // ==========================
    private void HandleMoveTileClicked(int x, int y)
    {
        if (_isTransitioning) return;
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (_playerToken == null) return;
        if (State.isBusy) return;

        Vector2Int target = new Vector2Int(x, y);
        if (target == State.playerCell) return;
        if (target == State.enemyCell) return;

        CombatUnitStats ps = _playerToken.GetComponent<CombatUnitStats>();
        int freeTotal = (ps != null) ? ps.FreeMovesPerTurn : 1;

        bool isFree = State.freeMovesUsedThisTurn < freeTotal;
        int moveCost = isFree ? 0 : 1;

        if (moveCost > 0 && State.currentAP < moveCost) return;

        if (!Movement.TryBuildPath(State.playerCell, target, State.enemyCell, out Vector2Int[] steps))
            return;

        StartCoroutine(PlayerMoveRoutine(steps, moveCost));
    }

    private IEnumerator PlayerMoveRoutine(Vector2Int[] steps, int moveCost)
    {
        State.isBusy = true;

        if (boardUI != null) boardUI.ClearHighlights();
        HidePlayerHint();

        bool spentAP = false;
        bool spentFree = false;

        // ✅ AP 차감 통일 (코루틴 중 자동 턴종료 금지)
        if (moveCost > 0)
        {
            SpendAP(moveCost, allowAutoEndTurn: false, refreshUI: true);
            spentAP = true;
        }
        else
        {
            State.freeMovesUsedThisTurn++;
            spentFree = true;

            if (hud != null) hud.RefreshAll();
            RefreshUI();
        }

        for (int i = 0; i < steps.Length; i++)
        {
            Vector2Int step = steps[i];

            if (IsBlocked(step) || step == State.enemyCell)
            {
                // 롤백
                if (spentAP)
                {
                    State.currentAP = Mathf.Clamp(State.currentAP + moveCost, 0, State.maxAP);
                }
                if (spentFree)
                {
                    State.freeMovesUsedThisTurn = Mathf.Max(0, State.freeMovesUsedThisTurn - 1);
                }

                State.isBusy = false;

                if (hud != null) hud.RefreshAll();
                RefreshUI();
                RefreshMoveHighlights();
                yield break;
            }

            yield return StartCoroutine(vfx.StepMoveToCell(_playerToken, step));
            boardUI.MoveExistingToken(_playerToken, step.x, step.y);
            State.playerCell = step;

            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        terrainFx.Apply(gridData, State.playerCell, _playerToken);

        State.isBusy = false;

        if (hud != null) hud.RefreshAll();
        RefreshUI();
        RefreshMoveHighlights();
        HidePlayerHint();

        TryAutoEndTurn();
    }

    // ==========================
    // Enemy Move / Attack (AI calls)
    // ==========================
    public IEnumerator EnemyMoveRoutine(Vector2Int[] steps)
    {
        if (_enemyToken == null) yield break;

        State.isBusy = true;

        for (int i = 0; i < steps.Length; i++)
        {
            Vector2Int step = steps[i];
            if (IsBlocked(step) || step == State.playerCell)
            {
                State.isBusy = false;
                yield break;
            }

            yield return StartCoroutine(vfx.StepMoveToCell(_enemyToken, step));
            boardUI.MoveExistingToken(_enemyToken, step.x, step.y);
            State.enemyCell = step;

            if (stepDelay > 0f) yield return new WaitForSeconds(stepDelay);
            else yield return null;
        }

        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        State.isBusy = false;
    }

    public IEnumerator EnemyAttackRoutine()
    {
        if (_enemyToken == null) yield break;

        State.isBusy = true;

        int dmg = Mathf.Max(0, GetEnemyAttackDamage());
        bool dash = GetEnemyUseDash();

        if (dash)
        {
            yield return StartCoroutine(vfx.EnemyDashHitReturn(
                attacker: _enemyToken,
                targetCell: State.playerCell,
                onImpact: () => TryDamagePlayer(dmg)
            ));
        }
        else
        {
            TryDamagePlayer(dmg);
            if (_playerToken != null) yield return StartCoroutine(vfx.HitPulse(_playerToken));
        }

        if (boardUI != null)
            boardUI.MoveExistingToken(_enemyToken, State.enemyCell.x, State.enemyCell.y);

        State.isBusy = false;
    }

    public IEnumerator Anim_PlayerDashHitReturn(Vector2Int targetCell, System.Action onImpact)
    {
        if (_playerToken == null) yield break;

        State.isBusy = true;

        yield return StartCoroutine(vfx.DashHitReturn(
            attacker: _playerToken,
            targetCell: targetCell,
            onImpact: onImpact,
            goTime: 0.06f,
            pauseTime: 0.03f,
            backTime: 0.07f
        ));

        if (boardUI != null)
            boardUI.MoveExistingToken(_playerToken, State.playerCell.x, State.playerCell.y);

        State.isBusy = false;
    }

    // ==========================
    // Damage / Evade
    // ==========================
    public bool TryDamageEnemy(int dmg)
    {
        if (dmg <= 0) return false;
        if (_enemyToken == null) return false;

        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        float evasion = GetEnemyBaseEvasion();
        CombatUnitStats cs = _enemyToken.GetComponent<CombatUnitStats>();
        if (cs != null) evasion = Mathf.Clamp01(evasion + cs.Evasion);

        if (evasion > 0f && Random.value < evasion)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null) vfx.popupPrefab = playerHintPrefab;
            vfx.ShowPopup(_enemyToken, "회피!");
            return false;
        }

        // Boss meditation damage reduction (50%)
        if (GetEnemyAIType() == EnemyAIType.AI4_Boss && bossAI != null)
        {
            BossState bs = bossAI.GetState();
            if (bs != null && bs.MeditationActive)
                dmg = Mathf.Max(0, Mathf.RoundToInt(dmg * 0.5f));
        }

        State.enemyHP = Mathf.Max(0, State.enemyHP - dmg);
        StartCoroutine(vfx.HitPulse(_enemyToken));

        RefreshUI();

        if (State.enemyHP <= 0)
            ExitFocusedCombat();

        return true;
    }

    public bool TryDamagePlayer(int dmg)
    {
        if (dmg <= 0) return false;
        if (_playerToken == null) return false;

        terrainFx.Apply(gridData, State.playerCell, _playerToken);

        CombatUnitStats s = _playerToken.GetComponent<CombatUnitStats>();
        float evasion = (s != null) ? s.Evasion : 0f;

        if (evasion > 0f && Random.value < evasion)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null) vfx.popupPrefab = playerHintPrefab;
            vfx.ShowPopup(_playerToken, "회피!");
            return false;
        }

        State.playerHP = Mathf.Max(0, State.playerHP - dmg);
        StartCoroutine(vfx.HitPulse(_playerToken));

        RefreshUI();
        return true;
    }

    // ==========================
    // Block / AP (player)
    // ==========================
    public bool IsBlocked(Vector2Int cell)
    {
        if (gridData != null)
        {
            if (!gridData.InBounds(cell.x, cell.y)) return true;
            if (gridData.Get(cell.x, cell.y).ToString() == "Wall") return true;
        }

        if (_blocker == null) return false;
        return _blocker.IsBlocked(cell.x, cell.y);
    }

    public bool CanSpendAP(int amount) => amount <= State.currentAP;

    /// <summary>
    /// ✅ AP 차감 통일 메서드.
    /// - 코루틴/연출 중에는 allowAutoEndTurn=false 권장 (중간에 턴 끝나는 부작용 방지)
    /// </summary>
    public void SpendAP(int amount, bool allowAutoEndTurn = true, bool refreshUI = true)
    {
        int delta = Mathf.Max(0, amount);
        if (delta <= 0) return;

        State.currentAP = Mathf.Max(0, State.currentAP - delta);

        if (refreshUI)
        {
            if (hud != null) hud.RefreshAll();
            RefreshUI();
        }

        if (allowAutoEndTurn)
            TryAutoEndTurn();
    }

    private void TryAutoEndTurn()
    {
        if (!autoEndTurnWhenAPZero) return;
        if (_isTransitioning) return;
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (State.isBusy) return;

        if (State.currentAP <= 0)
            RequestEndTurn();
    }

    // ==========================
    // UI
    // ==========================
    public void RefreshUIExternal() => RefreshUI();
    public void RefreshMoveHighlightsExternal() => RefreshMoveHighlights();

    private void RefreshUI()
    {
        int freeTotal = 1;
        if (_playerToken != null)
        {
            CombatUnitStats ps = _playerToken.GetComponent<CombatUnitStats>();
            freeTotal = (ps != null) ? ps.FreeMovesPerTurn : 1;
        }

        if (apText != null)
            apText.text = $"AP: {State.currentAP}/{State.maxAP}  | Free Move: {State.freeMovesUsedThisTurn}/{freeTotal}";

        if (hpText != null)
            hpText.text = $"HP: {State.playerHP}/{_effectivePlayerMaxHP}";

        if (hud != null) hud.RefreshAll();
    }

    private void RefreshMoveHighlights()
    {
        if (boardUI == null) return;
        if (actionController != null && actionController.IsInActionMode) return;

        boardUI.ClearHighlights();

        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (_playerToken == null) return;
        if (State.isBusy) return;

        CombatUnitStats ps = _playerToken.GetComponent<CombatUnitStats>();
        int freeTotal = (ps != null) ? ps.FreeMovesPerTurn : 1;

        bool isFree = State.freeMovesUsedThisTurn < freeTotal;
        int moveCost = isFree ? 0 : 1;
        if (moveCost > 0 && State.currentAP < moveCost) return;

        int r = Mathf.Max(1, moveRange);

        for (int x = State.playerCell.x - r; x <= State.playerCell.x + r; x++)
            for (int y = State.playerCell.y - r; y <= State.playerCell.y + r; y++)
            {
                if (!boardUI.InBounds(x, y)) continue;

                Vector2Int target = new Vector2Int(x, y);
                if (target == State.playerCell) continue;
                if (target == State.enemyCell) continue;

                int dx = Mathf.Abs(x - State.playerCell.x);
                int dy = Mathf.Abs(y - State.playerCell.y);
                if (Mathf.Max(dx, dy) > r) continue;

                if (Movement.TryBuildPath(State.playerCell, target, State.enemyCell, out _))
                    boardUI.SetHighlight(x, y, true);
            }
    }

    // ==========================
    // Hint
    // ==========================
    public void ShowPlayerHint(string text)
    {
        if (_isTransitioning) return;
        if (_playerToken == null) return;
        if (playerHintPrefab == null) return;

        if (_playerHintInstance == null)
        {
            _playerHintInstance = Instantiate(playerHintPrefab, _playerToken.transform);
            _playerHintInstance.name = "PlayerHintTMP";

            var rt = _playerHintInstance.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = playerHintOffset;
            rt.localScale = Vector3.one;
        }

        _playerHintInstance.text = text;
        _playerHintInstance.gameObject.SetActive(true);
    }

    public void HidePlayerHint()
    {
        if (_playerHintInstance != null)
            _playerHintInstance.gameObject.SetActive(false);
    }

    // ==========================
    // Enemy Stat Access
    // ==========================
    public int GetEnemyMaxHP()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.maxHP : enemyFallbackMaxHP;

    public int GetEnemyMoveRange()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.moveRange : enemyFallbackMoveRange;

    public int GetEnemyAttackRange()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.attackRange : enemyFallbackAttackRange;

    public int GetEnemyAttackDamage()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.attackDamage : enemyFallbackAttackDamage;

    public bool GetEnemyUseDash()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.useDashAttackMotion : enemyFallbackUseDashAttackMotion;

    public float GetEnemyBaseEvasion()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.baseEvasion : enemyFallbackBaseEvasion;

    public EnemyAIType GetEnemyAIType()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.aiType : EnemyAIType.AI1_MeleeChase;

    // ==========================
    // Input Lock
    // ==========================
    private void SetDisableDuringCombat(bool disable)
    {
        if (disableDuringCombat == null) return;

        for (int i = 0; i < disableDuringCombat.Length; i++)
        {
            if (disableDuringCombat[i] == null) continue;
            disableDuringCombat[i].enabled = !disable;
        }
    }

    public IEnumerator EnemyAttackRoutineOverride(int damageOverride, bool forceDash)
    {
        if (EnemyToken == null) yield break;

        State.isBusy = true;

        int dmg = Mathf.Max(0, damageOverride);
        bool dash = forceDash;

        if (dash)
        {
            yield return StartCoroutine(vfx.EnemyDashHitReturn(
                attacker: EnemyToken,
                targetCell: State.playerCell,
                onImpact: () => TryDamagePlayer(dmg)
            ));
        }
        else
        {
            TryDamagePlayer(dmg);
            if (PlayerToken != null) yield return StartCoroutine(vfx.HitPulse(PlayerToken));
        }

        if (boardUI != null)
            boardUI.MoveExistingToken(EnemyToken, State.enemyCell.x, State.enemyCell.y);

        State.isBusy = false;
    }

    // ==========================
    // Time System (Field Integration)
    // ==========================
    private void ConsumeTimeForPlayerTurn()
    {
        if (!consumeTimePerTurn) return;
        if (timePerPlayerTurn <= 0) return;
        if (fieldTimeManager == null) return;

        fieldTimeManager.Advance(timePerPlayerTurn);
    }

    private void ConsumeTimeForEnemyTurn()
    {
        if (!consumeTimePerTurn) return;
        if (timePerEnemyTurn <= 0) return;
        if (fieldTimeManager == null) return;

        fieldTimeManager.Advance(timePerEnemyTurn);
    }
    
    // ==========================
    // Phase 1: Status Effect System
    // ==========================
    private void InitializeStatusEffects()
    {
        // 플레이어 토큰에 StatusEffectManager 추가/가져오기
        if (_playerToken != null)
        {
            _playerStatusEffects = _playerToken.GetComponent<StatusEffectManager>();
            if (_playerStatusEffects == null)
                _playerStatusEffects = _playerToken.gameObject.AddComponent<StatusEffectManager>();
            _playerStatusEffects.ClearAll();
        }
        
        // 적 토큰에 StatusEffectManager 추가/가져오기
        if (_enemyToken != null)
        {
            _enemyStatusEffects = _enemyToken.GetComponent<StatusEffectManager>();
            if (_enemyStatusEffects == null)
                _enemyStatusEffects = _enemyToken.gameObject.AddComponent<StatusEffectManager>();
            _enemyStatusEffects.ClearAll();
        }
    }
    
    private void CleanupStatusEffects()
    {
        if (_playerStatusEffects != null)
            _playerStatusEffects.ClearAll();
        if (_enemyStatusEffects != null)
            _enemyStatusEffects.ClearAll();
            
        _playerStatusEffects = null;
        _enemyStatusEffects = null;
    }
    
    /// <summary>
    /// 적 방어력 가져오기
    /// </summary>
    public int GetEnemyDEF()
        => (_pendingEnemyDef != null) ? _pendingEnemyDef.defense : 0;
    
    /// <summary>
    /// 플레이어 → 적 피해 (전투 공식 적용)
    /// </summary>
    /// <param name="skillBonusDamage">스킬 추가 피해</param>
    /// <param name="forceCrit">확정 치명타</param>
    /// <param name="attackMultiplier">공격력 배율</param>
    /// <returns>피해 결과</returns>
    public DamageResult DamageEnemyWithFormula(int skillBonusDamage = 0, bool forceCrit = false, float attackMultiplier = 1f)
    {
        if (_playerStats == null || _enemyToken == null)
        {
            // fallback: 기존 방식
            int fallbackDmg = Mathf.Max(1, _playerStats?.ATK ?? 10);
            TryDamageEnemy(fallbackDmg);
            return new DamageResult { finalDamage = fallbackDmg };
        }
        
        // 지형 효과 적용
        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);
        
        // 적 회피율
        float enemyEVA = GetEnemyBaseEvasion();
        CombatUnitStats cs = _enemyToken.GetComponent<CombatUnitStats>();
        if (cs != null) enemyEVA = Mathf.Clamp01(enemyEVA + cs.Evasion);
        
        // 전투 공식으로 계산
        var result = CombatCalculator.CalculatePlayerAttack(
            _playerStats,
            _enemyStatusEffects,
            skillBonusDamage,
            GetEnemyDEF(),
            enemyEVA,
            forceCrit,
            attackMultiplier
        );
        
        if (result.isEvaded)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null) vfx.popupPrefab = playerHintPrefab;
            vfx.ShowPopup(_enemyToken, "회피!");
            return result;
        }
        
        // Boss meditation damage reduction
        if (GetEnemyAIType() == EnemyAIType.AI4_Boss && bossAI != null)
        {
            BossState bs = bossAI.GetState();
            if (bs != null && bs.MeditationActive)
                result.finalDamage = Mathf.Max(1, Mathf.RoundToInt(result.finalDamage * 0.5f));
        }
        
        // 피해 적용
        State.enemyHP = Mathf.Max(0, State.enemyHP - result.finalDamage);
        StartCoroutine(vfx.HitPulse(_enemyToken));
        
        // 치명타 팝업
        if (result.isCritical)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null) vfx.popupPrefab = playerHintPrefab;
            vfx.ShowPopup(_enemyToken, "치명타!");
        }
        
        RefreshUI();
        
        if (State.enemyHP <= 0)
            ExitFocusedCombat();
        
        return result;
    }
    
    /// <summary>
    /// 적 → 플레이어 피해 (전투 공식 적용)
    /// </summary>
    public DamageResult DamagePlayerWithFormula(int enemyATK)
    {
        if (_playerStats == null || _playerToken == null)
        {
            // fallback
            TryDamagePlayer(enemyATK);
            return new DamageResult { finalDamage = enemyATK };
        }
        
        // 지형 효과 적용
        terrainFx.Apply(gridData, State.playerCell, _playerToken);
        
        // 전투 공식으로 계산
        var result = CombatCalculator.CalculateEnemyAttack(
            enemyATK,
            _playerStats,
            _playerStatusEffects
        );
        
        if (result.isEvaded)
        {
            if (vfx.popupPrefab == null && playerHintPrefab != null) vfx.popupPrefab = playerHintPrefab;
            vfx.ShowPopup(_playerToken, "회피!");
            return result;
        }
        
        // 피해 적용
        State.playerHP = Mathf.Max(0, State.playerHP - result.finalDamage);
        StartCoroutine(vfx.HitPulse(_playerToken));
        
        RefreshUI();
        
        return result;
    }
    
    /// <summary>
    /// 턴 종료 시 상태이상 처리 (출혈 등)
    /// </summary>
    public void ProcessTurnEndStatusEffects(bool isPlayerTurn)
    {
        if (isPlayerTurn && _playerStatusEffects != null)
        {
            int bleedDmg = _playerStatusEffects.OnTurnEnd();
            if (bleedDmg > 0)
            {
                State.playerHP = Mathf.Max(0, State.playerHP - bleedDmg);
                if (vfx != null) vfx.ShowPopup(_playerToken, $"출혈 -{bleedDmg}");
                RefreshUI();
            }
        }
        else if (!isPlayerTurn && _enemyStatusEffects != null)
        {
            int bleedDmg = _enemyStatusEffects.OnTurnEnd();
            if (bleedDmg > 0)
            {
                State.enemyHP = Mathf.Max(0, State.enemyHP - bleedDmg);
                if (vfx != null) vfx.ShowPopup(_enemyToken, $"출혈 -{bleedDmg}");
                RefreshUI();
                
                if (State.enemyHP <= 0)
                    ExitFocusedCombat();
            }
        }
    }
}
