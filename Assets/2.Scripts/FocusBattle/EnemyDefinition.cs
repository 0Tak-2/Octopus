using UnityEngine;

public enum EnemyAIType
{
    AI1_MeleeChase = 1,
    AI2_HitAndRun = 2,
    AI3_Elite = 3
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
}
