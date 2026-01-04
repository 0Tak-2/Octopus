using UnityEngine;

public enum AttackPattern { Single, CrossPlus, CrossX }
public enum CombatSkillKind { Attack = 0, Buff = 1 }

[CreateAssetMenu(menuName = "Combat/Attack Definition", fileName = "AttackDefinition")]
public class CombatAttackDefinition : ScriptableObject
{
    [Header("UI")]
    public string displayName = "Attack";
    [TextArea] public string description = "설명";
    public Sprite icon;

    [Header("Kind")]
    public CombatSkillKind kind = CombatSkillKind.Attack;

    [Header("Focused Combat (Grid)")]
    public int apCost = 1;

    [Tooltip("공격 스킬일 때 데미지. 버프 스킬일 땐 0이어도 됨.")]
    public int damage = 1;

    [Tooltip("타겟을 지정할 수 있는 최대 거리(타일). 대각 포함 체비셰프 거리.")]
    public int range = 1;

    public AttackPattern pattern = AttackPattern.Single;

    [Tooltip("선택된 타겟 타일이 '적'이어야만 발동되는지. (나중에 지형 타겟 스킬은 false로)")]
    public bool requireEnemyOnTarget = false;

    // =========================
    // Buff (AI3 Skill #4)
    // =========================
    [Header("Buff (when kind = Buff)")]
    [Tooltip("버프 지속 턴 수 (AI3: 6턴)")]
    [Min(1)] public int buffTurns = 6;

    [Tooltip("버프 데미지 배수 (AI3: 2배)")]
    [Min(1f)] public float damageMultiplier = 2f;

    [Tooltip("버프 상태 아이콘(UI 표시용). 비워두면 icon을 대신 사용.")]
    public Sprite buffStatusIcon;

    // =========================
    // Field (Quick Combat)
    // =========================
    [Header("Field Combat (Quick)")]
    public bool enableFieldTrigger = false;

    public FieldCombat.FieldTrigger fieldTrigger;

    [Tooltip("필드 전투에서 이 스킬을 어떻게 '간이 처리'할지")]
    public FieldCombat.FieldResolutionType fieldResolutionType = FieldCombat.FieldResolutionType.SimpleDamage;

    [Tooltip("필드 전투에서 damage를 덮어쓸지 (0이면 focused damage 사용)")]
    public int fieldDamageOverride = 0;

    [Tooltip("필드 전투에서 부여할 상태 태그(예: Bleed, Stun 등). 비워두면 없음")]
    public string fieldStatusTag = "";
}
