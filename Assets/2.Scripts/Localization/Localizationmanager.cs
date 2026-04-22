using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ���ö������̼� �Ŵ��� (�̱���)
/// ScriptableObject ���̺��� �����Ͽ� Ű ��� ������ �����մϴ�.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [Header("��� ���̺� (Inspector���� �巡��)")]
    [SerializeField] private LocalizationTable koreanTable;
    [SerializeField] private LocalizationTable englishTable;

    [Header("�⺻ ���")]
    [SerializeField] private SystemLanguage defaultLanguage = SystemLanguage.English;

    public SystemLanguage CurrentLanguage { get; private set; }

    private LocalizationTable activeTable;

    /// <summary>
    /// ��� ���� �� ����Ǵ� �̺�Ʈ.
    /// ��� LocalizedText ������Ʈ�� �� �̺�Ʈ�� �����մϴ�.
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
        if (transform.parent != null) transform.SetParent(null);
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
    /// �� �����մϴ�.
    /// PlayerPrefs�� ����Ǿ� ���� ���� �ÿ��� �����˴ϴ�.
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
            Debug.LogError($"[Localization] {language} ���̺��� �Ҵ���� �ʾҽ��ϴ�!");
            return;
        }

        PlayerPrefs.SetString(LANGUAGE_PREF_KEY, language.ToString());
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Ű�� �ش��ϴ� ���� �ؽ�Ʈ�� ��ȯ�մϴ�.
    /// </summary>
    public string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (activeTable != null && activeTable.TryGetValue(key, out string value))
            return value;

        Debug.LogWarning($"[Localization] Ű�� ã�� �� �����ϴ�: {key}");
        return $"[{key}]";
    }

    /// <summary>
    /// ����� ���� �޼���.
    /// ��� ��: LocalizationManager.T("ITEM_NAME_CORAL")
    /// </summary>
    public static string T(string key)
    {
        if (Instance == null)
        {
            Debug.LogError("[Localization] LocalizationManager �ν��Ͻ��� �����ϴ�.");
            return $"[{key}]";
        }
        return Instance.Get(key);
    }

    /// <summary>
    /// ���� ���ڿ� ����.
    /// ��� ��: LocalizationManager.TF("SYS_DAMAGE", 25, "��ġ")
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
            Debug.LogWarning($"[Localization] ���� ����: {key}");
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