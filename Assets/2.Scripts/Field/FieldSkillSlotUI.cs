using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class FieldSkillSlotUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    public Image iconImage;
    public Image selectionFrame;
    public Image cooldownOverlay;
    public TMP_Text cooldownText;

    [Header("Runtime")]
    [SerializeField] private int slotIndex1Based = 1;

    private System.Action<int> _onClickSlot;
    private System.Action<int, bool> _onHoverSlot; // (slotIndex, isEnter)

    public void Init(int slotIndex, System.Action<int> onClickSlot, System.Action<int, bool> onHoverSlot)
    {
        slotIndex1Based = slotIndex;
        _onClickSlot = onClickSlot;
        _onHoverSlot = onHoverSlot;
    }

    public void SetSelected(bool selected)
    {
        if (selectionFrame != null)
            selectionFrame.enabled = selected;
    }

    public void SetIcon(Sprite sprite, bool hasSkill)
    {
        if (iconImage == null) return;

        iconImage.enabled = true;
        iconImage.sprite = sprite;

        // 스킬이 없으면 흐리게
        iconImage.color = hasSkill ? Color.white : new Color(1f, 1f, 1f, 0.15f);
    }

    public void SetCooldown(int remainingTurns)
    {
        remainingTurns = Mathf.Max(0, remainingTurns);
        bool onCd = remainingTurns > 0;

        if (cooldownOverlay != null)
            cooldownOverlay.enabled = onCd;

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCd);
            cooldownText.text = onCd ? remainingTurns.ToString() : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        _onClickSlot?.Invoke(slotIndex1Based);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _onHoverSlot?.Invoke(slotIndex1Based, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onHoverSlot?.Invoke(slotIndex1Based, false);
    }
}
