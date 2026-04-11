using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(50)]
public class VisionVisualizer : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public PlayerGridMover player;
    public PlayerCrouch playerCrouch;

    [Header("Vision")]
    [Min(0)] public int baseVisionRange = 5;
    public bool show = true;
    public KeyCode toggleKey = KeyCode.V;

    [Header("Render")]
    public int sortingOrder = 50;
    public Color normalVisionColor = new Color(1f, 1f, 1f, 0.2f);
    public Color crouchBonusVisionColor = new Color(0.2f, 0.95f, 1f, 0.58f);
    public int crouchBonusSortingOrderOffset = 2;
    [Range(1f, 1.15f)] public float crouchBonusScaleMul = 1.06f;

    private readonly Dictionary<Vector2Int, SpriteRenderer> _tiles = new();
    private Sprite _tileSprite;
    private Vector2Int _prevCenter = new(int.MinValue, int.MinValue);
    private int _prevRange = -1;
    private bool _prevCrouch;
    private bool _dirty = true;

    private void Awake()
    {
        _tileSprite = Create1x1Sprite();
    }

    private void Start()
    {
        FindAllRefs();
        if (playerCrouch != null)
        {
            playerCrouch.OnCrouchChanged -= OnCrouchToggled;
            playerCrouch.OnCrouchChanged += OnCrouchToggled;
        }
        _dirty = true;
    }

    private void OnDestroy()
    {
        if (playerCrouch != null)
            playerCrouch.OnCrouchChanged -= OnCrouchToggled;
    }

    private void FindAllRefs()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (player == null) player = FindObjectOfType<PlayerGridMover>();
        if (playerCrouch == null && player != null)
            playerCrouch = player.GetComponent<PlayerCrouch>();
        if (playerCrouch == null)
            playerCrouch = FindObjectOfType<PlayerCrouch>();

        var fvs = FieldVisionSystem.Instance;
        if (grid == null && fvs != null && fvs.gridBoard != null)
            grid = fvs.gridBoard;
    }

    private void OnCrouchToggled(bool crouching)
    {
        Debug.Log($"[VisionVisualizer] OnCrouchToggled({crouching}) received");
        _dirty = true;
        ForceRedrawNow();
    }

    /// <summary>어디서든 호출 가능한 즉시 갱신</summary>
    public void ForceRedrawNow()
    {
        FindAllRefs();
        if (grid == null || player == null) return;

        bool crouching = playerCrouch != null && playerCrouch.IsCrouching;
        int range = GetCurrentRange(crouching);
        Vector2Int center = grid.WorldToCell(player.transform.position);

        Debug.Log($"[VisionVisualizer] ForceRedrawNow center={center} range={range} crouch={crouching}");

        _prevCenter = center;
        _prevRange = range;
        _prevCrouch = crouching;
        _dirty = false;

        Redraw(center, range, crouching);
    }

    private int GetCurrentRange(bool crouching)
    {
        var fvs = FieldVisionSystem.Instance;
        if (fvs == null) return baseVisionRange;
        int r = Mathf.Max(0, fvs.playerVisionRange);
        if (crouching)
            r += Mathf.Max(0, fvs.crouchPlayerVisionBonus);
        return r;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            show = !show;
            SetAllActive(show);
            _dirty = true;
        }
    }

    private void LateUpdate()
    {
        if (!show) return;
        FindAllRefs();
        if (grid == null || player == null) return;

        bool crouching = playerCrouch != null && playerCrouch.IsCrouching;
        int range = GetCurrentRange(crouching);
        Vector2Int center = grid.WorldToCell(player.transform.position);

        bool changed = _dirty
            || center != _prevCenter
            || range != _prevRange
            || crouching != _prevCrouch;

        if (!changed) return;

        _prevCenter = center;
        _prevRange = range;
        _prevCrouch = crouching;
        _dirty = false;

        Redraw(center, range, crouching);
    }

    private void Redraw(Vector2Int center, int range, bool crouchActive)
    {
        var fvs = FieldVisionSystem.Instance;

        HashSet<Vector2Int> visible;
        HashSet<Vector2Int> baseVisible = null;

        if (fvs != null && fvs.gridBoard != null)
        {
            visible = fvs.GetVisibleCells(center, range);
            if (crouchActive)
                baseVisible = fvs.GetVisibleCells(center, fvs.GetBasePlayerVisionRange());
        }
        else
        {
            visible = new HashSet<Vector2Int> { center };
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > range) continue;
                    var cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!grid.InBounds(cell)) continue;
                    if (FieldCombatUtils.HasLineOfSight(grid, center, cell))
                        visible.Add(cell);
                }
        }

        foreach (var cell in visible)
            EnsureTile(cell);

        float baseSize = grid.cellSize;
        foreach (var cell in visible)
        {
            if (!_tiles.TryGetValue(cell, out var sr)) continue;

            bool bonus = baseVisible != null && !baseVisible.Contains(cell);
            sr.color = bonus ? crouchBonusVisionColor : normalVisionColor;
            sr.sortingOrder = bonus ? sortingOrder + crouchBonusSortingOrderOffset : sortingOrder;
            float mul = bonus ? crouchBonusScaleMul : 1f;
            sr.transform.localScale = new Vector3(baseSize * mul, baseSize * mul, 1f);
        }

        foreach (var kv in _tiles)
            kv.Value.gameObject.SetActive(visible.Contains(kv.Key) && show);
    }

    private void EnsureTile(Vector2Int cell)
    {
        if (_tiles.ContainsKey(cell)) return;

        var go = new GameObject($"VisionTile_{cell.x}_{cell.y}");
        go.transform.SetParent(transform, false);
        go.transform.position = grid.CellToWorld(cell);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _tileSprite;
        sr.sortingOrder = sortingOrder;
        sr.color = normalVisionColor;

        float size = grid.cellSize;
        go.transform.localScale = new Vector3(size, size, 1f);

        _tiles[cell] = sr;
    }

    private void SetAllActive(bool active)
    {
        foreach (var kv in _tiles)
            kv.Value.gameObject.SetActive(active);
    }

    private Sprite Create1x1Sprite()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
