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
    [Tooltip("í•„ë“œì—ì„œ í”Œë ˆì´ì–´ë¥¼ ê°ì§€í•˜ëŠ” ê±°ë¦¬ (Chebyshev)")]
    [Min(1)] public int fieldDetectionRange = 6;
    [Tooltip("í•„ë“œ íƒì§€ ì‹œ ì‹œì•¼(LoS) í•„ìš” ì—¬ë¶€")]
    public bool fieldRequireLoS = true;

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
    public CombatAttackDefinition bossSkill1_Basic;       // ±âº»°ø°İ (range1, 1AP)
    public CombatAttackDefinition bossSkill2_Stun;        // ±âÀı (range1, 1AP)
    public CombatAttackDefinition bossSkill3_Charge;      // Â÷Â¡/µ¹Áø (2AP, ´ÙÀ½ÅÏ µ¹Áø)
    public CombatAttackDefinition bossSkill4_Withdraw;    // ºüÁö±â°ø°İ (1AP, °ø°İ ÈÄ 3Ä­ ÈÄÅğ)
    public CombatAttackDefinition bossSkill5_Meditation;  // ¸í»ó (+1AP, ÇÇÇØ°¨¼Ò, ÇÃ·¹ÀÌ¾îÅÏ º¸Àå)

    // =========================
    // AI4 Boss Parameters (Inspector tuning)
    // =========================
    [Header("AI4 Boss Parameters")]

    [Tooltip("º¸½º ÃÖ´ë AP (ÃÊ°ú Çã¿ë X)")]
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
    [Tooltip("HP ºñÀ²ÀÌ ÀÌ °ª ÀÌÇÏÀÏ ¶§ °ø°İ ¹è¼ö°¡ ÃÖ´ëÄ¡¿¡ µµ´Ş")]
    [Range(0.05f, 1f)] public float bossRageFullRatio = 0.4f;
    [Tooltip("ÃÖ´ë °ø°İ·Â ¹è¼ö")]
    [Min(1f)] public float bossRageMaxMultiplier = 2f;

    [Header("Boss Movement (Optional)")]
    [Tooltip("º¸½ºµµ '¹«·áÀÌµ¿ 1È¸ + ÀÌÈÄ ÀÌµ¿ ¾×¼Ç AP 1'À» ¾²·Á¸é 1 À¯Áö")]
    [Min(0)] public int bossFreeMovesPerTurn = 1;
}
