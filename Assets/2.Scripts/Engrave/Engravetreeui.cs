using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class EngraveTreeUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject treePanel;

    [Header("상단 정보")]
    [SerializeField] private TextMeshProUGUI metaPointsText;
    [SerializeField] private TextMeshProUGUI totalInvestedText;

    [Header("카테고리 컬럼 (좌→우 순서: 체력, 공격, 허기/피로도, 방어, AP)")]
    [SerializeField] private List<EngraveCategoryUI> categoryColumns = new List<EngraveCategoryUI>();

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button resetButton;

    [Header("초기화 확인 팝업")]
    [SerializeField] private GameObject resetConfirmPopup;
    [SerializeField] private Button resetConfirmYes;
    [SerializeField] private Button resetConfirmNo;

    // 내부
    private bool isInitialized;

    // =========================================================
    // 라이프사이클
    // =========================================================

    private void Start()
    {
        // 버튼 바인딩
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (resetButton != null)
            resetButton.onClick.AddListener(ShowResetConfirm);

        if (resetConfirmYes != null)
            resetConfirmYes.onClick.AddListener(OnResetConfirmed);

        if (resetConfirmNo != null)
            resetConfirmNo.onClick.AddListener(HideResetConfirm);

        // 초기화 확인 팝업 숨김
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);

       
    }

    private void OnEnable()
    {
        if (EngraveManager.Instance != null)
        {
            EngraveManager.Instance.OnTreeChanged += RefreshAll;
            EngraveManager.Instance.OnMetaPointsChanged += OnMetaPointsChanged;
        }
    }

    private void OnDisable()
    {
        if (EngraveManager.Instance != null)
        {
            EngraveManager.Instance.OnTreeChanged -= RefreshAll;
            EngraveManager.Instance.OnMetaPointsChanged -= OnMetaPointsChanged;
        }
    }

    // =========================================================
    // 열기 / 닫기
    // =========================================================

    /// <summary>
    /// 각인 트리 UI 열기.
    /// 시작 화면 [각인] 버튼이나 인게임 ESC 메뉴 [각인] 버튼에서 호출.
    /// </summary>
    public void Open()
    {
        if (!isInitialized)
            InitializeTree();

        if (treePanel != null)
            treePanel.SetActive(true);

        RefreshAll();
    }

    public void Close()
    {
        if (treePanel != null)
            treePanel.SetActive(false);

        HideResetConfirm();
    }

    public void Toggle()
    {
        if (treePanel != null && treePanel.activeSelf)
            Close();
        else
            Open();
    }

    public bool IsOpen => treePanel != null && treePanel.activeSelf;

    // =========================================================
    // 초기화
    // =========================================================

    private void InitializeTree()
    {
        var manager = EngraveManager.Instance;
        if (manager == null || manager.TreeData == null)
        {
            Debug.LogError("[EngraveTreeUI] EngraveManager 또는 TreeData가 없습니다.");
            return;
        }

        var treeData = manager.TreeData;
        var categories = treeData.GetAllCategories();

        for (int i = 0; i < categoryColumns.Count && i < categories.Count; i++)
        {
            var cat = categories[i];
            var nodes = treeData.GetNodesByCategory(cat);

            // 기본 노드 먼저, 고급 노드는 맨 끝으로 정렬
            nodes.Sort((a, b) =>
            {
                if (a.tier != b.tier)
                    return a.tier == EngraveNodeTier.Basic ? -1 : 1;
                return 0; // 같은 tier 안에서는 allNodes 리스트 원래 순서 유지
            });

            var color = EngraveCategoryUI.GetDefaultCategoryColor(cat);
            categoryColumns[i].Initialize(cat, nodes, color);
        }

        isInitialized = true;
    }

    // =========================================================
    // 갱신
    // =========================================================

    private void RefreshAll()
    {
        var manager = EngraveManager.Instance;
        if (manager == null) return;

        // 상단 포인트 표시
        if (metaPointsText != null)
            metaPointsText.text = $"보유 포인트: {manager.AvailableMetaPoints}";

        if (totalInvestedText != null)
            totalInvestedText.text = $"투자 완료: {manager.TotalInvestedPoints}";

        // 각 카테고리 컬럼 갱신
        foreach (var col in categoryColumns)
        {
            col.Refresh();
        }
    }

    private void OnMetaPointsChanged(int newAmount)
    {
        if (metaPointsText != null)
            metaPointsText.text = $"보유 포인트: {newAmount}";
    }

    // =========================================================
    // 초기화(리셋) 확인 팝업
    // =========================================================

    private void ShowResetConfirm()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(true);
    }

    private void HideResetConfirm()
    {
        if (resetConfirmPopup != null)
            resetConfirmPopup.SetActive(false);
    }

    private void OnResetConfirmed()
    {
        EngraveManager.Instance?.ResetTree();
        HideResetConfirm();
    }
}