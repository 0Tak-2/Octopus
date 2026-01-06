using System;
using UnityEngine;

/// <summary>
/// 필드 전투 런타임 상태(1대1 기준).
/// - Time 1 = 1턴
/// - 기본 흐름: EnemyTurn -> PlayerTurn
/// </summary>
[Serializable]
public class FieldCombatState
{
    public enum Phase
    {
        None,
        EnemyTurn,
        PlayerTurn,
        Resolving,
        Ended
    }

    public Phase phase = Phase.None;

    public Transform player;
    public Transform enemy;

    public Vector2Int enemyHomeCell;      // 전투 시작 시 적 위치(도주 종료 시 복귀용)
    public bool returnEnemyToHomeOnEnd = true;

    public int turnIndex = 0;             // 전투 턴 카운트(디버그/로그/쿨다운 기준)

    // 감지/종료 판단(체비셰프+LOS)
    public int detectionRangeChebyshev = 6;

    // 전투 중 이동(플레이어는 공격 대신 1칸 이동)
    public bool allowPlayerMove = true;

    // 플레이어가 “현재 턴에 행동을 이미 했는지”
    public bool playerActedThisTurn = false;

    // Busy 플래그(애니/이펙트 넣을 때 확장)
    public bool busy = false;

    public void ResetForNewCombat(Transform playerTf, Transform enemyTf, Vector2Int enemyHome, int detectRange, bool returnHome)
    {
        phase = Phase.EnemyTurn;
        player = playerTf;
        enemy = enemyTf;
        enemyHomeCell = enemyHome;
        detectionRangeChebyshev = detectRange;
        returnEnemyToHomeOnEnd = returnHome;

        turnIndex = 0;
        playerActedThisTurn = false;
        busy = false;
    }
}
