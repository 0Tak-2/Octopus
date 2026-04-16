using UnityEngine;
using System.Collections.Generic;

public enum EngraveCategory
{
    Health,         // 체력
    Attack,         // 공격
    HungerFatigue,  // 허기·피로도
    Defense,        // 방어
    AP              // AP
}

public enum EngraveNodeTier
{
    Basic,
    Advanced
}

[CreateAssetMenu(fileName = "NewEngraveNode", menuName = "OCTO/Engrave/Node Data")]
public class EngraveNodeData : ScriptableObject
{
    [Header("기본 정보")]
    public string nodeId;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("카테고리")]
    public EngraveCategory category;
    public EngraveNodeTier tier = EngraveNodeTier.Basic;

    [Header("투자")]
    public int maxPoints = 3;

    [Header("트리 구조")]
    public List<EngraveNodeData> prerequisites = new List<EngraveNodeData>();

    [Header("효과 (영탁이 나중에 구현)")]
    [Tooltip("예: maxHp_percent, attack_flat, crit_chance 등")]
    public string effectType;
    public float effectValuePerPoint;
}