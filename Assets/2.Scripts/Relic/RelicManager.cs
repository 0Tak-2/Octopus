using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Tooltip("레거시 필드. 실제 상한은 UnlockedSlots 를 쓴다.")]
    public int maxEquippedRelics = 5;

    // ============================================
    // 장착 슬롯 (계정 영구)
    // ============================================
    // 가진 유물을 전부 적용하면 빌드 정체성이 사라진다. 슬롯을 제한해 '고르게' 만든다.
    // 슬롯은 계정 단위로 영구 해금되며, 뒤로 갈수록 해금 비용이 급격히 오른다.

    [Header("Relic Slots (Tuning)")]
    [Tooltip("시작 슬롯 수")]
    [Min(1)] public int baseSlots = 3;

    [Tooltip("최대 슬롯 수")]
    [Min(1)] public int maxSlotCount = 9;

    [Tooltip("슬롯 N번째를 열 때 드는 조각 수. 인덱스 0~2는 기본 제공이라 0. 뒤로 갈수록 가파르게.")]
    public int[] slotShardCosts = { 0, 0, 0, 1, 2, 3, 5, 8, 13 };

    [Header("Relic Stacking (Tuning)")]
    [Tooltip("유물 중첩 최대 단계. 이 이상 얻어도 효과는 안 오른다.")]
    [Min(1)] public int maxStackLevel = 5;

    [Tooltip("단계별 증가폭 감쇠율. 0.7 = 다음 단계는 직전 증가폭의 70%.")]
    [Range(0.1f, 1f)] public float stackFalloff = 0.7f;

    // 정적 접근용 기본값 — 씬에 RelicManager 가 없을 때(에디터 프리뷰 등) 쓰인다.
    public const int DefaultBaseSlots = 3;
    public const int DefaultMaxSlots = 9;

    public static int BaseSlots => Instance != null ? Mathf.Max(1, Instance.baseSlots) : DefaultBaseSlots;
    public static int MaxSlots => Instance != null ? Mathf.Max(1, Instance.maxSlotCount) : DefaultMaxSlots;

    private int[] SlotShardCost =>
        (slotShardCosts != null && slotShardCosts.Length > 0)
            ? slotShardCosts
            : new[] { 0, 0, 0, 1, 2, 3, 5, 8, 13 };

    private const string SAVE_KEY_SLOTS = "Relic_UnlockedSlots";
    private const string SAVE_KEY_SHARDS = "Relic_Shards";

    /// <summary>현재 열려 있는 장착 슬롯 수 (3~9).</summary>
    public int UnlockedSlots =>
        Mathf.Clamp(PlayerPrefs.GetInt(SAVE_KEY_SLOTS, BaseSlots), BaseSlots, MaxSlots);

    /// <summary>슬롯 해금에 쓰는 조각. 보스 처치 등으로 얻는다.</summary>
    public int Shards => Mathf.Max(0, PlayerPrefs.GetInt(SAVE_KEY_SHARDS, 0));

    /// <summary>다음 슬롯을 열기 위해 필요한 조각 수. 이미 최대면 0.</summary>
    public int NextSlotCost
    {
        get
        {
            int next = UnlockedSlots + 1;
            if (next > MaxSlots) return 0;

            // 인스펙터에서 배열 길이를 줄일 수 있으므로 범위를 벗어나면 마지막 값을 쓴다.
            var costs = SlotShardCost;
            int idx = Mathf.Clamp(next - 1, 0, costs.Length - 1);
            return Mathf.Max(0, costs[idx]);
        }
    }

    public bool CanUnlockNextSlot => UnlockedSlots < MaxSlots && Shards >= NextSlotCost;

    public event System.Action OnSlotsChanged;

    /// <summary>조각 획득. 보스 처치나 특별한 발견에서 호출한다.</summary>
    public void AddShards(int amount)
    {
        if (amount <= 0) return;

        PlayerPrefs.SetInt(SAVE_KEY_SHARDS, Shards + amount);
        PlayerPrefs.Save();
        OnSlotsChanged?.Invoke();

        if (showDebugLogs)
            Debug.Log($"[RelicManager] 유물 조각 +{amount} (보유 {Shards}, 다음 슬롯까지 {NextSlotCost})");
    }

    /// <summary>조각을 써서 슬롯을 하나 연다.</summary>
    public bool TryUnlockNextSlot()
    {
        if (!CanUnlockNextSlot) return false;

        int cost = NextSlotCost;
        PlayerPrefs.SetInt(SAVE_KEY_SHARDS, Shards - cost);
        PlayerPrefs.SetInt(SAVE_KEY_SLOTS, UnlockedSlots + 1);
        PlayerPrefs.Save();
        OnSlotsChanged?.Invoke();

        if (showDebugLogs)
            Debug.Log($"[RelicManager] 유물 슬롯 해금! 이제 {UnlockedSlots}칸 (조각 {cost} 소모)");

        return true;
    }
    
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
        if (transform.parent != null) transform.SetParent(null);
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
        if (_equippedRelics.Count >= UnlockedSlots) return false;
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
    // 플레이어 스탯 반영
    // ============================================

    /// <summary>
    /// 장착 유물의 합산치를 PlayerStats 에 밀어넣는다.
    /// 유물 전용 필드(relic*)를 쓴다 — 장비가 bonus* 를 대입해버리기 때문.
    /// </summary>
    public void ApplyToPlayerStats(PlayerStats stats)
    {
        if (stats == null) return;

        stats.relicMaxHPBonus = GetTotalMaxHPBonus();
        stats.relicATKPercent = GetTotalATKPercent();
        stats.relicDEF = GetTotalDEFBonus();
        stats.relicEVA = GetTotalEVABonus();
        stats.relicCRIT = GetTotalCRITBonus();
        stats.relicCRIT_DMG = GetTotalCRIT_DMGBonus();

        stats.ClampAll();

        if (showDebugLogs)
            Debug.Log($"[RelicManager] 유물 적용: MaxHP+{stats.relicMaxHPBonus}, ATK+{stats.relicATKPercent:P0}, " +
                      $"DEF+{stats.relicDEF}, EVA+{stats.relicEVA:P1}, CRIT+{stats.relicCRIT:P1}");
    }

    /// <summary>현재 장착 조합을 ID 목록으로. 캐릭터(런 슬롯) 세이브에 실린다.</summary>
    public List<string> GetEquippedRelicIDs()
    {
        var ids = new List<string>();
        foreach (var relic in _equippedRelics)
            if (relic != null) ids.Add(relic.relicID);
        return ids;
    }

    /// <summary>
    /// ID 목록으로 장착 조합을 세팅한다. 슬롯이 줄었거나 유물을 잃었으면 알아서 잘린다.
    /// 장착 조합은 캐릭터마다 다르므로 런 슬롯 세이브에서 들어온다.
    /// </summary>
    public void SetEquippedByIDs(List<string> ids)
    {
        _equippedRelics.Clear();

        if (ids != null)
        {
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (_equippedRelics.Count >= UnlockedSlots) break;
                if (!OwnsRelic(id)) continue;

                var relic = GetRelicByID(id);
                if (relic != null && !_equippedRelics.Contains(relic))
                    _equippedRelics.Add(relic);
            }
        }

        OnEquippedChanged?.Invoke();
    }

    private void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

    /// <summary>
    /// RelicManager 는 씬을 넘어 살아남지만 PlayerStats 는 씬마다 새로 생긴다.
    /// 챕터를 넘어갈 때마다 다시 붙여줘야 유물 효과가 유지된다.
    /// </summary>
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 장착 조합은 런 슬롯 세이브에서 들어온다(캐릭터마다 다름). 여기선 손대지 않는다.
        var stats = FindObjectOfType<PlayerStats>();
        if (stats != null)
            ApplyToPlayerStats(stats);
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

        // 중첩이 상한에 닿은 유물은 더 줘도 효과가 오르지 않는다.
        // 아직 성장 여지가 있는 쪽을 우선해서 보상이 헛돌지 않게 한다.
        var upgradable = candidates.FindAll(r => !RelicDefinition.IsStackMaxed(GetOwnedCount(r.relicID)));

        // 해당 등급이 전부 만렙이면 등급을 무시하고 아직 올릴 수 있는 것을 찾는다.
        if (upgradable.Count == 0)
            upgradable = allRelics.FindAll(r => r != null && !RelicDefinition.IsStackMaxed(GetOwnedCount(r.relicID)));

        // 전부 만렙이면 어쩔 수 없이 기존 후보에서 고른다.
        if (upgradable.Count > 0)
            candidates = upgradable;

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
