using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 장비 관리 UI (8칸 슬롯)
/// </summary>
public class EquipmentUI : MonoBehaviour
{
    public static EquipmentUI Instance { get; private set; }
    
    [Header("References")]
    public EquipmentManager equipmentManager;
    public InventoryManager inventoryManager;
    
    [Header("UI Root")]
    public GameObject uiRoot;
    public KeyCode toggleKey = KeyCode.E;
    
    [Header("Slot UI (8칸)")]
    public EquipmentSlotUI[] slotUIs = new EquipmentSlotUI[8];
    
    [Header("Equipment Info Panel")]
    public TMP_Text equipNameText;
    public TMP_Text equipDescText;
    public TMP_Text equipStatsText;
    public Image equipIconImage;
    
    [Header("Total Stats Display")]
    public TMP_Text totalStatsText;
    
    private EquipmentDefinition _selectedEquipment;
    private int _selectedSlot = -1;
    
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
        if (equipmentManager == null)
            equipmentManager = EquipmentManager.Instance;
        
        if (equipmentManager != null)
            equipmentManager.OnEquipmentChanged += RefreshAll;
        
        if (uiRoot != null)
            uiRoot.SetActive(false);
        
        RefreshAll();
    }
    
    private void OnDestroy()
    {
        if (equipmentManager != null)
            equipmentManager.OnEquipmentChanged -= RefreshAll;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            ToggleUI();
    }
    
    public void ToggleUI()
    {
        if (uiRoot == null) return;
        
        bool isActive = uiRoot.activeSelf;
        uiRoot.SetActive(!isActive);
        
        if (!isActive)
            RefreshAll();
    }
    
    public void RefreshAll()
    {
        RefreshSlots();
        RefreshTotalStats();
        ClearSelection();
    }
    
    // ============================================
    // 슬롯 UI
    // ============================================
    
    private void RefreshSlots()
    {
        if (equipmentManager == null) return;
        
        for (int i = 0; i < slotUIs.Length && i < EquipmentManager.MAX_SLOTS; i++)
        {
            if (slotUIs[i] == null) continue;
            
            var equipment = equipmentManager.GetSlot(i);
            slotUIs[i].SetEquipment(equipment);
            slotUIs[i].SetSlotIndex(i);
        }
    }
    
    public void OnSlotClicked(int slotIndex)
    {
        _selectedSlot = slotIndex;
        
        var equipment = equipmentManager?.GetSlot(slotIndex);
        if (equipment != null)
        {
            _selectedEquipment = equipment;
            ShowEquipmentInfo(equipment);
        }
        else
        {
            _selectedEquipment = null;
            ClearEquipmentInfo();
        }
    }
    
    public void OnSlotRightClicked(int slotIndex)
    {
        // 우클릭 = 해제
        equipmentManager?.Unequip(slotIndex);
    }
    
    // ============================================
    // 장비 정보 패널
    // ============================================
    
    private void ShowEquipmentInfo(EquipmentDefinition equipment)
    {
        if (equipment == null) return;
        
        if (equipNameText != null)
            equipNameText.text = equipment.equipmentName;
        
        if (equipDescText != null)
            equipDescText.text = equipment.description;
        
        if (equipStatsText != null)
        {
            var stats = new List<string>();
            
            if (equipment.bonusATK != 0) stats.Add($"공격력 +{equipment.bonusATK}");
            if (equipment.bonusDEF != 0) stats.Add($"방어력 +{equipment.bonusDEF}");
            if (equipment.bonusMaxHP != 0) stats.Add($"최대HP +{equipment.bonusMaxHP}");
            if (equipment.bonusEVA > 0) stats.Add($"회피율 +{equipment.bonusEVA * 100:F0}%");
            if (equipment.bonusCRIT > 0) stats.Add($"치명타 +{equipment.bonusCRIT * 100:F0}%");
            if (equipment.bonusCRIT_DMG > 0) stats.Add($"치명타 피해 +{equipment.bonusCRIT_DMG * 100:F0}%");
            if (equipment.stunChanceBonus > 0) stats.Add($"스턴 확률 +{equipment.stunChanceBonus * 100:F0}%");
            
            equipStatsText.text = stats.Count > 0 ? string.Join("\n", stats) : "(스탯 없음)";
        }
        
        if (equipIconImage != null)
        {
            if (equipment.icon != null)
            {
                equipIconImage.sprite = equipment.icon;
                equipIconImage.enabled = true;
            }
            else
            {
                equipIconImage.enabled = false;
            }
        }
    }
    
    private void ClearEquipmentInfo()
    {
        if (equipNameText != null) equipNameText.text = "";
        if (equipDescText != null) equipDescText.text = "";
        if (equipStatsText != null) equipStatsText.text = "";
        if (equipIconImage != null) equipIconImage.enabled = false;
    }
    
    private void ClearSelection()
    {
        _selectedEquipment = null;
        _selectedSlot = -1;
        ClearEquipmentInfo();
    }
    
    // ============================================
    // 총 스탯 표시
    // ============================================
    
    private void RefreshTotalStats()
    {
        if (totalStatsText == null || equipmentManager == null) return;
        
        var lines = new List<string>();
        lines.Add("<b>장비 보너스</b>");
        
        if (equipmentManager.TotalBonusATK != 0)
            lines.Add($"공격력 +{equipmentManager.TotalBonusATK}");
        if (equipmentManager.TotalBonusDEF != 0)
            lines.Add($"방어력 +{equipmentManager.TotalBonusDEF}");
        if (equipmentManager.TotalBonusMaxHP != 0)
            lines.Add($"최대HP +{equipmentManager.TotalBonusMaxHP}");
        if (equipmentManager.TotalBonusEVA > 0)
            lines.Add($"회피율 +{equipmentManager.TotalBonusEVA * 100:F0}%");
        if (equipmentManager.TotalBonusCRIT > 0)
            lines.Add($"치명타 +{equipmentManager.TotalBonusCRIT * 100:F0}%");
        if (equipmentManager.TotalBonusCRIT_DMG > 0)
            lines.Add($"치명타 피해 +{equipmentManager.TotalBonusCRIT_DMG * 100:F0}%");
        if (equipmentManager.TotalStunChance > 0)
            lines.Add($"스턴 확률 +{equipmentManager.TotalStunChance * 100:F0}%");
        
        totalStatsText.text = string.Join("\n", lines);
    }
    
    // ============================================
    // 장착/해제 버튼
    // ============================================
    
    /// <summary>
    /// 인벤토리에서 장비를 선택해서 장착
    /// </summary>
    public void EquipFromInventory(EquipmentDefinition equipment)
    {
        if (equipment == null || equipmentManager == null) return;
        
        equipmentManager.AutoEquip(equipment);
        RefreshAll();
    }
    
    /// <summary>
    /// 선택한 슬롯 해제
    /// </summary>
    public void UnequipSelectedSlot()
    {
        if (_selectedSlot < 0 || equipmentManager == null) return;
        
        equipmentManager.Unequip(_selectedSlot);
        RefreshAll();
    }
}
