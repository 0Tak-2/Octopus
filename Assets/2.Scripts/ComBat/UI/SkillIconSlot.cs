using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class SkillIconSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI (Name Only)")]
    [Tooltip("슬롯에 표시할 스킬 이름 TMP")]
    public TMP_Text nameText;

    [Tooltip("사용 가능/불가 표현용(없으면 자동추가)")]
    public CanvasGroup canvasGroup;

    [Header("Bound Skill")]
    public CombatAttackDefinition boundSkill;

    // ✅ EnemyEliteHUDPanel이 참조하던 이벤트 유지
    public event Action<CombatAttackDefinition, SkillIconSlot> OnHoverEnter;
    public event Action<CombatAttackDefinition, SkillIconSlot> OnHoverExit;

    private void Awake()
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void Bind(CombatAttackDefinition skill)
    {
        boundSkill = skill;

        if (nameText != null)
            nameText.text = (skill != null) ? skill.displayName : "";

        // 스킬이 없으면 시각적으로 흐리게(선택)
        SetUsable(skill != null);
    }

    // ✅ EnemyEliteHUDPanel이 쓰던 메서드 유지
    public void SetUsable(bool usable)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = usable ? 1f : 0.35f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHoverEnter?.Invoke(boundSkill, this);

        if (boundSkill == null) return;
        CombatTooltipUI.Show(boundSkill, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnHoverExit?.Invoke(boundSkill, this);
        CombatTooltipUI.Hide();
    }
}
