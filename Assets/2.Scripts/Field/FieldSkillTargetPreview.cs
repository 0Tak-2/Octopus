using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FieldSkillTargetPreview : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public GridBoard gridBoard;
    public FieldSkillCaster caster;

    [Header("Preview Rules")]
    public bool requireLoS = true;
    public bool hideIfOutOfRange = true;
    public bool hideIfSkillOnCooldown = true;
    public bool showOnlyWhenArmed = true;

    [Header("Visual")]
    public Sprite highlightSprite;
    public float zOffset = -0.11f;
    public int sortingOrder = 1000;
    public Vector2 scaleMultiplier = Vector2.one;

    [Header("Colors")]
    public Color centerColor = new Color(1f, 0.25f, 0.25f, 0.75f);
    public Color areaColor = new Color(1f, 0.25f, 0.25f, 0.35f);
    public Color buffColor = new Color(0.3f, 0.8f, 1f, 0.5f);

    [Header("Refresh")]
    public float refreshInterval = 0.03f;

    private readonly List<GameObject> _pool = new();
    private readonly List<GameObject> _active = new();

    private float _nextRefresh;
    private Vector2Int _lastMouseCell;
    private int _lastSlot = -1;
    private CombatAttackDefinition _lastDef;
    private int _lastCd = -999;
    private bool _lastArmed;

    private Camera _cam;

    private void Awake()
    {
        if (player == null) player = FindObjectOfType<FieldSkillCaster>()?.transform ?? GameObject.FindGameObjectWithTag("Player")?.transform;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (caster == null) caster = FindObjectOfType<FieldSkillCaster>();
        _cam = Camera.main;

        WarmPool(32);
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
        if (Time.time < _nextRefresh) return;
        _nextRefresh = Time.time + refreshInterval;

        if (player == null || gridBoard == null || caster == null) return;

        bool armed = caster.IsArmed;
        int slot = caster.SelectedSlot;
        var def = caster.GetDefinition(slot);
        int cd = caster.GetCooldownRemaining(slot);

        if (showOnlyWhenArmed && !armed)
        {
            ClearActive();
            return;
        }

        if (def == null)
        {
            ClearActive();
            return;
        }

        if (hideIfSkillOnCooldown && cd > 0)
        {
            ClearActive();
            return;
        }

        if (!TryGetMouseCell(out Vector2Int mouseCell))
        {
            ClearActive();
            return;
        }

        bool changed = (mouseCell != _lastMouseCell) || (slot != _lastSlot) || (def != _lastDef) || (cd != _lastCd) || (armed != _lastArmed);
        if (!changed) return;

        _lastMouseCell = mouseCell;
        _lastSlot = slot;
        _lastDef = def;
        _lastCd = cd;
        _lastArmed = armed;

        Redraw(def, mouseCell);
    }

    private void ForceRefresh()
    {
        _lastSlot = -999;
        _lastDef = null;
        _lastCd = -999;
        _lastArmed = caster != null && caster.IsArmed;
        _nextRefresh = 0f;
    }

    private void Redraw(CombatAttackDefinition def, Vector2Int mouseCell)
    {
        ClearActive();

        Vector2Int pCell = gridBoard.WorldToCell(player.position);

        // 타겟팅 스킬: 마우스 셀에 적이 있어야만 표시
        if (def.requireEnemyOnTarget)
        {
            if (!HasEnemyOnCell(mouseCell))
                return;
        }

        int dist = FieldCombatUtils.Chebyshev(pCell, mouseCell);
        if (hideIfOutOfRange && dist > def.range) return;

        if (requireLoS && !FieldCombatUtils.HasLineOfSight(gridBoard, pCell, mouseCell)) return;

        List<Vector2Int> affected = GetAffectedCells(def.pattern, mouseCell);

        for (int i = 0; i < affected.Count; i++)
        {
            bool isCenter = affected[i] == mouseCell;

            Color c;
            if (def.kind == CombatSkillKind.Buff) c = buffColor;
            else c = isCenter ? centerColor : areaColor;

            SpawnHighlight(affected[i], c);
        }
    }

    private List<Vector2Int> GetAffectedCells(AttackPattern pattern, Vector2Int center)
    {
        switch (pattern)
        {
            case AttackPattern.CrossPlus:
                return new List<Vector2Int>
                {
                    center,
                    center + Vector2Int.up,
                    center + Vector2Int.down,
                    center + Vector2Int.left,
                    center + Vector2Int.right
                };

            case AttackPattern.CrossX:
                return new List<Vector2Int>
                {
                    center,
                    center + new Vector2Int(1,1),
                    center + new Vector2Int(1,-1),
                    center + new Vector2Int(-1,1),
                    center + new Vector2Int(-1,-1)
                };

            default:
                return new List<Vector2Int> { center };
        }
    }

    private bool HasEnemyOnCell(Vector2Int cell)
    {
        if (caster == null || gridBoard == null) return false;

        // caster의 셀 탐색 반경/레이어를 그대로 사용하기 위해
        // caster에 공개 getter가 없으니 동일 방식으로 구현(필요하면 caster에 public getter로 빼도 됨)
        Vector3 w = gridBoard.CellToWorld(cell);
        Vector2 wp = new Vector2(w.x, w.y);

        Collider2D[] hits = Physics2D.OverlapCircleAll(wp, caster.cellOverlapRadius, caster.enemyQueryLayerMask);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var inst = hits[i].GetComponentInParent<EnemyInstance>();
            if (inst != null && inst.currentHP > 0) return true;
        }
        return false;
    }

    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || gridBoard == null) return false;

        Vector3 w = _cam.ScreenToWorldPoint(Input.mousePosition);
        w.z = 0f;
        cell = gridBoard.WorldToCell(w);
        return true;
    }

    // ---- pool/visual ----
    private void WarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = CreateTile();
            go.SetActive(false);
            _pool.Add(go);
        }
    }

    private GameObject CreateTile()
    {
        var go = new GameObject("SkillTargetPreviewTile");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = highlightSprite;
        sr.color = areaColor;
        sr.sortingOrder = sortingOrder;

        float cellSize = TryReadCellSize(gridBoard, 1f);
        go.transform.localScale = new Vector3(cellSize * scaleMultiplier.x, cellSize * scaleMultiplier.y, 1f);

        // 안전장치: 콜라이더 절대 금지
        var col2D = go.GetComponent<Collider2D>();
        if (col2D != null) Destroy(col2D);
        var col3D = go.GetComponent<Collider>();
        if (col3D != null) Destroy(col3D);

        return go;
    }

    private GameObject GetFromPool()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].activeSelf) return _pool[i];

        var go = CreateTile();
        _pool.Add(go);
        return go;
    }

    private void SpawnHighlight(Vector2Int cell, Color color)
    {
        var go = GetFromPool();
        go.transform.position = gridBoard.CellToWorld(cell) + new Vector3(0f, 0f, zOffset);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = color;

        go.SetActive(true);
        _active.Add(go);
    }

    private void ClearActive()
    {
        for (int i = 0; i < _active.Count; i++)
            _active[i].SetActive(false);
        _active.Clear();
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
