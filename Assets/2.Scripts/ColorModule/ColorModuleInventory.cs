using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어가 보유한 색 모듈 인벤토리
/// </summary>
public class ColorModuleInventory : MonoBehaviour
{
    public static ColorModuleInventory Instance { get; private set; }
    
    [Header("보유 모듈 (Definition 참조)")]
    [SerializeField] private List<ColorModuleDefinition> ownedModuleDefinitions = new List<ColorModuleDefinition>();
    
    /// <summary>
    /// 런타임 인스턴스 목록
    /// </summary>
    private List<ColorModuleInstance> _ownedModules = new List<ColorModuleInstance>();
    
    public IReadOnlyList<ColorModuleInstance> OwnedModules => _ownedModules;
    
    /// <summary>
    /// 모듈 변경 이벤트
    /// </summary>
    public event System.Action OnInventoryChanged;
    
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
        _ownedModules.Clear();
        foreach (var def in ownedModuleDefinitions)
        {
            if (def != null)
                _ownedModules.Add(new ColorModuleInstance(def));
        }
    }
    
    /// <summary>
    /// 모듈 획득
    /// </summary>
    public void AddModule(ColorModuleDefinition definition)
    {
        if (definition == null) return;
        
        // 이미 보유 중인지 체크 (같은 Definition)
        if (ownedModuleDefinitions.Contains(definition))
        {
            Debug.Log($"[ColorModuleInventory] 이미 보유 중: {definition.moduleName}");
            return;
        }
        
        ownedModuleDefinitions.Add(definition);
        _ownedModules.Add(new ColorModuleInstance(definition));
        
        Debug.Log($"[ColorModuleInventory] 모듈 획득: {definition.moduleName}");
        OnInventoryChanged?.Invoke();
    }
    
    /// <summary>
    /// 모듈 제거
    /// </summary>
    public bool RemoveModule(ColorModuleDefinition definition)
    {
        if (definition == null) return false;
        
        int index = ownedModuleDefinitions.IndexOf(definition);
        if (index < 0) return false;
        
        ownedModuleDefinitions.RemoveAt(index);
        _ownedModules.RemoveAt(index);
        
        Debug.Log($"[ColorModuleInventory] 모듈 제거: {definition.moduleName}");
        OnInventoryChanged?.Invoke();
        return true;
    }
    
    /// <summary>
    /// 특정 인스턴스 제거
    /// </summary>
    public bool RemoveModule(ColorModuleInstance instance)
    {
        if (instance?.definition == null) return false;
        return RemoveModule(instance.definition);
    }
    
    /// <summary>
    /// 모듈 보유 여부
    /// </summary>
    public bool HasModule(ColorModuleDefinition definition)
    {
        return ownedModuleDefinitions.Contains(definition);
    }
    
    /// <summary>
    /// 색상별 보유 모듈 필터
    /// </summary>
    public List<ColorModuleInstance> GetModulesByColor(ColorType color)
    {
        var result = new List<ColorModuleInstance>();
        foreach (var m in _ownedModules)
        {
            if (m.Color == color)
                result.Add(m);
        }
        return result;
    }
    
    /// <summary>
    /// 타입별 보유 모듈 필터
    /// </summary>
    public List<ColorModuleInstance> GetModulesByType(ModuleType type)
    {
        var result = new List<ColorModuleInstance>();
        foreach (var m in _ownedModules)
        {
            if (m.Type == type)
                result.Add(m);
        }
        return result;
    }
}
