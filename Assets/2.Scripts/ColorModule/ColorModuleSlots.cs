using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 색 모듈 장착 슬롯 (5칸)
/// </summary>
public class ColorModuleSlots : MonoBehaviour
{
    public static ColorModuleSlots Instance { get; private set; }
    
    public const int MAX_SLOTS = 5;
    
    [Header("장착된 모듈 (Definition 참조)")]
    [SerializeField] private ColorModuleDefinition[] equippedDefinitions = new ColorModuleDefinition[MAX_SLOTS];
    
    /// <summary>
    /// 런타임 인스턴스 (null = 빈 슬롯)
    /// </summary>
    private ColorModuleInstance[] _equippedModules = new ColorModuleInstance[MAX_SLOTS];
    
    /// <summary>
    /// 슬롯 변경 이벤트
    /// </summary>
    public event System.Action OnSlotsChanged;
    
    /// <summary>
    /// 안전 지역 체크용 (필드에서만 장착/해제 가능)
    /// </summary>
    [Header("안전 지역 체크")]
    public bool requireSafeZone = true;
    private System.Func<bool> _isSafeZoneCheck;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // Definition -> Instance 변환
        RefreshInstances();
    }
    
    private void RefreshInstances()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (equippedDefinitions[i] != null)
                _equippedModules[i] = new ColorModuleInstance(equippedDefinitions[i]);
            else
                _equippedModules[i] = null;
        }
    }
    
    /// <summary>
    /// 안전 지역 체크 함수 등록
    /// </summary>
    public void SetSafeZoneCheck(System.Func<bool> check)
    {
        _isSafeZoneCheck = check;
    }
    
    private bool IsSafeZone()
    {
        if (!requireSafeZone) return true;
        if (_isSafeZoneCheck != null) return _isSafeZoneCheck();
        
        // 기본: 집중전투 중이 아니면 안전
        var combat = FocusedCombatManager.Instance;
        return combat == null || !combat.IsInFocusedCombat;
    }
    
    /// <summary>
    /// 슬롯에 모듈 장착
    /// </summary>
    public bool Equip(int slotIndex, ColorModuleInstance module)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;
        if (!IsSafeZone())
        {
            Debug.Log("[ColorModuleSlots] 안전 지역에서만 장착 가능!");
            return false;
        }
        
        // 이미 다른 슬롯에 장착되어 있으면 해제
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] == module)
            {
                _equippedModules[i] = null;
                equippedDefinitions[i] = null;
            }
        }
        
        _equippedModules[slotIndex] = module;
        equippedDefinitions[slotIndex] = module?.definition;
        
        OnSlotsChanged?.Invoke();
        RecalculateColorBonuses();
        
        Debug.Log($"[ColorModuleSlots] 슬롯 {slotIndex}에 {module?.Name ?? "없음"} 장착");
        return true;
    }
    
    /// <summary>
    /// 슬롯에서 모듈 해제
    /// </summary>
    public bool Unequip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return false;
        if (!IsSafeZone())
        {
            Debug.Log("[ColorModuleSlots] 안전 지역에서만 해제 가능!");
            return false;
        }
        
        _equippedModules[slotIndex] = null;
        equippedDefinitions[slotIndex] = null;
        
        OnSlotsChanged?.Invoke();
        RecalculateColorBonuses();
        
        return true;
    }
    
    /// <summary>
    /// 슬롯 내용 가져오기
    /// </summary>
    public ColorModuleInstance GetSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= MAX_SLOTS) return null;
        return _equippedModules[slotIndex];
    }
    
    /// <summary>
    /// 모든 장착 모듈 가져오기
    /// </summary>
    public List<ColorModuleInstance> GetAllEquipped()
    {
        var list = new List<ColorModuleInstance>();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] != null)
                list.Add(_equippedModules[i]);
        }
        return list;
    }
    
    /// <summary>
    /// 장착된 스킬만 가져오기
    /// </summary>
    public List<ColorModuleInstance> GetEquippedSkills()
    {
        var list = new List<ColorModuleInstance>();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] != null && _equippedModules[i].Type == ModuleType.Skill)
                list.Add(_equippedModules[i]);
        }
        return list;
    }
    
    /// <summary>
    /// 장착된 패시브만 가져오기
    /// </summary>
    public List<ColorModuleInstance> GetEquippedPassives()
    {
        var list = new List<ColorModuleInstance>();
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] != null && _equippedModules[i].Type == ModuleType.Passive)
                list.Add(_equippedModules[i]);
        }
        return list;
    }
    
    /// <summary>
    /// 색상별 장착 개수
    /// </summary>
    public int GetColorCount(ColorType color)
    {
        int count = 0;
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] != null && _equippedModules[i].Color == color)
                count++;
        }
        return count;
    }
    
    /// <summary>
    /// 특정 모듈이 장착되어 있는지
    /// </summary>
    public bool IsEquipped(ColorModuleDefinition definition)
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i]?.definition == definition)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 특정 패시브가 장착되어 있는지
    /// </summary>
    public bool HasPassive(PassiveID passiveID)
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            if (_equippedModules[i] != null && _equippedModules[i].Passive == passiveID)
                return true;
        }
        return false;
    }
    
    /// <summary>
    /// 전투 시작 시 모든 모듈 초기화
    /// </summary>
    public void ResetAllForCombat()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            _equippedModules[i]?.ResetForCombat();
        }
    }
    
    /// <summary>
    /// 턴 종료 시 모든 모듈 처리
    /// </summary>
    public void OnTurnEnd()
    {
        for (int i = 0; i < MAX_SLOTS; i++)
        {
            _equippedModules[i]?.OnTurnEnd();
        }
    }
    
    // ============================================
    // 중첩 보너스 계산
    // ============================================
    
    /// <summary>
    /// 색상별 중첩 보너스 재계산 → PlayerStats에 적용
    /// </summary>
    public void RecalculateColorBonuses()
    {
        var playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats == null) return;
        
        int redCount = GetColorCount(ColorType.Red);
        int blueCount = GetColorCount(ColorType.Blue);
        int blackCount = GetColorCount(ColorType.Black);
        
        // === 빨강 중첩 ===
        // 버프: 공격력 +0/+5/+10/+15/+20%
        // 디버프: 회피율 -0/-3/-6/-9/-12%
        float redATKBonus = redCount switch
        {
            0 => 0f,
            1 => 0.05f,
            2 => 0.10f,
            3 => 0.15f,
            _ => 0.20f
        };
        float redEVAPenalty = redCount switch
        {
            0 => 0f,
            1 => 0.03f,
            2 => 0.06f,
            3 => 0.09f,
            _ => 0.12f
        };
        
        // === 파랑 중첩 ===
        // 버프: 회피율 +0/+4/+8/+12/+16%
        // 디버프: 타겟 변경 시 피해 -0/-5/-10/-15/-20% (별도 처리 필요)
        float blueEVABonus = blueCount switch
        {
            0 => 0f,
            1 => 0.04f,
            2 => 0.08f,
            3 => 0.12f,
            _ => 0.16f
        };
        
        // === 검정 중첩 ===
        // 버프: 치명타 피해량 +0/+10/+20/+30/+40%
        // 디버프: 회복 효율 -0/-10/-20/-30/-40%
        float blackCritDmgBonus = blackCount switch
        {
            0 => 0f,
            1 => 0.10f,
            2 => 0.20f,
            3 => 0.30f,
            _ => 0.40f
        };
        float blackHealPenalty = blackCount switch
        {
            0 => 0f,
            1 => 0.10f,
            2 => 0.20f,
            3 => 0.30f,
            _ => 0.40f
        };
        
        // PlayerStats에 적용
        playerStats.colorModuleATKPercent = redATKBonus;
        playerStats.colorModuleEVAPercent = blueEVABonus;
        playerStats.colorModuleEVAPenalty = redEVAPenalty;
        playerStats.colorModuleCRIT_DMGPercent = blackCritDmgBonus;
        playerStats.colorModuleHealPenalty = blackHealPenalty;
        
        Debug.Log($"[ColorModuleSlots] 중첩 보너스 - 빨강:{redCount} 파랑:{blueCount} 검정:{blackCount}");
    }
}
