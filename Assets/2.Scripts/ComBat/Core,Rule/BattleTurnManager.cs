using UnityEngine;

/// <summary>
/// ⚠️ 레거시:
/// - 이 프로젝트에서는 FocusedCombatManager가 전투 턴/AP를 관리합니다.
/// - 씬에 남아있어도 FocusedCombatManager가 존재하면 자동 비활성화하여 중복/혼선 방지.
/// - 최종적으로는 씬에서 제거 권장.
/// </summary>
[DisallowMultipleComponent]
public class BattleTurnManager : MonoBehaviour
{
    public static BattleTurnManager Instance { get; private set; }

    [Header("Battle Turn (Legacy)")]
    public int maxAPPerTurn = 3;

    public int CurrentTurn { get; private set; } = 1;
    public int CurrentAP { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        // FocusedCombatManager가 있으면, 이 컴포넌트는 전투에서 쓰이지 않으므로 비활성화
        if (FocusedCombatManager.Instance != null || FindObjectOfType<FocusedCombatManager>() != null)
        {
            Debug.LogWarning("[BattleTurnManager] Disabled because FocusedCombatManager manages turn/AP in this project. " +
                             "You can remove BattleTurnManager from the scene.");
            enabled = false;
        }
    }

    private void Start()
    {
        if (enabled)
            StartPlayerTurn();
    }

    public void StartPlayerTurn()
    {
        CurrentAP = maxAPPerTurn;
        Debug.Log($"[Battle Turn {CurrentTurn}] Player Turn Start | AP = {CurrentAP}");
    }

    public bool CanSpendAP(int cost) => CurrentAP >= cost;

    public bool SpendAP(int cost)
    {
        if (!CanSpendAP(cost)) return false;

        CurrentAP -= cost;
        Debug.Log($"[Battle] AP Spent: {cost} | Remaining AP = {CurrentAP}");

        if (CurrentAP <= 0)
            EndPlayerTurn();

        return true;
    }

    public void EndPlayerTurn()
    {
        Debug.Log($"[Battle Turn {CurrentTurn}] Player Turn End");
        CurrentTurn++;
        StartPlayerTurn();
    }
}
