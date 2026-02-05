using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Death screen UI - shows run statistics and relic reward
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("References")]
    public DeathManager deathManager;
    
    [Header("UI Elements - Stats")]
    public TMP_Text titleText;
    public TMP_Text playTimeText;
    public TMP_Text killsText;
    public TMP_Text progressText;
    public TMP_Text damageText;
    public TMP_Text survivalText;
    public TMP_Text causeOfDeathText;
    
    [Header("UI Elements - Relic Reward")]
    public GameObject relicRewardPanel;
    public Image relicIcon;
    public TMP_Text relicNameText;
    public TMP_Text relicDescText;
    public TMP_Text relicRarityText;
    
    [Header("Buttons")]
    public Button continueButton;
    public Button mainMenuButton;
    
    [Header("Rarity Colors")]
    public Color commonColor = Color.gray;
    public Color rareColor = Color.blue;
    public Color epicColor = new Color(0.6f, 0.2f, 0.8f); // Purple
    public Color legendaryColor = new Color(1f, 0.8f, 0f); // Gold
    
    private void Start()
    {
        if (deathManager == null)
            deathManager = DeathManager.Instance ?? FindObjectOfType<DeathManager>();
        
        if (deathManager != null)
            deathManager.OnPlayerDeath += OnPlayerDeath;
        
        // Setup buttons
        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
        
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        
        // Hide by default
        gameObject.SetActive(false);
    }
    
    private void OnDestroy()
    {
        if (deathManager != null)
            deathManager.OnPlayerDeath -= OnPlayerDeath;
    }
    
    private void OnPlayerDeath(RunStatistics stats, RelicDefinition rewardedRelic)
    {
        gameObject.SetActive(true);
        DisplayStats(stats);
        DisplayRelicReward(rewardedRelic);
    }
    
    private void DisplayStats(RunStatistics stats)
    {
        if (titleText != null)
            titleText.text = "You Died";
        
        if (playTimeText != null)
        {
            int minutes = Mathf.FloorToInt(stats.totalPlayTime / 60f);
            int seconds = Mathf.FloorToInt(stats.totalPlayTime % 60f);
            playTimeText.text = $"Play Time: {minutes}:{seconds:D2}";
        }
        
        if (killsText != null)
        {
            killsText.text = $"Kills: {stats.totalKills}\n" +
                            $"  Elites: {stats.eliteKills}\n" +
                            $"  Bosses: {stats.bossKills}";
        }
        
        if (progressText != null)
        {
            progressText.text = $"Highest Field: {stats.highestFieldReached}\n" +
                               $"Dungeons Cleared: {stats.dungeonsCleared}";
        }
        
        if (damageText != null)
        {
            damageText.text = $"Damage Dealt: {stats.totalDamageDealt}\n" +
                             $"Damage Taken: {stats.totalDamageTaken}\n" +
                             $"Close Calls: {stats.closeCalls}";
        }
        
        if (survivalText != null)
        {
            survivalText.text = $"Meals: {stats.mealsEaten}\n" +
                               $"Rests: {stats.timesRested}\n" +
                               $"Crafts: {stats.itemsCrafted}";
        }
        
        if (causeOfDeathText != null)
        {
            causeOfDeathText.text = $"Cause: {stats.causeOfDeath}";
        }
    }
    
    private void DisplayRelicReward(RelicDefinition relic)
    {
        if (relicRewardPanel == null) return;
        
        if (relic == null)
        {
            relicRewardPanel.SetActive(false);
            return;
        }
        
        relicRewardPanel.SetActive(true);
        
        if (relicIcon != null)
        {
            if (relic.icon != null)
            {
                relicIcon.sprite = relic.icon;
                relicIcon.enabled = true;
            }
            else
            {
                relicIcon.enabled = false;
            }
        }
        
        if (relicNameText != null)
            relicNameText.text = relic.relicName;
        
        if (relicDescText != null)
            relicDescText.text = relic.description;
        
        if (relicRarityText != null)
        {
            relicRarityText.text = relic.rarity.ToString();
            relicRarityText.color = GetRarityColor(relic.rarity);
        }
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
    
    private void OnContinueClicked()
    {
        deathManager?.OnContinueClicked();
    }
    
    private void OnMainMenuClicked()
    {
        deathManager?.OnMainMenuClicked();
    }
}
