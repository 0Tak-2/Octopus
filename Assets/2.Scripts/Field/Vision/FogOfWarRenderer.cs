using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3???? ?????? ??? ?????.
/// - ?????: ???? ????
/// - ??? ???(???): ??????? ???? ????
/// - ???? ????: ??? ????
/// ???? ???? + ???? ???? + ??? ?????? ?????? ??? ????.
/// FieldVisionSystem?? LoS ??? ????? ???.
/// ???? ??? ????? ???? ???? ???? (???? ????? ?? ????).
/// </summary>
public class FogOfWarRenderer : MonoBehaviour
{
    public static FogOfWarRenderer Instance { get; private set; }

    // ?? ????? ??? ??? (???? ???)
    private static readonly Dictionary<string, HashSet<Vector2Int>> _exploredBySceneName
        = new Dictionary<string, HashSet<Vector2Int>>();

    [Header("Refs")]
    public GridBoard grid;
    public PlayerGridMover player;

    [Header("Fog Colors (alpha = 0:???? ~ 1:??????)")]
    public Color visibleColor = new Color(0f, 0f, 0f, 0.0f);
    public Color exploredColor = new Color(0f, 0f, 0f, 0.55f);
    public Color unexploredColor = new Color(0f, 0f, 0f, 1.0f);

    [Header("Smoothing")]
    [Tooltip("???? ??? ???. 1=???? 1???, 2=???? 2x2???, 4=4x4. ???????? ?? ??????")]
    [Range(1, 8)] public int textureScale = 4;
    [Tooltip("??? ???? ???. ???????? ?? ??????? ???? ??? ?????")]
    [Range(0, 5)] public int blurPasses = 2;

    [Header("Sorting")]
    public int sortingOrder = 100;
    public string sortingLayerName = "Default";

    [Header("Enemy Hiding")]
    [Tooltip("???? ?? ???? SpriteRenderer ??????")]
    public bool hideEnemiesOutsideVision = true;

    [Tooltip("????? ???? ?????/??????/??? ??????? ???? (??? ??? ???? ????)")]
    public bool hideObjectsInUnexplored = true;

    [Header("Debug")]
    public bool showDebugLogs = false;

    private Texture2D _fogTex;
    private GameObject _fogQuad;
    private SpriteRenderer _fogSR;
    private readonly HashSet<Vector2Int> _explored = new();
    private Vector2Int _lastPlayerCell = new(int.MinValue, int.MinValue);
    private bool _lastCrouchState;
    private bool _initialized;
    private int _texW, _texH;
    private PlayerCrouch _playerCrouch;

    private static readonly Vector2Int[] NeighborDirs8 = new Vector2Int[]
{
    new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1),
    new Vector2Int(-1,  0),                        new Vector2Int(1,  0),
    new Vector2Int(-1,  1), new Vector2Int(0,  1), new Vector2Int(1,  1),
};
    private void Awake()
    {
        Instance = this;
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (player == null) player = FindObjectOfType<PlayerGridMover>();
    }

    private void Start()
    {
        FieldVisionSystem.EnsureInstance();

        if (grid == null)
        {
            Debug.LogError("[FogOfWar] GridBoard?? ??? ?? ???????.");
            return;
        }

        CreateFogTexture();
        CreateFogQuad();

        // ???? ??? ??? ????
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (_exploredBySceneName.TryGetValue(sceneName, out var saved))
        {
            _explored.UnionWith(saved);
            if (showDebugLogs)
                Debug.Log($"[FogOfWar] ?? '{sceneName}'?? ??? ??? {saved.Count}?? ????");
        }

        _playerCrouch = FindObjectOfType<PlayerCrouch>();
        if (_playerCrouch != null)
        {
            _lastCrouchState = _playerCrouch.IsCrouching;
            _playerCrouch.OnCrouchChanged += OnCrouchChanged;
        }

        _initialized = true;
        ForceRefresh();
    }

    private void OnCrouchChanged(bool crouching)
    {
        Debug.Log($"[FogOfWar] ??????? ???? ?? {crouching}, ??? ??? ????");
        ForceRefresh();
    }

    private void OnDestroy()
    {
        // ???? ??? ??? ????
        if (_explored.Count > 0)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            _exploredBySceneName[sceneName] = new HashSet<Vector2Int>(_explored);
            if (showDebugLogs)
                Debug.Log($"[FogOfWar] ?? '{sceneName}'?? ??? ??? {_explored.Count}?? ????");
        }

        if (_playerCrouch != null)
            _playerCrouch.OnCrouchChanged -= OnCrouchChanged;
        if (Instance == this) Instance = null;
        if (_fogTex != null) Destroy(_fogTex);
    }

    private void Update()
    {
        if (!_initialized || player == null) return;

        Vector2Int currentCell = player.CurrentCell;
        bool crouching = _playerCrouch != null && _playerCrouch.IsCrouching;

        if (currentCell != _lastPlayerCell || crouching != _lastCrouchState)
        {
            _lastPlayerCell = currentCell;
            _lastCrouchState = crouching;
            RefreshFog(currentCell);
        }
    }

    /// <summary>??????? ???? ???? (?? ????, ?? ???? ??)</summary>
    public void ForceRefresh()
    {
        if (player == null) return;
        _lastPlayerCell = player.CurrentCell;
        RefreshFog(_lastPlayerCell);
    }

    // ============================================================
    // ???? ????
    // ============================================================

    private void CreateFogTexture()
    {
        _texW = grid.width * textureScale;
        _texH = grid.height * textureScale;

        _fogTex = new Texture2D(_texW, _texH, TextureFormat.RGBA32, false);
        _fogTex.filterMode = FilterMode.Bilinear;
        _fogTex.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[_texW * _texH];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = unexploredColor;
        _fogTex.SetPixels(pixels);
        _fogTex.Apply();

        if (showDebugLogs)
            Debug.Log($"[FogOfWar] ???? ????: {_texW}x{_texH} (????? {grid.width}x{grid.height} ?? scale {textureScale})");
    }

    private void CreateFogQuad()
    {
        _fogQuad = new GameObject("FogQuad");
        _fogQuad.transform.SetParent(transform, false);

        _fogSR = _fogQuad.AddComponent<SpriteRenderer>();

        var sprite = Sprite.Create(
            _fogTex,
            new Rect(0, 0, _texW, _texH),
            new Vector2(0f, 0f),
            textureScale
        );
        _fogSR.sprite = sprite;
        _fogSR.sortingOrder = sortingOrder;
        if (!string.IsNullOrEmpty(sortingLayerName))
            _fogSR.sortingLayerName = sortingLayerName;

        Vector3 cell00Center = grid.CellToWorld(Vector2Int.zero);
        _fogQuad.transform.position = cell00Center - new Vector3(grid.cellSize * 0.5f, grid.cellSize * 0.5f, 0f);
        _fogQuad.transform.localScale = new Vector3(grid.cellSize, grid.cellSize, 1f);
    }

    // ============================================================
    // ??? ????
    // ============================================================

    private void RefreshFog(Vector2Int playerCell)
    {
        var visionSys = FieldVisionSystem.Instance;
        if (visionSys == null) return;

        HashSet<Vector2Int> visible = visionSys.GetPlayerVisibleCells(playerCell);

        // ???? ?? ???? ???? ???? ???
        foreach (var c in visible) _explored.Add(c);

        Color[] pixels = new Color[_texW * _texH];

        for (int gx = 0; gx < grid.width; gx++)
        {
            for (int gy = 0; gy < grid.height; gy++)
            {
                Vector2Int cell = new Vector2Int(gx, gy);
                Color cellColor;

                if (visible.Contains(cell))
                    cellColor = visibleColor;
                else if (_explored.Contains(cell))
                    cellColor = exploredColor;
                else
                    cellColor = unexploredColor;

                int px0 = gx * textureScale;
                int py0 = gy * textureScale;
                for (int dx = 0; dx < textureScale; dx++)
                {
                    for (int dy = 0; dy < textureScale; dy++)
                    {
                        int idx = (py0 + dy) * _texW + (px0 + dx);
                        pixels[idx] = cellColor;
                    }
                }
            }
        }

        for (int i = 0; i < blurPasses; i++)
            pixels = BoxBlur(pixels, _texW, _texH);

        _fogTex.SetPixels(pixels);
        _fogTex.Apply();

        if (hideEnemiesOutsideVision)
            RefreshEnemyVisibility(visible);

        if (hideObjectsInUnexplored)
            RefreshObjectVisibility();
    }

    private static Color[] BoxBlur(Color[] src, int w, int h)
    {
        Color[] dst = new Color[w * h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                float r = 0, g = 0, b = 0, a = 0;
                int count = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= w) continue;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        var c = src[ny * w + nx];
                        r += c.r; g += c.g; b += c.b; a += c.a;
                        count++;
                    }
                }
                float inv = 1f / count;
                dst[y * w + x] = new Color(r * inv, g * inv, b * inv, a * inv);
            }
        }
        return dst;
    }

    private void RefreshEnemyVisibility(HashSet<Vector2Int> visible)
    {
        var enemies = FindObjectsOfType<EnemyInstance>();
        var occ = GridOccupancyRegistry.Instance;
        foreach (var enemy in enemies)
        {
            if (enemy == null) continue;
            // 이동 중 transform은 셀 경계 근처라 WorldToCell이 논리 격자(점유)와 어긋날 수 있음.
            Vector2Int ec;
            if (occ != null && occ.TryGetCurrentCell(enemy.transform, out ec))
            { }
            else
                ec = grid.WorldToCell(enemy.transform.position);
            bool show = visible.Contains(ec);
            ToggleRenderers(enemy.gameObject, show);
        }
    }

    private void RefreshObjectVisibility()
    {
        // ????? (????, ?????, ??? ??) - ?? ????? ???? ????? ????
        var harvestables = FindObjectsOfType<Harvestable>();
        foreach (var h in harvestables)
        {
            if (h == null) continue;
            Vector2Int cell = grid.WorldToCell(h.transform.position);
            bool show = _explored.Contains(cell);
            ToggleRenderers(h.gameObject, show);
        }

        // ???? ??????
        var drops = FindObjectsOfType<DroppedItem>();
        foreach (var d in drops)
        {
            if (d == null) continue;
            Vector2Int cell = grid.WorldToCell(d.transform.position);
            bool show = _explored.Contains(cell);
            ToggleRenderers(d.gameObject, show);
        }

        // ?? (GridCellBlocker)
        var blockers = FindObjectsOfType<GridCellBlocker>();
        foreach (var b in blockers)
        {
            if (b == null) continue;
            Vector2Int cell = grid.WorldToCell(b.transform.position);

            bool show = false;
            foreach (var dir in NeighborDirs8)
            {
                Vector2Int neighbor = cell + dir;
                if (!grid.InBounds(neighbor)) continue;
                if (grid.IsVisionBlocked(neighbor)) continue; // ?? ????? ???
                if (_explored.Contains(neighbor))              // floor ????? ???????
                {
                    show = true;
                    break;
                }
            }

            ToggleRenderers(b.gameObject, show);
        }
    }

    private static void ToggleRenderers(GameObject go, bool show)
    {
        var sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in sprites)
            if (r.enabled != show) r.enabled = show;

        var texts = go.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in texts)
            if (t.enabled != show) t.enabled = show;
    }
}