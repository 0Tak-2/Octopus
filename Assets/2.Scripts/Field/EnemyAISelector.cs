using UnityEngine;

/// <summary>
/// Automatically enables the correct AI component based on EnemyDefinition.aiType
/// Add all AI scripts to the prefab, this script will enable only the correct one
/// </summary>
[RequireComponent(typeof(EnemyInstance))]
public class EnemyAISelector : MonoBehaviour
{
    [Header("AI Components (assign in prefab)")]
    [Tooltip("AI1~AI4: Standard chase/combat AI")]
    public FieldEnemyWanderChase wanderChaseAI;
    
    [Tooltip("AI5: Neutral - counterattack when hit")]
    public FieldEnemyNeutral neutralAI;
    
    [Tooltip("AI6: Passive - flee when hit")]
    public FieldEnemyPassive passiveAI;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private EnemyInstance _enemy;

    private void Awake()
    {
        _enemy = GetComponent<EnemyInstance>();
        
        // Disable all AI components first
        DisableAllAI();
    }

    private void Start()
    {
        // Enable the correct AI based on definition
        SelectAndEnableAI();
    }

    private void DisableAllAI()
    {
        if (wanderChaseAI != null) wanderChaseAI.enabled = false;
        if (neutralAI != null) neutralAI.enabled = false;
        if (passiveAI != null) passiveAI.enabled = false;
    }

    private void SelectAndEnableAI()
    {
        if (_enemy == null || _enemy.definition == null)
        {
            // Default to wander/chase
            if (wanderChaseAI != null)
            {
                wanderChaseAI.enabled = true;
                if (showDebugLogs)
                    Debug.Log($"[AISelector] No definition, defaulting to WanderChase");
            }
            return;
        }

        var aiType = _enemy.definition.aiType;

        switch (aiType)
        {
            case EnemyAIType.AI1_MeleeChase:
            case EnemyAIType.AI2_HitAndRun:
            case EnemyAIType.AI3_Elite:
            case EnemyAIType.AI4_Boss:
                // Standard AI
                if (wanderChaseAI != null)
                {
                    wanderChaseAI.enabled = true;
                    if (showDebugLogs)
                        Debug.Log($"[AISelector] {_enemy.definition.displayName} using WanderChase AI ({aiType})");
                }
                else
                {
                    Debug.LogWarning($"[AISelector] WanderChase AI not assigned for {_enemy.definition.displayName}");
                }
                break;

            case EnemyAIType.AI5_Neutral:
                // Neutral AI
                if (neutralAI != null)
                {
                    neutralAI.enabled = true;
                    if (showDebugLogs)
                        Debug.Log($"[AISelector] {_enemy.definition.displayName} using Neutral AI");
                }
                else
                {
                    Debug.LogWarning($"[AISelector] Neutral AI not assigned for {_enemy.definition.displayName}");
                    // Fallback to wander
                    if (wanderChaseAI != null) wanderChaseAI.enabled = true;
                }
                break;

            case EnemyAIType.AI6_Passive:
                // Passive AI
                if (passiveAI != null)
                {
                    passiveAI.enabled = true;
                    if (showDebugLogs)
                        Debug.Log($"[AISelector] {_enemy.definition.displayName} using Passive AI");
                }
                else
                {
                    Debug.LogWarning($"[AISelector] Passive AI not assigned for {_enemy.definition.displayName}");
                    // Fallback to wander
                    if (wanderChaseAI != null) wanderChaseAI.enabled = true;
                }
                break;

            default:
                // Unknown - default to wander
                if (wanderChaseAI != null)
                {
                    wanderChaseAI.enabled = true;
                    if (showDebugLogs)
                        Debug.Log($"[AISelector] Unknown AI type {aiType}, defaulting to WanderChase");
                }
                break;
        }
    }

    /// <summary>
    /// Check if this enemy can counterattack (for combat system)
    /// </summary>
    public bool CanCounterattack()
    {
        if (_enemy == null || _enemy.definition == null)
            return true; // Default to yes

        switch (_enemy.definition.aiType)
        {
            case EnemyAIType.AI6_Passive:
                return false; // Passive never counterattacks

            case EnemyAIType.AI5_Neutral:
                // Neutral only counterattacks when hostile
                if (neutralAI != null)
                    return neutralAI.IsHostile;
                return false;

            default:
                return true;
        }
    }

    /// <summary>
    /// Check if this enemy should be included in combat
    /// </summary>
    public bool WillEngageInCombat()
    {
        if (_enemy == null || _enemy.definition == null)
            return true;

        switch (_enemy.definition.aiType)
        {
            case EnemyAIType.AI6_Passive:
                return false; // Passive never engages

            case EnemyAIType.AI5_Neutral:
                // Neutral only engages when hostile
                if (neutralAI != null)
                    return neutralAI.IsHostile;
                return false;

            default:
                return true;
        }
    }
}
