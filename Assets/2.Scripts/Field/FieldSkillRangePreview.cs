using System.Collections.Generic;
using UnityEngine;

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
    public bool showOnlyWhenArmed = true;

    [Header("Visual")]
    public Sprite highlightSprite;
    public float zOffset = -0.1f;
    public Vector2 scaleMultiplier = Vector2.one;

    [Header("Pooling")]
    public int initialPoolSize = 128;

    [Header("Refresh")]
    public float refreshInterval = 0.08f;
    private float _nextRefreshTime = 0f;

    private readonly List<GameObject> _pool = new();
    private readonly List<GameObject> _active = new();

    private Vector2Int _lastPlayerCell;
    private int _lastSlot = -1;
    private int _lastCooldown = -999;
    private CombatAttackDefinition _lastDef;
    private bool _lastArmed;

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
            caster.OnArmedChanged += _ => ForceRefresh();
        }
        ForceRefresh();
    }

    private void OnDisable()
    {
        if (caster != null)
        {
            caster.OnSelectedSlotChanged -= _ => ForceRefresh();
            caster.OnCooldownChanged -= () => ForceRefresh();
            caster.OnArmedChanged -= _ => ForceRefresh();
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
        bool armed = caster.IsArmed;

        bool changed =
            pCell != _lastPlayerCell ||
            slot != _lastSlot ||
            cd != _lastCooldown ||
            def != _lastDef ||
            armed != _lastArmed;

        if (!changed) return;

        _lastPlayerCell = pCell;
        _lastSlot = slot;
        _lastCooldown = cd;
        _lastDef = def;
        _lastArmed = armed;

        Redraw();
    }

    private void ForceRefresh()
    {
        _lastSlot = -999;
        _lastCooldown = -999;
        _lastDef = null;
        _lastArmed = !caster ? false : caster.IsArmed;
        _nextRefreshTime = 0f;
    }

    private void Redraw()
    {
        ClearActive();

        if (caster == null || player == null || gridBoard == null) return;

        if (showOnlyWhenArmed && !caster.IsArmed)
            return;

        int slot = caster.SelectedSlot;
        CombatAttackDefinition def = caster.GetDefinition(slot);

        if (def == null && hideWhenNoSkillSelected)
            return;

        if (def == null)
            return;

        int cd = caster.GetCooldownRemaining(slot);
        if (showOnlyWhenSkillReady && cd > 0)
            return;

        Vector2Int pCell = gridBoard.WorldToCell(player.position);
        int range = Mathf.Max(0, def.range);

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                Vector2Int c = new Vector2Int(pCell.x + dx, pCell.y + dy);
                if (FieldCombatUtils.Chebyshev(pCell, c) > range) continue;

                if (!IsInBoundsIfPossible(c)) continue;

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
        sr.color = new Color(1f, 1f, 1f, 0.25f);
        sr.sortingOrder = 999;

        float cellSize = TryReadCellSize(gridBoard, fallback: 1f);
        go.transform.localScale = new Vector3(cellSize * scaleMultiplier.x, cellSize * scaleMultiplier.y, 1f);

        // 안전장치: 콜라이더 절대 금지
        var col2D = go.GetComponent<Collider2D>();
        if (col2D != null) Destroy(col2D);
        var col3D = go.GetComponent<Collider>();
        if (col3D != null) Destroy(col3D);

        return go;
    }

    private void ClearActive()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].SetActive(false);
        _active.Clear();
    }

    private bool IsInBoundsIfPossible(Vector2Int cell)
    {
        var m = gridBoard.GetType().GetMethod("IsInBounds",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

        if (m != null)
        {
            var r = m.Invoke(gridBoard, new object[] { cell });
            if (r is bool b) return b;
        }
        return true;
    }

    private static float TryReadCellSize(GridBoard board, float fallback)
    {
        if (board == null) return fallback;

        var t = board.GetType();
        var f = t.GetField("cellSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (f != null && f.FieldType == typeof(float))
            return (float)f.GetValue(board);

        var p = t.GetProperty("cellSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
        if (p != null && p.PropertyType == typeof(float))
            return (float)p.GetValue(board);

        return fallback;
    }
}
