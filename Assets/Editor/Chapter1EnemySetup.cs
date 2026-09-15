using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 챕터 1(Field_1)의 적 구성을 정리한다.
///
/// 문제였던 것: Field_1 에 적 12종이 전부 스폰돼서, 얕은 바다에
/// 엘리트(참돔 90HP, 노랑가오리 100HP)와 최종 보스(청새치)까지 잡몹으로 돌아다녔다.
/// 난이도 커브가 없고 보스 조우의 특별함도 사라진다.
///
/// 정리 후 구성 — AI 2종(추격/중립·초식)만 남겨 학습 부담을 낮춘다:
///   일반(추격): 꽃게, 오징어
///   중립(반격만): 멸치, 정어리
///   초식(공격력 0): 바지락
/// </summary>
public static class Chapter1EnemySetup
{
    private const string FieldPath = "Assets/Field_Definition/Field_1.asset";
    private const string EnemyPrefabDir = "Assets/Prefab/Enemy";

    // 남길 적 (프리팹 이름 기준)
    private static readonly string[] KeepNormal = { "Enemy_FlowerCrab", "Enemy_Squid" };
    private static readonly string[] KeepNeutral = { "Enemy_Anchovy", "Enemy_Sardine" };
    private static readonly string[] KeepPassive = { "Enemy_Clam" };

    [MenuItem("Octopus/Setup/Trim Chapter 1 Enemies")]
    public static void Run()
    {
        var field = AssetDatabase.LoadAssetAtPath<ScriptableObject>(FieldPath);
        if (field == null)
        {
            Debug.LogError($"[Chapter1EnemySetup] Field 에셋을 찾을 수 없다: {FieldPath}");
            return;
        }

        var so = new SerializedObject(field);

        bool changed = false;
        changed |= ApplyList(so, "normalEnemyPrefabs", KeepNormal);
        changed |= ApplyList(so, "neutralEnemyPrefabs", KeepNeutral);
        changed |= ApplyList(so, "passiveEnemyPrefabs", KeepPassive);
        changed |= ApplyList(so, "eliteEnemyPrefabs", new string[0]);

        if (!changed)
        {
            Debug.Log("[Chapter1EnemySetup] 변경 사항 없음 (이미 정리됨).");
            return;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(field);
        AssetDatabase.SaveAssets();

        Debug.Log("[Chapter1EnemySetup] Field_1 적 구성 정리 완료.");
    }

    private static bool ApplyList(SerializedObject so, string propertyName, string[] keepNames)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[Chapter1EnemySetup] {propertyName} 을(를) 찾을 수 없다.");
            return false;
        }

        var wanted = new List<GameObject>();
        foreach (var name in keepNames)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{EnemyPrefabDir}/{name}.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[Chapter1EnemySetup] 프리팹을 찾을 수 없다: {name}");
                continue;
            }
            wanted.Add(prefab);
        }

        // 이미 같은 구성이면 건드리지 않는다.
        var current = new List<Object>();
        for (int i = 0; i < prop.arraySize; i++)
            current.Add(prop.GetArrayElementAtIndex(i).objectReferenceValue);

        if (current.Count == wanted.Count && !current.Where((t, i) => t != wanted[i]).Any())
        {
            Debug.Log($"[Chapter1EnemySetup] {propertyName} 는 이미 원하는 구성이다.");
            return false;
        }

        string before = string.Join(", ", current.Select(o => o != null ? o.name : "null"));

        prop.ClearArray();
        for (int i = 0; i < wanted.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];
        }

        string after = string.Join(", ", wanted.Select(o => o.name));
        Debug.Log($"[Chapter1EnemySetup] {propertyName}\n  이전: [{before}]\n  이후: [{after}]");
        return true;
    }
}
