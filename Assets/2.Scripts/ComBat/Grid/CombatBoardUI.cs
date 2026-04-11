using System;
using UnityEngine;
using UnityEngine.UI;

public class CombatBoardUI : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform boardRoot;
    public GridLayoutGroup grid;

    [Header("Token Visual")]
    [Tooltip("토큰을 타일보다 얼마나 작게 보이게 할지(px). 값이 클수록 더 작아짐.")]
    public float tokenPadding = 12f;

    [Header("Tile Prefab")]
    public Image tilePrefab;

    [Header("Overlay Sprite (Full Rect)")]
    [Tooltip("하이라이트/스페셜 오버레이는 타일 모양과 상관없이 '전체 덮기'용 사각 스프라이트를 사용합니다. (권장: Unity 기본 UISprite/White square)")]
    public Sprite overlayFillSprite;

    [Header("Runtime")]
    public int width;
    public int height;

    public Action<int, int> OnTileClicked;
    public Action<int, int> OnTileHovered;
    public Action<int, int> OnTileUnhovered;

    private RectTransform[,] _tiles;
    private Image[,] _tileImages;      // 바닥(지형색)
    private Color[,] _baseColors;      // 바닥 기본색

    // ✅ 지속 표식(도망타일 등) + 하이라이트(이동/프리뷰)
    private Image[,] _specialImages;
    private Image[,] _highlightImages;

    [Header("Highlight Colors (Overlay)")]
    public Color targetGreen = new Color(0f, 1f, 0f, 0.35f);
    public Color targetRed = new Color(1f, 0f, 0f, 0.35f);
    public Color previewGreen = new Color(0f, 1f, 0f, 0.55f);

    [Header("Special Colors")]
    public Color escapeTileColor = new Color(1f, 0.80f, 0.15f, 0.55f);

    private static readonly Color ClearColor = new Color(0, 0, 0, 0);

    private void Awake()
    {
        if (boardRoot == null) boardRoot = GetComponentInChildren<RectTransform>(true);
        if (grid == null) grid = GetComponentInChildren<GridLayoutGroup>(true);
    }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public void Build(int w, int h)
    {
        width = w;
        height = h;

        Width = width;
        Height = height;

        if (boardRoot == null) boardRoot = GetComponentInChildren<RectTransform>(true);
        if (grid == null) grid = GetComponentInChildren<GridLayoutGroup>(true);

        Clear();

        if (tilePrefab == null)
        {
            Debug.LogError("[CombatBoardUI] tilePrefab is missing!");
            return;
        }

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = width;
        grid.childAlignment = TextAnchor.MiddleCenter;

        _tiles = new RectTransform[width, height];
        _tileImages = new Image[width, height];
        _baseColors = new Color[width, height];

        _specialImages = new Image[width, height];
        _highlightImages = new Image[width, height];

        for (int genY = 0; genY < height; genY++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, grid.transform);
                tile.name = $"Tile_{x}_{genY}";
                tile.raycastTarget = true;

                int y = (height - 1) - genY;

                _tiles[x, y] = tile.rectTransform;
                _tiles[x, y].localScale = Vector3.one;

                _tileImages[x, y] = tile;
                _baseColors[x, y] = tile.color;

                // ✅ 2중 오버레이 생성: Special(지속) + Highlight(하이라이트)
                CreateOverlays(tile, out _specialImages[x, y], out _highlightImages[x, y]);

                // 클릭
                var btn = tile.GetComponent<Button>();
                if (btn == null) btn = tile.gameObject.AddComponent<Button>();
                btn.targetGraphic = tile;

                int cx = x;
                int cy = y;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTileClicked?.Invoke(cx, cy));

                // 호버
                var hover = tile.GetComponent<CombatTileInput>();
                if (hover == null) hover = tile.gameObject.AddComponent<CombatTileInput>();
                hover.board = this;
                hover.x = cx;
                hover.y = cy;
            }
        }

        AutoSizeAndCenterBoard();
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid.GetComponent<RectTransform>());
    }

    private void CreateOverlays(Image tile, out Image special, out Image highlight)
    {
        special = CreateOverlayImage(tile, "SpecialOverlay");
        highlight = CreateOverlayImage(tile, "HighlightOverlay");

        // ✅ 순서 고정: Special(가장 아래) -> Highlight -> Token(맨 위)
        special.color = ClearColor;
        highlight.color = ClearColor;

        special.transform.SetAsFirstSibling();     // index 0
        highlight.transform.SetSiblingIndex(1);    // index 1
    }

    private Image CreateOverlayImage(Image tile, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(tile.transform, worldPositionStays: false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;

        // ✅ 전체 덮기 스프라이트 사용(없으면 tile.sprite fallback)
        img.sprite = (overlayFillSprite != null) ? overlayFillSprite : tile.sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;
        img.color = ClearColor;

        return img;
    }

    private void FitTokenToTile(CombatUnitToken token)
    {
        if (token == null) return;

        var rt = token.GetComponent<RectTransform>();
        if (rt == null) return;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.offsetMin = new Vector2(tokenPadding, tokenPadding);
        rt.offsetMax = new Vector2(-tokenPadding, -tokenPadding);

        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }

    public void ApplyTerrainVisual(CombatGridData data)
    {
        if (data == null) return;
        if (_tileImages == null || _baseColors == null) return;
        if (data.width != width || data.height != height) return;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                CombatTileType t = data.Get(x, y);

                Color c = Color.white;
                switch (t)
                {
                    case CombatTileType.Empty: c = Color.white; break;
                    case CombatTileType.Wall: c = new Color(0.18f, 0.18f, 0.18f, 1f); break;
                    case CombatTileType.Seaweed: c = new Color(0.20f, 0.80f, 0.40f, 1f); break;
                    case CombatTileType.Hill: c = new Color(0.75f, 0.65f, 0.20f, 1f); break;
                    case CombatTileType.Current: c = new Color(0.20f, 0.60f, 0.95f, 1f); break;
                }

                var img = _tileImages[x, y];
                if (img == null) continue;

                img.color = c;
                _baseColors[x, y] = c;

                // ✅ 오버레이 초기화 + 순서 고정
                if (_highlightImages != null && _highlightImages[x, y] != null)
                    _highlightImages[x, y].color = ClearColor;

                if (_specialImages != null && _specialImages[x, y] != null)
                    _specialImages[x, y].color = ClearColor;

                EnforceChildOrder(x, y);
            }
    }

    private void EnforceChildOrder(int x, int y)
    {
        if (!InBounds(x, y)) return;
        var tile = GetTile(x, y);
        if (tile == null) return;

        var sp = (_specialImages != null) ? _specialImages[x, y] : null;
        var hi = (_highlightImages != null) ? _highlightImages[x, y] : null;

        if (sp != null) sp.transform.SetAsFirstSibling();
        if (hi != null) hi.transform.SetSiblingIndex(1);
        // token은 Place/Move에서 last로 올림
    }

    private void AutoSizeAndCenterBoard()
    {
        if (boardRoot == null || grid == null) return;

        var padding = grid.padding;
        var cell = grid.cellSize;
        var sp = grid.spacing;

        float totalW = padding.left + padding.right + (cell.x * width) + (sp.x * (width - 1));
        float totalH = padding.top + padding.bottom + (cell.y * height) + (sp.y * (height - 1));

        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        boardRoot.pivot = new Vector2(0.5f, 0.5f);
        boardRoot.anchoredPosition = Vector2.zero;
        boardRoot.anchoredPosition3D = Vector3.zero;
        boardRoot.localPosition = Vector3.zero;
        boardRoot.localScale = Vector3.one;
        boardRoot.sizeDelta = new Vector2(totalW, totalH);

        var gridRT = grid.GetComponent<RectTransform>();
        gridRT.anchorMin = Vector2.zero;
        gridRT.anchorMax = Vector2.one;
        gridRT.offsetMin = Vector2.zero;
        gridRT.offsetMax = Vector2.zero;
        gridRT.localScale = Vector3.one;
    }

    public void Clear()
    {
        if (grid == null) return;

        for (int i = grid.transform.childCount - 1; i >= 0; i--)
            Destroy(grid.transform.GetChild(i).gameObject);

        _tiles = null;
        _tileImages = null;
        _baseColors = null;
        _specialImages = null;
        _highlightImages = null;
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public RectTransform GetTile(int x, int y)
    {
        if (_tiles == null) return null;
        if (!InBounds(x, y)) return null;
        return _tiles[x, y];
    }

    public Vector3 GetTileWorldCenter(int x, int y)
    {
        var t = GetTile(x, y);
        if (t == null) return Vector3.zero;
        return t.position;
    }

    // ===== Hover bridge =====
    public void NotifyTileHovered(int x, int y) => OnTileHovered?.Invoke(x, y);
    public void NotifyTileUnhovered(int x, int y) => OnTileUnhovered?.Invoke(x, y);

    // ===== Highlight =====
    public void ClearHighlights()
    {
        if (_highlightImages == null) return;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var ov = _highlightImages[x, y];
                if (ov == null) continue;
                ov.color = ClearColor;
            }
    }

    private void SetHighlightOverlay(int x, int y, bool on, Color c)
    {
        if (!InBounds(x, y)) return;
        if (_highlightImages == null) return;

        var ov = _highlightImages[x, y];
        if (ov == null) return;

        ov.color = on ? c : ClearColor;
    }

    public void SetHighlight(int x, int y, bool on) => SetHighlightOverlay(x, y, on, targetGreen);
    public void SetPreviewHighlight(int x, int y, bool on) => SetHighlightOverlay(x, y, on, previewGreen);
    public void SetInvalidTarget(int x, int y) => SetHighlightOverlay(x, y, true, targetRed);

    // ===== Special (Persistent) =====
    public void ClearSpecials()
    {
        if (_specialImages == null) return;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var sp = _specialImages[x, y];
                if (sp == null) continue;
                sp.color = ClearColor;
            }
    }

    public void SetEscapeTile(int x, int y, bool on)
    {
        if (!InBounds(x, y)) return;
        if (_specialImages == null) return;

        var sp = _specialImages[x, y];
        if (sp == null) return;

        sp.color = on ? escapeTileColor : ClearColor;
    }

    // ===== Token API =====
    public CombatUnitToken PlaceToken(CombatUnitToken tokenPrefab, int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null || tokenPrefab == null) return null;

        ClearTokenAt(x, y);

        var token = Instantiate(tokenPrefab, tile);
        token.name = $"{tokenPrefab.unitType}_Token_{x}_{y}";

        FitTokenToTile(token);

        // ✅ 토큰은 항상 맨 위
        token.transform.SetAsLastSibling();

        // ✅ 오버레이 순서 고정
        EnforceChildOrder(x, y);

        return token;
    }

    public void ClearTokenAt(int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null) return;

        for (int i = tile.childCount - 1; i >= 0; i--)
        {
            string n = tile.GetChild(i).name;
            if (n == "SpecialOverlay") continue;
            if (n == "HighlightOverlay") continue;
            Destroy(tile.GetChild(i).gameObject);
        }

        EnforceChildOrder(x, y);
    }

    public void MoveExistingToken(CombatUnitToken tokenInstance, int toX, int toY)
    {
        if (tokenInstance == null) return;

        var targetTile = GetTile(toX, toY);
        if (targetTile == null) return;

        tokenInstance.transform.SetParent(targetTile, worldPositionStays: false);
        FitTokenToTile(tokenInstance);

        tokenInstance.name = $"{tokenInstance.unitType}_Token_{toX}_{toY}";

        // ✅ 토큰은 항상 맨 위
        tokenInstance.transform.SetAsLastSibling();

        EnforceChildOrder(toX, toY);
    }

    /// <summary>
    /// 특정 셀에 배치된 토큰 찾기 (excludeToken 제외). 토큰은 타일의 자식으로 둔다.
    /// </summary>
    public CombatUnitToken FindTokenAtCell(int x, int y, CombatUnitToken excludeToken)
    {
        var tile = GetTile(x, y);
        if (tile == null) return null;

        for (int i = 0; i < tile.childCount; i++)
        {
            var child = tile.GetChild(i);
            string n = child.name;
            if (n == "SpecialOverlay" || n == "HighlightOverlay") continue;

            var tok = child.GetComponent<CombatUnitToken>();
            if (tok == null) continue;
            if (tok == excludeToken) continue;
            return tok;
        }

        return null;
    }

    /// <summary>보드에서 토큰 인스턴스 제거 (다중 적 사망 시)</summary>
    public void RemoveToken(CombatUnitToken token)
    {
        if (token == null) return;
        Destroy(token.gameObject);
    }
}
