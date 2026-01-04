using UnityEngine;

public enum FieldTriggerType
{
    None,
    RandomEachTurn,
    OnAttack,
    OnHit,
    OnLowHP
}

public enum FieldResolutionType
{
    SimpleDamage,
    DamageAndStatus,
    StatusOnly
}

[System.Serializable]
public struct FieldTrigger
{
    public FieldTriggerType triggerType;
    [Range(0f, 1f)] public float chance;
    [Min(0)] public int cooldownTurns;
    public float param;
}
