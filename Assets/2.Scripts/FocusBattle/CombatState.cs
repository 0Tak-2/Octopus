using UnityEngine;

[System.Serializable]
public class CombatState
{
    public bool isInCombat;
    public bool isPlayerTurn;
    public bool isBusy;

    public Vector2Int playerCell;
    public Vector2Int enemyCell;

    public int playerHP;
    public int enemyHP;

    public int currentAP;
    public int maxAP;
    public int startAP;

    // 무료 이동 사용 횟수(해류로 2회 가능)
    public int freeMovesUsedThisTurn;

    public void ResetTurn()
    {
        freeMovesUsedThisTurn = 0;
    }
}
