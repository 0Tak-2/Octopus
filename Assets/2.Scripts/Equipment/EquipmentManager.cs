using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장비 관리자 - 8칸 슬롯 (문어 다리 컨셉, 자유 조합)
/// </summary>
public class EquipmentManager : MonoBehaviour
{
    public static EquipmentManager Instance { get; private set; }
    
    public const int MAX_SLOTS = 8;
    
    [Header("장착된 장비")]
    [SerializeField] private EquipmentDefinition[] equippedItems = new EquipmentDefinition[MAX_SLOTS];
    
    [Header("References")]
    public PlayerStats playerStats;
    
    /// <summary>
    /// 슬롯 변경 이벤트
    /// </summary>
    public event System.Action OnEquipmentChanged;
    
    // 캐시된 총 보너스
    private int _totalBonusATK;
    private int _totalBonusDEF;
    private int _totalBonusMaxHP;
    private float _totalBonusEVA;
    private float _totalBonusCRIT;
    private float _totalBonusCRIT_DMG;
    private float _totalStunChance;
    
    // 읽기 전용 프로퍼티
    public int TotalBonusATK => _totalBonusATK;
    public int TotalBonusDEF => _totalBonusDEF;
    public int TotalBonusMaxHP => _totalBonusMaxHP;
    public float TotalBonusEVA => _totalBonusEVA;
    public float TotalBonusCRIT => _totalBonusCRIT;
    public float TotalBonusCRIT_DMG => _totalBonusCRIT_DMG;
    public float TotalStunChance => _totalStunChance;
    
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
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
        
        RecalculateStats();
    }
    
    /// <summary>
    /// 슬롯을 직접 설정 (인벤 드래그용). null이면 빈 슬롯.
    /// </summary>
    public void SetSlotWithoutUnequip(int slotIndex, EquipmentDefinition equipment)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return;
        equippedItems[slotIndex] = equipment;
        RecalculateStats();
        OnEquipmentChanged?.Invoke();
    }

    public void SwapEquipmentSlots(int a, int b)
    {
        if (a < 0 || a >= MAX_SLOTS || b < 0 || b >= MAX_SLOTS) return;
        if (a == b) return;
        var t = equippedItems[a];
        equippedItems[a] = equippedItems[b];
        equippedItems[b] = t;
        RecalculateStats();
        OnEquipmentChanged?.Invoke();
    }

    /// <summary>
    /// 슬롯에 장비 장착
    /// </summary>
    public bool Equip(int slotIndex, EquipmentDefinition equipment)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;
        
        // 이미 장착 중이면 먼저 해제
        if (equippedItems[slotIndex] != null)
        {
            Unequip(slotIndex);
        }
        
        equippedItems[slotIndex] = equipment;
        
        RecalculateStats();
        OnEquipmentChanged?.Invoke();
        
        Debug.Log($"[EquipmentManager] 슬롯 {slotIndex}에 {equipment?.equipmentName ?? "없음"} 장착");
        return true;
    }
    
    /// <summary>
    /// 슬롯에서 장비 해제
    /// </summary>
    public bool Unequip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;
        if (equippedItems[slotIndex] == null) return false;
        
        string name = equippedItems[slotIndex].equipmentName;
        equippedItems[slotIndex] = null;
        
        RecalculateStats();
        OnEquipmentChanged?.Invoke();
        
        Debug.Log($"[EquipmentManager] 슬롯 {slotIndex}에서 {name} 해제");
        return true;
    }
    
    /// <summary>
    /// 슬롯 내용 가져오기
    /// </summary>
    public EquipmentDefinition GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return null;
        return equippedItems[slotIndex];
    }
    
    /// <summary>
    /// 모든 장착 장비 가져오기
    /// </summary>
    public List<EquipmentDefinition> GetAllEquipped()
    {
        var list = new List<EquipmentDefinition>();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedItems[i] != null)
                list.Add(equippedItems[i]);
        }
        return list;
    }
    
    /// <summary>
    /// 특정 장비가 장착되어 있는지
    /// </summary>
    public bool IsEquipped(EquipmentDefinition equipment)
    {
        if (equipment == null) return false;
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedItems[i] == equipment)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 특정 장비가 장착된 슬롯 인덱스 (-1 = 없음)
    /// </summary>
    public int GetEquippedSlot(EquipmentDefinition equipment)
    {
        if (equipment == null) return -1;
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedItems[i] == equipment)
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// 타입별 장착 개수
    /// </summary>
    public int GetTypeCount(EquipmentType type)
    {
        int count = 0;
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedItems[i] != null && equippedItems[i].equipmentType == type)
                count++;
        }
        return count;
    }
    
    /// <summary>
    /// 빈 슬롯 인덱스 찾기 (-1 = 없음)
    /// </summary>
    public int FindEmptySlot()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedItems[i] == null)
                return i;
        }
        return -1;
    }
    
    /// <summary>
    /// 첫 번째 빈 슬롯에 자동 장착
    /// </summary>
    public bool AutoEquip(EquipmentDefinition equipment)
    {
        int slot = FindEmptySlot();
        if (slot < 0)
        {
            Debug.Log("[EquipmentManager] 빈 슬롯이 없습니다!");
            return false;
        }
        return Equip(slot, equipment);
    }
    
    // ============================================
    // 스탯 계산
    // ============================================
    
    /// <summary>
    /// 모든 장비의 스탯 보너스 재계산
    /// </summary>
    public void RecalculateStats()
    {
        _totalBonusATK = 0;
        _totalBonusDEF = 0;
        _totalBonusMaxHP = 0;
        _totalBonusEVA = 0f;
        _totalBonusCRIT = 0f;
        _totalBonusCRIT_DMG = 0f;
        _totalStunChance = 0f;
        
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            var eq = equippedItems[i];
            if (eq == null) continue;
            
            _totalBonusATK += eq.bonusATK;
            _totalBonusDEF += eq.bonusDEF;
            _totalBonusMaxHP += eq.bonusMaxHP;
            _totalBonusEVA += eq.bonusEVA;
            _totalBonusCRIT += eq.bonusCRIT;
            _totalBonusCRIT_DMG += eq.bonusCRIT_DMG;
            _totalStunChance += eq.stunChanceBonus;
        }
        
        // PlayerStats에 적용
        ApplyToPlayerStats();
        
        Debug.Log($"[EquipmentManager] 스탯 재계산 - ATK+{_totalBonusATK}, DEF+{_totalBonusDEF}, MaxHP+{_totalBonusMaxHP}");
    }
    
    private void ApplyToPlayerStats()
    {
        if (playerStats == null) return;
        
        playerStats.bonusATK = _totalBonusATK;
        playerStats.bonusDEF = _totalBonusDEF;
        playerStats.bonusEVA = _totalBonusEVA;
        playerStats.bonusCRIT = _totalBonusCRIT;
        playerStats.bonusCRIT_DMG = _totalBonusCRIT_DMG;
        
        // MaxHP 보너스 적용
        playerStats.equipmentMaxHPBonus = _totalBonusMaxHP;
    }
}
