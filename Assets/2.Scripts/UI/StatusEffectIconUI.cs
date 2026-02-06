using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Displays a status effect icon
/// Shows remaining turns and stacks
/// </summary>
public class StatusEffectIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image borderImage;
    public TMP_Text turnsText;
    public TMP_Text stacksText;
    
    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;
    
    [Header("Effect Colors")]
    public Color bleedColor = new Color(0.8f, 0.2f, 0.2f);
    public Color incompleteColor = new Color(0.6f, 0.4f, 0.8f);
    public Color stunColor = new Color(1f, 0.9f, 0.3f);
    public Color buffColor = new Color(0.3f, 0.8f, 0.3f);
    public Color debuffColor = new Color(0.8f, 0.4f, 0.2f);
    
    private StatusEffectInstance _effect;
    
    public void Setup(StatusEffectInstance effect)
    {
        _effect = effect;
        
        if (effect == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        // Icon
        if (iconImage != null)
        {
            if (effect.icon != null)
            {
                iconImage.sprite = effect.icon;
            }
            else
            {
                // Color based on effect type
                iconImage.color = GetEffectColor(effect.effectName);
            }
            iconImage.enabled = true;
        }
        
        // Border
        if (borderImage != null)
            borderImage.color = GetEffectColor(effect.effectName);
        
        // Turns remaining
        if (turnsText != null)
        {
            if (effect.remainingTurns > 0)
            {
                turnsText.text = effect.remainingTurns.ToString();
                turnsText.gameObject.SetActive(true);
            }
            else
            {
                turnsText.gameObject.SetActive(false);
            }
        }
        
        // Stacks
        if (stacksText != null)
        {
            if (effect.stacks > 1)
            {
                stacksText.text = $"x{effect.stacks}";
                stacksText.gameObject.SetActive(true);
            }
            else
            {
                stacksText.gameObject.SetActive(false);
            }
        }
        
        // Hide tooltip
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
        
        gameObject.SetActive(true);
    }
    
    private Color GetEffectColor(string effectName)
    {
        if (string.IsNullOrEmpty(effectName))
            return Color.white;
        
        string lower = effectName.ToLower();
        
        if (lower.Contains("bleed") || lower.Contains("출혈"))
            return bleedColor;
        if (lower.Contains("incomplete") || lower.Contains("불완전"))
            return incompleteColor;
        if (lower.Contains("stun") || lower.Contains("스턴"))
            return stunColor;
        if (lower.Contains("buff") || lower.Contains("강화"))
            return buffColor;
        if (lower.Contains("debuff") || lower.Contains("약화"))
            return debuffColor;
        
        return Color.white;
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }
    
    private void ShowTooltip()
    {
        if (tooltipPanel == null || _effect == null)
            return;
        
        if (tooltipText != null)
        {
            string text = $"<b>{_effect.effectName}</b>\n";
            
            if (_effect.remainingTurns > 0)
                text += $"Turns: {_effect.remainingTurns}\n";
            
            if (_effect.stacks > 1)
                text += $"Stacks: {_effect.stacks}\n";
            
            // Add effect description based on type
            text += "\n" + GetEffectDescription(_effect.effectName);
            
            tooltipText.text = text;
        }
        
        tooltipPanel.SetActive(true);
    }
    
    private void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
    
    private string GetEffectDescription(string effectName)
    {
        if (string.IsNullOrEmpty(effectName))
            return "";
        
        string lower = effectName.ToLower();
        
        if (lower.Contains("bleed") || lower.Contains("출혈"))
            return "Takes 3 damage at end of turn.";
        
        if (lower.Contains("incomplete") || lower.Contains("불완전"))
            return "Takes 25% extra damage per stack from next attack.";
        
        if (lower.Contains("stun") || lower.Contains("스턴"))
            return "Cannot act. Loses 1 AP.";
        
        return "";
    }
}
