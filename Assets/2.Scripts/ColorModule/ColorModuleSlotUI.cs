using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 개별 색 모듈 슬롯 UI
/// </summary>
public class ColorModuleSlotUI : MonoBehaviour, IPointerClickHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image backgroundImage;
    public TMP_Text nameText;
    public TMP_Text apCostText;
    public GameObject emptyIndicator;
    
    [Header("Colors")]
    public Color emptyColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
    public Color redColor = new Color(1f, 0.3f, 0.3f, 0.8f);
    public Color blueColor = new Color(0.3f, 0.3f, 1f, 0.8f);
    public Color blackColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    
    private ColorModuleInstance _module;
    private int _slotIndex = -1;
    
    public ColorModuleInstance Module => _module;
    public int SlotIndex => _slotIndex;
    
    public void SetSlotIndex(int index)
    {
        _slotIndex = index;
    }
    
    public void SetModule(ColorModuleInstance module)
    {
        _module = module;
        Refresh();
    }
    
    public void Refresh()
    {
        if (_module == null || _module.definition == null)
        {
            // 빈 슬롯
            if (iconImage != null) iconImage.enabled = false;
            if (nameText != null) nameText.text = "";
            if (apCostText != null) apCostText.text = "";
            if (emptyIndicator != null) emptyIndicator.SetActive(true);
            if (backgroundImage != null) backgroundImage.color = emptyColor;
        }
        else
        {
            // 모듈 있음
            if (iconImage != null)
            {
                if (_module.Icon != null)
                {
                    iconImage.sprite = _module.Icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }
            
            if (nameText != null)
                nameText.text = _module.Name;
            
            if (apCostText != null)
            {
                if (_module.Type == ModuleType.Skill)
                    apCostText.text = $"AP {_module.APCost}";
                else
                    apCostText.text = "패시브";
            }
            
            if (emptyIndicator != null)
                emptyIndicator.SetActive(false);
            
            if (backgroundImage != null)
            {
                backgroundImage.color = _module.Color switch
                {
                    ColorType.Red => redColor,
                    ColorType.Blue => blueColor,
                    ColorType.Black => blackColor,
                    _ => emptyColor
                };
            }
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (ColorModuleUI.Instance == null) return;
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ColorModuleUI.Instance.OnSlotClicked(_slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            ColorModuleUI.Instance.OnSlotRightClicked(_slotIndex);
        }
    }
}
