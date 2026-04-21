using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "EngraveTree", menuName = "OCTO/Engrave/Tree Data")]
public class EngraveTreeData : ScriptableObject
{
    [Header("��ü ��� (30��)")]
    public List<EngraveNodeData> allNodes = new List<EngraveNodeData>();

    [Header("���� �ر� ����")]
    [Tooltip("�� �ι����� �� ����ŭ�� �⺻ ��忡 1����Ʈ �̻� �����ϸ� ���� ��� �ر�")]
    public int advancedUnlockThreshold = 5;

    private IEnumerable<EngraveNodeData> DistinctNodes(IEnumerable<EngraveNodeData> src)
    {
        var seen = new HashSet<string>();
        foreach (var n in src)
        {
            if (n == null) continue;
            string key = n.GetStableNodeId();
            if (!seen.Add(key)) continue;
            yield return n;
        }
    }

    public List<EngraveNodeData> GetNodesByCategory(EngraveCategory category)
    {
        return DistinctNodes(allNodes.Where(n => n.category == category)).ToList();
    }

    public List<EngraveNodeData> GetBasicNodes(EngraveCategory category)
    {
        return DistinctNodes(allNodes.Where(n => n.category == category && n.tier == EngraveNodeTier.Basic)).ToList();
    }

    public EngraveNodeData GetAdvancedNode(EngraveCategory category)
    {
        return DistinctNodes(allNodes.Where(n => n.category == category && n.tier == EngraveNodeTier.Advanced)).FirstOrDefault();
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