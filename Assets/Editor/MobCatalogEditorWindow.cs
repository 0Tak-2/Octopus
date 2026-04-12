using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 필드/던전에 쓰이는 적(EnemyDefinition)과 스폰 참조를 한 화면에서 검색·편집합니다.
/// 메뉴: Tools / Octopus / Mob Catalog
/// </summary>
public class MobCatalogEditorWindow : EditorWindow
{
    enum PrimaryTab
    {
        Enemies,
        Fields,
        Dungeons
    }

    PrimaryTab _tab = PrimaryTab.Enemies;
    Vector2 _scrollLeft;
    Vector2 _scrollRight;
    string _filter = "";

    readonly List<EnemyDefinition> _enemies = new List<EnemyDefinition>();
    readonly List<FieldDefinition> _fields = new List<FieldDefinition>();
    readonly List<DungeonDefinition> _dungeons = new List<DungeonDefinition>();

    Object _selected;
    SerializedObject _serializedSelected;

    readonly List<FieldDefinition> _usageFields = new List<FieldDefinition>();
    readonly List<DungeonDefinition> _usageDungeons = new List<DungeonDefinition>();

    [MenuItem("Tools/Octopus/Mob Catalog")]
    public static void Open()
    {
        var w = GetWindow<MobCatalogEditorWindow>();
        w.titleContent = new GUIContent("Mob Catalog");
        w.minSize = new Vector2(680, 420);
        w.Show();
    }

    void OnEnable() => RefreshCaches();

    void OnDisable()
    {
        if (_serializedSelected != null)
        {
            _serializedSelected.Dispose();
            _serializedSelected = null;
        }
    }

    void RefreshCaches()
    {
        _enemies.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:EnemyDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var e = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (e != null) _enemies.Add(e);
        }
        _enemies.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.displayName, b.displayName);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        });

        _fields.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:FieldDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var f = AssetDatabase.LoadAssetAtPath<FieldDefinition>(path);
            if (f != null) _fields.Add(f);
        }
        _fields.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.fieldName, b.fieldName);
            return c != 0 ? c : string.CompareOrdinal(a.fieldID, b.fieldID);
        });

        _dungeons.Clear();
        foreach (var guid in AssetDatabase.FindAssets("t:DungeonDefinition"))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var d = AssetDatabase.LoadAssetAtPath<DungeonDefinition>(path);
            if (d != null) _dungeons.Add(d);
        }
        _dungeons.Sort((a, b) =>
        {
            int c = string.CompareOrdinal(a.dungeonName, b.dungeonName);
            return c != 0 ? c : string.CompareOrdinal(a.dungeonID, b.dungeonID);
        });

        if (_selected != null)
        {
            if (_selected is EnemyDefinition ed && !_enemies.Contains(ed)) SetSelection(null);
            else if (_selected is FieldDefinition fd && !_fields.Contains(fd)) SetSelection(null);
            else if (_selected is DungeonDefinition dd && !_dungeons.Contains(dd)) SetSelection(null);
            else if (_selected is EnemyDefinition ed2) RebuildUsage(ed2);
        }
    }

    static EnemyDefinition GetDefFromPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        var inst = prefab.GetComponentInChildren<EnemyInstance>(true);
        return inst != null ? inst.definition : null;
    }

    static bool PrefabArrayReferences(GameObject[] arr, EnemyDefinition def)
    {
        if (arr == null || def == null) return false;
        foreach (var g in arr)
        {
            if (GetDefFromPrefab(g) == def) return true;
        }
        return false;
    }

    void RebuildUsage(EnemyDefinition def)
    {
        _usageFields.Clear();
        _usageDungeons.Clear();
        if (def == null) return;

        foreach (var f in _fields)
        {
            if (PrefabArrayReferences(f.normalEnemyPrefabs, def) ||
                PrefabArrayReferences(f.eliteEnemyPrefabs, def) ||
                PrefabArrayReferences(f.neutralEnemyPrefabs, def) ||
                PrefabArrayReferences(f.passiveEnemyPrefabs, def))
                _usageFields.Add(f);
        }

        foreach (var d in _dungeons)
        {
            bool u = PrefabArrayReferences(d.normalEnemyPrefabs, def);
            if (d.eliteEnemyPrefab != null && GetDefFromPrefab(d.eliteEnemyPrefab) == def) u = true;
            if (d.bossPrefab != null && GetDefFromPrefab(d.bossPrefab) == def) u = true;
            if (u) _usageDungeons.Add(d);
        }
    }

    void SetSelection(Object obj)
    {
        if (_selected == obj) return;
        _selected = obj;
        if (_serializedSelected != null)
        {
            _serializedSelected.Dispose();
            _serializedSelected = null;
        }
        if (_selected != null)
            _serializedSelected = new SerializedObject(_selected);

        if (_selected is EnemyDefinition ed)
            RebuildUsage(ed);
        else
        {
            _usageFields.Clear();
            _usageDungeons.Clear();
        }
    }

    bool PassesFilter(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(_filter)) return true;
        var f = _filter.Trim();
        return (a != null && a.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
               (b != null && b.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    static string ItemOneLine(ItemData item, float chance)
    {
        if (item == null) return "—";
        return $"{item.itemName} ({chance:P0})";
    }

    void OnGUI()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(72)))
            RefreshCaches();
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("필드·던전 스폰과 EnemyDefinition을 한 창에서 편집", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        _tab = (PrimaryTab)GUILayout.Toolbar((int)_tab, new[] { "적 (EnemyDefinition)", "필드 스폰", "던전 스폰" });

        float leftW = Mathf.Clamp(position.width * 0.36f, 220f, 420f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(leftW));
        _filter = EditorGUILayout.TextField("검색", _filter);
        _scrollLeft = EditorGUILayout.BeginScrollView(_scrollLeft);
        switch (_tab)
        {
            case PrimaryTab.Enemies:
                DrawEnemyList();
                break;
            case PrimaryTab.Fields:
                DrawFieldList();
                break;
            case PrimaryTab.Dungeons:
                DrawDungeonList();
                break;
        }
        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        _scrollRight = EditorGUILayout.BeginScrollView(_scrollRight);
        DrawSelectedInspector();
        EditorGUILayout.EndScrollView();
        GUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    void DrawEnemyList()
    {
        foreach (var e in _enemies)
        {
            if (!PassesFilter(e.displayName, e.name)) continue;
            bool sel = _selected == e;
            var prev = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
            var drops = $"Food {ItemOneLine(e.dropFood, e.foodDropChance)} | Mat {ItemOneLine(e.dropMaterial, e.materialDropChance)}";
            if (GUILayout.Button(
                    $"{e.displayName}  ({e.name})\nHP {e.maxHP} | {e.aiType}\n{drops}",
                    GUILayout.Height(52)))
            {
                SetSelection(e);
                Selection.activeObject = e;
                EditorGUIUtility.PingObject(e);
            }
            GUI.backgroundColor = prev;
        }
    }

    void DrawFieldList()
    {
        foreach (var f in _fields)
        {
            if (!PassesFilter(f.fieldName, f.fieldID)) continue;
            bool sel = _selected == f;
            var prev = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
            int n = f.normalEnemyPrefabs != null ? f.normalEnemyPrefabs.Length : 0;
            int ne = f.eliteEnemyPrefabs != null ? f.eliteEnemyPrefabs.Length : 0;
            int nn = f.neutralEnemyPrefabs != null ? f.neutralEnemyPrefabs.Length : 0;
            int np = f.passiveEnemyPrefabs != null ? f.passiveEnemyPrefabs.Length : 0;
            if (GUILayout.Button(
                    $"{f.fieldName}  [{f.fieldID}]\n일반×{f.normalEnemyCount} (프리팹 {n}) | 엘리트×{f.eliteEnemyCount} ({ne}) | 중립×{f.neutralEnemyCount} ({nn}) | 패시브×{f.passiveEnemyCount} ({np})",
                    GUILayout.Height(44)))
            {
                SetSelection(f);
                Selection.activeObject = f;
                EditorGUIUtility.PingObject(f);
            }
            GUI.backgroundColor = prev;
        }
    }

    void DrawDungeonList()
    {
        foreach (var d in _dungeons)
        {
            if (!PassesFilter(d.dungeonName, d.dungeonID)) continue;
            bool sel = _selected == d;
            var prev = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = new Color(0.55f, 0.78f, 1f);
            int n = d.normalEnemyPrefabs != null ? d.normalEnemyPrefabs.Length : 0;
            string elite = d.eliteEnemyPrefab != null ? d.eliteEnemyPrefab.name : "—";
            string boss = d.bossPrefab != null ? d.bossPrefab.name : "—";
            if (GUILayout.Button(
                    $"{d.dungeonName}  [{d.dungeonID}]\n일반 풀 {n} | 엘리트: {elite} ({d.eliteSpawnChance:P0}) | 보스: {boss}",
                    GUILayout.Height(44)))
            {
                SetSelection(d);
                Selection.activeObject = d;
                EditorGUIUtility.PingObject(d);
            }
            GUI.backgroundColor = prev;
        }
    }

    void DrawSelectedInspector()
    {
        if (_selected == null)
        {
            EditorGUILayout.HelpBox(
                "왼쪽에서 항목을 선택하세요. 아래 영역은 인스펙터와 같이 ScriptableObject 필드를 직접 수정합니다. 저장은 Unity가 자동으로 처리합니다.",
                MessageType.Info);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("선택 중", EditorStyles.boldLabel);
        if (GUILayout.Button("Ping", GUILayout.Width(56)))
        {
            EditorGUIUtility.PingObject(_selected);
            Selection.activeObject = _selected;
        }
        if (GUILayout.Button("인스펙터로", GUILayout.Width(88)))
            Selection.activeObject = _selected;
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField("Asset", _selected, _selected.GetType(), false);
        EditorGUI.EndDisabledGroup();

        if (_selected is EnemyDefinition)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("이 적이 등장하는 곳", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"필드 {_usageFields.Count}곳 · 던전 {_usageDungeons.Count}곳");
            foreach (var f in _usageFields)
            {
                if (GUILayout.Button($"→ 필드: {f.fieldName} ({f.fieldID})", EditorStyles.miniButton))
                {
                    _tab = PrimaryTab.Fields;
                    SetSelection(f);
                    Selection.activeObject = f;
                    EditorGUIUtility.PingObject(f);
                }
            }
            foreach (var d in _usageDungeons)
            {
                if (GUILayout.Button($"→ 던전: {d.dungeonName} ({d.dungeonID})", EditorStyles.miniButton))
                {
                    _tab = PrimaryTab.Dungeons;
                    SetSelection(d);
                    Selection.activeObject = d;
                    EditorGUIUtility.PingObject(d);
                }
            }
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("속성", EditorStyles.boldLabel);
        if (_serializedSelected != null)
        {
            _serializedSelected.Update();
            DrawAllProperties(_serializedSelected);
            _serializedSelected.ApplyModifiedProperties();
        }
    }

    static void DrawAllProperties(SerializedObject so)
    {
        var prop = so.GetIterator();
        if (!prop.NextVisible(true)) return;
        do
        {
            if (prop.name == "m_Script") continue;
            EditorGUILayout.PropertyField(prop, true);
        } while (prop.NextVisible(false));
    }
}
