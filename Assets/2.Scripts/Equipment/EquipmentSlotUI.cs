using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 장비 슬롯 UI
/// </summary>
public class EquipmentSlotUI : MonoBehaviour,
    IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image backgroundImage;
    public Image typeIndicator;
    public TMP_Text nameText;
    public GameObject emptyIndicator;
    
    [Header("Type Colors")]
    public Color weaponColor = new Color(1f, 0.5f, 0.3f, 0.8f);
    public Color armorColor = new Color(0.3f, 0.6f, 1f, 0.8f);
    public Color accessoryColor = new Color(0.8f, 0.3f, 0.8f, 0.8f);
    public Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    
    private EquipmentDefinition _equipment;
    private int _slotIndex = -1;
    
    public EquipmentDefinition Equipment => _equipment;
    public int SlotIndex => _slotIndex;
    
    public void SetSlotIndex(int index)
    {
        _slotIndex = index;
    }
    
    public void SetEquipment(EquipmentDefinition equipment)
    {
        _equipment = equipment;
        Refresh();
    }
    
    public void Refresh()
    {
        if (_equipment == null)
        {
            // 빈 슬롯
            if (iconImage != null) iconImage.enabled = false;
            if (nameText != null) nameText.text = "";
            if (emptyIndicator != null) emptyIndicator.SetActive(true);
            if (backgroundImage != null) backgroundImage.color = emptyColor;
            if (typeIndicator != null) typeIndicator.enabled = false;
        }
        else
        {
            // 장비 있음
            if (iconImage != null)
            {
                if (_equipment.icon != null)
                {
                    iconImage.sprite = _equipment.icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
            
            if (nameText != null)
                nameText.text = !string.IsNullOrEmpty(_equipment.nameKey)
    ? LocalizationManager.T(_equipment.nameKey) : _equipment.equipmentName;

            if (emptyIndicator != null)
                emptyIndicator.SetActive(false);
            
            // 타입별 색상
            Color typeColor = _equipment.equipmentType switch
            {
                EquipmentType.Weapon => weaponColor,
                EquipmentType.Armor => armorColor,
                EquipmentType.Accessory => accessoryColor,
                _ => emptyColor
            };
            
            if (backgroundImage != null)
                backgroundImage.color = typeColor;
            
            if (typeIndicator != null)
            {
                typeIndicator.color = typeColor;
                typeIndicator.enabled = true;
            }
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!eventData.eligibleForClick) return;
        if (EquipmentUI.Instance == null) return;
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            EquipmentUI.Instance.OnSlotClicked(_slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            EquipmentUI.Instance.OnSlotRightClicked(_slotIndex);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_equipment == null || _slotIndex < 0) return;
        InventoryDragSession.BeginEquipment(_slotIndex);

        if (iconImage != null && iconImage.sprite != null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                GameObject g = new GameObject("EqDragGhost");
                g.transform.SetParent(c.transform, false);
                g.transform.SetAsLastSibling();
                var img = g.AddComponent<Image>();
                img.sprite = iconImage.sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                var rt = g.GetComponent<RectTransform>();
                rt.sizeDelta = iconImage.rectTransform.rect.size;
                InventoryDragSession.DragGhost = g;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InventoryDragSession.DragGhost != null)
            InventoryDragSession.DragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragSession.Clear();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_slotIndex < 0) return;
        if (InventoryDragResolver.TryDropOnEquipmentSlot(_slotIndex))
            InventoryDragSession.Clear();
    }
}
