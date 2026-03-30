using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Displays a passive effect icon in combat
/// Shows tooltip on hover
/// </summary>
public class PassiveIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public Image borderImage;
    public TMP_Text stackText;

    [Header("Tooltip")]
    public GameObject tooltipPanel;
    public TMP_Text tooltipText;

    [Header("Color Type Colors")]
    public Color redColor = new Color(0.9f, 0.3f, 0.3f);
    public Color blueColor = new Color(0.3f, 0.5f, 0.9f);
    public Color blackColor = new Color(0.3f, 0.3f, 0.3f);

    private ColorModuleInstance _module;

    public void Setup(ColorModuleInstance module)
    {
        _module = module;

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
                // Use color-based default
                iconImage.enabled = true;
                iconImage.color = GetColorTypeColor(def.colorType);
            }
        }

        // Border
        if (borderImage != null)
            borderImage.color = GetColorTypeColor(def.colorType);

        // Stack text (if applicable)
        if (stackText != null)
            stackText.gameObject.SetActive(false);

        // Hide tooltip initially
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        gameObject.SetActive(true);
    }

    private Color GetColorTypeColor(ColorType colorType)
    {
        switch (colorType)
        {
            case ColorType.Red: return redColor;
            case ColorType.Blue: return blueColor;
            case ColorType.Black: return blackColor;
            default: return Color.white;
        }
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
        if (tooltipPanel == null || _module == null || _module.definition == null)
            return;

        var def = _module.definition;

        if (tooltipText != null)
        {
            string localName = !string.IsNullOrEmpty(def.nameKey)
    ? LocalizationManager.T(def.nameKey) : def.moduleName;
            string text = $"<b>{localName}</b>\n";
            text += $"<color=#{ColorUtility.ToHtmlStringRGB(GetColorTypeColor(def.colorType))}>{def.colorType} Passive</color>\n\n";
            text += !string.IsNullOrEmpty(def.descKey)
                ? LocalizationManager.T(def.descKey) : def.description;

            tooltipText.text = text;
        }

        tooltipPanel.SetActive(true);
    }

    private void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
}