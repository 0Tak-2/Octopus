using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class EngraveNodeUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("참조")]
    [SerializeField] private Image nodeBackground;
    [SerializeField] private Image nodeIcon;
    [SerializeField] private Image nodeBorder;
    [SerializeField] private TextMeshProUGUI progressText;  // "0/3", "1/3" 등

    [Header("호버 시 비용 표시")]
    [SerializeField] private GameObject costPanel;
    [SerializeField] private TextMeshProUGUI costText;

    [Header("상태별 색상")]
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
    [SerializeField] private Color availableColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    [SerializeField] private Color investedColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color maxedBorderColor = new Color(1f, 0.85f, 0.3f, 1f);

    [Header("카테고리 색상")]
    [SerializeField] private Color categoryTintColor = Color.white;

    // 내부 상태
    private EngraveNodeData nodeData;
    private EngraveManager manager;
    private bool isInitialized;

    // === 외부에서 카테고리 색상 세팅 ===
    public void SetCategoryColor(Color color)
    {
        categoryTintColor = color;
    }

    // =========================================================
    // 초기화
    // =========================================================

    public void Initialize(EngraveNodeData data)
    {
        nodeData = data;
        manager = EngraveManager.Instance;
        isInitialized = true;

        // 아이콘 세팅 (없으면 플레이스홀더)
        if (nodeIcon != null)
        {
            if (data.icon != null)
                nodeIcon.sprite = data.icon;
            // icon이 null이면 기본 스프라이트 유지 (Inspector에서 플레이스홀더 지정)
        }

        // 비용 패널 기본 숨김
        if (costPanel != null)
            costPanel.SetActive(false);

        // 비용 텍스트
        if (costText != null)
            costText.text = "1 포인트";

        Refresh();
    }

    // =========================================================
    // 시각 상태 갱신
    // =========================================================

    public void Refresh()
    {
        if (!isInitialized || manager == null) return;

        int invested = manager.GetInvestedPoints(nodeData);
        bool unlocked = manager.IsNodeUnlocked(nodeData);
        bool canInvest = manager.CanInvest(nodeData);
        bool maxed = manager.IsNodeMaxed(nodeData);

        // 진행도 텍스트
        if (progressText != null)
            progressText.text = $"{invested}/{nodeData.maxPoints}";

        // 배경 색상
        if (nodeBackground != null)
        {
            if (!unlocked)
            {
                // 잠김: 어두운 회색
                nodeBackground.color = lockedColor;
            }
            else if (maxed)
            {
                // 만렙: 카테고리 색상 100%
                nodeBackground.color = categoryTintColor;
            }
            else if (invested > 0)
            {
                // 투자 중: 카테고리 색상 연하게
                nodeBackground.color = Color.Lerp(availableColor, categoryTintColor, 0.5f);
            }
            else
            {
                // 해금됨, 미투자: 밝은 회색
                nodeBackground.color = availableColor;
            }
        }

        // 테두리 강조
        if (nodeBorder != null)
        {
            if (maxed)
            {
                nodeBorder.color = maxedBorderColor;
                nodeBorder.gameObject.SetActive(true);
            }
            else if (canInvest)
            {
                nodeBorder.color = categoryTintColor;
                nodeBorder.gameObject.SetActive(true);
            }
            else
            {
                nodeBorder.gameObject.SetActive(false);
            }
        }

        // 아이콘 밝기
        if (nodeIcon != null)
        {
            nodeIcon.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 0.5f);
        }
    }

    // =========================================================
    // 인터랙션
    // =========================================================

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isInitialized || manager == null) return;

        if (manager.CanInvest(nodeData))
        {
            manager.Invest(nodeData);
            // Refresh는 EngraveTreeUI에서 OnTreeChanged 이벤트로 일괄 처리
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInitialized) return;

        // 비용 패널 표시
        if (costPanel != null && !manager.IsNodeMaxed(nodeData))
            costPanel.SetActive(true);

        // ===================================================
        // 기존 툴팁 시스템 연동 (영탁이 수정할 부분)
        // ===================================================
        // 예시: TooltipManager.Instance.Show(nodeData.displayName, nodeData.description);
        // 또는 기존 아이템 툴팁 시스템의 Show 메서드 호출
        //
        // 표시할 정보:
        //   - nodeData.displayName  (노드 이름)
        //   - nodeData.description  (효과 설명)
        //   - 현재 투자: {invested}/{maxPoints}
        //   - 카테고리: nodeData.category
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (costPanel != null)
            costPanel.SetActive(false);

        // 툴팁 숨기기
        // 예시: TooltipManager.Instance.Hide();
    }
}