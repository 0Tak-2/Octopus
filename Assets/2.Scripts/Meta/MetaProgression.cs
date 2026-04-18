using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// 킬 기반 메타 XP, 환경 배율 f(P), K마다 각인 포인트(EngraveManager) 지급.
/// </summary>
public class MetaProgression : MonoBehaviour
{
    public static MetaProgression Instance { get; private set; }

    [Header("각인 포인트 전환")]
    [Tooltip("누적 메타 XP가 이 값마다 EngraveManager.AddMetaPoints(1)")]
    [Min(1f)] public float metaXpPerEngravePoint = 100f;

    [Header("환경 배율 f(P) — 진척 인덱스 구간")]
    [Tooltip("P < thresholds[i] 이면 multipliers[i]. 마지막 구간은 multipliers[마지막]. (multipliers 길이 = thresholds + 1)")]
    public float[] progressThresholds = { 2f, 5f, 10f };
    public float[] progressMultipliers = { 1f, 1.15f, 1.35f, 1.55f };

    [Header("스킬 등 추가 XP% (추후 연동)")]
    [Range(0f, 5f)] public float bonusXpMultiplier = 0f;

    [Header("디버그")]
    public bool logEachKill = false;

    private float _unspentMetaXp;

    public float UnspentMetaXp => _unspentMetaXp;

    public event Action<float> OnUnspentMetaXpChanged;

    private const string PrefsUnspent = "Meta_UnspentXpFloat";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureDefaultCurves();
        Load();
    }

    private void EnsureDefaultCurves()
    {
        if (progressThresholds == null || progressThresholds.Length == 0
            || progressMultipliers == null || progressMultipliers.Length < progressThresholds.Length + 1)
        {
            progressThresholds = new[] { 2f, 5f, 10f };
            progressMultipliers = new[] { 1f, 1.15f, 1.35f, 1.55f };
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>적 처치 1회분 (필드 TakeDamage 또는 집중전투 OnActiveEnemyDefeated에서만 호출)</summary>
    public void RegisterKill(EnemyDefinition def)
    {
        if (def == null)
            return;

        float p = GetProgressIndex();
        float mult = EvaluateMultiplier(p);
        int metaBase = def.metaXpValue > 0 ? def.metaXpValue : 10;
        int charBase = def.characterXpValue > 0 ? def.characterXpValue : 12;
        float gained = metaBase * mult * (1f + bonusXpMultiplier);
        _unspentMetaXp += gained;

        GrantEngravePointsFromOverflow();

        if (CharacterLevelProgression.Instance != null)
            CharacterLevelProgression.Instance.AddCharacterKillXp(charBase);

        bool isBoss = def.aiType == EnemyAIType.AI4_Boss;
        bool isElite = def.aiType == EnemyAIType.AI3_Elite;
        if (DeathManager.Instance != null)
            DeathManager.Instance.RecordKill(def.displayName, isBoss, isElite);

        Save();
        OnUnspentMetaXpChanged?.Invoke(_unspentMetaXp);

        if (logEachKill)
            Debug.Log($"[MetaProgression] Kill {def.displayName}: +{gained:F1} metaXP (P={p:F1}, f={mult:F2}), bank={_unspentMetaXp:F1}");
    }

    private void GrantEngravePointsFromOverflow()
    {
        if (metaXpPerEngravePoint <= 0f)
            return;

        var engrave = EngraveManager.Instance;
        if (engrave == null)
            return;

        while (_unspentMetaXp >= metaXpPerEngravePoint)
        {
            _unspentMetaXp -= metaXpPerEngravePoint;
            engrave.AddMetaPoints(1);
        }
    }

    /// <summary>필드 인덱스 + 던전 층을 단순 합성한 진척 스칼라.</summary>
    public float GetProgressIndex()
    {
        var w = WorldProgressManager.Instance;
        if (w == null)
            return 0f;

        if (w.IsInDungeon)
            return w.CurrentFieldIndex * 10f + Mathf.Max(0, w.CurrentDungeonFloor);

        return w.CurrentFieldIndex;
    }

    public float EvaluateMultiplier(float p)
    {
        if (progressMultipliers == null || progressMultipliers.Length == 0)
            return 1f;
        if (progressThresholds == null || progressThresholds.Length == 0)
            return progressMultipliers[0];

        for (int i = 0; i < progressThresholds.Length; i++)
        {
            if (p < progressThresholds[i])
                return progressMultipliers[Mathf.Min(i, progressMultipliers.Length - 1)];
        }

        return progressMultipliers[progressMultipliers.Length - 1];
    }

    private void Save()
    {
        PlayerPrefs.SetString(PrefsUnspent, _unspentMetaXp.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
    }

    private void Load()
    {
        string s = PlayerPrefs.GetString(PrefsUnspent, "0");
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            _unspentMetaXp = Mathf.Max(0f, v);
        else
            _unspentMetaXp = 0f;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug/Add 50 Meta XP")]
    private void EditorAddMetaXp()
    {
        _unspentMetaXp += 50f;
        GrantEngravePointsFromOverflow();
        Save();
        OnUnspentMetaXpChanged?.Invoke(_unspentMetaXp);
    }

    [ContextMenu("Debug/Unlock engrave title menu (account)")]
    private void EditorUnlockEngraveMenu()
    {
        EngraveAccountUnlock.UnlockMenuFromFirstLevelUp();
        PlayerPrefs.SetInt("Account_FirstReachedLv2", 1);
        PlayerPrefs.Save();
        Debug.Log("[MetaProgression] Account engrave menu unlocked (debug).");
    }
#endif
}
