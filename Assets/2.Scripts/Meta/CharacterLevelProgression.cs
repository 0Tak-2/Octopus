using System;
using UnityEngine;

/// <summary>
/// 런(플레이 세션) 단위 캐릭터 레벨/XP. 계정 최초 Lv2 시 각인 메뉴 해금.
/// </summary>
public class CharacterLevelProgression : MonoBehaviour
{
    public static CharacterLevelProgression Instance { get; private set; }

    [Header("레벨 곡선")]
    [Tooltip("Lv1→2 필요 XP 등: need = round(base * level^pow)")]
    public float xpBase = 40f;
    public float xpPow = 1.25f;

    [Header("런 시작 레벨")]
    [Min(1)] public int startingLevel = 1;

    private int _level = 1;
    private int _currentXp;

    public int Level => _level;
    public int CurrentXp => _currentXp;

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 XP</summary>
    public int XpToNext => GetXpRequiredToAdvanceFrom(_level);

    public event Action<int, int> OnLevelChanged;
    public event Action<int, int> OnXpChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _level = Mathf.Max(1, startingLevel);
        _currentXp = 0;
    }

    private void Start()
    {
        OnXpChanged?.Invoke(_currentXp, XpToNext);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetRunProgress()
    {
        _level = Mathf.Max(1, startingLevel);
        _currentXp = 0;
        OnLevelChanged?.Invoke(_level, _currentXp);
        OnXpChanged?.Invoke(_currentXp, XpToNext);
    }


    /// <summary>킬 등에서 주는 캐릭터 XP (메타 XP와 별도)</summary>
    public void AddCharacterKillXp(int amount)
    {
        if (amount <= 0)
            return;

        _currentXp += amount;

        while (true)
        {
            int need = GetXpRequiredToAdvanceFrom(_level);
            if (need <= 0 || _currentXp < need)
                break;

            _currentXp -= need;
            int newLevel = _level + 1;
            ApplyLevelUp(newLevel);
        }

        OnXpChanged?.Invoke(_currentXp, XpToNext);
    }

    private void ApplyLevelUp(int newLevel)
    {
        _level = newLevel;
        OnLevelChanged?.Invoke(_level, _currentXp);

        if (_level == 2 && PlayerPrefs.GetInt("Account_FirstReachedLv2", 0) == 0)
        {
            PlayerPrefs.SetInt("Account_FirstReachedLv2", 1);
            PlayerPrefs.Save();
            EngraveAccountUnlock.UnlockMenuFromFirstLevelUp();
            Debug.Log("[CharacterLevel] 계정 최초 Lv2 — 각인 메뉴 해금. (튜토리얼 UI는 이후 연결)");
        }
    }

    public int GetXpRequiredToAdvanceFrom(int currentLevel)
    {
        int lv = Mathf.Max(1, currentLevel);
        return Mathf.Max(1, Mathf.RoundToInt(xpBase * Mathf.Pow(lv, xpPow)));
    }
}
