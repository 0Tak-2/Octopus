using UnityEngine;

public class FieldSkillLoadout : MonoBehaviour
{
    [Header("Slots (1~4)")]
    public ScriptableObject slot1; // CombatAttackDefinition
    public ScriptableObject slot2;
    public ScriptableObject slot3;
    public ScriptableObject slot4;

    public ScriptableObject GetSlot(int index1Based)
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
