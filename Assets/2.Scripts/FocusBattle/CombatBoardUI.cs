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

    [Header("Runtime")]
    public int width;
    public int height;

    public Action<int, int> OnTileClicked;
    public Action<int, int> OnTileHovered;
    public Action<int, int> OnTileUnhovered;

    private RectTransform[,] _tiles;
    private Image[,] _tileImages;      // 바닥(지형색)
    private Color[,] _baseColors;      // 바닥 기본색
    private Image[,] _overlayImages;   // ✅ 하이라이트 전용

    [Header("Highlight Colors (Overlay)")]
    public Color targetGreen = new Color(0f, 1f, 0f, 0.35f);
    public Color targetRed = new Color(1f, 0f, 0f, 0.35f);
    public Color previewGreen = new Color(0f, 1f, 0f, 0.55f);

    private static readonly Color ClearColor = new Color(0, 0, 0, 0);

    private void Awake()
    {
        if (boardRoot == null) boardRoot = GetComponentInChildren<RectTransform>(true);
        if (grid == null) grid = GetComponentInChildren<GridLayoutGroup>(true);
    }

    public void Build(int w, int h)
    {
        width = w;
        height = h;

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
        _overlayImages = new Image[width, height];

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

                // ✅ Overlay 생성(흰 판 방지: tile sprite 공유)
                _overlayImages[x, y] = CreateOverlay(tile);

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

    private Image CreateOverlay(Image tile)
    {
        var go = new GameObject("HighlightOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(tile.transform, worldPositionStays: false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.raycastTarget = false;

        // ✅ 중요: 흰색 판 방지(타일과 같은 sprite 사용)
        img.sprite = tile.sprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = false;

        // 투명 시작
        img.color = ClearColor;

        // ✅ 항상 최상단(토큰보다 위)
        

        return img;
    }

    private void FitTokenToTile(CombatUnitToken token)
    {
        if (token == null) return;

        var rt = token.GetComponent<RectTransform>();
        if (rt == null) return;

        // ✅ 타일을 꽉 채우되, 패딩만큼 안쪽으로 줄이기 (가장 안정적)
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.offsetMin = new Vector2(tokenPadding, tokenPadding);
        rt.offsetMax = new Vector2(-tokenPadding, -tokenPadding);

        rt.localScale = Vector3.one;
        rt.anchoredPosition = Vector2.zero;
    }

    // ✅ 지형 시각 적용 + baseColor 갱신

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

                // 오버레이는 항상 투명으로 초기화
                if (_overlayImages != null && _overlayImages[x, y] != null)
                    _overlayImages[x, y].color = ClearColor;
            }
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
        _overlayImages = null;
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
        if (_overlayImages == null) return;

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var ov = _overlayImages[x, y];
                if (ov == null) continue;
                ov.color = ClearColor;
            }
    }

    private void SetOverlay(int x, int y, bool on, Color c)
    {
        if (!InBounds(x, y)) return;
        if (_overlayImages == null) return;

        var ov = _overlayImages[x, y];
        if (ov == null) return;

        ov.color = on ? c : ClearColor;
        // ✅ 토큰 위로 항상 올리기
        ov.transform.SetAsLastSibling();
    }

    public void SetHighlight(int x, int y, bool on) => SetOverlay(x, y, on, targetGreen);
    public void SetPreviewHighlight(int x, int y, bool on) => SetOverlay(x, y, on, previewGreen);
    public void SetInvalidTarget(int x, int y) => SetOverlay(x, y, true, targetRed);

    // ===== Token API =====
    public CombatUnitToken PlaceToken(CombatUnitToken tokenPrefab, int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null || tokenPrefab == null) return null;

        ClearTokenAt(x, y);

        var token = Instantiate(tokenPrefab, tile);
        token.name = $"{tokenPrefab.unitType}_Token_{x}_{y}";

        var rt = token.GetComponent<RectTransform>();
        FitTokenToTile(token);
        // ✅ 오버레이는 토큰 위로
        BringOverlayToFront(x, y);

        return token;
    }

    public void ClearTokenAt(int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null) return;

        for (int i = tile.childCount - 1; i >= 0; i--)
        {
            // ✅ Overlay는 지우면 안 됨
            if (tile.GetChild(i).name == "HighlightOverlay") continue;
            Destroy(tile.GetChild(i).gameObject);
        }

        // 오버레이가 남아있다면 최상단 유지
        BringOverlayToFront(x, y);
    }

    public void MoveExistingToken(CombatUnitToken tokenInstance, int toX, int toY)
    {
        if (tokenInstance == null) return;

        var targetTile = GetTile(toX, toY);
        if (targetTile == null) return;

        tokenInstance.transform.SetParent(targetTile, worldPositionStays: false);

        var rt = tokenInstance.GetComponent<RectTransform>();
        FitTokenToTile(tokenInstance);


        tokenInstance.name = $"{tokenInstance.unitType}_Token_{toX}_{toY}";

        // ✅ 오버레이는 토큰 위로
        BringOverlayToFront(toX, toY);
    }

    private void BringOverlayToFront(int x, int y)
    {
        if (!InBounds(x, y)) return;
        if (_overlayImages == null) return;

        var ov = _overlayImages[x, y];
        if (ov == null) return;

        ov.transform.SetAsLastSibling();
    }
}
