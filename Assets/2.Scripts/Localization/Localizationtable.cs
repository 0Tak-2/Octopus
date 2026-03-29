using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 언어에 대한 로컬라이제이션 테이블 (ScriptableObject)
/// 에디터에서 직접 키-값 쌍을 편집할 수 있습니다.
/// Create: Assets > Create > OCTO > Localization Table
/// </summary>
[CreateAssetMenu(fileName = "NewLocalizationTable", menuName = "OCTO/Localization Table")]
public class LocalizationTable : ScriptableObject
{
    [Header("언어 설정")]
    public SystemLanguage language = SystemLanguage.Korean;

    [Header("로컬라이제이션 항목")]
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();

    // 런타임 검색용 딕셔너리 (캐시)
    private Dictionary<string, string> lookupCache;
    private bool isCacheDirty = true;

    /// <summary>
    /// 딕셔너리를 빌드하고 캐싱합니다.
    /// </summary>
    public void BuildCache()
    {
        lookupCache = new Dictionary<string, string>(entries.Count);
        foreach (var entry in entries)
        {
            if (string.IsNullOrEmpty(entry.key))
                continue;

            if (!lookupCache.TryAdd(entry.key, entry.value))
                Debug.LogWarning($"[Localization] 중복 키: {entry.key} ({language})");
        }
        isCacheDirty = false;
    }

    /// <summary>
    /// 키에 해당하는 값을 반환합니다.
    /// </summary>
    public bool TryGetValue(string key, out string value)
    {
        if (lookupCache == null || isCacheDirty)
            BuildCache();

        return lookupCache.TryGetValue(key, out value);
    }

    /// <summary>
    /// 에디터에서 값이 변경되면 캐시를 갱신합니다.
    /// </summary>
    private void OnValidate()
    {
        isCacheDirty = true;
    }
}

[Serializable]
public class LocalizationEntry
{
    public string key;

    [TextArea(1, 3)]
    public string value;
}