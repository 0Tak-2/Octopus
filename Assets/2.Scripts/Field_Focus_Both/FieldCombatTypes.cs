using UnityEngine;

namespace FieldCombat
{
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

        [Range(0f, 1f)]
        public float chance;

        [Min(0)]
        public int cooldownTurns;

        [Tooltip("triggerType에 따라 의미가 달라짐 (예: OnLowHP일 때 0.3 = 30% 이하)")]
        public float param;
    }
}
