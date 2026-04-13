using UnityEngine;

public enum EnemyAIType
{
    AI1_MeleeChase = 1,    // Melee normal - chase and attack
    AI2_HitAndRun = 2,     // Ranged normal - hit and run
    AI3_Elite = 3,         // Elite - advanced AI with skills
    AI4_Boss = 4,          // Boss - complex patterns
    AI5_Neutral = 5,       // Neutral - no aggro, counterattack when hit
    AI6_Passive = 6,       // Passive - no aggro, no counterattack, flee when hit
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
    [Min(0)] public int defense = 0;
    [Min(1)] public int moveRange = 2;
    [Min(1)] public int attackRange = 1;
    [Min(0)] public int attackDamage = 1;

    [Header("Behavior")]
    public EnemyAIType aiType = EnemyAIType.AI1_MeleeChase;
    public bool useDashAttackMotion = true;

    [Header("Field Detection")]
    [Tooltip("Field player detection range (Chebyshev)")]
    [Min(1)] public int fieldDetectionRange = 6;
    [Tooltip("Field detection LoS required")]
    public bool fieldRequireLoS = true;

    [Header("Aggro System")]
    [Tooltip("필드 추적 시 플레이어와 Chebyshev 거리가 이 값을 넘으면 어그로 해제 (시야 반경과 별개로 최대 추적 거리)")]
    [Min(1)] public int aggroDropDistance = 15;
    [Tooltip("Ignore LoS when aggro")]
    public bool ignoreLoSWhenAggro = true;

    [Header("Field Wander")]
    [Tooltip("Wander move chance per turn (0~1, 0.3 = 30%)")]
    [Range(0f, 1f)] public float wanderMoveChance = 0.3f;
    [Tooltip("Detection alert duration (seconds)")]
    [Min(0f)] public float detectionAlertDuration = 1.5f;

    [Header("Projectile")]
    [Tooltip("Projectile prefab for ranged attack (null = default effect)")]
    public GameObject projectilePrefab;
    [Tooltip("Does projectile rotate? (arrow = true, sphere = false)")]
    public bool projectileRotates = true;

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
    public CombatAttackDefinition bossSkill1_Basic;       // Basic attack (range1, 1AP)
    public CombatAttackDefinition bossSkill2_Stun;        // Stun (range1, 1AP)
    public CombatAttackDefinition bossSkill3_Charge;      // Charge/Pierce (2AP, next turn pierce)
    public CombatAttackDefinition bossSkill4_Withdraw;    // Withdraw attack (1AP, attack then retreat 3 tiles)
    public CombatAttackDefinition bossSkill5_Meditation;  // Meditation (+1AP, damage reduction, player turn guaranteed)

    // =========================
    // AI4 Boss Parameters (Inspector tuning)
    // =========================
    [Header("AI4 Boss Parameters")]

    [Tooltip("Boss max AP (no overflow)")]
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
    [Tooltip("Attack multiplier reaches max when HP ratio is below this value")]
    [Range(0.05f, 1f)] public float bossRageFullRatio = 0.4f;
    [Tooltip("Max attack multiplier")]
    [Min(1f)] public float bossRageMaxMultiplier = 2f;

    [Header("Boss Movement (Optional)")]
    [Tooltip("Boss free moves per turn (1 free move + AP cost for additional moves)")]
    [Min(0)] public int bossFreeMovesPerTurn = 1;

    // =========================
    // Color Module Drop (Phase 2)
    // =========================
    [Header("Color Module Drop")]
    [Tooltip("드랍할 색 모듈 아이템 (ItemData)")]
    public ItemData dropColorModule;

    [Tooltip("색 모듈 드랍 확률 (0~1)")]
    [Range(0f, 1f)] public float colorModuleDropChance = 0.15f;

    // =========================
    // Item Drop (Phase 4)
    // =========================
    [Header("Item Drop")]
    [Tooltip("Food item dropped on death")]
    public ItemData dropFood;
    
    [Tooltip("Material item dropped on death")]
    public ItemData dropMaterial;
    
    [Tooltip("Secondary material drop (optional)")]
    public ItemData dropMaterial2;
    
    [Header("Drop Chances")]
    [Tooltip("Food drop chance (0~1)")]
    [Range(0f, 1f)] public float foodDropChance = 0.5f;
    
    [Tooltip("Material drop chance (0~1)")]
    [Range(0f, 1f)] public float materialDropChance = 0.7f;
    
    [Tooltip("Secondary material drop chance (0~1)")]
    [Range(0f, 1f)] public float material2DropChance = 0.3f;
    
    // =========================
    // AI5/AI6 Specific (Phase 4)
    // =========================
    [Header("Neutral/Passive AI Settings")]
    [Tooltip("AI6: Flee distance when hit")]
    [Min(1)] public int fleeDistance = 3;
    
    [Tooltip("AI6: Chance to flee per turn when aggro (0~1)")]
    [Range(0f, 1f)] public float fleeChance = 0.8f;
}
