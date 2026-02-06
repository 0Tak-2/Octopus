using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Individual skill button in focused combat
/// Shows skill icon, name, AP cost, and handles clicks
/// </summary>
public class CombatSkillButtonUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Button button;
    public Image iconImage;
    public Image backgroundImage;
    public Image borderImage;
    public TMP_Text nameText;
    public TMP_Text apCostText;
    public TMP_Text hotkeyText;
    
    [Header("Colors")]
    public Color normalColor = Color.white;
    public Color disabledColor = Color.gray;
    public Color notEnoughAPColor = new Color(0.5f, 0.3f, 0.3f);
    
    [Header("Color Module Colors")]
    public Color redColor = new Color(0.9f, 0.3f, 0.3f);
    public Color blueColor = new Color(0.3f, 0.5f, 0.9f);
    public Color blackColor = new Color(0.3f, 0.3f, 0.3f);
    
    private ColorModuleInstance _module;
    private System.Action<ColorModuleInstance> _onClickCallback;
    private int _apCost = 1;
    
    public ColorModuleInstance Module => _module;
    public int APCost => _apCost;
    
    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
        
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }
    
    /// <summary>
    /// Setup button with skill data
    /// </summary>
    public void Setup(ColorModuleInstance module, System.Action<ColorModuleInstance> onClick)
    {
        _module = module;
        _onClickCallback = onClick;
        
        if (module == null || module.definition == null)
        {
            gameObject.SetActive(false);
            return;
        }
        
        var def = module.definition;
        
        // Icon
        if (iconImage != null)
        {
            if (def.icon != null)
            {
                iconImage.sprite = def.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.enabled = false;
            }
        }
        
        // Name
        if (nameText != null)
            nameText.text = def.moduleName;
        
        // AP Cost
        _apCost = def.apCost;
        if (apCostText != null)
            apCostText.text = $"{_apCost} AP";
        
        // Border color based on color type
        if (borderImage != null)
            borderImage.color = GetColorTypeColor(def.colorType);
        
        // Background tint
        if (backgroundImage != null)
        {
            Color bgColor = GetColorTypeColor(def.colorType);
            bgColor.a = 0.3f;
            backgroundImage.color = bgColor;
        }
        
        gameObject.SetActive(true);
    }
    
    /// <summary>
    /// Update button state based on available AP
    /// </summary>
    public void UpdateState(int currentAP)
    {
        bool canAfford = currentAP >= _apCost;
        
        if (button != null)
            button.interactable = canAfford;
        
        // Visual feedback
        if (backgroundImage != null)
        {
            Color color = canAfford ? normalColor : notEnoughAPColor;
            color.a = backgroundImage.color.a;
            backgroundImage.color = color;
        }
        
        if (iconImage != null)
            iconImage.color = canAfford ? Color.white : disabledColor;
        
        if (nameText != null)
            nameText.color = canAfford ? Color.white : disabledColor;
        
        if (apCostText != null)
            apCostText.color = canAfford ? Color.yellow : Color.red;
    }
    
    /// <summary>
    /// Set hotkey display
    /// </summary>
    public void SetHotkey(string hotkey)
    {
        if (hotkeyText != null)
        {
            hotkeyText.text = hotkey;
            hotkeyText.gameObject.SetActive(!string.IsNullOrEmpty(hotkey));
        }
    }
    
    private Color GetColorTypeColor(ColorType colorType)
    {
        switch (colorType)
        {
            case ColorType.Red: return redColor;
            case ColorType.Blue: return blueColor;
            case ColorType.Black: return blackColor;
            default: return normalColor;
        }
    }
    
    private void OnClicked()
    {
        if (_module != null && _onClickCallback != null)
        {
            _onClickCallback.Invoke(_module);
        }
    }
    
    /// <summary>
    /// Get tooltip text for this skill
    /// </summary>
    public string GetTooltipText()
    {
        if (_module == null || _module.definition == null)
            return "";
        
        var def = _module.definition;
        
        string text = $"<b>{def.moduleName}</b>\n";
        text += $"<color=yellow>AP Cost: {def.apCost}</color>\n";
        text += $"<color=#{ColorUtility.ToHtmlStringRGB(GetColorTypeColor(def.colorType))}>{def.colorType}</color>\n\n";
        text += def.description;
        
        return text;
    }
}
