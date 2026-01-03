using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

public class SkillIconSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    public Image iconImage;
    public TMP_Text apCostText;
    public CanvasGroup canvasGroup;

    [Header("State (ReadOnly at runtime)")]
    public CombatAttackDefinition boundSkill;

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

        // Icon
        if (iconImage != null)
        {
            Sprite s = (skill != null) ? skill.icon : null;
            iconImage.sprite = s;
            iconImage.enabled = (s != null);

            // 혹시 알파가 0으로 되어있을 수 있어서 강제로 1
            var c = iconImage.color; c.a = 1f; iconImage.color = c;
        }

        // AP Cost text: "1", "2" 같이 숫자만 표시
        if (apCostText != null)
        {
            apCostText.text = (skill != null) ? skill.apCost.ToString() : "";
            var c = apCostText.color; c.a = 1f; apCostText.color = c;
        }

        // 기본 usable은 true로
        SetUsable(true);
    }

    public void SetUsable(bool usable)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = usable ? 1f : 0.35f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnHoverEnter?.Invoke(boundSkill, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnHoverExit?.Invoke(boundSkill, this);
    }
}
