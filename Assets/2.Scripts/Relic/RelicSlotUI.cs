using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual relic slot UI component
/// Used in both relic list and equipped display
/// </summary>
public class RelicSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image backgroundImage;
    public Image borderImage;
    public TMP_Text nameText;
    public TMP_Text countText;
    public TMP_Text rarityText;
    public Button button;
    
    [Header("States")]
    public GameObject equippedIndicator;
    public GameObject emptyIndicator;
    
    [Header("Rarity Colors")]
    public Color commonColor = Color.gray;
    public Color rareColor = Color.blue;
    public Color epicColor = new Color(0.6f, 0.2f, 0.8f);
    public Color legendaryColor = new Color(1f, 0.8f, 0f);
    
    [Header("Background Colors")]
    public Color normalBgColor = new Color(0.2f, 0.2f, 0.2f);
    public Color equippedBgColor = new Color(0.2f, 0.4f, 0.2f);
    public Color emptyBgColor = new Color(0.1f, 0.1f, 0.1f);
    
    private RelicDefinition _relic;
    private int _stackCount;
    private System.Action<RelicDefinition> _onClickCallback;
    
    public RelicDefinition Relic => _relic;
    public int StackCount => _stackCount;
    
    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }
    
    /// <summary>
    /// Setup slot with relic data
    /// </summary>
    public void Setup(RelicDefinition relic, int stackCount, System.Action<RelicDefinition> onClick = null)
    {
        _relic = relic;
        _stackCount = stackCount;
        _onClickCallback = onClick;
        
        if (relic == null)
        {
            SetEmpty();
            return;
        }
        
        // Icon
        if (iconImage != null)
        {
            if (relic.icon != null)
            {
                iconImage.sprite = relic.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
        
        // Name
        if (nameText != null)
            nameText.text = relic.relicName;
        
        // Stack count
        if (countText != null)
        {
            if (stackCount > 1)
            {
                countText.text = $"x{stackCount}";
                countText.gameObject.SetActive(true);
            }
            else
            {
                countText.gameObject.SetActive(false);
            }
        }
        
        // Rarity
        if (rarityText != null)
        {
            rarityText.text = relic.rarity.ToString();
            rarityText.color = GetRarityColor(relic.rarity);
        }
        
        // Border color based on rarity
        if (borderImage != null)
            borderImage.color = GetRarityColor(relic.rarity);
        
        // Reset equipped state
        SetEquippedState(false);
        
        // Show slot
        if (emptyIndicator != null)
            emptyIndicator.SetActive(false);
        
        // Enable button
        if (button != null)
            button.interactable = true;
    }
    
    /// <summary>
    /// Set slot as empty
    /// </summary>
    public void SetEmpty()
    {
        _relic = null;
        _stackCount = 0;
        
        if (iconImage != null)
            iconImage.enabled = false;
        
        if (nameText != null)
            nameText.text = "Empty";
        
        if (countText != null)
            countText.gameObject.SetActive(false);
        
        if (rarityText != null)
            rarityText.gameObject.SetActive(false);
        
        if (backgroundImage != null)
            backgroundImage.color = emptyBgColor;
        
        if (borderImage != null)
            borderImage.color = Color.gray;
        
        if (equippedIndicator != null)
            equippedIndicator.SetActive(false);
        
        if (emptyIndicator != null)
            emptyIndicator.SetActive(true);
        
        if (button != null)
            button.interactable = false;
    }
    
    /// <summary>
    /// Set equipped visual state
    /// </summary>
    public void SetEquippedState(bool equipped)
    {
        if (equippedIndicator != null)
            equippedIndicator.SetActive(equipped);
        
        if (backgroundImage != null)
            backgroundImage.color = equipped ? equippedBgColor : normalBgColor;
    }
    
    private Color GetRarityColor(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Common: return commonColor;
            case RelicRarity.Rare: return rareColor;
            case RelicRarity.Epic: return epicColor;
            case RelicRarity.Legendary: return legendaryColor;
            default: return Color.white;
        }
    }
    
    private void OnClicked()
    {
        if (_relic != null && _onClickCallback != null)
        {
            _onClickCallback.Invoke(_relic);
        }
    }
    
    /// <summary>
    /// Show tooltip on hover (optional)
    /// </summary>
    public string GetTooltipText()
    {
        if (_relic == null) return "";
        
        string text = $"<b>{_relic.relicName}</b>\n";
        text += $"<color=#{ColorUtility.ToHtmlStringRGB(GetRarityColor(_relic.rarity))}>{_relic.rarity}</color>\n\n";
        text += _relic.description + "\n\n";
        
        // Show stat bonuses
        text += "<b>Bonuses (per stack):</b>\n";
        
        if (_relic.bonusMaxHP > 0)
            text += $"  Max HP +{_relic.bonusMaxHP}\n";
        if (_relic.bonusATKPercent > 0)
            text += $"  ATK +{_relic.bonusATKPercent * 100:F0}%\n";
        if (_relic.bonusDEF > 0)
            text += $"  DEF +{_relic.bonusDEF}\n";
        if (_relic.bonusEVA > 0)
            text += $"  EVA +{_relic.bonusEVA * 100:F0}%\n";
        if (_relic.bonusCRIT > 0)
            text += $"  CRIT +{_relic.bonusCRIT * 100:F0}%\n";
        if (_relic.bonusCRIT_DMG > 0)
            text += $"  CRIT DMG +{_relic.bonusCRIT_DMG * 100:F0}%\n";
        if (_relic.hungerReductionPercent > 0)
            text += $"  Hunger -{_relic.hungerReductionPercent * 100:F0}%\n";
        
        if (_stackCount > 1)
        {
            float mult = _relic.GetStackMultiplier(_stackCount);
            text += $"\n<color=yellow>Stack x{_stackCount} = {mult}x bonus</color>";
        }
        
        return text;
    }
}
