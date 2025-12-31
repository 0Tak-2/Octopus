using System.Collections;
using UnityEngine;

public class FocusedCombatManager : MonoBehaviour
{
    public static FocusedCombatManager Instance { get; private set; }

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
    public CombatUnitToken enemyTokenPrefab;

    [Header("Map Sizes")]
    public Vector2Int[] mapSizes = new Vector2Int[]
    {
        new Vector2Int(8,8),
        new Vector2Int(10,6),
        new Vector2Int(6,10)
    };

    [Header("Input Lock Targets (disable during focused combat)")]
    public MonoBehaviour[] disableDuringCombat;

    [Header("Turn / AP")]
    public int startAP = 2;
    public int maxAP = 5;

    public bool IsInFocusedCombat { get; private set; }

    private Vector2Int _playerCell;
    private Vector2Int _enemyCell;

    private CombatUnitToken _playerTokenInstance;
    private CombatUnitToken _enemyTokenInstance;

    private bool _isPlayerTurn = false;

    private int _currentAP = 0;
    private bool _freeMoveUsedThisTurn = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 전투 UI는 시작 시 꺼두기
        if (combatUIRoot != null)
            combatUIRoot.SetActive(false);

        // 시작 검정 방지(인스펙터가 뭐든 상관없이)
        if (fader != null)
            fader.ForceClear();
    }

    public void EnterFocusedCombat()
    {
        if (IsInFocusedCombat) return;
        StartCoroutine(EnterRoutine());
    }

    public void ExitFocusedCombat()
    {
        if (!IsInFocusedCombat) return;
        StartCoroutine(ExitRoutine());
    }

    private IEnumerator EnterRoutine()
    {
        IsInFocusedCombat = true;

        // ✅ 페이더가 없으면 그냥 즉시 전환(크래시 방지)
        if (fader == null)
        {
            DoEnter();
            yield break;
        }

        // ✅ 확실하게 StartCoroutine로 실행 보장
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

        // 맵 선택 + 생성
        Vector2Int size = mapSizes[Random.Range(0, mapSizes.Length)];

        if (boardUI != null)
        {
            boardUI.Build(size.x, size.y);

            // ✅ 타일 클릭 이벤트 연결
            boardUI.OnTileClicked -= HandleTileClicked; // 중복 방지
            boardUI.OnTileClicked += HandleTileClicked;
        }

        // 시작 좌표 계산(아래/위 중앙)
        int centerX = GetCenterX(size.x);
        _playerCell = new Vector2Int(centerX, 0);
        _enemyCell = new Vector2Int(centerX, size.y - 1);

        // 토큰 배치
        if (boardUI != null)
        {
            if (playerTokenPrefab != null)
                _playerTokenInstance = boardUI.PlaceToken(playerTokenPrefab, _playerCell.x, _playerCell.y);

            if (enemyTokenPrefab != null)
                _enemyTokenInstance = boardUI.PlaceToken(enemyTokenPrefab, _enemyCell.x, _enemyCell.y);
        }

        // ✅ 턴/AP 초기화
        _currentAP = Mathf.Clamp(startAP, 0, maxAP);
        BeginPlayerTurn(initialStart: true);
    }

    private void DoExit()
    {
        // ✅ 이벤트 해제(안전)
        if (boardUI != null)
        {
            boardUI.OnTileClicked -= HandleTileClicked;
        }

        if (combatUIRoot != null) combatUIRoot.SetActive(false);
        if (fieldRoot != null) fieldRoot.SetActive(true);

        SetDisableDuringCombat(false);

        IsInFocusedCombat = false;

        // 나갈 때도 혹시 남아있을 수 있는 검정 제거
        if (fader != null)
            fader.ForceClear();
    }

    private void BeginPlayerTurn(bool initialStart)
    {
        _isPlayerTurn = true;

        // 전투 시작 직후에는 startAP 그대로, 이후 턴부터 +1
        if (!initialStart)
        {
            _currentAP = Mathf.Min(_currentAP + 1, maxAP);
        }

        _freeMoveUsedThisTurn = false;

        // 디버그(원하면 지워도 됨)
        Debug.Log($"[Combat] Player Turn Start | AP={_currentAP} | FreeMoveUsed={_freeMoveUsedThisTurn}");
    }

    // (추후 적턴 붙일 때 사용)
    private void EndPlayerTurn()
    {
        _isPlayerTurn = false;
    }

    private void HandleTileClicked(int x, int y)
    {
        if (!IsInFocusedCombat) return;
        if (!_isPlayerTurn) return;
        if (_playerTokenInstance == null) return;

        Vector2Int target = new Vector2Int(x, y);

        // 같은 칸 클릭 무시
        if (target == _playerCell) return;

        // ✅ 이동 규칙: 대각선 포함 인접 8방향(체비셰프 거리 1)
        if (!IsAdjacent8(_playerCell, target)) return;

        // 적이 있는 칸으로 이동은(지금 단계에서는) 불가
        if (target == _enemyCell) return;

        // ✅ AP 비용: 턴당 1회 무료 이동, 그 이후 이동은 1 AP
        int cost = (_freeMoveUsedThisTurn) ? 1 : 0;

        if (cost > 0 && _currentAP < cost)
        {
            Debug.Log($"[Combat] Not enough AP. Need={cost}, Have={_currentAP}");
            return;
        }

        // 비용 지불
        if (cost > 0) _currentAP -= cost;
        if (!_freeMoveUsedThisTurn) _freeMoveUsedThisTurn = true;

        // 이동 실행(토큰 재배치)
        MovePlayerTokenTo(target);

        Debug.Log($"[Combat] Move -> {target} | AP={_currentAP} | FreeMoveUsed={_freeMoveUsedThisTurn}");
    }

    private void MovePlayerTokenTo(Vector2Int to)
    {
        if (boardUI == null) return;
        if (_playerTokenInstance == null) return;

        // 기존 타일에서 토큰 제거(자식 전체 삭제는 위험할 수 있으니 "토큰만" 옮긴다)
        boardUI.MoveExistingToken(_playerTokenInstance, to.x, to.y);

        _playerCell = to;
    }

    private bool IsAdjacent8(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return (dx <= 1 && dy <= 1) && !(dx == 0 && dy == 0);
    }

    private int GetCenterX(int width)
    {
        // 폭이 짝수면 왼쪽 중앙
        return (width - 1) / 2;
    }

    private void SetDisableDuringCombat(bool disable)
    {
        if (disableDuringCombat == null) return;

        for (int i = 0; i < disableDuringCombat.Length; i++)
        {
            if (disableDuringCombat[i] == null) continue;
            disableDuringCombat[i].enabled = !disable;
        }
    }
}
