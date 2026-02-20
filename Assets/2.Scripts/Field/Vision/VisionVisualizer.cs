using System.Collections.Generic;
using UnityEngine;

public class VisionVisualizer : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public PlayerGridMover player;

    [Header("Vision")]
    [Min(0)] public int baseVisionRange = 5;
    public int crouchBonusRange = 0;
    public bool show = true;
    public KeyCode toggleKey = KeyCode.V;

    [Header("Render")]
    public int sortingOrder = 50;
    public float alpha = 0.18f;

    private readonly Dictionary<Vector2Int, SpriteRenderer> _tiles = new();
    private Sprite _tileSprite;
    private Vector2Int _lastCenter = new(int.MinValue, int.MinValue);
    private int _lastRange = -999;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (player == null) player = FindObjectOfType<PlayerGridMover>();
        _tileSprite = Create1x1Sprite();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            show = !show;
            SetAllActive(show);
        }

        if (!show) return;
        if (grid == null || player == null) return;

        Vector2Int center = player.CurrentCell;
        int range = baseVisionRange;

        if (center != _lastCenter || range != _lastRange)
        {
            _lastCenter = center;
            _lastRange = range;
            Redraw(center, range);
        }
    }

    private void Redraw(Vector2Int center, int range)
    {
        HashSet<Vector2Int> visible = new HashSet<Vector2Int>();

        var visionSys = FieldVisionSystem.Instance ?? FieldVisionSystem.EnsureInstance();
        if (visionSys != null)
        {
            visible = visionSys.GetVisibleCells(center, range);
        }
        else
        {
            // Fallback
            visible.Add(center);
            for (int dx = -range; dx <= range; dx++)
                for (int dy = -range; dy <= range; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) > range) continue;
                    Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                    if (!grid.InBounds(cell)) continue;
                    if (FieldCombatUtils.HasLineOfSight(grid, center, cell))
                        visible.Add(cell);
                }
        }

        foreach (var cell in visible)
            EnsureTile(cell);

        foreach (var kv in _tiles)
        {
            bool active = visible.Contains(kv.Key);
            kv.Value.gameObject.SetActive(active && show);
        }
    }

    private void EnsureTile(Vector2Int cell)
    {
        if (_tiles.TryGetValue(cell, out var sr))
        {
            sr.gameObject.SetActive(true);
            return;
        }

        GameObject go = new GameObject($"VisionTile_{cell.x}_{cell.y}");
        go.transform.SetParent(transform, false);
        go.transform.position = grid.CellToWorld(cell);

        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _tileSprite;
        sr.sortingOrder = sortingOrder;

        float size = grid.cellSize;
        go.transform.localScale = new Vector3(size, size, 1f);
        sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));

        _tiles[cell] = sr;
    }

    private void SetAllActive(bool active)
    {
        foreach (var kv in _tiles)
            kv.Value.gameObject.SetActive(active);
    }

    private Sprite Create1x1Sprite()
    {
        Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}