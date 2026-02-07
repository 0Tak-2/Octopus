using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays player status bars (HP, Fatigue, Hunger)
/// Usually placed at top center of screen
/// </summary>
public class PlayerStatusBarUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;

    [Header("HP Bar")]
    public Slider hpSlider;
    public Image hpFillImage;
    public TMP_Text hpText;
    public Color hpColorFull = Color.green;
    public Color hpColorLow = Color.red;
    [Range(0f, 0.5f)] public float hpLowThreshold = 0.3f;

    [Header("Fatigue Bar")]
    public Slider fatigueSlider;
    public Image fatigueFillImage;
    public TMP_Text fatigueText;
    public Color fatigueColorFull = new Color(0.4f, 0.6f, 1f); // Light blue
    public Color fatigueColorLow = new Color(0.3f, 0.3f, 0.5f); // Dark blue
    [Range(0f, 0.5f)] public float fatigueLowThreshold = 0.2f;

    [Header("Hunger Bar")]
    public Slider hungerSlider;
    public Image hungerFillImage;
    public TMP_Text hungerText;
    public Color hungerColorFull = new Color(1f, 0.8f, 0.4f); // Orange
    public Color hungerColorLow = new Color(0.6f, 0.3f, 0.2f); // Dark red
    [Range(0f, 0.5f)] public float hungerLowThreshold = 0.25f;

    [Header("Status Icons (Optional)")]
    public GameObject lowHPWarning;
    public GameObject lowFatigueWarning;
    public GameObject lowHungerWarning;

    [Header("Animation")]
    public bool animateLowBars = true;
    public float pulseSpeed = 2f;

    private float _pulseTimer;

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();

        // Hide warnings initially
        if (lowHPWarning != null) lowHPWarning.SetActive(false);
        if (lowFatigueWarning != null) lowFatigueWarning.SetActive(false);
        if (lowHungerWarning != null) lowHungerWarning.SetActive(false);
    }

    private void Update()
    {
        if (playerStats == null) return;

        UpdateHPBar();
        UpdateFatigueBar();
        UpdateHungerBar();

        if (animateLowBars)
            _pulseTimer += Time.deltaTime * pulseSpeed;
    }

    // ============================================
    // HP Bar
    // ============================================

    private void UpdateHPBar()
    {
        int current = playerStats.hp;
        int max = playerStats.maxHP;
        float ratio = max > 0 ? (float)current / max : 0f;

        // Slider
        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.value = current;
        }

        // Text
        if (hpText != null)
            hpText.text = $"{current}/{max}";

        // Color
        if (hpFillImage != null)
        {
            Color targetColor = Color.Lerp(hpColorLow, hpColorFull, ratio);

            if (ratio <= hpLowThreshold && animateLowBars)
            {
                // Pulse effect when low
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
                targetColor = Color.Lerp(hpColorLow, Color.white, pulse * 0.3f);
            }

            hpFillImage.color = targetColor;
        }

        // Warning icon
        if (lowHPWarning != null)
            lowHPWarning.SetActive(ratio <= hpLowThreshold);
    }

    // ============================================
    // Fatigue Bar
    // ============================================

    private void UpdateFatigueBar()
    {
        // Assuming PlayerStats has fatigue fields
        int current = GetFatigue();
        int max = GetMaxFatigue();
        float ratio = max > 0 ? (float)current / max : 0f;

        // Slider
        if (fatigueSlider != null)
        {
            fatigueSlider.maxValue = max;
            fatigueSlider.value = current;
        }

        // Text
        if (fatigueText != null)
            fatigueText.text = $"{current}/{max}";

        // Color
        if (fatigueFillImage != null)
        {
            Color targetColor = Color.Lerp(fatigueColorLow, fatigueColorFull, ratio);

            if (ratio <= fatigueLowThreshold && animateLowBars)
            {
                float pulse = (Mathf.Sin(_pulseTimer * 0.8f) + 1f) * 0.5f;
                targetColor = Color.Lerp(fatigueColorLow, Color.gray, pulse * 0.3f);
            }

            fatigueFillImage.color = targetColor;
        }

        // Warning
        if (lowFatigueWarning != null)
            lowFatigueWarning.SetActive(ratio <= fatigueLowThreshold);
    }

    // ============================================
    // Hunger Bar
    // ============================================

    private void UpdateHungerBar()
    {
        int current = GetHunger();
        int max = GetMaxHunger();
        float ratio = max > 0 ? (float)current / max : 0f;

        // Slider
        if (hungerSlider != null)
        {
            hungerSlider.maxValue = max;
            hungerSlider.value = current;
        }

        // Text
        if (hungerText != null)
            hungerText.text = $"{current}/{max}";

        // Color
        if (hungerFillImage != null)
        {
            Color targetColor = Color.Lerp(hungerColorLow, hungerColorFull, ratio);

            if (ratio <= hungerLowThreshold && animateLowBars)
            {
                float pulse = (Mathf.Sin(_pulseTimer * 1.2f) + 1f) * 0.5f;
                targetColor = Color.Lerp(hungerColorLow, Color.red, pulse * 0.4f);
            }

            hungerFillImage.color = targetColor;
        }

        // Warning
        if (lowHungerWarning != null)
            lowHungerWarning.SetActive(ratio <= hungerLowThreshold);
    }

    // ============================================
    // Helper methods to get stats
    // (Adjust these based on your PlayerStats implementation)
    // ============================================

    private int GetFatigue()
    {
        // Try to get fatigue from PlayerStats
        var field = playerStats.GetType().GetField("currentFatigue");
        if (field != null)
            return (int)field.GetValue(playerStats);

        var prop = playerStats.GetType().GetProperty("Fatigue");
        if (prop != null)
            return (int)prop.GetValue(playerStats);

        return 100; // Default
    }

    private int GetMaxFatigue()
    {
        var field = playerStats.GetType().GetField("maxFatigue");
        if (field != null)
            return (int)field.GetValue(playerStats);

        var prop = playerStats.GetType().GetProperty("MaxFatigue");
        if (prop != null)
            return (int)prop.GetValue(playerStats);

        return 100;
    }

    private int GetHunger()
    {
        var field = playerStats.GetType().GetField("currentHunger");
        if (field != null)
            return (int)field.GetValue(playerStats);

        var prop = playerStats.GetType().GetProperty("Hunger");
        if (prop != null)
            return (int)prop.GetValue(playerStats);

        return 100;
    }

    private int GetMaxHunger()
    {
        var field = playerStats.GetType().GetField("maxHunger");
        if (field != null)
            return (int)field.GetValue(playerStats);

        var prop = playerStats.GetType().GetProperty("MaxHunger");
        if (prop != null)
            return (int)prop.GetValue(playerStats);

        return 100;
    }

    // ============================================
    // Public methods for external updates
    // ============================================

    /// <summary>
    /// Flash HP bar (when taking damage)
    /// </summary>
    public void FlashHP()
    {
        StartCoroutine(FlashBarCoroutine(hpFillImage, Color.white));
    }

    /// <summary>
    /// Flash Hunger bar (when eating)
    /// </summary>
    public void FlashHunger()
    {
        StartCoroutine(FlashBarCoroutine(hungerFillImage, Color.yellow));
    }

    private System.Collections.IEnumerator FlashBarCoroutine(Image image, Color flashColor)
    {
        if (image == null) yield break;

        Color originalColor = image.color;
        image.color = flashColor;

        yield return new WaitForSeconds(0.1f);

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            image.color = Color.Lerp(flashColor, originalColor, t / 0.2f);
            yield return null;
        }

        image.color = originalColor;
    }
}