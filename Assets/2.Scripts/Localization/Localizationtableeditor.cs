using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// LocalizationTable의 커스텀 에디터.
/// 검색, 카테고리 필터, 항목 수 표시 등 편의 기능을 제공합니다.
/// 위치: Assets/Editor/LocalizationTableEditor.cs
/// </summary>
[CustomEditor(typeof(LocalizationTable))]
public class LocalizationTableEditor : Editor
{
    private string searchFilter = "";
    private string categoryFilter = "All";
    private bool showAddSection = false;
    private string newKey = "";
    private string newValue = "";
    private Vector2 scrollPos;

    // 카테고리 목록 (키 접두어 기반)
    private static readonly string[] categories = new string[]
    {
        "All", "PASSIVE_", "SKILL_", "EQUIP_", "ENEMY_",
        "ITEM_", "STAT_", "STATUS_", "UI_", "SYS_", "TUT_", "FOCUS_", "MODULE_"
    };

    public override void OnInspectorGUI()
    {
        LocalizationTable table = (LocalizationTable)target;

        // 언어 설정
        EditorGUILayout.PropertyField(serializedObject.FindProperty("language"));
        EditorGUILayout.Space(5);

        // 통계
        int totalCount = table.entries.Count;
        EditorGUILayout.HelpBox($"총 {totalCount}개 항목", MessageType.Info);

        EditorGUILayout.Space(5);

        // ── 검색 바 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("검색", GUILayout.Width(40));
        searchFilter = EditorGUILayout.TextField(searchFilter);
        if (GUILayout.Button("X", GUILayout.Width(25)))
            searchFilter = "";
        EditorGUILayout.EndHorizontal();

        // ── 카테고리 필터 ──
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("카테고리", GUILayout.Width(55));
        int catIndex = System.Array.IndexOf(categories, categoryFilter);
        if (catIndex < 0) catIndex = 0;
        catIndex = EditorGUILayout.Popup(catIndex, categories);
        categoryFilter = categories[catIndex];
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // ── 항목 추가 ──
        showAddSection = EditorGUILayout.Foldout(showAddSection, "새 항목 추가", true);
        if (showAddSection)
        {
            EditorGUI.indentLevel++;
            newKey = EditorGUILayout.TextField("Key", newKey);
            newValue = EditorGUILayout.TextField("Value", newValue);

            if (GUILayout.Button("추가"))
            {
                if (!string.IsNullOrEmpty(newKey))
                {
                    bool exists = table.entries.Any(e => e.key == newKey);
                    if (exists)
                    {
                        EditorUtility.DisplayDialog("중복 키", $"'{newKey}' 키가 이미 존재합니다.", "확인");
                    }
                    else
                    {
                        Undo.RecordObject(table, "Add Localization Entry");
                        table.entries.Add(new LocalizationEntry { key = newKey, value = newValue });
                        newKey = "";
                        newValue = "";
                        EditorUtility.SetDirty(table);
                    }
                }
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // ── 항목 리스트 ──
        serializedObject.Update();
        SerializedProperty entriesProp = serializedObject.FindProperty("entries");

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(600));

        int visibleCount = 0;
        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty entry = entriesProp.GetArrayElementAtIndex(i);
            string key = entry.FindPropertyRelative("key").stringValue;
            string value = entry.FindPropertyRelative("value").stringValue;

            // 필터 적용
            if (categoryFilter != "All" && !key.StartsWith(categoryFilter))
                continue;
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lowerSearch = searchFilter.ToLower();
                if (!key.ToLower().Contains(lowerSearch) && !value.ToLower().Contains(lowerSearch))
                    continue;
            }

            visibleCount++;

            EditorGUILayout.BeginHorizontal();

            // 키 (읽기 전용 스타일)
            EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(260));

            // 값 (편집 가능)
            SerializedProperty valueProp = entry.FindPropertyRelative("value");
            valueProp.stringValue = EditorGUILayout.TextField(valueProp.stringValue);

            // 삭제 버튼
            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                if (EditorUtility.DisplayDialog("삭제 확인", $"'{key}' 항목을 삭제하시겠습니까?", "삭제", "취소"))
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        if (visibleCount == 0)
            EditorGUILayout.HelpBox("필터와 일치하는 항목이 없습니다.", MessageType.Warning);
        else if (visibleCount < totalCount)
            EditorGUILayout.LabelField($"표시: {visibleCount} / {totalCount}", EditorStyles.miniLabel);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);

        // ── 정렬 버튼 ──
        if (GUILayout.Button("키 이름순 정렬"))
        {
            Undo.RecordObject(table, "Sort Localization Entries");
            table.entries.Sort((a, b) => string.Compare(a.key, b.key, System.StringComparison.Ordinal));
            EditorUtility.SetDirty(table);
        }
    }
}