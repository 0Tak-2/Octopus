using UnityEngine;

/// <summary>
/// ⚠️ 정리 결과:
/// - 기존 TurnManager는 BattleTurnManager와 기능 중복(턴/AP)이라 유지하면 모순/버그 원인이 됨.
/// - 이 클래스는 "기존 코드 호환"을 위해 남겨두되, 실제 동작은 BattleTurnManager로 위임한다.
/// - 프로젝트에서 Turn/AP의 단일 진실 소스는 BattleTurnManager(또는 FocusedCombatManager)로 통일 권장.
/// </summary>
[DisallowMultipleComponent]
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn (Legacy)")]
    [Tooltip("BattleTurnManager가 없을 때만 이 값이 사용됩니다.")]
    public int maxAPPerTurn = 3;

    public int CurrentTurn { get; private set; } = 1;
    public int CurrentAP { get; private set; }

    // ✅ 실제 턴/AP는 여기로 위임(있으면)
    private BattleTurnManager _battle;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // BattleTurnManager가 있으면 그걸 단일 소스로 사용
        _battle = BattleTurnManager.Instance != null
            ? BattleTurnManager.Instance
            : FindObjectOfType<BattleTurnManager>();

        if (_battle != null)
        {
            Debug.LogWarning("[TurnManager] (Legacy) TurnManager is forwarding to BattleTurnManager. " +
                             "Please migrate references to BattleTurnManager.");
        }
    }

    private void Start()
    {
        // BattleTurnManager가 있으면 그쪽 Start가 이미 StartPlayerTurn을 호출할 가능성이 큼.
        // 중복 호출 방지: BattleTurnManager가 없을 때만 레거시 로직 수행.
        if (_battle == null)
        {
            StartPlayerTurn();
        }
        else
        {
            // 상태 스냅샷 동기화 (외부가 TurnManager.CurrentAP/Turn을 읽어도 값이 맞게)
            SyncFromBattle();
        }
    }

    private void Update()
    {
        // 런타임에서도 지속 동기화 (UI나 다른 코드가 TurnManager만 바라보는 경우 대비)
        if (_battle != null)
            SyncFromBattle();
    }

    private void SyncFromBattle()
    {
        // BattleTurnManager의 현재 값으로 미러링
        CurrentTurn = _battle.CurrentTurn;
        CurrentAP = _battle.CurrentAP;

        // maxAPPerTurn도 "표시/참조" 용도로 맞춰줌(실제 소모는 Battle 쪽)
        maxAPPerTurn = _battle.maxAPPerTurn;
    }

    // =========================
    // Legacy API (Forwarding)
    // =========================

    public void StartPlayerTurn()
    {
        if (_battle != null)
        {
            _battle.StartPlayerTurn();
            SyncFromBattle();
            return;
        }

        // 레거시 단독 동작(이제는 가능하면 쓰지 말기)
        CurrentAP = maxAPPerTurn;
        Debug.Log($"[Turn {CurrentTurn}] Player Turn Start | AP = {CurrentAP}");
    }

    public bool CanSpendAP(int cost)
    {
        if (_battle != null) return _battle.CanSpendAP(cost);
        return CurrentAP >= cost;
    }

    public bool SpendAP(int cost)
    {
        if (_battle != null)
        {
            bool ok = _battle.SpendAP(cost);
            SyncFromBattle();
            return ok;
        }

        if (!CanSpendAP(cost)) return false;
        CurrentAP -= cost;

        Debug.Log($"AP Spent: {cost} | Remaining AP = {CurrentAP}");

        if (CurrentAP <= 0)
            EndPlayerTurn();

        return true;
    }

    public void EndPlayerTurn()
    {
        if (_battle != null)
        {
            _battle.EndPlayerTurn();
            SyncFromBattle();
            return;
        }

        Debug.Log($"[Turn {CurrentTurn}] Player Turn End");
        CurrentTurn++;
        StartPlayerTurn();
    }
}
