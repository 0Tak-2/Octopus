using UnityEngine;

public enum AttackPattern
{
    Single,     // 단일(타겟 한 칸)
    CrossPlus,  // 십자가(+): center + up/down/left/right
    CrossX      // X자: center + diagonals
}

[CreateAssetMenu(menuName = "Combat/Attack Definition", fileName = "AttackDefinition")]
public class CombatAttackDefinition : ScriptableObject
{
    public string displayName = "Attack";
    public int apCost = 1;
    public int damage = 1;

    [Tooltip("타겟을 지정할 수 있는 최대 거리(타일). 대각 포함 체비셰프 거리.")]
    public int range = 1;

    public AttackPattern pattern = AttackPattern.Single;

    [Tooltip("선택된 타겟 타일이 '적'이어야만 발동되는지. (나중에 지형 타겟 스킬은 false로)")]
    public bool requireEnemyOnTarget = false;
}
