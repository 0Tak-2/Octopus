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

    // ===== 다중 적 =====
    private System.Collections.Generic.List<CombatEnemyState> _enemyStates
        = new System.Collections.Generic.List<CombatEnemyState>();
    private int _activeEnemyIndex = 0;

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

        EnsureDisableDuringCombatTargets();

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

    // ★★★ 이 메서드 전체로 기존 DoEnter()를 교체하세요 ★★★
    // ★★★ FocusedCombatManager.cs 의 private void DoEnter() { ... } 전체 ★★★

    private void DoEnter()
    {
        SetDisableDuringCombat(true);
        ApplyFieldHudCombatMode(true);

        if (fieldRoot != null) fieldRoot.SetActive(false);
        if (combatUIRoot != null) combatUIRoot.SetActive(true);

        ResolveBlocker();

        // ============ 맵 생성 분기 ============
        BuildResult buildResult = null;
        Vector2Int size;

        if (_pendingContext != null && _pendingContext.HasEnvironment)
        {
            // 신 주시 시스템: 10x10 환경 스캔 기반
            buildResult = FocusCombatBuilder.Build(_pendingContext);
            size = new Vector2Int(buildResult.gridData.width, buildResult.gridData.height);
        }
        else
        {
            // 기존 1v1: 랜덤 크기
            size = mapSizes[Random.Range(0, mapSizes.Length)];
        }

        if (boardUI != null)
        {
            boardUI.Build(size.x, size.y);
            boardUI.OnTileClicked -= HandleTileClicked;
            boardUI.OnTileClicked += HandleTileClicked;
        }

        if (buildResult != null)
        {
            // 신 시스템: 빌더가 만든 스폰 + 지형 사용
            State.playerCell = buildResult.playerSpawn;
            State.enemyCell = buildResult.primaryEnemySpawn;
            gridData = buildResult.gridData;
        }
        else
        {
            // 기존 시스템: 스폰 플래너 + 지형 생성기
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
        }

        if (boardUI != null && gridData != null)
            boardUI.ApplyTerrainVisual(gridData);

        // ============ 토큰 배치 ============
        if (boardUI != null)
        {
            // 플레이어 토큰
            if (playerTokenPrefab != null)
                _playerToken = boardUI.PlaceToken(playerTokenPrefab, State.playerCell.x, State.playerCell.y);

            if (buildResult != null && _pendingContext != null && _pendingContext.IsMultiEnemy)
            {
                // 신 시스템: 다중 적 토큰 배치
                for (int i = 0; i < _pendingContext.enemies.Count; i++)
                {
                    var entry = _pendingContext.enemies[i];
                    if (entry.instance == null || entry.instance.definition == null) continue;

                    CombatUnitToken prefab = entry.instance.definition.tokenPrefab ?? enemyTokenPrefab;
                    if (prefab == null) continue;

                    Vector2Int spawn = buildResult.enemySpawns[i];
                    var token = boardUI.PlaceToken(prefab, spawn.x, spawn.y);

                    // 주시 대상(인덱스 0)은 기존 _enemyToken에도 할당 (하위 호환)
                    if (i == 0)
                        _enemyToken = token;
                }
            }
            else
            {
                // 기존 1v1: 적 토큰 1개
                CombatUnitToken enemyPrefabToUse = enemyTokenPrefab;
                if (_pendingEnemyDef != null && _pendingEnemyDef.tokenPrefab != null)
                    enemyPrefabToUse = _pendingEnemyDef.tokenPrefab;

                if (enemyPrefabToUse != null)
                    _enemyToken = boardUI.PlaceToken(enemyPrefabToUse, State.enemyCell.x, State.enemyCell.y);
            }
        }

        // ============ 다중 적 상태 초기화 ============
        _enemyStates.Clear();
        _activeEnemyIndex = 0;

        if (_pendingContext != null && _pendingContext.enemies != null && buildResult != null)
        {
            for (int i = 0; i < _pendingContext.enemies.Count; i++)
            {
                var entry = _pendingContext.enemies[i];
                if (entry.instance == null || entry.instance.definition == null) continue;
                if (buildResult.enemySpawns == null || i >= buildResult.enemySpawns.Count) continue;

                int mhp = Mathf.Max(1, entry.instance.definition.maxHP);
                int chp = Mathf.Clamp(entry.instance.currentHP, 1, mhp);

                CombatUnitToken tok = null;
                if (i == 0)
                {
                    tok = _enemyToken;
                }
                else if (boardUI != null)
                {
                    Vector2Int spawn = buildResult.enemySpawns[i];
                    tok = boardUI.FindTokenAtCell(spawn.x, spawn.y, _playerToken);
                }

                _enemyStates.Add(new CombatEnemyState
                {
                    fieldInstance = entry.instance,
                    fieldOriginalCell = entry.fieldCell,
                    token = tok,
                    hp = chp,
                    maxHP = mhp,
                    cell = buildResult.enemySpawns[i],
                    isDead = false,
                    definition = entry.instance.definition
                });
            }
        }
        else if (_pendingContext != null && _pendingContext.enemy != null)
        {
            int mhp = Mathf.Max(1, GetEnemyMaxHP());
            int ehp = Mathf.Clamp(_pendingContext.enemy.currentHP, 0, mhp);
            if (ehp <= 0) ehp = mhp;
            _enemyStates.Add(new CombatEnemyState
            {
                fieldInstance = _pendingContext.enemy,
                fieldOriginalCell = _pendingContext.enemyCell,
                token = _enemyToken,
                hp = ehp,
                maxHP = mhp,
                cell = State.enemyCell,
                isDead = false,
                definition = _pendingEnemyDef
            });
        }

        // ============ VFX 바인드 ============
        vfx.Bind(boardUI);

        // ============ HP 동기화 ============
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

        // ============ AP 초기화 ============
        State.startAP = startAP;
        State.maxAP = maxAP;
        State.currentAP = Mathf.Clamp(startAP, 0, maxAP);
        State.freeMovesUsedThisTurn = 0;

        // ============ 이동 시스템 ============
        Movement = new CombatMovementService(
            moveRange: moveRange,
            isBlocked: IsBlocked,
            inBounds: (x, y) => boardUI != null && boardUI.InBounds(x, y)
        );

        // ============ 지형 효과 ============
        terrainFx.Apply(gridData, State.playerCell, _playerToken);
        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        // ============ 탈출 타일 ============
        BuildEscapeTile();

        // ============ 상태이상 / 스턴 ============
        _playerStunTurns = 0;

        // ✅ Phase 1: 상태이상 매니저 초기화
        InitializeStatusEffects();

        // ✅ Phase 1: 플레이어 스탯 참조 캐싱
        if (_pendingContext != null && _pendingContext.playerStats != null)
            _playerStats = _pendingContext.playerStats;
        else
            _playerStats = FindObjectOfType<PlayerStats>();

        if (bossAI != null) bossAI.ResetBossState();

        // ============ 턴 시작 ============
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
            if (IsEnemyCell(c)) continue;
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
            // 플레이어 HP 역동기화
            if (_pendingContext.playerStats != null)
            {
                _pendingContext.playerStats.hp = Mathf.Clamp(State.playerHP, 0, _pendingContext.playerStats.maxHP);
                _pendingContext.playerStats.ClampAll();
            }

            // 다중 적 결과 반영
            SaveActiveEnemyState(); // 현재 활성 적 상태 저장
            foreach (var es in _enemyStates)
            {
                if (es.fieldInstance == null) continue;

                if (es.isDead || es.hp <= 0)
                {
                    // 필드에서 적 제거
                    if (destroyFieldEnemyOnDefeat)
                        Destroy(es.fieldInstance.gameObject);
                }
                else
                {
                    // 살아남은 적: HP 역동기화
                    es.fieldInstance.currentHP = es.hp;
                }
            }
        }

        // _enemyStates 정리
        _enemyStates.Clear();
        _activeEnemyIndex = 0;

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
        ApplyFieldHudCombatMode(false);

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
        // ✅ Phase 1: 적이 스턴 상태면 턴 스킵 (활성 적 기준)
        if (_enemyStatusEffects != null && _enemyStatusEffects.IsStunned)
        {
            if (vfx != null) vfx.ShowPopup(_enemyToken, "기절!");
            yield return new WaitForSeconds(0.5f);

            ProcessTurnEndStatusEffects(isPlayerTurn: false);
            ConsumeTimeForEnemyTurn();

            if (!State.isInCombat) yield break;
            BeginPlayerTurn(initialStart: false);
            yield break;
        }

        // 다중 적: 살아있는 모든 적이 순서대로 행동
        if (_enemyStates.Count > 1)
        {
            for (int i = 0; i < _enemyStates.Count; i++)
            {
                if (_enemyStates[i].isDead) continue;
                if (!State.isInCombat) yield break;

                // 이 적을 활성으로 전환
                SetActiveEnemy(i);

                // AI 실행
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

                // AI 행동 후 상태 저장
                SaveActiveEnemyState();

                // 짧은 딜레이 (적 간 행동 구분)
                yield return new WaitForSeconds(0.15f);
            }
        }
        else
        {
            // 기존 1v1: 한 마리만 행동
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
        // 색 모듈 스킬 타일 선택 (늘어나는촉수 등)
        var skillBar = FindObjectOfType<CombatSkillBarUI>();
        if (skillBar != null && skillBar.IsSkillSelected)
        {
            skillBar.OnTileClicked(x, y);
            return;  // ← 스킬 선택 중이면 이동 안 함
        }

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

        // 다중 적: 적 셀 클릭 시 타겟 전환
        if (IsEnemyCell(target))
        {
            SwitchTargetAtCell(target);
            return;
        }

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

            if (IsBlocked(step))
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
            SyncActiveEnemyCellFromState();

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
            OnActiveEnemyDefeated();

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
        SyncPlayerStatsHpFromCombatState();
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

        // 다중 적: 살아있는 모든 적 위치를 차단
        if (_enemyStates.Count > 0)
        {
            foreach (var es in _enemyStates)
                if (!es.isDead && es.cell == cell) return true;
        }
        else if (State.isInCombat && cell == State.enemyCell)
        {
            // _enemyStates 없이 1v1만 켠 경우(정의 전용 진입 등)
            return true;
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

    /// <summary>
    /// 집중전투는 State.playerHP만 갱신하는 경우가 많아, 필드 UI(PlayerStatusBarUI)가 읽는 PlayerStats.hp와 어긋난다.
    /// 피해/출혈 등 State를 바꾼 직후 호출한다.
    /// </summary>
    private void SyncPlayerStatsHpFromCombatState()
    {
        if (_playerStats == null) return;
        _playerStats.hp = Mathf.Clamp(State.playerHP, 0, _playerStats.maxHP);
    }

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
        var skillBar = FindObjectOfType<CombatSkillBarUI>();
        if (skillBar != null) skillBar.RefreshButtonStates();
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
                if (IsEnemyCell(target)) continue;

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

    /// <summary>
    /// 던전 씬 등에서 인스펙터에 PlayerGridMover 등이 비어 있을 때 자동으로 채움.
    /// </summary>
    private void EnsureDisableDuringCombatTargets()
    {
        if (disableDuringCombat != null && disableDuringCombat.Length > 0)
        {
            for (int i = 0; i < disableDuringCombat.Length; i++)
            {
                if (disableDuringCombat[i] != null) return;
            }
        }

        PlayerStats stats = FindObjectOfType<PlayerStats>();
        if (stats == null) return;

        Transform t = stats.transform;
        var list = new System.Collections.Generic.List<MonoBehaviour>(4);
        var grid = t.GetComponent<PlayerGridMover>();
        var crouch = t.GetComponent<PlayerCrouch>();
        var rest = t.GetComponent<RestController>();
        if (grid != null) list.Add(grid);
        if (crouch != null) list.Add(crouch);
        if (rest != null) list.Add(rest);
        if (list.Count == 0) return;

        disableDuringCombat = list.ToArray();
    }

    /// <summary>
    /// 집중전투 진입 시 필드용 HUD(스킬바, 턴 HUD 등)를 끄고, 퇴장 시 복구.
    /// GameHUDManager가 씬에 없어도 FieldSkillBarUI / TurnHUD를 찾아 처리한다.
    /// </summary>
    private void ApplyFieldHudCombatMode(bool inCombat)
    {
        if (inCombat && TooltipManager.Instance != null)
            TooltipManager.Instance.Hide();

        if (GameHUDManager.Instance != null)
        {
            if (inCombat) GameHUDManager.Instance.EnterCombatMode();
            else GameHUDManager.Instance.ExitCombatMode();
            return;
        }

        var sort = FindObjectsSortMode.None;
        foreach (var bar in FindObjectsByType<FieldSkillBarUI>(FindObjectsInactive.Include, sort))
        {
            if (bar != null) bar.gameObject.SetActive(!inCombat);
        }

        foreach (var hud in FindObjectsByType<TurnHUD>(FindObjectsInactive.Include, sort))
        {
            if (hud != null) hud.gameObject.SetActive(!inCombat);
        }

        foreach (var prompt in FindObjectsByType<WorldPromptUI>(FindObjectsInactive.Include, sort))
        {
            if (prompt != null) prompt.gameObject.SetActive(!inCombat);
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

        // === 패시브 ATK 배율 자동 적용 ===
        var skillExec = ColorSkillExecutor.Instance;
        if (skillExec != null)
        {
            int enemyStatusCount = _enemyStatusEffects != null ? _enemyStatusEffects.TotalEffectCount : 0;
            int enemyMaxHP = _pendingEnemyDef != null ? _pendingEnemyDef.maxHP : State.enemyHP;
            float passiveMultiplier = skillExec.CalculatePassiveATKMultiplier(
                State.playerHP, _effectivePlayerMaxHP,
                State.enemyHP, enemyMaxHP,
                enemyStatusCount
            );
            attackMultiplier *= passiveMultiplier;
        }
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
            OnActiveEnemyDefeated();
        
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
        SyncPlayerStatsHpFromCombatState();
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
                SyncPlayerStatsHpFromCombatState();
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
                    OnActiveEnemyDefeated();
            }
        }
    }

    // ===== 다중 적: 활성 적 스왑 =====

    /// <summary>현재 활성 적 상태를 _enemyStates에 저장</summary>
    private void SaveActiveEnemyState()
    {
        if (_activeEnemyIndex < 0 || _activeEnemyIndex >= _enemyStates.Count) return;
        var es = _enemyStates[_activeEnemyIndex];
        es.hp = State.enemyHP;
        es.cell = State.enemyCell;
        es.token = _enemyToken;
    }

    /// <summary>
    /// 적 이동 등으로 <see cref="CombatState.enemyCell"/>만 바뀐 직후 호출.
    /// 다중 적에서 <see cref="IsBlocked"/>/<see cref="IsEnemyCell"/>은 리스트의 cell을 보므로, 동기화하지 않으면 토큰 위치와 논리 격자가 어긋난다.
    /// </summary>
    private void SyncActiveEnemyCellFromState()
    {
        if (_activeEnemyIndex < 0 || _activeEnemyIndex >= _enemyStates.Count) return;
        _enemyStates[_activeEnemyIndex].cell = State.enemyCell;
    }

    /// <summary>index번째 적을 활성으로 전환 (State/토큰 스왑)</summary>
    private void SetActiveEnemy(int index)
    {
        if (index < 0 || index >= _enemyStates.Count) return;
        SaveActiveEnemyState();
        _activeEnemyIndex = index;
        var es = _enemyStates[index];
        State.enemyHP = es.hp;
        State.enemyCell = es.cell;
        _enemyToken = es.token;
        _pendingEnemyDef = es.definition;

        _enemyStatusEffects = null;
        if (_enemyToken != null)
        {
            _enemyStatusEffects = _enemyToken.GetComponent<StatusEffectManager>();
            if (_enemyStatusEffects == null)
                _enemyStatusEffects = _enemyToken.gameObject.AddComponent<StatusEffectManager>();
        }
    }

    /// <summary>해당 셀에 살아있는 적이 있는지</summary>
    private bool IsEnemyCell(Vector2Int cell)
    {
        foreach (var es in _enemyStates)
            if (!es.isDead && es.cell == cell) return true;
        return false;
    }

    /// <summary>해당 셀의 적으로 타겟 전환</summary>
    private void SwitchTargetAtCell(Vector2Int cell)
    {
        for (int i = 0; i < _enemyStates.Count; i++)
        {
            if (!_enemyStates[i].isDead && _enemyStates[i].cell == cell)
            {
                SetActiveEnemy(i);
                RefreshMoveHighlights();
                RefreshUI();
                return;
            }
        }
    }

    /// <summary>현재 활성 적이 죽었을 때 호출</summary>
    private void OnActiveEnemyDefeated()
    {
        if (_activeEnemyIndex >= 0 && _activeEnemyIndex < _enemyStates.Count)
        {
            var es = _enemyStates[_activeEnemyIndex];
            es.isDead = true;
            es.hp = 0;

            // 토큰 제거
            if (es.token != null)
            {
                if (boardUI != null)
                    boardUI.RemoveToken(es.token);
                else
                    Destroy(es.token.gameObject);
            }
        }

        // 전부 죽었으면 전투 종료
        if (AreAllEnemiesDead())
        {
            ExitFocusedCombat();
            return;
        }

        // 다음 살아있는 적으로 타겟 전환
        for (int i = 0; i < _enemyStates.Count; i++)
        {
            if (!_enemyStates[i].isDead)
            {
                SetActiveEnemy(i);
                RefreshUI();
                RefreshMoveHighlights();
                return;
            }
        }
    }

    /// <summary>모든 적이 죽었는지</summary>
    private bool AreAllEnemiesDead()
    {
        foreach (var es in _enemyStates)
            if (!es.isDead) return false;
        return true;
    }

    /// <summary>모든 살아있는 적의 셀 목록</summary>
    private System.Collections.Generic.HashSet<Vector2Int> GetAllAliveEnemyCells()
    {
        var cells = new System.Collections.Generic.HashSet<Vector2Int>();
        foreach (var es in _enemyStates)
            if (!es.isDead) cells.Add(es.cell);
        return cells;
    }
}
