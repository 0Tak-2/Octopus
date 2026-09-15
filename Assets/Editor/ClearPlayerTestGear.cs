using UnityEditor;
using UnityEngine;

/// <summary>
/// Player 프리팹에 테스트용으로 박아둔 장비·컬러모듈을 비운다.
///
/// 이게 남아 있으면 새 캐릭터가 항상 장비 4개 + 모듈 5개를 끼고 시작하고,
/// 플레이 중에 벗어도 다음에 들어올 때 프리팹 기본값으로 되살아난다.
/// </summary>
public static class ClearPlayerTestGear
{
    private const string PlayerPrefabPath = "Assets/Prefab/Player.prefab";

    [MenuItem("Octopus/Setup/Clear Player Test Gear")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[ClearPlayerTestGear] 프리팹을 찾을 수 없다: {PlayerPrefabPath}");
            return;
        }

        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        bool changed = false;

        changed |= ClearArray(root, "EquipmentManager", "equippedItems");
        changed |= ClearArray(root, "ColorModuleSlots", "equippedDefinitions");
        changed |= ClearArray(root, "ColorModuleInventory", "ownedModuleDefinitions");

        if (changed)
        {
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[ClearPlayerTestGear] Player 프리팹 저장 완료.");
        }
        else
        {
            Debug.Log("[ClearPlayerTestGear] 비울 것이 없었다.");
        }

        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// 컴포넌트의 배열/리스트 프로퍼티를 비운다.
    /// 고정 길이 슬롯(EquipmentManager.equippedItems 같은)은 길이를 유지하고 내용만 null 로 만든다.
    /// </summary>
    private static bool ClearArray(GameObject root, string componentTypeName, string propertyName)
    {
        Component target = null;
        foreach (var c in root.GetComponentsInChildren<Component>(true))
        {
            if (c != null && c.GetType().Name == componentTypeName)
            {
                target = c;
                break;
            }
        }

        if (target == null)
        {
            Debug.Log($"[ClearPlayerTestGear] {componentTypeName} 없음 — 건너뜀");
            return false;
        }

        var so = new SerializedObject(target);
        var prop = so.FindProperty(propertyName);
        if (prop == null || !prop.isArray)
        {
            Debug.LogWarning($"[ClearPlayerTestGear] {componentTypeName}.{propertyName} 를 찾을 수 없거나 배열이 아니다.");
            return false;
        }

        int filled = 0;
        for (int i = 0; i < prop.arraySize; i++)
        {
            var el = prop.GetArrayElementAtIndex(i);
            if (el.objectReferenceValue != null)
            {
                el.objectReferenceValue = null;
                filled++;
            }
        }

        if (filled == 0)
        {
            Debug.Log($"[ClearPlayerTestGear] {componentTypeName}.{propertyName} 는 이미 비어 있다.");
            return false;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log($"[ClearPlayerTestGear] {componentTypeName}.{propertyName} 에서 {filled}개 비움 (슬롯 {prop.arraySize}칸 유지).");
        return true;
    }
}
