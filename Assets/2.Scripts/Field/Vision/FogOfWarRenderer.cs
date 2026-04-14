using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    /// <summary>필드 맵을 새 시드로 다시 굴릴 때 탐험 안개도 초기화 (GameManager)</summary>
    public static void ClearExploredCacheForField(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        _exploredBySceneName.Remove(sceneName + "_Field");
        _exploredBySceneName.Remove(sceneName);
    }

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
    [Tooltip("FogOverlay 레이어가 있으면 자동 사용(타일맵이 타일별로 위에 그려지는 문제 방지). 없으면 여기 값 사용")]
    public string sortingLayerName = "Default";

    private const string FogOverlaySortingLayerName = "FogOverlay";

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

    private Color _defaultExploredColor;
    private Color _defaultUnexploredColor;

    /// <summary>탐험 저장 키 (씬이름_Field / 씬이름_Dungeon 등)</summary>
    private string _exploredCacheKey;

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
        _defaultExploredColor = exploredColor;
        _defaultUnexploredColor = unexploredColor;
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

        string sceneName = SceneManager.GetActiveScene().name;
        _exploredCacheKey = sceneName + "_Field";
        if (_exploredBySceneName.TryGetValue(_exploredCacheKey, out var saved))
        {
            _explored.UnionWith(saved);
            if (showDebugLogs)
                Debug.Log($"[FogOfWar] 탐험 로드: '{_exploredCacheKey}' ({saved.Count}칸)");
        }
        else if (_exploredBySceneName.TryGetValue(sceneName, out var legacy))
        {
            _explored.UnionWith(legacy);
            if (showDebugLogs)
                Debug.Log($"[FogOfWar] 레거시 키 '{sceneName}' 에서 탐험 이전 ({legacy.Count}칸)");
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
        if (_explored.Count > 0 && !string.IsNullOrEmpty(_exploredCacheKey))
        {
            _exploredBySceneName[_exploredCacheKey] = new HashSet<Vector2Int>(_explored);
            if (showDebugLogs)
                Debug.Log($"[FogOfWar] 탐험 저장: '{_exploredCacheKey}' ({_explored.Count}칸)");
        }

        if (_playerCrouch != null)
            _playerCrouch.OnCrouchChanged -= OnCrouchChanged;
        if (Instance == this) Instance = null;
        if (_fogQuad != null) Destroy(_fogQuad);
        if (_fogTex != null) Destroy(_fogTex);
    }

    /// <summary>필드 ↔ 던전 전환 시 GridBoard·안개 텍스처 재구성 (ModeManager)</summary>
    /// <param name="fogStateKey">던전: 던전ID+층 등 — 비우면 예전처럼 씬이름+접미사만 (모든 던전이 탐험 공유됨)</param>
    public void BindRuntimeGrid(GridBoard newGrid, string modeSuffix, string fogStateKey = null)
    {
        if (newGrid == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (_initialized && !string.IsNullOrEmpty(_exploredCacheKey) && _explored.Count > 0)
            _exploredBySceneName[_exploredCacheKey] = new HashSet<Vector2Int>(_explored);

        grid = newGrid;
        _exploredCacheKey = string.IsNullOrEmpty(fogStateKey)
            ? sceneName + modeSuffix
            : sceneName + modeSuffix + "_" + fogStateKey;

        unexploredColor = _defaultUnexploredColor;
        exploredColor = _defaultExploredColor;

        _explored.Clear();
        if (_exploredBySceneName.TryGetValue(_exploredCacheKey, out var saved))
            _explored.UnionWith(saved);

        if (_fogQuad != null)
        {
            Destroy(_fogQuad);
            _fogQuad = null;
            _fogSR = null;
        }

        if (_fogTex != null)
        {
            Destroy(_fogTex);
            _fogTex = null;
        }

        CreateFogTexture();
        CreateFogQuad();
        _initialized = true;
        _lastPlayerCell = new Vector2Int(int.MinValue, int.MinValue);
        ForceRefresh();
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
        ApplyFogSpriteSorting();

        // Unity Grid 셀 중심·간격과 GridBoard.width/height 로 맵 전체 AABB 를 잡는다.
        // (셀 한 칸만 기준으로 하면 회전/셀 비율/부모 스케일에서 타일맵과 어긋나 카메라 밖이 검게 보일 수 있음)
        ComputeFogQuadWorldPlacement(out Vector3 bottomLeftWorld, out Vector2 mapWorldSize);
        _fogQuad.transform.position = bottomLeftWorld;
        float nativeW = Mathf.Max(sprite.bounds.size.x, 0.0001f);
        float nativeH = Mathf.Max(sprite.bounds.size.y, 0.0001f);
        _fogQuad.transform.localScale = new Vector3(
            mapWorldSize.x / nativeW,
            mapWorldSize.y / nativeH,
            1f);
    }

    /// <summary>셀 (0,0)~(w-1,h-1) 을 덮는 축정렬 바운딩 박스. Grid 가 없으면 GridBoard 폴백.</summary>
    private void ComputeFogQuadWorldPlacement(out Vector3 bottomLeftWorld, out Vector2 mapWorldSize)
    {
        var ug = grid.GetComponent<UnityEngine.Grid>();
        int w = Mathf.Max(1, grid.width);
        int h = Mathf.Max(1, grid.height);

        if (ug != null)
        {
            Vector3 c00 = ug.GetCellCenterWorld(new Vector3Int(0, 0, 0));
            Vector3 c10 = ug.GetCellCenterWorld(new Vector3Int(1, 0, 0));
            Vector3 c01 = ug.GetCellCenterWorld(new Vector3Int(0, 1, 0));
            float halfW = Mathf.Abs(c10.x - c00.x) * 0.5f;
            float halfH = Mathf.Abs(c01.y - c00.y) * 0.5f;
            if (halfW < 0.0001f) halfW = Mathf.Abs((c10 - c00).magnitude * 0.5f);
            if (halfH < 0.0001f) halfH = Mathf.Abs((c01 - c00).magnitude * 0.5f);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            var corners = new (int x, int y)[]
            {
                (0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)
            };
            foreach (var (cx, cy) in corners)
            {
                Vector3 c = ug.GetCellCenterWorld(new Vector3Int(cx, cy, 0));
                minX = Mathf.Min(minX, c.x - halfW);
                maxX = Mathf.Max(maxX, c.x + halfW);
                minY = Mathf.Min(minY, c.y - halfH);
                maxY = Mathf.Max(maxY, c.y + halfH);
            }

            bottomLeftWorld = new Vector3(minX, minY, c00.z);
            mapWorldSize = new Vector2(Mathf.Abs(maxX - minX), Mathf.Abs(maxY - minY));
            return;
        }

        float cellW = grid.GetEffectiveCellWorldSize();
        Vector3 cell00Center = grid.CellToWorld(Vector2Int.zero);
        bottomLeftWorld = cell00Center - new Vector3(cellW * 0.5f, cellW * 0.5f, 0f);
        mapWorldSize = new Vector2(cellW * w, cellW * h);
    }

    /// <summary>
    /// Tilemap(Individual 모드)은 타일마다 정렬이 갈라져 Fog 스프라이트 한 장이 타일보다 먼저 그려질 수 있음.
    /// Default 위에 두는 전용 Sorting Layer 가 있으면 항상 그쪽에 올린다.
    /// </summary>
    private void ApplyFogSpriteSorting()
    {
        if (_fogSR == null) return;

        foreach (var sl in SortingLayer.layers)
        {
            if (sl.name == FogOverlaySortingLayerName)
            {
                _fogSR.sortingLayerID = sl.id;
                _fogSR.sortingOrder = sortingOrder;
                return;
            }
        }

        if (!string.IsNullOrEmpty(sortingLayerName))
            _fogSR.sortingLayerName = sortingLayerName;
        _fogSR.sortingOrder = sortingOrder;
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
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
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
            if (h == null || !h.gameObject.activeInHierarchy) continue;
            Vector2Int cell = grid.WorldToCell(h.transform.position);
            bool show = _explored.Contains(cell);
            ToggleRenderers(h.gameObject, show);
        }

        // ???? ??????
        var drops = FindObjectsOfType<DroppedItem>();
        foreach (var d in drops)
        {
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            Vector2Int cell = grid.WorldToCell(d.transform.position);
            bool show = _explored.Contains(cell);
            ToggleRenderers(d.gameObject, show);
        }

        // ?? (GridCellBlocker)
        var blockers = FindObjectsOfType<GridCellBlocker>();
        foreach (var b in blockers)
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
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