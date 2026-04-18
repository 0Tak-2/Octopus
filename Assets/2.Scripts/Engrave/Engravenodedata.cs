using UnityEngine;
using System.Collections.Generic;

public enum EngraveCategory
{
    Health,
    Attack,
    HungerFatigue,
    Defense,
    AP
}

public enum EngraveNodeTier
{
    Basic,
    Advanced
}

[CreateAssetMenu(fileName = "NewEngraveNode", menuName = "OCTO/Engrave/Node Data")]
public class EngraveNodeData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Save key. If empty, asset name (e.g. EN_HP_1) is used.")]
    public string nodeId;
    public string displayName;
    [TextArea(2, 4)]
    public string description;
    public Sprite icon;

    [Header("Category")]
    public EngraveCategory category;
    public EngraveNodeTier tier = EngraveNodeTier.Basic;

    [Header("Investment")]
    public int maxPoints = 3;

    [Header("Tree")]
    public List<EngraveNodeData> prerequisites = new List<EngraveNodeData>();

    [Header("Effect (future)")]
    [Tooltip("e.g. maxHp_percent, attack_flat")]
    public string effectType;
    public float effectValuePerPoint;

    /// <summary>Dictionary / save key. Empty nodeId falls back to ScriptableObject name.</summary>
    public string GetStableNodeId()
    {
        if (!string.IsNullOrWhiteSpace(nodeId))
            return nodeId.Trim();
        return name;
    }
}
