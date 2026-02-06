using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages the main game HUD layout and visibility
/// Controls all UI panels and their toggle states
/// </summary>
public class GameHUDManager : MonoBehaviour
{
    public static GameHUDManager Instance { get; private set; }
    
    [Header("=== TOP LEFT: Equipment & Modules ===")]
    public GameObject equipmentPanel;
    public EquipmentUI equipmentUI;
    public Button equipmentToggleButton;
    
    public GameObject colorModulePanel;
    public ColorModuleUI colorModuleUI;
    public Button colorModuleToggleButton;
    
    [Header("=== TOP RIGHT: Inventory ===")]
    public GameObject inventoryPanel;
    public InventoryUI inventoryUI;
    public Button inventoryToggleButton;
    
    [Header("=== BOTTOM LEFT: Crafting ===")]
    public GameObject craftingPanel;
    public CraftingUI craftingUI;
    public Button craftingToggleButton;
    
    [Header("=== TOP CENTER: Status Bars ===")]
    public GameObject statusBarPanel;
    public PlayerStatusBarUI statusBarUI;
    
    [Header("=== BOTTOM CENTER: Field Skills ===")]
    public GameObject fieldSkillPanel;
    public FieldSkillBarUI fieldSkillBarUI;
    
    [Header("=== Hotkeys ===")]
    public KeyCode inventoryKey = KeyCode.I;
    public KeyCode equipmentKey = KeyCode.E;
    public KeyCode colorModuleKey = KeyCode.M;
    public KeyCode craftingKey = KeyCode.C;
    public KeyCode closeAllKey = KeyCode.Escape;
    
    [Header("=== Settings ===")]
    [Tooltip("Close other panels when opening a new one")]
    public bool exclusivePanels = false;
    
    [Tooltip("Pause game when UI is open")]
    public bool pauseWhenUIOpen = false;
    
    private bool _anyPanelOpen = false;
    
    public bool IsAnyPanelOpen => _anyPanelOpen;
    
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
        // Setup button listeners
        SetupButtons();
        
        // Close all panels at start
        CloseAllPanels();
        
        // Status bar always visible
        if (statusBarPanel != null)
            statusBarPanel.SetActive(true);
        
        // Field skills always visible
        if (fieldSkillPanel != null)
            fieldSkillPanel.SetActive(true);
    }
    
    private void Update()
    {
        HandleHotkeys();
        UpdatePanelState();
    }
    
    private void SetupButtons()
    {
        if (inventoryToggleButton != null)
            inventoryToggleButton.onClick.AddListener(ToggleInventory);
        
        if (equipmentToggleButton != null)
            equipmentToggleButton.onClick.AddListener(ToggleEquipment);
        
        if (colorModuleToggleButton != null)
            colorModuleToggleButton.onClick.AddListener(ToggleColorModule);
        
        if (craftingToggleButton != null)
            craftingToggleButton.onClick.AddListener(ToggleCrafting);
    }
    
    private void HandleHotkeys()
    {
        if (Input.GetKeyDown(inventoryKey))
            ToggleInventory();
        
        if (Input.GetKeyDown(equipmentKey))
            ToggleEquipment();
        
        if (Input.GetKeyDown(colorModuleKey))
            ToggleColorModule();
        
        if (Input.GetKeyDown(craftingKey))
            ToggleCrafting();
        
        if (Input.GetKeyDown(closeAllKey))
            CloseAllPanels();
    }
    
    private void UpdatePanelState()
    {
        _anyPanelOpen = 
            (inventoryPanel != null && inventoryPanel.activeSelf) ||
            (equipmentPanel != null && equipmentPanel.activeSelf) ||
            (colorModulePanel != null && colorModulePanel.activeSelf) ||
            (craftingPanel != null && craftingPanel.activeSelf);
        
        // Handle pause if enabled
        if (pauseWhenUIOpen)
        {
            Time.timeScale = _anyPanelOpen ? 0f : 1f;
        }
    }
    
    // ============================================
    // Panel Toggles
    // ============================================
    
    public void ToggleInventory()
    {
        if (inventoryPanel == null) return;
        
        bool willOpen = !inventoryPanel.activeSelf;
        
        if (willOpen && exclusivePanels)
            CloseAllPanels();
        
        inventoryPanel.SetActive(willOpen);
    }
    
    public void ToggleEquipment()
    {
        if (equipmentPanel == null) return;
        
        bool willOpen = !equipmentPanel.activeSelf;
        
        if (willOpen && exclusivePanels)
            CloseAllPanels();
        
        equipmentPanel.SetActive(willOpen);
    }
    
    public void ToggleColorModule()
    {
        if (colorModulePanel == null) return;
        
        bool willOpen = !colorModulePanel.activeSelf;
        
        if (willOpen && exclusivePanels)
            CloseAllPanels();
        
        colorModulePanel.SetActive(willOpen);
    }
    
    public void ToggleCrafting()
    {
        if (craftingPanel == null) return;
        
        bool willOpen = !craftingPanel.activeSelf;
        
        if (willOpen && exclusivePanels)
            CloseAllPanels();
        
        craftingPanel.SetActive(willOpen);
    }
    
    public void CloseAllPanels()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (equipmentPanel != null) equipmentPanel.SetActive(false);
        if (colorModulePanel != null) colorModulePanel.SetActive(false);
        if (craftingPanel != null) craftingPanel.SetActive(false);
        
        _anyPanelOpen = false;
        
        if (pauseWhenUIOpen)
            Time.timeScale = 1f;
    }
    
    // ============================================
    // Show/Hide specific panels
    // ============================================
    
    public void ShowInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(true);
    }
    
    public void HideInventory()
    {
        if (inventoryPanel != null)
            inventoryPanel.SetActive(false);
    }
    
    public void ShowEquipment()
    {
        if (equipmentPanel != null)
            equipmentPanel.SetActive(true);
    }
    
    public void HideEquipment()
    {
        if (equipmentPanel != null)
            equipmentPanel.SetActive(false);
    }
    
    // ============================================
    // Combat Mode
    // ============================================
    
    /// <summary>
    /// Called when entering focused combat - hide field UI
    /// </summary>
    public void EnterCombatMode()
    {
        CloseAllPanels();
        
        if (fieldSkillPanel != null)
            fieldSkillPanel.SetActive(false);
        
        // Status bar stays visible
    }
    
    /// <summary>
    /// Called when exiting focused combat - restore field UI
    /// </summary>
    public void ExitCombatMode()
    {
        if (fieldSkillPanel != null)
            fieldSkillPanel.SetActive(true);
    }
    
    // ============================================
    // Utility
    // ============================================
    
    /// <summary>
    /// Check if player input should be blocked (UI is open)
    /// </summary>
    public bool ShouldBlockPlayerInput()
    {
        return _anyPanelOpen;
    }
}
