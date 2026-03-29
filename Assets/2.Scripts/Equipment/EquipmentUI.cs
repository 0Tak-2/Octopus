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
        if (equipmentManager == null) return;

        var equipment = equipmentManager.GetSlot(slotIndex);
        if (equipment == null) return;

        // 인벤토리에 해당 아이템이 있는지 확인, 없으면 추가
        EnsureItemInInventory(equipment);

        // 장비 해제
        equipmentManager.Unequip(slotIndex);
        Debug.Log($"[EquipmentUI] 슬롯 {slotIndex} 장비 해제 → 인벤토리로 복귀");
    }

    /// <summary>
    /// 장비에 대응하는 아이템이 인벤토리에 없으면 추가
    /// (Inspector에서 직접 장착한 경우 대비)
    /// </summary>
    private void EnsureItemInInventory(EquipmentDefinition equipment)
    {
        if (equipment == null) return;

        var db = ItemDatabase.Instance;
        var inv = InventoryManager.Instance;
        if (db == null || inv == null) return;

        ItemData itemData = db.GetItemByEquipment(equipment);
        if (itemData == null)
        {
            Debug.LogWarning($"[EquipmentUI] {equipment.equipmentName}에 대응하는 ItemData가 없습니다.");
            return;
        }

        // 인벤토리에 이미 있는지 확인
        if (!inv.items.ContainsKey(itemData.itemID))
        {
            // 없으면 추가
            inv.AddItemFromCrafting(itemData.itemID, itemData.itemName, 1);
            Debug.Log($"[EquipmentUI] {itemData.itemName}을(를) 인벤토리에 추가");
        }
    }

    // ============================================
    // 장비 정보 패널
    // ============================================

    private void ShowEquipmentInfo(EquipmentDefinition equipment)
    {
        if (equipment == null) return;

        equipNameText.text = !string.IsNullOrEmpty(equipment.nameKey)
    ? LocalizationManager.T(equipment.nameKey) : equipment.equipmentName;

        equipDescText.text = !string.IsNullOrEmpty(equipment.descKey)
    ? LocalizationManager.T(equipment.descKey) : equipment.description;

        if (equipStatsText != null)
        {
            var stats = new List<string>();

            if (equipment.bonusATK != 0) stats.Add($"{LocalizationManager.T("STAT_ATK")} +{equipment.bonusATK}");
            if (equipment.bonusDEF != 0) stats.Add($"{LocalizationManager.T("STAT_DEF")} +{equipment.bonusDEF}");
            if (equipment.bonusMaxHP != 0) stats.Add($"{LocalizationManager.T("STAT_HP")} +{equipment.bonusMaxHP}");
            if (equipment.bonusEVA > 0) stats.Add($"{LocalizationManager.T("STAT_EVA")} +{equipment.bonusEVA * 100:F0}%");
            if (equipment.bonusCRIT > 0) stats.Add($"{LocalizationManager.T("STAT_CRIT")} +{equipment.bonusCRIT * 100:F0}%");
            if (equipment.bonusCRIT_DMG > 0) stats.Add($"{LocalizationManager.T("STAT_CRIT_DMG")} +{equipment.bonusCRIT_DMG * 100:F0}%");
            if (equipment.stunChanceBonus > 0) stats.Add($"{LocalizationManager.T("STAT_STUN_CHANCE")} +{equipment.stunChanceBonus * 100:F0}%");

            equipStatsText.text = stats.Count > 0 ? string.Join("\n", stats) : $"({LocalizationManager.T("UI_NO_STATS")})";
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
        lines.Add($"<b>{LocalizationManager.T("UI_EQUIP_BONUS")}</b>");

        if (equipmentManager.TotalBonusATK != 0)
            lines.Add($"{LocalizationManager.T("STAT_ATK")} +{equipmentManager.TotalBonusATK}");
        if (equipmentManager.TotalBonusDEF != 0)
            lines.Add($"{LocalizationManager.T("STAT_DEF")} +{equipmentManager.TotalBonusDEF}");
        if (equipmentManager.TotalBonusMaxHP != 0)
            lines.Add($"{LocalizationManager.T("STAT_HP")} +{equipmentManager.TotalBonusMaxHP}");
        if (equipmentManager.TotalBonusEVA > 0)
            lines.Add($"{LocalizationManager.T("STAT_EVA")} +{equipmentManager.TotalBonusEVA * 100:F0}%");
        if (equipmentManager.TotalBonusCRIT > 0)
            lines.Add($"{LocalizationManager.T("STAT_CRIT")} +{equipmentManager.TotalBonusCRIT * 100:F0}%");
        if (equipmentManager.TotalBonusCRIT_DMG > 0)
            lines.Add($"{LocalizationManager.T("STAT_CRIT_DMG")} +{equipmentManager.TotalBonusCRIT_DMG * 100:F0}%");
        if (equipmentManager.TotalStunChance > 0)
            lines.Add($"{LocalizationManager.T("STAT_STUN_CHANCE")} +{equipmentManager.TotalStunChance * 100:F0}%");

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

        var equipment = equipmentManager.GetSlot(_selectedSlot);
        if (equipment != null)
            EnsureItemInInventory(equipment);

        equipmentManager.Unequip(_selectedSlot);
        RefreshAll();
    }
}