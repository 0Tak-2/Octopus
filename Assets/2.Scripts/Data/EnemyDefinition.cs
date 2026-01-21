using UnityEngine;

public enum EnemyAIType
{
    AI1_MeleeChase = 1,
    AI2_HitAndRun = 2,
    AI3_Elite = 3,
    AI4_Boss = 4,
}

[CreateAssetMenu(menuName = "Combat/Enemy Definition", fileName = "EnemyDefinition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Enemy";

    [Header("Combat Token (UI)")]
    public CombatUnitToken tokenPrefab;

    [Header("Stats")]
    [Min(1)] public int maxHP = 10;
    [Min(1)] public int moveRange = 2;
    [Min(1)] public int attackRange = 1;
    [Min(0)] public int attackDamage = 1;

    [Header("Behavior")]
    public EnemyAIType aiType = EnemyAIType.AI1_MeleeChase;
    public bool useDashAttackMotion = true;

    [Header("Field Detection")]
    [Tooltip("필드에서 플레이어를 감지하는 거리 (Chebyshev)")]
    [Min(1)] public int fieldDetectionRange = 6;
    [Tooltip("필드 감지 시야(LoS) 필요 여부")]
    public bool fieldRequireLoS = true;

    [Header("Field Wander (배회)")]
    [Tooltip("배회 시 매 턴마다 움직일 확률 (0~1, 0.3 = 30%)")]
    [Range(0f, 1f)] public float wanderMoveChance = 0.3f;
    [Tooltip("인식 시 느낌표 표시 시간 (초)")]
    [Min(0f)] public float detectionAlertDuration = 1.5f;

    [Header("Evasion (optional)")]
    [Range(0f, 0.95f)] public float baseEvasion = 0f;

    // =========================
    // AI3 Elite AP
    // =========================
    [Header("AI3 Elite AP")]
    public bool enableEliteAP = false;
    public int eliteStartAP = 1;
    public int eliteMaxAP = 5;
    public int eliteRegenPerTurn = 1;

    // =========================
    // AI3 Elite Skills (4 slots)
    // =========================
    [Header("AI3 Elite Skills (Slots)")]
    public CombatAttackDefinition skill1_MeleeNormal;   // R1 AP1
    public CombatAttackDefinition skill2_MeleeCritical; // R1 AP2
    public CombatAttackDefinition skill3_Range2;        // R2 AP1
    public CombatAttackDefinition skill4_AttackBuff;    // Buff AP2 (6T, x2)

    // =========================
    // AI4 Boss Skills (5 slots)
    // =========================
    [Header("AI4 Boss Skills (Slots)")]
    public CombatAttackDefinition bossSkill1_Basic;       // 기본공격 (range1, 1AP)
    public CombatAttackDefinition bossSkill2_Stun;        // 기절 (range1, 1AP)
    public CombatAttackDefinition bossSkill3_Charge;      // 차지/돌진 (2AP, 다음턴 돌진)
    public CombatAttackDefinition bossSkill4_Withdraw;    // 빈지기공격 (1AP, 공격 후 3칸 후퇴)
    public CombatAttackDefinition bossSkill5_Meditation;  // 명상 (+1AP, 피해감소, 플레이어턴 보장)

    // =========================
    // AI4 Boss Parameters (Inspector tuning)
    // =========================
    [Header("AI4 Boss Parameters")]

    [Tooltip("보스 최대 AP (초과 허용 X)")]
    [Range(1, 5)]
    public int bossMaxAP = 5;

    [Header("Boss Costs")]
    [Min(0)] public int bossBasicApCost = 1;
    [Min(0)] public int bossStunApCost = 1;
    [Min(0)] public int bossChargeApCost = 2;
    [Min(0)] public int bossWithdrawApCost = 1;

    [Header("Stun")]
    [Min(1)] public int bossStunTurns = 1;

    [Header("Charge (Prepare -> Next turn Dash/Pierce)")]
    [Min(1)] public int bossChargeDashRange = 3;
    [Range(0f, 1f)] public float bossChargeUseChance = 0.35f;

    [Header("Withdraw (Hit -> Move away)")]
    [Min(0)] public int bossWithdrawMoveDistance = 3;

    [Header("Meditation")]
    [Min(0)] public int bossMeditationApGain = 1;
    [Min(1)] public int bossMeditationTurns = 1;
    [Range(0f, 1f)] public float bossMeditationDamageReduceRatio = 0.5f;
    [Min(0)] public int bossMeditationApThreshold = 2;
    [Range(0f, 1f)] public float bossMeditationUseChance = 0.5f;

    [Header("Passive Rage")]
    [Tooltip("HP 비율이 이 값 이하일 때 공격 배수가 최대치에 도달")]
    [Range(0.05f, 1f)] public float bossRageFullRatio = 0.4f;
    [Tooltip("최대 공격력 배수")]
    [Min(1f)] public float bossRageMaxMultiplier = 2f;

    [Header("Boss Movement (Optional)")]
    [Tooltip("보스도 '무료이동 1회 + 이후 이동 액션 AP 1'을 쓸려면 1 유지")]
    [Min(0)] public int bossFreeMovesPerTurn = 1;
}