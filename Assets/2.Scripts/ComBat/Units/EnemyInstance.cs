using UnityEngine;

public class EnemyInstance : MonoBehaviour
{
    [Header("Definition")]
    public EnemyDefinition definition;

    [Header("Runtime")]
    public int currentHP;

    private void Awake()
    {
        if (definition != null && currentHP <= 0)
            currentHP = Mathf.Max(1, definition.maxHP);
    }

    public void Clamp()
    {
        if (definition == null) return;
        currentHP = Mathf.Clamp(currentHP, 0, Mathf.Max(1, definition.maxHP));
    }
}
