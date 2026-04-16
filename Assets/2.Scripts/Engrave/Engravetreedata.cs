using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "EngraveTree", menuName = "OCTO/Engrave/Tree Data")]
public class EngraveTreeData : ScriptableObject
{
    [Header("전체 노드 (30개)")]
    public List<EngraveNodeData> allNodes = new List<EngraveNodeData>();

    [Header("고급 해금 조건")]
    [Tooltip("한 부문에서 이 수만큼의 기본 노드에 1포인트 이상 투자하면 고급 노드 해금")]
    public int advancedUnlockThreshold = 5;

    public List<EngraveNodeData> GetNodesByCategory(EngraveCategory category)
    {
        return allNodes.Where(n => n.category == category).ToList();
    }

    public List<EngraveNodeData> GetBasicNodes(EngraveCategory category)
    {
        return allNodes.Where(n => n.category == category && n.tier == EngraveNodeTier.Basic).ToList();
    }

    public EngraveNodeData GetAdvancedNode(EngraveCategory category)
    {
        return allNodes.FirstOrDefault(n => n.category == category && n.tier == EngraveNodeTier.Advanced);
    }

    public List<EngraveCategory> GetAllCategories()
    {
        return new List<EngraveCategory>
        {
            EngraveCategory.Health,
            EngraveCategory.Attack,
            EngraveCategory.HungerFatigue,
            EngraveCategory.Defense,
            EngraveCategory.AP
        };
    }
}