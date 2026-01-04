using UnityEngine;

public class BossState : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int apMax = 5;
    [SerializeField] private int freeMovesPerTurn = 1;

    [Header("Runtime")]
    [SerializeField] private int ap = 1;
    [SerializeField] private int freeMovesUsedThisTurn = 0;

    [SerializeField] private bool isCharging = false;

    [SerializeField] private bool meditationActive = false;
    [SerializeField] private int meditationTurnsLeft = 0;

    [SerializeField] private bool forceFollowUpAfterStun = false;

    public int AP => ap;
    public int APMax => apMax;

    public bool IsCharging => isCharging;

    public bool MeditationActive => meditationActive;
    public int MeditationTurnsLeft => meditationTurnsLeft;

    public bool ForceFollowUpAfterStun => forceFollowUpAfterStun;

    public void Configure(int maxAp, int freeMoves)
    {
        apMax = Mathf.Clamp(maxAp, 1, 5);
        freeMovesPerTurn = Mathf.Max(0, freeMoves);
        ap = Mathf.Clamp(ap, 0, apMax);
    }

    public void ResetForCombat(int startAp = 1)
    {
        ap = Mathf.Clamp(startAp, 0, apMax);
        freeMovesUsedThisTurn = 0;

        isCharging = false;

        meditationActive = false;
        meditationTurnsLeft = 0;

        forceFollowUpAfterStun = false;
    }

    public void OnBossTurnStart()
    {
        // 명상 만료: "사용 후 -> 플레이어 턴 -> 보스 턴 시작 시 사라짐" = 1턴
        if (meditationActive)
        {
            meditationTurnsLeft = Mathf.Max(0, meditationTurnsLeft - 1);
            if (meditationTurnsLeft <= 0)
            {
                meditationActive = false;
                meditationTurnsLeft = 0;
            }
        }

        ap = Mathf.Min(ap + 1, apMax);
        freeMovesUsedThisTurn = 0;
    }

    public bool CanSpendAP(int amount) => ap >= Mathf.Max(0, amount);

    public void SpendAP(int amount)
    {
        ap = Mathf.Max(0, ap - Mathf.Max(0, amount));
    }

    public void GainAP(int amount)
    {
        ap = Mathf.Clamp(ap + Mathf.Max(0, amount), 0, apMax);
    }

    public bool CanPayMoveActionCost()
    {
        if (freeMovesUsedThisTurn < freeMovesPerTurn) return true;
        return ap >= 1;
    }

    public bool PayMoveActionCost()
    {
        if (freeMovesUsedThisTurn < freeMovesPerTurn)
        {
            freeMovesUsedThisTurn++;
            return true;
        }

        if (ap >= 1)
        {
            ap = Mathf.Max(0, ap - 1);
            return true;
        }

        return false;
    }

    public void SetCharging(bool on) => isCharging = on;

    public void ActivateMeditation(int turns)
    {
        meditationActive = true;
        meditationTurnsLeft = Mathf.Max(1, turns);
    }

    public void SetFollowUpAfterStun(bool on) => forceFollowUpAfterStun = on;
}
