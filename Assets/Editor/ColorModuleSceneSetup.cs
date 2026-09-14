using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 컬러 모듈 시스템의 씬 배선을 맞춰준다.
///
/// 문제: ColorModuleInventory 와 ColorModuleDropper 가 씬에 없어서
/// 적을 잡아도 모듈을 얻을 방법이 아예 없었다. (ColorModuleDropper.TryDropModule 호출자 0곳,
/// 게다가 Instance 도 null)
///
/// 씬 YAML 을 손으로 고치면 깨질 위험이 크므로 Unity API 로 처리한다.
/// 메뉴에서 실행하거나 배치 모드에서 -executeMethod 로 호출한다.
/// </summary>
public static class ColorModuleSceneSetup
{
    private const string ScenePath = "Assets/1.Scene/NewMapScene.unity";
    private const string ModuleFolder = "Assets/ColorModule_Definition";

    [MenuItem("Octopus/Setup/Wire Color Module System")]
    public static void Run()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        bool changed = false;
        changed |= EnsureInventory();
        changed |= EnsureDropper();

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ColorModuleSceneSetup] 씬 저장 완료.");
        }
        else
        {
            Debug.Log("[ColorModuleSceneSetup] 변경 사항 없음 (이미 배선됨).");
        }
    }

    private static bool EnsureInventory()
    {
        if (Object.FindObjectOfType<ColorModuleInventory>() != null)
        {
            Debug.Log("[ColorModuleSceneSetup] ColorModuleInventory 이미 존재.");
            return false;
        }

        var go = new GameObject("ColorModuleInventory");
        go.AddComponent<ColorModuleInventory>();
        Undo.RegisterCreatedObjectUndo(go, "Create ColorModuleInventory");

        Debug.Log("[ColorModuleSceneSetup] ColorModuleInventory 생성.");
        return true;
    }

    private static bool EnsureDropper()
    {
        var dropper = Object.FindObjectOfType<ColorModuleDropper>();
        bool created = false;

        if (dropper == null)
        {
            var go = new GameObject("ColorModuleDropper");
            dropper = go.AddComponent<ColorModuleDropper>();
            Undo.RegisterCreatedObjectUndo(go, "Create ColorModuleDropper");
            created = true;
            Debug.Log("[ColorModuleSceneSetup] ColorModuleDropper 생성.");
        }

        var all = LoadAllModules();
        if (all.Count == 0)
        {
            Debug.LogWarning($"[ColorModuleSceneSetup] {ModuleFolder} 에서 ColorModuleDefinition 을 찾지 못했다.");
            return created;
        }

        // 스킬 모듈만 드랍 풀에 넣는다. 패시브까지 섞으면 스킬바가 비는 채로 굴러갈 수 있다.
        var skills = all.Where(m => m.moduleType == ModuleType.Skill).ToList();
        var pool = skills.Count > 0 ? skills : all;

        var red = pool.Where(m => m.colorType == ColorType.Red).ToList();
        var blue = pool.Where(m => m.colorType == ColorType.Blue).ToList();
        var black = pool.Where(m => m.colorType == ColorType.Black).ToList();

        var so = new SerializedObject(dropper);
        bool changed = created;
        changed |= AssignList(so, "redModulePool", red);
        changed |= AssignList(so, "blueModulePool", blue);
        changed |= AssignList(so, "blackModulePool", black);
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[ColorModuleSceneSetup] 드랍 풀 설정: 빨강 {red.Count}, 파랑 {blue.Count}, 검정 {black.Count} " +
                  $"(전체 {all.Count}, 스킬 {skills.Count})");

        return changed;
    }

    private static bool AssignList(SerializedObject so, string propertyName, List<ColorModuleDefinition> values)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"[ColorModuleSceneSetup] 프로퍼티를 찾을 수 없음: {propertyName}");
            return false;
        }

        prop.ClearArray();
        for (int i = 0; i < values.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        return true;
    }

    private static List<ColorModuleDefinition> LoadAllModules()
    {
        return AssetDatabase
            .FindAssets("t:ColorModuleDefinition", new[] { ModuleFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ColorModuleDefinition>)
            .Where(m => m != null)
            .OrderBy(m => m.name)
            .ToList();
    }
}
