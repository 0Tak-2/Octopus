using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 집중전투 중 스킬 버튼 바
/// 장착된 스킬 모듈을 동적으로 버튼 생성
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
    
    private List<CombatSkillButton> _buttons = new List<CombatSkillButton>();
    private ColorModuleInstance _selectedSkill;
    
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
        foreach (var skill in skills)
        {
            CreateSkillButton(skill);
        }
        
        _selectedSkill = null;
        UpdateSelectedText();
    }
    
    private void CreateSkillButton(ColorModuleInstance module)
    {
        var go = Instantiate(skillButtonPrefab, buttonContainer);
        var btn = go.GetComponent<CombatSkillButton>();
        
        if (btn == null)
            btn = go.AddComponent<CombatSkillButton>();
        
        btn.Initialize(module, this);
        _buttons.Add(btn);
    }
    
    /// <summary>
    /// 스킬 버튼 클릭 시
    /// </summary>
    public void OnSkillButtonClicked(ColorModuleInstance module)
    {
        if (module == null) return;
        
        // AP 체크
        if (combat != null && !combat.CanSpendAP(module.APCost))
        {
            Debug.Log($"[CombatSkillBar] AP 부족! 필요: {module.APCost}");
            return;
        }
        
        // 늘어나는촉수는 타겟 선택 필요
        if (module.Skill == SkillID.Blue_StretchTentacle)
        {
            _selectedSkill = module;
            UpdateSelectedText();
            Debug.Log("[CombatSkillBar] 이동할 위치를 클릭하세요 (3칸 이내)");
            return;
        }
        
        // 공격 스킬은 적 위치로 바로 실행
        if (combat != null && combat.IsInFocusedCombat)
        {
            StartCoroutine(skillExecutor.ExecuteSkill(module, combat.State.enemyCell));
        }
    }
    
    /// <summary>
    /// 타일 클릭 시 (이동 스킬 등)
    /// </summary>
    public void OnTileClicked(int x, int y)
    {
        if (_selectedSkill == null) return;
        
        Vector2Int targetCell = new Vector2Int(x, y);
        
        if (_selectedSkill.Skill == SkillID.Blue_StretchTentacle)
        {
            StartCoroutine(skillExecutor.ExecuteSkill(_selectedSkill, targetCell));
            _selectedSkill = null;
            UpdateSelectedText();
        }
    }
    
    /// <summary>
    /// 스킬 선택 취소
    /// </summary>
    public void CancelSelection()
    {
        _selectedSkill = null;
        UpdateSelectedText();
    }
    
    private void UpdateSelectedText()
    {
        if (selectedSkillText == null) return;
        
        if (_selectedSkill != null)
            selectedSkillText.text = $"선택: {_selectedSkill.Name}";
        else
            selectedSkillText.text = "";
    }
    
    /// <summary>
    /// 모든 버튼 상태 갱신 (AP 변경 시)
    /// </summary>
    public void RefreshButtonStates()
    {
        foreach (var btn in _buttons)
        {
            if (btn != null)
                btn.RefreshState();
        }
    }
}

/// <summary>
/// 개별 스킬 버튼
/// </summary>
public class CombatSkillButton : MonoBehaviour
{
    public Image iconImage;
    public Image backgroundImage;
    public TMP_Text nameText;
    public TMP_Text apCostText;
    public Button button;
    
    private ColorModuleInstance _module;
    private CombatSkillBarUI _bar;
    
    public void Initialize(ColorModuleInstance module, CombatSkillBarUI bar)
    {
        _module = module;
        _bar = bar;
        
        if (button == null) button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClick);
        
        // UI 설정
        if (nameText != null)
            nameText.text = module.Name;
        
        if (apCostText != null)
            apCostText.text = $"AP {module.APCost}";
        
        if (iconImage != null && module.Icon != null)
            iconImage.sprite = module.Icon;
        
        // 색상 배경
        if (backgroundImage != null)
        {
            backgroundImage.color = module.Color switch
            {
                ColorType.Red => new Color(1f, 0.3f, 0.3f),
                ColorType.Blue => new Color(0.3f, 0.5f, 1f),
                ColorType.Black => new Color(0.3f, 0.3f, 0.3f),
                _ => Color.gray
            };
        }
        
        RefreshState();
    }
    
    public void RefreshState()
    {
        if (button == null || _module == null) return;
        
        var combat = FocusedCombatManager.Instance;
        bool canUse = combat != null && combat.CanSpendAP(_module.APCost);
        
        button.interactable = canUse;
        
        // 비활성화 시 어둡게
        if (backgroundImage != null)
        {
            var c = backgroundImage.color;
            c.a = canUse ? 1f : 0.5f;
            backgroundImage.color = c;
        }
    }
    
    private void OnClick()
    {
        _bar?.OnSkillButtonClicked(_module);
    }
}
