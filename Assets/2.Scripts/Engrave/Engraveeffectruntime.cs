using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 각인 투자 포인트를 실제 플레이어/전투 수치로 변환해 적용한다.
/// - 노드별 효과는 asset name(EN_HP_1...) 기준 프리셋을 우선 사용
/// - effectType/effectValuePerPoint가 채워진 노드는 단일 효과로도 해석 가능
/// </summary>
public class EngraveEffectRuntime : MonoBehaviour
{
    public static EngraveEffectRuntime Instance { get; private set; }

    private EngraveManager _boundManager;
    private EngraveAppliedStats _current;

    public EngraveAppliedStats Current => _current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryBindManager();
        RebuildAndApply();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindManager();
    }

    private void Update()
    {
        if (_boundManager == null)
            TryBindManager();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToAllPlayerStats();
    }

    private void TryBindManager()
    {
        var mgr = EngraveManager.Instance;
        if (mgr == null || mgr == _boundManager)
            return;

        UnbindManager();
        _boundManager = mgr;
        _boundManager.OnTreeChanged += RebuildAndApply;
        _boundManager.OnMetaPointsChanged += OnMetaChanged;
        RebuildAndApply();
    }

    private void UnbindManager()
    {
        if (_boundManager == null)
            return;

        _boundManager.OnTreeChanged -= RebuildAndApply;
        _boundManager.OnMetaPointsChanged -= OnMetaChanged;
        _boundManager = null;
    }

    private void OnMetaChanged(int _)
    {
        // 투자량이 바뀌지 않으면 수치는 그대로지만, 안전하게 재적용.
        ApplyToAllPlayerStats();
    }

    public void RebuildAndApply()
    {
        _current = BuildStatsFromInvestments();
        ApplyToAllPlayerStats();
    }

    private EngraveAppliedStats BuildStatsFromInvestments()
    {
        var result = new EngraveAppliedStats();
        var mgr = EngraveManager.Instance;
        if (mgr == null || mgr.TreeData == null)
            return result;

        foreach (var cat in mgr.TreeData.GetAllCategories())
        {
            var nodes = mgr.TreeData.GetNodesByCategory(cat);
            foreach (var node in nodes)
            {
                int pts = mgr.GetInvestedPoints(node);
                if (pts <= 0)
                    continue;

                if (!ApplyPreset(node.name, pts, ref result))
                    ApplyEffectTypeFallback(node, pts, ref result);
            }
        }

        result.Clamp();
        return result;
    }

    private static bool ApplyPreset(string nodeName, int points, ref EngraveAppliedStats s)
    {
        if (string.IsNullOrEmpty(nodeName) || points <= 0)
            return false;

        switch (nodeName)
        {
            // Health
            case "EN_HP_1": s.maxHpPercent += 0.02f * points; return true;
            case "EN_HP_2": s.startShieldFlat += 2 * points; return true;
            case "EN_HP_3": s.restRecoveryPercent += 0.04f * points; return true;
            case "EN_HP_4": s.dotDamageResistPercent += 0.04f * points; return true;
            case "EN_HP_5": s.critDamageTakenReductionPercent += 0.04f * points; return true;
            case "EN_HP_ADV":
                s.maxHpPercent += 0.08f * points;
                s.startShieldEfficiencyPercent += 0.20f * points;
                return true;

            // Attack
            case "EN_ATK_1": s.attackPercent += 0.02f * points; return true;
            case "EN_ATK_2": s.critChanceBonus += 0.01f * points; return true;
            case "EN_ATK_3": s.critDamagePercent += 0.02f * points; return true;
            case "EN_ATK_4": s.bossDamagePercent += 0.02f * points; return true;
            case "EN_ATK_5": s.normalDamagePercent += 0.016f * points; return true;
            case "EN_ATK_ADV":
                s.attackPercent += 0.06f * points;
                s.critDamagePercent += 0.10f * points;
                return true;

            // Hunger / Fatigue
            case "EN_HF_1": s.hungerDrainReductionPercent += 0.04f * points; return true;
            case "EN_HF_2": s.fatigueDrainReductionPercent += 0.04f * points; return true;
            case "EN_HF_3": s.mealEfficiencyPercent += 0.04f * points; return true;
            case "EN_HF_4": s.restRecoveryPercent += 0.04f * points; return true;
            case "EN_HF_5": s.hfPenaltyReductionPercent += 0.04f * points; return true;
            case "EN_HF_ADV":
                s.hungerDrainReductionPercent += 0.12f * points;
                s.fatigueDrainReductionPercent += 0.12f * points;
                return true;

            // Defense
            case "EN_DEF_1": s.defenseFlat += 2 * points; return true;
            case "EN_DEF_2": s.damageTakenReductionPercent += 0.02f * points; return true;
            case "EN_DEF_3": s.evasionBonus += 0.008f * points; return true;
            case "EN_DEF_4": s.statusDurationReductionPercent += 0.04f * points; return true;
            case "EN_DEF_5": s.comboDamageReductionPercent += 0.02f * points; return true;
            case "EN_DEF_ADV":
                s.damageTakenReductionPercent += 0.06f * points;
                s.statusResistPercent += 0.10f * points;
                return true;

            // AP
            case "EN_AP_1": s.startApBonus += 0.4f * points; return true;
            case "EN_AP_2": s.maxApBonus += 0.4f * points; return true;
            case "EN_AP_3": s.turnStartApRegenChance += 0.02f * points; return true;
            case "EN_AP_4": s.apCostReduceChance += 0.02f * points; return true;
            case "EN_AP_5": s.killApRefundChance += 0.02f * points; return true;
            case "EN_AP_ADV":
                s.apChanceGlobalBonus += 0.06f * points;
                s.initialCombatApBonus += 2 * points;
                return true;
        }

        return false;
    }

    private static void ApplyEffectTypeFallback(EngraveNodeData node, int points, ref EngraveAppliedStats s)
    {
        if (node == null || string.IsNullOrWhiteSpace(node.effectType) || points <= 0)
            return;

        string k = node.effectType.Trim().ToLowerInvariant();
        float amount = node.effectValuePerPoint * points;

        switch (k)
        {
            case "hp_max_pct":
            case "maxhp_percent":
                s.maxHpPercent += amount;
                break;
            case "atk_pct":
            case "attack_percent":
                s.attackPercent += amount;
                break;
            case "def_flat":
                s.defenseFlat += Mathf.RoundToInt(amount);
                break;
            case "eva_pct":
            case "evasion_pct":
                s.evasionBonus += amount;
                break;
            case "crit_pct":
                s.critChanceBonus += amount;
                break;
            case "crit_dmg_pct":
                s.critDamagePercent += amount;
                break;
            case "dmg_taken_reduce_pct":
                s.damageTakenReductionPercent += amount;
                break;
            case "start_ap":
                s.startApBonus += amount;
                break;
            case "max_ap":
                s.maxApBonus += amount;
                break;
        }
    }

    private void ApplyToAllPlayerStats()
    {
        var all = FindObjectsOfType<PlayerStats>();
        for (int i = 0; i < all.Length; i++)
            all[i].ApplyEngraveModifiers(_current);
    }

    public static int GetStartApBonusInt()
    {
        if (Instance == null) return 0;
        return Mathf.FloorToInt(Instance._current.startApBonus);
    }

    public static int GetMaxApBonusInt()
    {
        if (Instance == null) return 0;
        return Mathf.FloorToInt(Instance._current.maxApBonus);
    }

    public static int GetInitialCombatApBonusInt()
    {
        if (Instance == null) return 0;
        return Mathf.Max(0, Instance._current.initialCombatApBonus);
    }

    public static float GetTurnStartApRegenChance()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp01(Instance._current.turnStartApRegenChance + Instance._current.apChanceGlobalBonus);
    }

    public static float GetApCostReduceChance()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp01(Instance._current.apCostReduceChance + Instance._current.apChanceGlobalBonus);
    }

    public static float GetKillApRefundChance()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp01(Instance._current.killApRefundChance + Instance._current.apChanceGlobalBonus);
    }

    public static float GetBossDamagePercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Max(0f, Instance._current.bossDamagePercent);
    }

    public static float GetNormalDamagePercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Max(0f, Instance._current.normalDamagePercent);
    }

    public static float GetPlayerDotResistPercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp(Instance._current.dotDamageResistPercent, 0f, 0.95f);
    }

    public static float GetPlayerStatusDurationReductionPercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp(Instance._current.statusDurationReductionPercent, 0f, 0.9f);
    }

    public static float GetPlayerCritDamageTakenReductionPercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Clamp(Instance._current.critDamageTakenReductionPercent, 0f, 0.9f);
    }

    public static int GetStartShieldFlat()
    {
        if (Instance == null) return 0;
        return Mathf.Max(0, Instance._current.startShieldFlat);
    }

    public static float GetStartShieldEfficiencyPercent()
    {
        if (Instance == null) return 0f;
        return Mathf.Max(0f, Instance._current.startShieldEfficiencyPercent);
    }
}

[Serializable]
public struct EngraveAppliedStats
{
    // Health
    public float maxHpPercent;
    public int startShieldFlat;
    public float startShieldEfficiencyPercent;
    public float restRecoveryPercent;
    public float dotDamageResistPercent;
    public float critDamageTakenReductionPercent;

    // Attack
    public float attackPercent;
    public float critChanceBonus;
    public float critDamagePercent;
    public float bossDamagePercent;
    public float normalDamagePercent;

    // Hunger / Fatigue
    public float hungerDrainReductionPercent;
    public float fatigueDrainReductionPercent;
    public float mealEfficiencyPercent;
    public float hfPenaltyReductionPercent;

    // Defense
    public int defenseFlat;
    public float damageTakenReductionPercent;
    public float evasionBonus;
    public float statusDurationReductionPercent;
    public float comboDamageReductionPercent;
    public float statusResistPercent;

    // AP
    public float startApBonus;
    public float maxApBonus;
    public int initialCombatApBonus;
    public float turnStartApRegenChance;
    public float apCostReduceChance;
    public float killApRefundChance;
    public float apChanceGlobalBonus;

    public void Clamp()
    {
        maxHpPercent = Mathf.Max(0f, maxHpPercent);
        attackPercent = Mathf.Max(0f, attackPercent);
        critChanceBonus = Mathf.Clamp(critChanceBonus, 0f, 0.95f);
        evasionBonus = Mathf.Clamp(evasionBonus, 0f, 0.95f);

        hungerDrainReductionPercent = Mathf.Clamp(hungerDrainReductionPercent, 0f, 0.9f);
        fatigueDrainReductionPercent = Mathf.Clamp(fatigueDrainReductionPercent, 0f, 0.9f);

        damageTakenReductionPercent = Mathf.Clamp(damageTakenReductionPercent, 0f, 0.8f);
        turnStartApRegenChance = Mathf.Clamp01(turnStartApRegenChance);
        apCostReduceChance = Mathf.Clamp01(apCostReduceChance);
        killApRefundChance = Mathf.Clamp01(killApRefundChance);
        apChanceGlobalBonus = Mathf.Clamp01(apChanceGlobalBonus);
    }
}
