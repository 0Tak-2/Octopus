using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Character and relic selection screen before starting a run
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    [Header("References")]
    public RelicManager relicManager;

    [Header("Scene")]
    public string gameSceneName = "GameScene";

    [Header("Character Display")]
    public TMP_Text characterNameText;
    public TMP_Text characterDescText;
    public Image characterImage;

    [Header("Relic Selection")]
    public Transform relicListParent;
    public GameObject relicSlotPrefab;
    public int maxRelicSlots = 5;

    [Header("Equipped Relics Display")]
    public Transform equippedRelicParent;
    public GameObject equippedRelicSlotPrefab;

    [Header("Stats Preview")]
    public TMP_Text statsPreviewText;

    [Header("Buttons")]
    public Button startButton;
    public Button backButton;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private List<RelicSlotUI> _relicSlots = new List<RelicSlotUI>();
    private List<RelicSlotUI> _equippedSlots = new List<RelicSlotUI>();

    private void Start()
    {
        if (relicManager == null)
            relicManager = RelicManager.Instance ?? FindObjectOfType<RelicManager>();

        // Setup buttons
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        // Subscribe to relic changes
        if (relicManager != null)
        {
            relicManager.OnEquippedChanged += RefreshEquippedDisplay;
            relicManager.OnEquippedChanged += RefreshStatsPreview;
        }

        // Initialize displays
        SetupCharacterDisplay();
        PopulateRelicList();
        RefreshEquippedDisplay();
        RefreshStatsPreview();
    }

    private void OnDestroy()
    {
        if (relicManager != null)
        {
            relicManager.OnEquippedChanged -= RefreshEquippedDisplay;
            relicManager.OnEquippedChanged -= RefreshStatsPreview;
        }
    }

    // ============================================
    // Character Display
    // ============================================

    private void SetupCharacterDisplay()
    {
        // For demo, just one character (Octopus)
        if (characterNameText != null)
            characterNameText.text = LocalizationManager.T("UI_CHARACTER_OCTOPUS");

        if (characterDescText != null)
            characterDescText.text = LocalizationManager.T("UI_CHARACTER_OCTOPUS_DESC");
    }

    // ============================================
    // Relic List
    // ============================================

    private void PopulateRelicList()
    {
        if (relicManager == null || relicListParent == null || relicSlotPrefab == null)
            return;

        // Clear existing
        foreach (Transform child in relicListParent)
        {
            Destroy(child.gameObject);
        }
        _relicSlots.Clear();

        // Get owned relics
        var ownedRelics = relicManager.GetAllOwnedRelics();

        foreach (var kvp in ownedRelics)
        {
            var relic = kvp.Key;
            int count = kvp.Value;

            // Create slot
            GameObject slotObj = Instantiate(relicSlotPrefab, relicListParent);
            var slotUI = slotObj.GetComponent<RelicSlotUI>();

            if (slotUI != null)
            {
                slotUI.Setup(relic, count, OnRelicSlotClicked);
                _relicSlots.Add(slotUI);
            }
        }

        if (showDebugLogs)
            Debug.Log($"[CharacterSelect] Populated {ownedRelics.Count} owned relics");
    }

    private void OnRelicSlotClicked(RelicDefinition relic)
    {
        if (relicManager == null || relic == null) return;

        // Check if already equipped
        var equipped = relicManager.GetEquippedRelics();

        if (equipped.Contains(relic))
        {
            // Unequip
            relicManager.UnequipRelic(relic);
        }
        else
        {
            // Try to equip
            if (equipped.Count < relicManager.maxEquippedRelics)
            {
                relicManager.EquipRelic(relic);
            }
            else
            {
                if (showDebugLogs)
                    Debug.Log("[CharacterSelect] Max relics equipped!");
            }
        }

        // Refresh slot visuals
        RefreshRelicSlotVisuals();
    }

    private void RefreshRelicSlotVisuals()
    {
        if (relicManager == null) return;

        var equipped = relicManager.GetEquippedRelics();

        foreach (var slot in _relicSlots)
        {
            if (slot != null && slot.Relic != null)
            {
                bool isEquipped = equipped.Contains(slot.Relic);
                slot.SetEquippedState(isEquipped);
            }
        }
    }

    // ============================================
    // Equipped Display
    // ============================================

    private void RefreshEquippedDisplay()
    {
        if (relicManager == null || equippedRelicParent == null)
            return;

        // Clear existing
        foreach (Transform child in equippedRelicParent)
        {
            Destroy(child.gameObject);
        }
        _equippedSlots.Clear();

        var equipped = relicManager.GetEquippedRelics();

        // Show equipped relics
        foreach (var relic in equipped)
        {
            if (equippedRelicSlotPrefab != null)
            {
                GameObject slotObj = Instantiate(equippedRelicSlotPrefab, equippedRelicParent);
                var slotUI = slotObj.GetComponent<RelicSlotUI>();

                if (slotUI != null)
                {
                    int count = relicManager.GetOwnedCount(relic.relicID);
                    slotUI.Setup(relic, count, OnRelicSlotClicked);
                    slotUI.SetEquippedState(true);
                    _equippedSlots.Add(slotUI);
                }
            }
        }

        // Show empty slots
        int emptySlots = relicManager.maxEquippedRelics - equipped.Count;
        for (int i = 0; i < emptySlots; i++)
        {
            if (equippedRelicSlotPrefab != null)
            {
                GameObject slotObj = Instantiate(equippedRelicSlotPrefab, equippedRelicParent);
                var slotUI = slotObj.GetComponent<RelicSlotUI>();

                if (slotUI != null)
                {
                    slotUI.SetEmpty();
                    _equippedSlots.Add(slotUI);
                }
            }
        }

        // Also refresh the selection list visuals
        RefreshRelicSlotVisuals();
    }

    // ============================================
    // Stats Preview
    // ============================================

    private void RefreshStatsPreview()
    {
        if (statsPreviewText == null || relicManager == null)
            return;

        // Base stats
        int baseHP = 100;
        int baseATK = 10;
        int baseDEF = 0;
        float baseEVA = 0.05f;
        float baseCRIT = 0.05f;
        float baseCRIT_DMG = 1.5f;

        // Relic bonuses
        int bonusHP = relicManager.GetTotalMaxHPBonus();
        float bonusATK = relicManager.GetTotalATKPercent();
        int bonusDEF = relicManager.GetTotalDEFBonus();
        float bonusEVA = relicManager.GetTotalEVABonus();
        float bonusCRIT = relicManager.GetTotalCRITBonus();
        float bonusCRIT_DMG = relicManager.GetTotalCRIT_DMGBonus();
        float hungerReduce = relicManager.GetTotalHungerReduction();

        // Calculate final
        int finalHP = baseHP + bonusHP;
        int finalATK = Mathf.RoundToInt(baseATK * (1f + bonusATK));
        int finalDEF = baseDEF + bonusDEF;
        float finalEVA = baseEVA + bonusEVA;
        float finalCRIT = baseCRIT + bonusCRIT;
        float finalCRIT_DMG = baseCRIT_DMG + bonusCRIT_DMG;

        string text = "<b>Final Stats</b>\n";
        text += $"HP: {finalHP}";
        if (bonusHP > 0) text += $" <color=green>(+{bonusHP})</color>";
        text += "\n";

        text += $"ATK: {finalATK}";
        if (bonusATK > 0) text += $" <color=green>(+{bonusATK * 100:F0}%)</color>";
        text += "\n";

        text += $"DEF: {finalDEF}";
        if (bonusDEF > 0) text += $" <color=green>(+{bonusDEF})</color>";
        text += "\n";

        text += $"EVA: {finalEVA * 100:F0}%";
        if (bonusEVA > 0) text += $" <color=green>(+{bonusEVA * 100:F0}%)</color>";
        text += "\n";

        text += $"CRIT: {finalCRIT * 100:F0}%";
        if (bonusCRIT > 0) text += $" <color=green>(+{bonusCRIT * 100:F0}%)</color>";
        text += "\n";

        text += $"CRIT DMG: {finalCRIT_DMG * 100:F0}%";
        if (bonusCRIT_DMG > 0) text += $" <color=green>(+{bonusCRIT_DMG * 100:F0}%)</color>";

        if (hungerReduce > 0)
        {
            text += $"\n<color=yellow>Hunger -{hungerReduce * 100:F0}%</color>";
        }

        statsPreviewText.text = text;
    }

    // ============================================
    // Buttons
    // ============================================

    private void OnStartClicked()
    {
        if (showDebugLogs)
        {
            var equipped = relicManager?.GetEquippedRelics();
            Debug.Log($"[CharacterSelect] Starting game with {equipped?.Count ?? 0} relics equipped");
        }

        SceneManager.LoadScene(gameSceneName);
    }

    private void OnBackClicked()
    {
        // Clear equipped relics
        relicManager?.ClearEquipped();

        SceneManager.LoadScene("MainMenu");
    }
}