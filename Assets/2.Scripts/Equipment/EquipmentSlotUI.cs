using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 장비 슬롯 UI
/// </summary>
public class EquipmentSlotUI : MonoBehaviour, IPointerClickHandler
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
                nameText.text = _equipment.equipmentName;
            
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
}
