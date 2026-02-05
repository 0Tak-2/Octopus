using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages permanent relic collection across runs
/// Persists using PlayerPrefs
/// </summary>
public class RelicManager : MonoBehaviour
{
    public static RelicManager Instance { get; private set; }
    
    [Header("All Relics (assign in inspector)")]
    [Tooltip("All available relics in the game")]
    public List<RelicDefinition> allRelics = new List<RelicDefinition>();
    
    [Header("Equipped Relics")]
    [Tooltip("Max relics that can be equipped per run")]
    public int maxEquippedRelics = 5;
    
    [Header("Debug")]
    public bool showDebugLogs = true;
    
    // Owned relics (relicID -> stack count)
    private Dictionary<string, int> _ownedRelics = new Dictionary<string, int>();
    
    // Currently equipped for this run
    private List<RelicDefinition> _equippedRelics = new List<RelicDefinition>();
    
    // Events
    public event System.Action OnRelicsChanged;
    public event System.Action OnEquippedChanged;
    
    private const string SAVE_KEY_PREFIX = "Relic_";
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        LoadRelics();
    }
    
    // ============================================
    // Ownership
    // ============================================
    
    /// <summary>
    /// Add a relic to permanent collection
    /// </summary>
    public void AddRelic(RelicDefinition relic)
    {
        if (relic == null) return;
        
        if (_ownedRelics.ContainsKey(relic.relicID))
        {
            _ownedRelics[relic.relicID]++;
        }
        else
        {
            _ownedRelics[relic.relicID] = 1;
        }
        
        SaveRelics();
        OnRelicsChanged?.Invoke();
        
        if (showDebugLogs)
            Debug.Log($"[RelicManager] Added relic: {relic.relicName} (now have {_ownedRelics[relic.relicID]})");
    }
    
    /// <summary>
    /// Add relic by ID
    /// </summary>
    public void AddRelic(string relicID)
    {
        var relic = GetRelicByID(relicID);
        if (relic != null)
            AddRelic(relic);
    }
    
    /// <summary>
    /// Get owned stack count for a relic
    /// </summary>
    public int GetOwnedCount(string relicID)
    {
        return _ownedRelics.TryGetValue(relicID, out int count) ? count : 0;
    }
    
    /// <summary>
    /// Get all owned relics with their counts
    /// </summary>
    public Dictionary<RelicDefinition, int> GetAllOwnedRelics()
    {
        var result = new Dictionary<RelicDefinition, int>();
        
        foreach (var kvp in _ownedRelics)
        {
            var relic = GetRelicByID(kvp.Key);
            if (relic != null && kvp.Value > 0)
            {
                result[relic] = kvp.Value;
            }
        }
        
        return result;
    }
    
    /// <summary>
    /// Check if player owns at least one of this relic
    /// </summary>
    public bool OwnsRelic(string relicID)
    {
        return GetOwnedCount(relicID) > 0;
    }
    
    // ============================================
    // Equipping (per run)
    // ============================================
    
    /// <summary>
    /// Equip a relic for the current run
    /// </summary>
    public bool EquipRelic(RelicDefinition relic)
    {
        if (relic == null) return false;
        if (!OwnsRelic(relic.relicID)) return false;
        if (_equippedRelics.Count >= maxEquippedRelics) return false;
        if (_equippedRelics.Contains(relic)) return false;
        
        _equippedRelics.Add(relic);
        OnEquippedChanged?.Invoke();
        
        if (showDebugLogs)
            Debug.Log($"[RelicManager] Equipped: {relic.relicName}");
        
        return true;
    }
    
    /// <summary>
    /// Unequip a relic
    /// </summary>
    public bool UnequipRelic(RelicDefinition relic)
    {
        if (relic == null) return false;
        
        bool removed = _equippedRelics.Remove(relic);
        if (removed)
        {
            OnEquippedChanged?.Invoke();
            if (showDebugLogs)
                Debug.Log($"[RelicManager] Unequipped: {relic.relicName}");
        }
        
        return removed;
    }
    
    /// <summary>
    /// Get currently equipped relics
    /// </summary>
    public List<RelicDefinition> GetEquippedRelics()
    {
        return new List<RelicDefinition>(_equippedRelics);
    }
    
    /// <summary>
    /// Clear equipped relics (for new run)
    /// </summary>
    public void ClearEquipped()
    {
        _equippedRelics.Clear();
        OnEquippedChanged?.Invoke();
    }
    
    // ============================================
    // Stat Calculation
    // ============================================
    
    /// <summary>
    /// Get total MaxHP bonus from equipped relics
    /// </summary>
    public int GetTotalMaxHPBonus()
    {
        int total = 0;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.GetTotalMaxHPBonus(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total ATK% bonus from equipped relics
    /// </summary>
    public float GetTotalATKPercent()
    {
        float total = 0f;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.GetTotalATKPercent(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total DEF bonus from equipped relics
    /// </summary>
    public int GetTotalDEFBonus()
    {
        int total = 0;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.GetTotalDEFBonus(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total EVA% bonus from equipped relics
    /// </summary>
    public float GetTotalEVABonus()
    {
        float total = 0f;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.bonusEVA * relic.GetStackMultiplier(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total CRIT% bonus from equipped relics
    /// </summary>
    public float GetTotalCRITBonus()
    {
        float total = 0f;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.bonusCRIT * relic.GetStackMultiplier(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total CRIT_DMG% bonus from equipped relics
    /// </summary>
    public float GetTotalCRIT_DMGBonus()
    {
        float total = 0f;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.bonusCRIT_DMG * relic.GetStackMultiplier(stack);
        }
        return total;
    }
    
    /// <summary>
    /// Get total hunger reduction % from equipped relics
    /// </summary>
    public float GetTotalHungerReduction()
    {
        float total = 0f;
        foreach (var relic in _equippedRelics)
        {
            int stack = GetOwnedCount(relic.relicID);
            total += relic.hungerReductionPercent * relic.GetStackMultiplier(stack);
        }
        return Mathf.Min(total, 0.9f); // Cap at 90%
    }
    
    // ============================================
    // Random Relic Generation (for death rewards)
    // ============================================
    
    /// <summary>
    /// Get a random relic based on rarity weights
    /// </summary>
    public RelicDefinition GetRandomRelic(float commonWeight = 0.6f, float rareWeight = 0.25f, 
                                           float epicWeight = 0.12f, float legendaryWeight = 0.03f)
    {
        float roll = Random.value;
        RelicRarity targetRarity;
        
        if (roll < legendaryWeight)
            targetRarity = RelicRarity.Legendary;
        else if (roll < legendaryWeight + epicWeight)
            targetRarity = RelicRarity.Epic;
        else if (roll < legendaryWeight + epicWeight + rareWeight)
            targetRarity = RelicRarity.Rare;
        else
            targetRarity = RelicRarity.Common;
        
        // Get relics of target rarity
        var candidates = allRelics.FindAll(r => r.rarity == targetRarity);
        
        // Fallback to common if no relics of that rarity
        if (candidates.Count == 0)
            candidates = allRelics.FindAll(r => r.rarity == RelicRarity.Common);
        
        if (candidates.Count == 0)
            return null;
        
        return candidates[Random.Range(0, candidates.Count)];
    }
    
    // ============================================
    // Save/Load
    // ============================================
    
    private void SaveRelics()
    {
        foreach (var kvp in _ownedRelics)
        {
            PlayerPrefs.SetInt(SAVE_KEY_PREFIX + kvp.Key, kvp.Value);
        }
        PlayerPrefs.Save();
    }
    
    private void LoadRelics()
    {
        _ownedRelics.Clear();
        
        foreach (var relic in allRelics)
        {
            if (relic == null) continue;
            
            int count = PlayerPrefs.GetInt(SAVE_KEY_PREFIX + relic.relicID, 0);
            if (count > 0)
            {
                _ownedRelics[relic.relicID] = count;
            }
        }
        
        if (showDebugLogs)
            Debug.Log($"[RelicManager] Loaded {_ownedRelics.Count} owned relics");
    }
    
    /// <summary>
    /// Reset all relic data (for testing)
    /// </summary>
    public void ResetAllRelics()
    {
        foreach (var relic in allRelics)
        {
            if (relic != null)
                PlayerPrefs.DeleteKey(SAVE_KEY_PREFIX + relic.relicID);
        }
        
        _ownedRelics.Clear();
        _equippedRelics.Clear();
        PlayerPrefs.Save();
        
        OnRelicsChanged?.Invoke();
        OnEquippedChanged?.Invoke();
        
        if (showDebugLogs)
            Debug.Log("[RelicManager] All relic data reset!");
    }
    
    // ============================================
    // Helpers
    // ============================================
    
    private RelicDefinition GetRelicByID(string relicID)
    {
        return allRelics.Find(r => r != null && r.relicID == relicID);
    }
}
