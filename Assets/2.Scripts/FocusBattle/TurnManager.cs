using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    [Header("Turn")]
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

    private void Start()
    {
        StartPlayerTurn();
    }

    public void StartPlayerTurn()
    {
        CurrentAP = maxAPPerTurn;
        Debug.Log($"[Turn {CurrentTurn}] Player Turn Start | AP = {CurrentAP}");
    }

    public bool CanSpendAP(int cost)
    {
        return CurrentAP >= cost;
    }

    public bool SpendAP(int cost)
    {
        if (!CanSpendAP(cost)) return false;
        CurrentAP -= cost;

        Debug.Log($"AP Spent: {cost} | Remaining AP = {CurrentAP}");

        if (CurrentAP <= 0)
        {
            EndPlayerTurn();
        }

        return true;
    }

    public void EndPlayerTurn()
    {
        Debug.Log($"[Turn {CurrentTurn}] Player Turn End");
        // 지금은 적 턴 없음 → 바로 다음 턴
        CurrentTurn++;
        StartPlayerTurn();
    }
}
