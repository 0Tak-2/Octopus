using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로컬라이제이션 매니저 (싱글톤)
/// ScriptableObject 테이블을 참조하여 키 기반 번역을 제공합니다.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [Header("언어 테이블 (Inspector에서 드래그)")]
    [SerializeField] private LocalizationTable koreanTable;
    [SerializeField] private LocalizationTable englishTable;

    [Header("기본 언어")]
    [SerializeField] private SystemLanguage defaultLanguage = SystemLanguage.English;

    public SystemLanguage CurrentLanguage { get; private set; }

    private LocalizationTable activeTable;

    /// <summary>
    /// 언어 변경 시 발행되는 이벤트.
    /// 모든 LocalizedText 컴포넌트가 이 이벤트를 구독합니다.
    /// </summary>
    public static event Action OnLanguageChanged;

    private const string LANGUAGE_PREF_KEY = "OCTO_Language";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (koreanTable != null) koreanTable.BuildCache();
        if (englishTable != null) englishTable.BuildCache();

        LoadSavedLanguage();
    }

    private void LoadSavedLanguage()
    {
        if (PlayerPrefs.HasKey(LANGUAGE_PREF_KEY))
        {
            string saved = PlayerPrefs.GetString(LANGUAGE_PREF_KEY);
            if (Enum.TryParse(saved, out SystemLanguage lang))
            {
                SetLanguage(lang);
                return;
            }
        }

        SetLanguage(defaultLanguage);
    }

    /// <summary>
    /// 언어를 변경합니다.
    /// PlayerPrefs에 저장되어 다음 실행 시에도 유지됩니다.
    /// </summary>
    public void SetLanguage(SystemLanguage language)
    {
        CurrentLanguage = language;

        activeTable = language switch
        {
            SystemLanguage.Korean => koreanTable,
            _ => englishTable
        };

        if (activeTable == null)
        {
            Debug.LogError($"[Localization] {language} 테이블이 할당되지 않았습니다!");
            return;
        }

        PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language.ToString());
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// 키에 해당하는 번역 텍스트를 반환합니다.
    /// </summary>
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (activeTable != null && activeTable.TryGetValue(key, out string value))
            return value;

        Debug.LogWarning($"[Localization] 키를 찾을 수 없습니다: {key}");
        return $"[{key}]";
    }

    /// <summary>
    /// 축약형 정적 메서드.
    /// 사용 예: LocalizationManager.T("ITEM_NAME_CORAL")
    /// </summary>
    public static string T(string key)
    {
        if (Instance == null)
        {
            Debug.LogError("[Localization] LocalizationManager 인스턴스가 없습니다.");
            return $"[{key}]";
        }
        return Instance.Get(key);
    }

    /// <summary>
    /// 포맷 문자열 지원.
    /// 사용 예: LocalizationManager.TF("SYS_DAMAGE", 25, "멸치")
    /// </summary>
    public static string TF(string key, params object[] args)
    {
        string template = T(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Localization] 포맷 오류: {key}");
            return template;
        }
    }

    public bool HasKey(string key)
    {
        return activeTable != null && activeTable.TryGetValue(key, out _);
    }

    public void ToggleLanguage()
    {
        SetLanguage(CurrentLanguage == SystemLanguage.Korean
            ? SystemLanguage.English
            : SystemLanguage.Korean);
    }

    public void SetKorean() => SetLanguage(SystemLanguage.Korean);
    public void SetEnglish() => SetLanguage(SystemLanguage.English);
}