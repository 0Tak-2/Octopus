using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the focused combat HUD
/// Handles skill buttons, AP display, status effects
/// </summary>
public class CombatHUDManager : MonoBehaviour
{
    public static CombatHUDManager Instance { get; private set; }
    
    [Header("References")]
    public FocusedCombatManager combatManager;
    public ColorModuleSlots colorModuleSlots;
    public ColorSkillExecutor skillExecutor;
    
    [Header("=== Combat Panel ===")]
    public GameObject combatPanel;
    
    [Header("=== AP Display ===")]
    public TMP_Text apText;
    public Slider apSlider;
    public Image[] apPips; // Individual AP pip images
    
    [Header("=== Skill Buttons ===")]
    public Transform skillButtonParent;
    public GameObject skillButtonPrefab;
    public int maxSkillButtons = 6;
    
    [Header("=== Action Buttons ===")]
    public Button attackButton;
    public Button moveButton;
    public Button waitButton;
    public Button fleeButton;
    
    [Header("=== Status Effects ===")]
    public Transform playerStatusParent;
    public Transform enemyStatusParent;
    public GameObject statusIconPrefab;
    
    [Header("=== Passive Effects Display ===")]
    public Transform passiveIconParent;
    public GameObject passiveIconPrefab;
    
    [Header("=== Turn Indicator ===")]
    public TMP_Text turnText;
    public Image turnIndicatorBg;
    public Color playerTurnColor = new Color(0.2f, 0.5f, 0.8f);
    public Color enemyTurnColor = new Color(0.8f, 0.3f, 0.2f);
    
    [Header("=== Combat Log ===")]
    public TMP_Text combatLogText;
    public ScrollRect combatLogScroll;
    public int maxLogLines = 20;
    
    private List<GameObject> _skillButtons = new List<GameObject>();
    private List<string> _logLines = new List<string>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void Start()
    {
        if (combatManager == null)
            combatManager = FindObjectOfType<FocusedCombatManager>();
        
        if (colorModuleSlots == null)
            colorModuleSlots = FindObjectOfType<ColorModuleSlots>();
        
        if (skillExecutor == null)
            skillExecutor = FindObjectOfType<ColorSkillExecutor>();
        
        SetupActionButtons();
        
        // Hide at start
        if (combatPanel != null)
            combatPanel.SetActive(false);
    }
    
    private void SetupActionButtons()
    {
        if (attackButton != null)
            attackButton.onClick.AddListener(OnAttackClicked);
        
        if (moveButton != null)
            moveButton.onClick.AddListener(OnMoveClicked);
        
        if (waitButton != null)
            waitButton.onClick.AddListener(OnWaitClicked);
        
        if (fleeButton != null)
            fleeButton.onClick.AddListener(OnFleeClicked);
    }
    
    // ============================================
    // Show/Hide Combat UI
    // ============================================
    
    public void ShowCombatUI()
    {
        if (combatPanel != null)
            combatPanel.SetActive(true);
        
        RefreshSkillButtons();
        RefreshPassiveIcons();
        ClearLog();
        
        // Notify HUD manager
        GameHUDManager.Instance?.EnterCombatMode();
    }
    
    public void HideCombatUI()
    {
        if (combatPanel != null)
            combatPanel.SetActive(false);
        
        ClearSkillButtons();
        
        // Notify HUD manager
        GameHUDManager.Instance?.ExitCombatMode();
    }
    
    // ============================================
    // AP Display
    // ============================================
    
    public void UpdateAPDisplay(int currentAP, int maxAP)
    {
        if (apText != null)
            apText.text = $"AP: {currentAP}/{maxAP}";
        
        if (apSlider != null)
        {
            apSlider.maxValue = maxAP;
            apSlider.value = currentAP;
        }
        
        // Update pip display
        if (apPips != null)
        {
            for (int i = 0; i < apPips.Length; i++)
            {
                if (apPips[i] != null)
                {
                    apPips[i].gameObject.SetActive(i < maxAP);
                    apPips[i].color = i < currentAP ? Color.yellow : Color.gray;
                }
            }
        }
        
        // Update skill button interactability
        UpdateSkillButtonStates(currentAP);
    }
    
    // ============================================
    // Skill Buttons
    // ============================================
    
    public void RefreshSkillButtons()
    {
        ClearSkillButtons();
        
        if (colorModuleSlots == null || skillButtonParent == null || skillButtonPrefab == null)
            return;
        
        // Get equipped skills
        var equippedModules = colorModuleSlots.GetEquippedModules();
        
        foreach (var module in equippedModules)
        {
            if (module == null || module.definition == null) continue;
            if (module.definition.moduleType != ModuleType.Skill) continue;
            
            // Create button
            GameObject btnObj = Instantiate(skillButtonPrefab, skillButtonParent);
            var btnUI = btnObj.GetComponent<CombatSkillButtonUI>();
            
            if (btnUI != null)
            {
                btnUI.Setup(module, OnSkillButtonClicked);
            }
            
            _skillButtons.Add(btnObj);
            
            if (_skillButtons.Count >= maxSkillButtons)
                break;
        }
    }
    
    private void ClearSkillButtons()
    {
        foreach (var btn in _skillButtons)
        {
            if (btn != null)
                Destroy(btn);
        }
        _skillButtons.Clear();
    }
    
    private void UpdateSkillButtonStates(int currentAP)
    {
        foreach (var btnObj in _skillButtons)
        {
            if (btnObj == null) continue;
            
            var btnUI = btnObj.GetComponent<CombatSkillButtonUI>();
            if (btnUI != null)
            {
                btnUI.UpdateState(currentAP);
            }
        }
    }
    
    private void OnSkillButtonClicked(ColorModuleInstance module)
    {
        if (skillExecutor != null && module != null)
        {
            // Execute skill through combat manager
            // This will be handled by the combat system
            Debug.Log($"[CombatHUD] Skill clicked: {module.definition.moduleName}");
        }
    }
    
    // ============================================
    // Passive Icons
    // ============================================
    
    public void RefreshPassiveIcons()
    {
        // Clear existing
        if (passiveIconParent != null)
        {
            foreach (Transform child in passiveIconParent)
            {
                Destroy(child.gameObject);
            }
        }
        
        if (colorModuleSlots == null || passiveIconParent == null || passiveIconPrefab == null)
            return;
        
        // Get equipped passives
        var equippedModules = colorModuleSlots.GetEquippedModules();
        
        foreach (var module in equippedModules)
        {
            if (module == null || module.definition == null) continue;
            if (module.definition.moduleType != ModuleType.Passive) continue;
            
            // Create icon
            GameObject iconObj = Instantiate(passiveIconPrefab, passiveIconParent);
            var iconUI = iconObj.GetComponent<PassiveIconUI>();
            
            if (iconUI != null)
            {
                iconUI.Setup(module);
            }
        }
    }
    
    // ============================================
    // Turn Indicator
    // ============================================
    
    public void UpdateTurnIndicator(bool isPlayerTurn)
    {
        if (turnText != null)
            turnText.text = isPlayerTurn ? "YOUR TURN" : "ENEMY TURN";
        
        if (turnIndicatorBg != null)
            turnIndicatorBg.color = isPlayerTurn ? playerTurnColor : enemyTurnColor;
        
        // Disable/enable buttons based on turn
        SetActionButtonsInteractable(isPlayerTurn);
    }
    
    private void SetActionButtonsInteractable(bool interactable)
    {
        if (attackButton != null) attackButton.interactable = interactable;
        if (moveButton != null) moveButton.interactable = interactable;
        if (waitButton != null) waitButton.interactable = interactable;
        if (fleeButton != null) fleeButton.interactable = interactable;
        
        foreach (var btnObj in _skillButtons)
        {
            var btn = btnObj?.GetComponent<Button>();
            if (btn != null) btn.interactable = interactable;
        }
    }
    
    // ============================================
    // Status Effects Display
    // ============================================
    
    public void UpdatePlayerStatus(List<StatusEffectInstance> effects)
    {
        UpdateStatusDisplay(playerStatusParent, effects);
    }
    
    public void UpdateEnemyStatus(List<StatusEffectInstance> effects)
    {
        UpdateStatusDisplay(enemyStatusParent, effects);
    }
    
    private void UpdateStatusDisplay(Transform parent, List<StatusEffectInstance> effects)
    {
        if (parent == null) return;
        
        // Clear existing
        foreach (Transform child in parent)
        {
            Destroy(child.gameObject);
        }
        
        if (effects == null || statusIconPrefab == null) return;
        
        // Create icons for each effect
        foreach (var effect in effects)
        {
            if (effect == null) continue;
            
            GameObject iconObj = Instantiate(statusIconPrefab, parent);
            var iconUI = iconObj.GetComponent<StatusEffectIconUI>();
            
            if (iconUI != null)
            {
                iconUI.Setup(effect);
            }
        }
    }
    
    // ============================================
    // Combat Log
    // ============================================
    
    public void AddLogMessage(string message)
    {
        _logLines.Add(message);
        
        // Trim if too many lines
        while (_logLines.Count > maxLogLines)
        {
            _logLines.RemoveAt(0);
        }
        
        // Update text
        if (combatLogText != null)
        {
            combatLogText.text = string.Join("\n", _logLines);
        }
        
        // Scroll to bottom
        if (combatLogScroll != null)
        {
            Canvas.ForceUpdateCanvases();
            combatLogScroll.verticalNormalizedPosition = 0f;
        }
    }
    
    public void ClearLog()
    {
        _logLines.Clear();
        if (combatLogText != null)
            combatLogText.text = "";
    }
    
    // ============================================
    // Action Button Callbacks
    // ============================================
    
    private void OnAttackClicked()
    {
        // Handled by combat system
        Debug.Log("[CombatHUD] Attack clicked");
    }
    
    private void OnMoveClicked()
    {
        Debug.Log("[CombatHUD] Move clicked");
    }
    
    private void OnWaitClicked()
    {
        Debug.Log("[CombatHUD] Wait clicked");
    }
    
    private void OnFleeClicked()
    {
        Debug.Log("[CombatHUD] Flee clicked");
    }
}

// ============================================
// Helper class for status effect instance
// ============================================
[System.Serializable]
public class StatusEffectInstance
{
    public string effectName;
    public Sprite icon;
    public int remainingTurns;
    public int stacks;
}
