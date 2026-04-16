using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EngraveCategoryUI : MonoBehaviour
{
    [Header("헤더")]
    [SerializeField] private TextMeshProUGUI categoryNameText;
    [SerializeField] private TextMeshProUGUI counterText;   // "0 / 6"
    [SerializeField] private Image categoryIcon;

    [Header("노드 슬롯 (Inspector에서 트리 순서대로 할당)")]
    [Tooltip("기본 노드 5개 → 고급 노드 1개 순서로 할당")]
    [SerializeField] private List<EngraveNodeUI> nodeSlots = new List<EngraveNodeUI>();

    [Header("연결선")]
    [Tooltip("노드 사이 연결선 이미지들. 고급 노드 앞의 점선은 별도 처리")]
    [SerializeField] private List<Image> connectionLines = new List<Image>();
    [SerializeField] private Image advancedConnectionLine;  // 기본→고급 사이 점선

    [Header("카테고리 색상")]
    [SerializeField] private Color categoryColor = Color.white;

    [Header("연결선 색상")]
    [SerializeField] private Color lineActiveColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    [SerializeField] private Color lineLockedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

    // 내부
    private EngraveCategory category;
    private List<EngraveNodeData> assignedNodes = new List<EngraveNodeData>();

    // =========================================================
    // 초기화
    // =========================================================

    /// <summary>
    /// 카테고리 UI 초기화. EngraveTreeUI에서 호출.
    /// </summary>
    /// <param name="cat">이 컬럼의 카테고리</param>
    /// <param name="nodes">이 카테고리의 노드 목록 (기본5 + 고급1, 순서대로)</param>
    /// <param name="catColor">카테고리 대표 색상</param>
    public void Initialize(EngraveCategory cat, List<EngraveNodeData> nodes, Color catColor)
    {
        category = cat;
        assignedNodes = nodes;
        categoryColor = catColor;

        // 헤더 세팅
        if (categoryNameText != null)
            categoryNameText.text = GetCategoryDisplayName(cat);

        if (categoryIcon != null)
            categoryIcon.color = categoryColor;

        // 노드 슬롯 초기화
        for (int i = 0; i < nodeSlots.Count && i < nodes.Count; i++)
        {
            nodeSlots[i].SetCategoryColor(categoryColor);
            nodeSlots[i].Initialize(nodes[i]);
        }

        // 남는 슬롯 비활성화 (혹시 슬롯이 더 많을 경우)
        for (int i = nodes.Count; i < nodeSlots.Count; i++)
        {
            nodeSlots[i].gameObject.SetActive(false);
        }

        Refresh();
    }

    // =========================================================
    // 갱신
    // =========================================================

    public void Refresh()
    {
        if (EngraveManager.Instance == null) return;

        var manager = EngraveManager.Instance;

        // 카운터 갱신
        int investedCount = manager.GetCategoryInvestedNodeCount(category);
        int totalNodes = assignedNodes.Count;
        if (counterText != null)
            counterText.text = $"{investedCount} / {totalNodes}";

        // 노드 갱신
        foreach (var slot in nodeSlots)
        {
            slot.Refresh();
        }

        // 연결선 색상 갱신
        RefreshConnectionLines();
    }

    // =========================================================
    // 연결선
    // =========================================================

    private void RefreshConnectionLines()
    {
        var manager = EngraveManager.Instance;

        // 기본 연결선: 선행 노드가 만렙이면 활성 색상
        for (int i = 0; i < connectionLines.Count && i < assignedNodes.Count; i++)
        {
            if (connectionLines[i] == null) continue;

            // i번째 연결선은 i번째 노드가 해금됐는지에 따라 색상 결정
            bool unlocked = manager.IsNodeUnlocked(assignedNodes[Mathf.Min(i + 1, assignedNodes.Count - 1)]);
            connectionLines[i].color = unlocked ? lineActiveColor : lineLockedColor;
        }

        // 고급 연결선: 고급 노드가 해금됐는지
        if (advancedConnectionLine != null && assignedNodes.Count > 0)
        {
            var advancedNode = assignedNodes[assignedNodes.Count - 1];
            bool advUnlocked = manager.IsNodeUnlocked(advancedNode);
            advancedConnectionLine.color = advUnlocked
                ? new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.8f)
                : lineLockedColor;
        }
    }

    // =========================================================
    // 유틸
    // =========================================================

    public static string GetCategoryDisplayName(EngraveCategory cat)
    {
        switch (cat)
        {
            case EngraveCategory.Health: return "체력";
            case EngraveCategory.Attack: return "공격";
            case EngraveCategory.HungerFatigue: return "허기/피로도";
            case EngraveCategory.Defense: return "방어";
            case EngraveCategory.AP: return "AP";
            default: return cat.ToString();
        }
    }

    public static Color GetDefaultCategoryColor(EngraveCategory cat)
    {
        switch (cat)
        {
            case EngraveCategory.Health: return new Color(0.91f, 0.29f, 0.29f); // 빨강
            case EngraveCategory.Attack: return new Color(0.85f, 0.35f, 0.19f); // 주황
            case EngraveCategory.HungerFatigue: return new Color(0.94f, 0.62f, 0.15f); // 노랑
            case EngraveCategory.Defense: return new Color(0.22f, 0.37f, 0.65f); // 파랑
            case EngraveCategory.AP: return new Color(0.49f, 0.29f, 0.72f); // 보라
            default: return Color.gray;
        }
    }
}