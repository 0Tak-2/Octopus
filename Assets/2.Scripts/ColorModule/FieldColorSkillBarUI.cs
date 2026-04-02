using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 필드 색 모듈 스킬 바 UI
/// 장착된 스킬을 아이콘으로 표시, 쿨다운 표시, 숫자키/클릭으로 선택
/// </summary>
public class FieldColorSkillBarUI : MonoBehaviour
{
    [Header("References")]
    public FieldColorSkillCaster caster;
    public ColorModuleSlots moduleSlots;

    [Header("UI Container")]
    public Transform slotContainer;
    public GameObject slotPrefab;

    [Header("Selected Info")]
    public TMP_Text selectedSkillText;

    private List<FieldColorSkillSlot> _slots = new List<FieldColorSkillSlot>();

    private void Start()
    {
        if (caster == null) caster = FindObjectOfType<FieldColorSkillCaster>();
        if (moduleSlots == null) moduleSlots = ColorModuleSlots.Instance;

        if (caster != null)
        {
            caster.OnSelectedChanged += HandleSelectedChanged;
            caster.OnCooldownChanged += RefreshAll;
            caster.OnArmedChanged += HandleArmedChanged;
        }

        if (moduleSlots != null)
            moduleSlots.OnSlotsChanged += RefreshAll;

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (caster != null)
        {
            caster.OnSelectedChanged -= HandleSelectedChanged;
            caster.OnCooldownChanged -= RefreshAll;
            caster.OnArmedChanged -= HandleArmedChanged;
        }

        if (moduleSlots != null)
            moduleSlots.OnSlotsChanged -= RefreshAll;
    }

    /// <summary>
    /// 전체 슬롯 갱신
    /// </summary>
    public void RefreshAll()
    {
        // 기존 슬롯 제거
        foreach (var slot in _slots)
        {
            if (slot != null && slot.gameObject != null)
                Destroy(slot.gameObject);
        }
        _slots.Clear();

        if (moduleSlots == null || slotContainer == null || slotPrefab == null) return;

        // 장착된 스킬만 표시
        var skills = moduleSlots.GetEquippedSkills();
        for (int i = 0; i < skills.Count; i++)
        {
            CreateSlot(i, skills[i]);
        }

        UpdateSelectedText();
    }

    private void CreateSlot(int index, ColorModuleInstance module)
    {
        var go = Instantiate(slotPrefab, slotContainer);
        var slot = go.GetComponent<FieldColorSkillSlot>();

        if (slot == null)
        {
            slot = go.AddComponent<FieldColorSkillSlot>();
        }

        int cooldown = caster != null ? caster.GetCooldownRemaining(index) : 0;
        bool isSelected = caster != null && caster.IsArmed && caster.SelectedSlotIndex == index;

        slot.Setup(index, module, cooldown, isSelected, OnSlotClicked);
        _slots.Add(slot);
    }

    private void OnSlotClicked(int index)
    {
        if (caster == null || moduleSlots == null) return;

        var skills = moduleSlots.GetEquippedSkills();
        if (index < 0 || index >= skills.Count) return;

        caster.SelectSkill(index, skills[index]);
    }

    private void HandleSelectedChanged(int index)
    {
        RefreshSlotStates();
        UpdateSelectedText();
    }

    private void HandleArmedChanged(bool armed)
    {
        RefreshSlotStates();
        UpdateSelectedText();
    }

    private void RefreshSlotStates()
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] == null) continue;

            int cooldown = caster != null ? caster.GetCooldownRemaining(i) : 0;
            bool isSelected = caster != null && caster.IsArmed && caster.SelectedSlotIndex == i;

            _slots[i].UpdateState(cooldown, isSelected);
        }
    }

    private void UpdateSelectedText()
    {
        if (selectedSkillText == null) return;

        if (caster != null && caster.IsArmed && caster.SelectedSkill != null)
        {
            string hint = caster.SelectedSkill.Skill == SkillID.Blue_StretchTentacle
                ? "이동할 위치를 클릭"
                : "적을 클릭하여 공격";
            selectedSkillText.text = $"{caster.SelectedSkill.Name} - {hint} (우클릭/ESC 취소)";
        }
        else
        {
            selectedSkillText.text = "";
        }
    }
}

/// <summary>
/// 필드 스킬바의 개별 슬롯
/// </summary>
public class FieldColorSkillSlot : MonoBehaviour
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image backgroundImage;
    public TMP_Text hotkeyText;
    public TMP_Text cooldownText;
    public Image cooldownOverlay;
    public Image selectionBorder;

    [Header("Colors")]
    public Color readyColor = new Color(1f, 1f, 1f, 1f);
    public Color cooldownColor = new Color(0.4f, 0.4f, 0.4f, 0.8f);
    public Color selectedBorderColor = new Color(1f, 1f, 0f, 1f);

    private int _index;
    private ColorModuleInstance _module;
    private System.Action<int> _onClick;

    public void Setup(int index, ColorModuleInstance module, int cooldown, bool isSelected, System.Action<int> onClick)
    {
        _index = index;
        _module = module;
        _onClick = onClick;

        // 아이콘
        if (iconImage != null)
        {
            if (module.Icon != null)
            {
                iconImage.sprite = module.Icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // 배경색 (색상별)
        if (backgroundImage != null)
        {
            backgroundImage.color = module.Color switch
            {
                ColorType.Red => new Color(0.8f, 0.2f, 0.2f, 0.6f),
                ColorType.Blue => new Color(0.2f, 0.3f, 0.8f, 0.6f),
                ColorType.Black => new Color(0.2f, 0.2f, 0.2f, 0.6f),
                _ => new Color(0.3f, 0.3f, 0.3f, 0.6f)
            };
        }

        // 단축키 표시
        if (hotkeyText != null)
            hotkeyText.text = $"{index + 1}";

        // 버튼 클릭
        var button = GetComponent<Button>();
        if (button == null) button = gameObject.AddComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => _onClick?.Invoke(_index));

        UpdateState(cooldown, isSelected);
    }

    public void UpdateState(int cooldown, bool isSelected)
    {
        // 쿨다운 표시
        bool onCooldown = cooldown > 0;

        if (cooldownText != null)
        {
            cooldownText.text = onCooldown ? $"{cooldown}" : "";
            cooldownText.enabled = onCooldown;
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.enabled = onCooldown;
        }

        // 아이콘 밝기
        if (iconImage != null)
        {
            iconImage.color = onCooldown ? cooldownColor : readyColor;
        }

        // 선택 테두리
        if (selectionBorder != null)
        {
            selectionBorder.enabled = isSelected;
            if (isSelected)
                selectionBorder.color = selectedBorderColor;
        }

        // 버튼 활성화
        var button = GetComponent<Button>();
        if (button != null)
            button.interactable = !onCooldown;
    }
}