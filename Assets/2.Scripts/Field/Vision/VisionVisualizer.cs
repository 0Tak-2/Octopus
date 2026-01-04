using System.Collections.Generic;
using UnityEngine;

public class VisionVisualizer : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard grid;
    public PlayerGridMover player;
    public PlayerCrouch crouch;

    [Header("Vision")]
    [Min(0)] public int baseVisionRange = 3;
    public int crouchBonusRange = 1;

    public bool useChebyshev = true;
    public bool show = true;
    public KeyCode toggleKey = KeyCode.V;

    [Header("Render")]
    public int sortingOrder = 50;
    public float alpha = 0.18f;

    private readonly Dictionary<Vector2Int, SpriteRenderer> _tiles = new();
    private Sprite _tileSprite;

    private Vector2Int _lastCenter = new(int.MinValue, int.MinValue);
    private int _lastRange = -999;
    private bool _lastMode;
    private bool _lastCrouch;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (player == null) player = FindObjectOfType<PlayerGridMover>();
        if (crouch == null) crouch = FindObjectOfType<PlayerCrouch>();

        _tileSprite = Create1x1Sprite();
        _lastMode = useChebyshev;
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

        bool isCrouching = crouch != null && crouch.IsCrouching;
        int range = GetEffectiveRange(isCrouching);

        if (center != _lastCenter || range != _lastRange || useChebyshev != _lastMode || isCrouching != _lastCrouch)
        {
            _lastCenter = center;
            _lastRange = range;
            _lastMode = useChebyshev;
            _lastCrouch = isCrouching;

            Redraw(center, range);
        }
    }

    private int GetEffectiveRange(bool isCrouching)
    {
        int r = baseVisionRange;
        if (isCrouching) r += crouchBonusRange;
        return Mathf.Max(0, r);
    }

    private void Redraw(Vector2Int center, int range)
    {
        HashSet<Vector2Int> visible = new HashSet<Vector2Int>();

        if (grid.InBounds(center))
        {
            visible.Add(center);
            EnsureTile(center);
        }

        for (int dx = -range; dx <= range; dx++)
        {
            for (int dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                bool inRange = useChebyshev
                    ? (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) <= range)
                    : (Mathf.Abs(dx) + Mathf.Abs(dy) <= range);

                if (!inRange) continue;

                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dy);
                if (!grid.InBounds(cell)) continue;

                if (HasLineOfSight(center, cell))
                {
                    visible.Add(cell);
                    EnsureTile(cell);
                }
            }
        }

        foreach (var kv in _tiles)
        {
            bool active = visible.Contains(kv.Key);
            kv.Value.gameObject.SetActive(active && show);
        }
    }

    private bool HasLineOfSight(Vector2Int center, Vector2Int target)
    {
        foreach (var p in BresenhamLine(center, target))
        {
            if (p == center) continue;

            if (p == target) return true;

            if (grid.IsVisionBlocked(p))
                return false;
        }
        return true;
    }

    private IEnumerable<Vector2Int> BresenhamLine(Vector2Int a, Vector2Int b)
    {
        int x0 = a.x;
        int y0 = a.y;
        int x1 = b.x;
        int y1 = b.y;

        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);

        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;

        while (true)
        {
            yield return new Vector2Int(x0, y0);
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
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
