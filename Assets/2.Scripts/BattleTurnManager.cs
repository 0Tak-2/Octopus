using UnityEngine;

public class BattleTurnManager : MonoBehaviour
{
    public static BattleTurnManager Instance { get; private set; }

    [Header("Battle Turn")]
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
