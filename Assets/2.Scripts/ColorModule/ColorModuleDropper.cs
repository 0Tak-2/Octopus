using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 적 처치 시 색 모듈 드랍 처리
/// </summary>
public class ColorModuleDropper : MonoBehaviour
{
    public static ColorModuleDropper Instance { get; private set; }
    
    [Header("Drop Tables")]
    [Tooltip("빨강 모듈 풀")]
    public List<ColorModuleDefinition> redModulePool = new List<ColorModuleDefinition>();
    
    [Tooltip("파랑 모듈 풀")]
    public List<ColorModuleDefinition> blueModulePool = new List<ColorModuleDefinition>();
    
    [Tooltip("검정 모듈 풀")]
    public List<ColorModuleDefinition> blackModulePool = new List<ColorModuleDefinition>();
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    /// <summary>
    /// 적 처치 시 모듈 드랍 시도
    /// </summary>
    /// <param name="redChance">빨강 드랍 확률 (0~1)</param>
    /// <param name="blueChance">파랑 드랍 확률 (0~1)</param>
    /// <param name="blackChance">검정 드랍 확률 (0~1)</param>
    public void TryDropModule(float redChance, float blueChance, float blackChance)
    {
        var inventory = ColorModuleInventory.Instance;
        if (inventory == null) return;
        
        ColorModuleDefinition dropped = null;
        
        // 빨강 드랍 체크
        if (redChance > 0 && Random.value < redChance && redModulePool.Count > 0)
        {
            dropped = GetRandomFromPool(redModulePool, inventory);
        }
        // 파랑 드랍 체크
        else if (blueChance > 0 && Random.value < blueChance && blueModulePool.Count > 0)
        {
            dropped = GetRandomFromPool(blueModulePool, inventory);
        }
        // 검정 드랍 체크
        else if (blackChance > 0 && Random.value < blackChance && blackModulePool.Count > 0)
        {
            dropped = GetRandomFromPool(blackModulePool, inventory);
        }
        
        if (dropped != null)
        {
            inventory.AddModule(dropped);
            Debug.Log($"<color=yellow>[ColorModuleDropper] 모듈 획득: {dropped.moduleName}</color>");
            
            // TODO: 드랍 이펙트, UI 알림
        }
    }
    
    private ColorModuleDefinition GetRandomFromPool(List<ColorModuleDefinition> pool, ColorModuleInventory inventory)
    {
        // 이미 보유 중이지 않은 모듈 중에서 선택
        var available = new List<ColorModuleDefinition>();
        foreach (var m in pool)
        {
            if (m != null && !inventory.HasModule(m))
                available.Add(m);
        }
        
        if (available.Count == 0)
        {
            // 모두 보유 중이면 아무거나
            if (pool.Count > 0)
                return pool[Random.Range(0, pool.Count)];
            return null;
        }
        
        return available[Random.Range(0, available.Count)];
    }
}
