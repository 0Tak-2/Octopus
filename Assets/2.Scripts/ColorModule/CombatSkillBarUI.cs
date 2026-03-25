using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 집중전투 중 스킬 버튼 바
/// 스킬 선택 → 타일/적 클릭으로 대상 지정 방식
/// </summary>
public class CombatSkillBarUI : MonoBehaviour
{
    [Header("References")]
    public FocusedCombatManager combat;
    public ColorModuleSlots slots;
    public ColorSkillExecutor skillExecutor;

    [Header("UI")]
    public Transform buttonContainer;
    public GameObject skillButtonPrefab;

    [Header("State")]
    public TMP_Text selectedSkillText;

    private List<CombatSkillButtonUI> _buttons = new List<CombatSkillButtonUI>();
    private ColorModuleInstance _selectedSkill;

    /// <summary>
    /// 현재 스킬 선택 모드인지
    /// </summary>
    public bool IsSkillSelected => _selectedSkill != null;

    private void Start()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        if (slots == null) slots = ColorModuleSlots.Instance;
        if (skillExecutor == null) skillExecutor = ColorSkillExecutor.Instance;
    }

    private void OnEnable()
    {
        RefreshButtons();
    }

    private void Update()
    {
        if (combat == null || !combat.IsInFocusedCombat || !combat.State.isPlayerTurn)
            return;

        // 숫자키 1~5 단축키
        for (int i = 0; i < _buttons.Count && i < 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                OnSkillButtonClicked(_buttons[i].Module);
                break;
            }
        }

        // 우클릭 또는 ESC로 스킬 선택 취소
        if (IsSkillSelected)
        {
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelSelection();
            }
        }
    }

    /// <summary>
    /// 스킬 버튼 갱신
    /// </summary>
    public void RefreshButtons()
    {
        // 기존 버튼 제거
        foreach (var btn in _buttons)
        {
            if (btn != null && btn.gameObject != null)
                Destroy(btn.gameObject);
        }
        _buttons.Clear();

        if (slots == null || buttonContainer == null || skillButtonPrefab == null)
            return;

        // 장착된 스킬 버튼 생성
        var skills = slots.GetEquippedSkills();
        int index = 0;
        foreach (var skill in skills)
        {
            CreateSkillButton(skill, index);
            index++;
        }

        _selectedSkill = null;
        UpdateSelectedText();
    }

    private void CreateSkillButton(ColorModuleInstance module, int index)
    {
        var go = Instantiate(skillButtonPrefab, buttonContainer);

        var btn = go.GetComponent<CombatSkillButtonUI>();
        if (btn == null)
        {
            Debug.LogWarning("[CombatSkillBar] 프리팹에 CombatSkillButtonUI 없음!");
            Destroy(go);
            return;
        }

        btn.Setup(module, OnSkillButtonClicked);

        // 단축키 표시 (1~5)
        if (index < 5)
            btn.SetHotkey($"{index + 1}");

        // AP 상태 갱신
        if (combat != null)
            btn.UpdateState(combat.State.currentAP);

        _buttons.Add(btn);
    }

    /// <summary>
    /// 스킬 버튼 클릭 시 → 스킬 선택 모드 진입
    /// </summary>
    public void OnSkillButtonClicked(ColorModuleInstance module)
    {
        if (module == null) return;
        if (combat == null || !combat.IsInFocusedCombat || !combat.State.isPlayerTurn) return;

        // AP 체크
        if (!combat.CanSpendAP(module.APCost))
        {
            Debug.Log($"[CombatSkillBar] AP 부족! 필요: {module.APCost}");
            return;
        }

        // 응징하는촉수는 자기 버프 (타겟 불필요, 바로 사용)
        if (module.Skill == SkillID.Black_PunishingTentacle)
        {
            StartCoroutine(ExecuteSelfBuffRoutine(module));
            return;
        }

        // 같은 스킬 다시 누르면 취소
        if (_selectedSkill == module)
        {
            CancelSelection();
            return;
        }

        // 스킬 선택 모드 진입
        _selectedSkill = module;
        UpdateSelectedText();
        ShowSkillRange();

        Debug.Log($"[CombatSkillBar] {module.Name} 선택! 대상을 클릭하세요.");
    }

    /// <summary>
    /// 타일 클릭 시 → 선택된 스킬 실행
    /// FocusedCombatManager.HandleTileClicked에서 호출됨
    /// </summary>
    public void OnTileClicked(int x, int y)
    {
        if (_selectedSkill == null) return;
        if (combat == null || combat.IsBusyForInput) return;

        Vector2Int targetCell = new Vector2Int(x, y);

        // === 늘어나는촉수 (이동기) ===
        if (_selectedSkill.Skill == SkillID.Blue_StretchTentacle)
        {
            // 자기 위치 클릭 불가
            if (targetCell == combat.State.playerCell)
                return;

            // 적 위치 클릭 불가
            if (targetCell == combat.State.enemyCell)
            {
                Debug.Log("[CombatSkillBar] 적 위치로 이동 불가!");
                return;
            }

            // 사거리 체크 (3칸)
            int moveDist = Chebyshev(combat.State.playerCell, targetCell);
            if (moveDist > 3)
            {
                Debug.Log("[CombatSkillBar] 3칸 초과!");
                return;
            }

            // 벽 체크
            if (combat.IsBlocked(targetCell))
            {
                Debug.Log("[CombatSkillBar] 벽으로 이동 불가!");
                return;
            }

            ColorModuleInstance skill = _selectedSkill;
            _selectedSkill = null;
            UpdateSelectedText();
            ClearSkillHighlights();
            StartCoroutine(ExecuteSkillRoutine(skill, targetCell));
            return;
        }

        // === 공격 스킬 (단일 대상) ===
        // 적이 있는 타일을 클릭해야 함
        if (targetCell != combat.State.enemyCell)
        {
            Debug.Log("[CombatSkillBar] 적을 클릭하세요!");
            return;
        }

        // 사거리 체크
        int range = _selectedSkill.definition != null ? _selectedSkill.definition.range : 1;
        int dist = Chebyshev(combat.State.playerCell, combat.State.enemyCell);

        if (dist > range)
        {
            Debug.Log($"[CombatSkillBar] 사거리 밖! 거리: {dist}, 사거리: {range}");
            return;
        }

        // 공격 실행
        ColorModuleInstance attackSkill = _selectedSkill;
        _selectedSkill = null;
        UpdateSelectedText();
        ClearSkillHighlights();
        StartCoroutine(ExecuteSkillRoutine(attackSkill, targetCell));
    }

    // ============================================
    // 스킬 실행 코루틴
    // ============================================

    private IEnumerator ExecuteSkillRoutine(ColorModuleInstance module, Vector2Int targetCell)
    {
        yield return skillExecutor.ExecuteSkill(module, targetCell);
        RefreshButtonStates();
        combat.RefreshUIExternal();
        combat.RefreshMoveHighlightsExternal();
    }

    private IEnumerator ExecuteSelfBuffRoutine(ColorModuleInstance module)
    {
        yield return skillExecutor.ExecuteSkill(module, combat.State.playerCell);
        RefreshButtonStates();
        combat.RefreshUIExternal();
    }

    // ============================================
    // 사거리 하이라이트 표시
    // ============================================

    /// <summary>
    /// 선택된 스킬의 사거리 범위를 보드에 표시
    /// </summary>
    private void ShowSkillRange()
    {
        if (_selectedSkill == null || combat == null || combat.boardUI == null) return;

        combat.boardUI.ClearHighlights();

        // 늘어나는촉수: 3칸 이내 이동 가능 영역 표시
        if (_selectedSkill.Skill == SkillID.Blue_StretchTentacle)
        {
            Vector2Int from = combat.State.playerCell;
            for (int x = from.x - 3; x <= from.x + 3; x++)
            {
                for (int y = from.y - 3; y <= from.y + 3; y++)
                {
                    if (!combat.boardUI.InBounds(x, y)) continue;
                    Vector2Int cell = new Vector2Int(x, y);
                    if (cell == from) continue;
                    if (cell == combat.State.enemyCell) continue;
                    if (combat.IsBlocked(cell)) continue;

                    int d = Chebyshev(from, cell);
                    if (d <= 3)
                        combat.boardUI.SetHighlight(x, y, true);
                }
            }
            return;
        }

        // 공격 스킬: 적이 사거리 내에 있으면 적 위치 하이라이트
        int range = _selectedSkill.definition != null ? _selectedSkill.definition.range : 1;
        int dist = Chebyshev(combat.State.playerCell, combat.State.enemyCell);

        if (dist <= range)
        {
            combat.boardUI.SetHighlight(combat.State.enemyCell.x, combat.State.enemyCell.y, true);
        }
    }

    private void ClearSkillHighlights()
    {
        if (combat != null && combat.boardUI != null)
        {
            combat.boardUI.ClearHighlights();
            combat.RefreshMoveHighlightsExternal();
        }
    }

    /// <summary>
    /// 스킬 선택 취소
    /// </summary>
    public void CancelSelection()
    {
        _selectedSkill = null;
        UpdateSelectedText();
        ClearSkillHighlights();
        Debug.Log("[CombatSkillBar] 스킬 선택 취소");
    }

    private void UpdateSelectedText()
    {
        if (selectedSkillText == null) return;

        if (_selectedSkill != null)
        {
            string hint = _selectedSkill.Skill == SkillID.Blue_StretchTentacle
                ? "이동할 위치를 클릭 (우클릭/ESC 취소)"
                : "적을 클릭하여 공격 (우클릭/ESC 취소)";
            selectedSkillText.text = $"{_selectedSkill.Name} - {hint}";
        }
        else
        {
            selectedSkillText.text = "";
        }
    }

    /// <summary>
    /// 모든 버튼 상태 갱신 (AP 변경 시)
    /// </summary>
    public void RefreshButtonStates()
    {
        if (combat == null) return;

        int currentAP = combat.State.currentAP;
        foreach (var btn in _buttons)
        {
            if (btn != null)
                btn.UpdateState(currentAP);
        }
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }
}