using UnityEngine;

public class FieldSkillLoadout : MonoBehaviour
{
    [Header("Slots (1~4)")]
    public CombatAttackDefinition slot1;
    public CombatAttackDefinition slot2;
    public CombatAttackDefinition slot3;
    public CombatAttackDefinition slot4;

    public CombatAttackDefinition GetSlot(int index1Based)
    {
        switch (index1Based)
        {
            case 1: return slot1;
            case 2: return slot2;
            case 3: return slot3;
            case 4: return slot4;
            default: return null;
        }
    }
}
