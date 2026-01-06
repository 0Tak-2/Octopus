using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class FieldSkillRangePreview : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public GridBoard gridBoard;
    public FieldSkillCaster caster;

    [Header("Preview Options")]
    public bool showOnlyWhenSkillReady = true;
    public bool requireLoS = true;
    public bool hideWhenNoSkillSelected = true;

    [Header("Visual")]
    [Tooltip("타일 하이라이트용 스프라이트(흰 사각형 같은 것)")]
    public Sprite highlightSprite;
    public float zOffset = -0.1f;
    public Vector2 scaleMultiplier = Vector2.one;

    [Header("Pooling")]
    public int initialPoolSize = 128;

    [Header("Refresh")]
    public float refreshInterval = 0.08f; // 너무 자주 그리지 않기
    private float _nextRefreshTime = 0f;

    private readonly List<GameObject> _pool = new();
    private readonly List<GameObject> _active = new();

    private Vector2Int _lastPlayerCell;
    private int _lastSlot = -1;
    private int _lastCooldown = -999;
    private ScriptableObject _lastDef;

    private void Awake()
    {
        if (player == null) player = FindObjectOfType<FieldSkillCaster>()?.transform ?? GameObject.FindGameObjectWithTag("Player")?.transform;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (caster == null) caster = FindObjectOfType<FieldSkillCaster>();

        WarmPool();
    }

    private void OnEnable()
    {
        if (caster != null)
        {
            caster.OnSelectedSlotChanged += _ => ForceRefresh();
            caster.OnCooldownChanged += () => ForceRefresh();
        }
        ForceRefresh();
    }

    private void OnDisable()
    {
        if (caster != null)
        {
            caster.OnSelectedSlotChanged -= _ => ForceRefresh();
            caster.OnCooldownChanged -= () => ForceRefresh();
        }
        ClearActive();
    }

    private void Update()
    {
        if (Time.time < _nextRefreshTime) return;
        _nextRefreshTime = Time.time + refreshInterval;

        if (player == null || gridBoard == null || caster == null)
            return;

        Vector2Int pCell = gridBoard.WorldToCell(player.position);

        int slot = caster.SelectedSlot;
        int cd = caster.GetCooldownRemaining(slot);
        var def = caster.GetDefinition(slot);

        bool changed =
            pCell != _lastPlayerCell ||
            slot != _lastSlot ||
            cd != _lastCooldown ||
            def != _lastDef;

        if (!changed) return;

        _lastPlayerCell = pCell;
        _lastSlot = slot;
        _lastCooldown = cd;
        _lastDef = def;

        Redraw();
    }

    private void ForceRefresh()
    {
        _lastSlot = -999;
        _lastCooldown = -999;
        _lastDef = null;
        _nextRefreshTime = 0f;
    }

    private void Redraw()
    {
        ClearActive();

        int slot = caster.SelectedSlot;
        ScriptableObject def = caster.GetDefinition(slot);

        if (def == null && hideWhenNoSkillSelected)
            return;

        if (def == null)
            return;

        int cd = caster.GetCooldownRemaining(slot);
        if (showOnlyWhenSkillReady && cd > 0)
            return;

        Vector2Int pCell = gridBoard.WorldToCell(player.position);
        int range = ReadInt(def, new[] { "range", "castRange", "attackRange" }, 1);

        // Chebyshev square range
        // (r=2 -> x±2, y±2)
        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                Vector2Int c = new Vector2Int(pCell.x + dx, pCell.y + dy);
                if (FieldCombatUtils.Chebyshev(pCell, c) > range) continue;

                if (!IsInBoundsIfPossible(c)) continue;

                // LOS 옵션: 플레이어->셀
                if (requireLoS && !FieldCombatUtils.HasLineOfSight(gridBoard, pCell, c))
                    continue;

                SpawnHighlightAtCell(c);
            }
        }
    }

    private void SpawnHighlightAtCell(Vector2Int cell)
    {
        var go = GetFromPool();
        go.transform.position = gridBoard.CellToWorld(cell) + new Vector3(0f, 0f, zOffset);
        go.SetActive(true);
        _active.Add(go);
    }

    private GameObject GetFromPool()
    {
        for (int i = 0; i < _pool.Count; i++)
        {
            if (!_pool[i].activeSelf)
                return _pool[i];
        }

        // 부족하면 추가 생성
        var extra = CreateHighlightObject();
        _pool.Add(extra);
        return extra;
    }

    private void WarmPool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = CreateHighlightObject();
            go.SetActive(false);
            _pool.Add(go);
        }
    }

    private GameObject CreateHighlightObject()
    {
        var go = new GameObject("RangePreviewTile");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = highlightSprite;

        // 기본 흰색이면 “투명도”만 조절해서 하이라이트 느낌
        sr.color = new Color(1f, 1f, 1f, 0.25f);
        sr.sortingOrder = 999; // 필드 위에 보이게(필요하면 조절)

        // 셀 크기에 맞춰 자동 스케일
        // (스프라이트 기본이 1유닛=1일 때 기준)
        float cellSize = TryReadCellSize(gridBoard, fallback: 1f);
        go.transform.localScale = new Vector3(cellSize * scaleMultiplier.x, cellSize * scaleMultiplier.y, 1f);

        return go;
    }

    private void ClearActive()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].SetActive(false);
        _active.Clear();
    }

    // ----- Reflection helpers (Definition / GridBoard) -----
    private static int ReadInt(ScriptableObject def, string[] names, int fallback)
    {
        if (def == null) return fallback;
        var t = def.GetType();

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(float)))
            {
                object v = f.GetValue(def);
                if (v is int i) return i;
                if (v is float fl) return Mathf.RoundToInt(fl);
            }

            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && (p.PropertyType == typeof(int) || p.PropertyType == typeof(float)))
            {
                object v = p.GetValue(def);
                if (v is int i2) return i2;
                if (v is float fl2) return Mathf.RoundToInt(fl2);
            }
        }

        return fallback;
    }

    private bool IsInBoundsIfPossible(Vector2Int cell)
    {
        // GridBoard에 IsInBounds(Vector2Int) 같은 게 있으면 사용
        var m = gridBoard.GetType().GetMethod("IsInBounds", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (m != null)
        {
            var r = m.Invoke(gridBoard, new object[] { cell });
            if (r is bool b) return b;
        }
        return true; // bounds 정보 없으면 그냥 보여줌
    }

    private static float TryReadCellSize(GridBoard board, float fallback)
    {
        if (board == null) return fallback;

        var t = board.GetType();
        var f = t.GetField("cellSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float))
            return (float)f.GetValue(board);

        var p = t.GetProperty("cellSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(float))
            return (float)p.GetValue(board);

        return fallback;
    }
}
