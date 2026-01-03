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
    public int moveRange = 2;         // 플레이어 이동 범위(기존)
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

    [Header("Grid Blocker (legacy)")]
    public MonoBehaviour blockerBehaviour;
    private ICombatGridBlocker _blocker;

    [Header("Field Sync")]
    [Tooltip("집중전투에서 적이 죽으면 필드의 해당 적 오브젝트를 제거할지")]
    public bool destroyFieldEnemyOnDefeat = true;

    // ===== Runtime =====
    private CombatUnitToken _playerToken;
    private CombatUnitToken _enemyToken;

    // ✅ 필드에서 넘어온 적 정의서 (단일 소스)
    private EnemyDefinition _pendingEnemyDef;
    public EnemyDefinition CurrentEnemyDef => _pendingEnemyDef;

    // ✅ 필드에서 넘어온 전투 컨텍스트(플레이어/적 상태 이관)
    private EncounterContext _pendingContext;

    // ✅ HUD 표시용 플레이어 MaxHP (PlayerStats.maxHP를 반영)
    private int _effectivePlayerMaxHP;

    public CombatState State { get; private set; } = new CombatState();
    public CombatMovementService Movement { get; private set; }

    public bool IsInFocusedCombat => State.isInCombat;
    public bool IsBusyForInput => State.isBusy;

    // ✅ Escape tile runtime
    private bool _hasEscapeTile = false;
    private Vector2Int _escapeCell;

    // 플레이어 사거리 보너스(언덕) 제공 (기존 유지)
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

        if (endTurnButton != null)
        {
            endTurnButton.onClick.RemoveListener(RequestEndTurn);
            endTurnButton.onClick.AddListener(RequestEndTurn);
        }

        _effectivePlayerMaxHP = Mathf.Max(1, playerMaxHP);
    }

    private void Update()
    {
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (State.isBusy) return;

        // 턴 넘기기
        if (Input.GetKeyDown(endTurnKey))
            RequestEndTurn();

        // ✅ 도망 타일 처리
        HandleEscapeInput();
    }

    private void HandleEscapeInput()
    {
        if (!enableEscapeTile) return;
        if (!_hasEscapeTile) return;
        if (State.isBusy) return;

        // 액션 모드 중이면(공격 선택 중 등) 도망키로 꼬이는거 방지
        if (actionController != null && actionController.IsInActionMode)
            return;

        if (State.playerCell != _escapeCell)
            return;

        // 프롬프트
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
        // ✅ 페이드/전투종료 들어가기 전에 UI 튀는거 방지
        HidePlayerHint();
        State.isBusy = true;

        // AP 소모(즉시 UI 반영 원하면 hud.RefreshAll도 여기서)
        if (escapeApCost > 0)
        {
            State.currentAP = Mathf.Max(0, State.currentAP - escapeApCost);
            if (hud != null) hud.RefreshAll();
            RefreshUI();
        }

        bool success = (Random.value < escapeSuccessChance);

        // 팝업(머리 위)
        if (vfx != null)
        {
            if (success) vfx.ShowPopup(_playerToken, "도망에 성공했다!");
            else vfx.ShowPopup(_playerToken, "도망에 실패했다!");
        }

        // 문구 보이는 시간(취향)
        yield return new WaitForSeconds(0.5f);

        State.isBusy = false;

        if (!State.isInCombat) yield break;

        if (success)
        {
            // 즉시 필드로
            ExitFocusedCombat();
        }
        else
        {
            // ✅ 즉시 적 턴 (플레이어 AP 남아 있어도 강제)
            BeginEnemyTurn();
        }
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

    // ✅ 필드 상태(플레이어/적 HP)를 그대로 가져오는 진입
    public void EnterFocusedCombat(EncounterContext ctx)
    {
        if (State.isInCombat) return;
        if (ctx == null || ctx.enemy == null || ctx.enemy.definition == null) return;

        _pendingContext = ctx;
        _pendingEnemyDef = ctx.enemy.definition;

        StartCoroutine(EnterRoutine());
    }

    // ✅ 구형/폴백: 정의만 들어오는 경우(HP 이관 없음)
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

        // ✅ 토큰 배치: Definition.tokenPrefab 우선
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

        // ==========================
        // ✅ HP 이관 (PlayerStats가 정본)
        // ==========================
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

        // 적 HP: EnemyInstance가 있으면 그 currentHP 유지, 없으면 정의/폴백
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

        // AP 초기화 (기존 유지)
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

        // ✅ 도망 타일 생성 + 표시
        BuildEscapeTile();

        BeginPlayerTurn(initialStart: true);
        HidePlayerHint();

        if (hud != null)
            hud.Bind(this);

        RefreshUI();
    }

    private void BuildEscapeTile()
    {
        _hasEscapeTile = false;

        if (!enableEscapeTile) return;
        if (boardUI == null || gridData == null) return;

        // 스페셜 표시 초기화
        boardUI.ClearSpecials();

        const int MAX_TRY = 500;
        for (int i = 0; i < MAX_TRY; i++)
        {
            int x = Random.Range(0, gridData.width);
            int y = Random.Range(0, gridData.height);

            Vector2Int c = new Vector2Int(x, y);

            // 플레이어/적 시작칸 제외
            if (c == State.playerCell) continue;
            if (c == State.enemyCell) continue;

            // 벽/막힘 제외
            if (IsBlocked(c)) continue;

            _escapeCell = c;
            _hasEscapeTile = true;

            // 표시
            boardUI.SetEscapeTile(_escapeCell.x, _escapeCell.y, true);
            break;
        }
    }

    private void DoExit()
    {
        // ==========================
        // ✅ 전투 결과를 필드 정본에 반영
        // ==========================
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

        // ✅ 도망 타일 상태 리셋
        _hasEscapeTile = false;

        // ✅ 다음 전투에 섞이지 않게
        _pendingEnemyDef = null;
        _pendingContext = null;

        _effectivePlayerMaxHP = Mathf.Max(1, playerMaxHP);

        if (fader != null)
            fader.ForceClear();
    }

    // ==========================
    // Turn Rules
    // ==========================
    public void RequestEndTurn()
    {
        if (!State.isInCombat) return;
        if (!State.isPlayerTurn) return;
        if (State.isBusy) return;

        if (actionController != null && actionController.IsInActionMode)
            actionController.CancelAction();

        BeginEnemyTurn();
    }

    private void BeginPlayerTurn(bool initialStart)
    {
        State.isPlayerTurn = true;
        State.isBusy = false;

        if (!initialStart)
            State.currentAP = Mathf.Min(State.currentAP + 1, State.maxAP);

        // ✅ AP+1 즉시 HUD
        if (hud != null) hud.RefreshAll();

        State.ResetTurn();

        terrainFx.Apply(gridData, State.playerCell, _playerToken);
        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);

        RefreshUI();
        RefreshMoveHighlights();
    }

    private void BeginEnemyTurn()
    {
        State.isPlayerTurn = false;
        State.isBusy = false;

        terrainFx.Apply(gridData, State.enemyCell, _enemyToken);
        terrainFx.Apply(gridData, State.playerCell, _playerToken);

        RefreshUI();
        if (boardUI != null) boardUI.ClearHighlights();

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return StartCoroutine(enemyAI.TakeTurn());
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
    // Player Move (기존 유지)
    // ==========================
    private void HandleMoveTileClicked(int x, int y)
    {
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

        // ✅ 1) 이동 확정 순간에 비용 먼저 소비 + UI 즉시 갱신
        bool spentAP = false;
        bool spentFree = false;

        if (moveCost > 0)
        {
            State.currentAP = Mathf.Max(0, State.currentAP - moveCost);
            spentAP = true;
        }
        else
        {
            State.freeMovesUsedThisTurn++;
            spentFree = true;
        }

        if (hud != null) hud.RefreshAll();
        RefreshUI();

        // ✅ 2) 실제 이동(1칸씩)
        for (int i = 0; i < steps.Length; i++)
        {
            Vector2Int step = steps[i];

            if (IsBlocked(step) || step == State.enemyCell)
            {
                if (spentAP) State.currentAP = Mathf.Min(State.currentAP + moveCost, State.maxAP);
                if (spentFree) State.freeMovesUsedThisTurn = Mathf.Max(0, State.freeMovesUsedThisTurn - 1);

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

        // ✅ 3) 이동 종료 후 지형 효과
        terrainFx.Apply(gridData, State.playerCell, _playerToken);

        // ✅ 4) Busy 해제 → 하이라이트 복구
        State.isBusy = false;

        if (hud != null) hud.RefreshAll();
        RefreshUI();
        RefreshMoveHighlights();
        HidePlayerHint();

        TryAutoEndTurn();
    }

    // ==========================
    // Enemy Move / Attack (AI가 호출)
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

    // 플레이어 돌진 타격 (기존 유지)
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
    // Damage / Evade (기존 + 적 baseEvasion 반영)
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
    // Block / AP
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

    public void SpendAP(int amount)
    {
        if (amount <= 0) return;

        State.currentAP = Mathf.Max(0, State.currentAP - amount);

        // ✅ 즉시 반영
        if (hud != null) hud.RefreshAll();

        RefreshUI();
        TryAutoEndTurn();
    }

    private void TryAutoEndTurn()
    {
        if (!autoEndTurnWhenAPZero) return;
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
    // Enemy Stat Access (단일 소스)
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

}

