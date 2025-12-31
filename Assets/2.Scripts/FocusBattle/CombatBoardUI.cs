using System;
using UnityEngine;
using UnityEngine.UI;

public class CombatBoardUI : MonoBehaviour
{
    [Header("Layout")]
    public RectTransform boardRoot;     // BoardRoot
    public GridLayoutGroup grid;        // Grid

    [Header("Tile Prefab")]
    public Image tilePrefab;

    [Header("Runtime")]
    public int width;
    public int height;

    public Action<int, int> OnTileClicked;

    private RectTransform[,] _tiles;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        // ✅ 가장 안전한 방식:
        // - CombatBoardUI가 BoardRoot에 붙어있으면 boardRoot = 자기 자신 RectTransform
        // - 아니면 자식 중 "BoardRoot" 이름을 우선 탐색
        if (boardRoot == null)
        {
            boardRoot = GetComponent<RectTransform>();
            if (boardRoot == null || boardRoot.gameObject.name != "BoardRoot")
            {
                var found = FindChildRect(transform, "BoardRoot");
                if (found != null) boardRoot = found;
                else boardRoot = GetComponentInChildren<RectTransform>(true); // 최후 fallback
            }
        }

        // ✅ grid는 "Grid" 이름을 최우선으로 찾기
        if (grid == null)
        {
            var gridRT = FindChildRect(transform, "Grid");
            if (gridRT != null)
                grid = gridRT.GetComponent<GridLayoutGroup>();

            if (grid == null)
                grid = GetComponentInChildren<GridLayoutGroup>(true);
        }
    }

    private RectTransform FindChildRect(Transform root, string name)
    {
        if (root == null) return null;
        var all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i].GetComponent<RectTransform>();
        }
        return null;
    }

    public void Build(int w, int h)
    {
        width = w;
        height = h;

        ResolveReferences();
        Clear();

        if (grid == null || boardRoot == null || tilePrefab == null)
        {
            Debug.LogError("[CombatBoardUI] Missing references. boardRoot/grid/tilePrefab 확인 필요!");
            return;
        }

        // ✅ 맵 크기에 맞춰 열 수 자동
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = width;

        // ✅ 가운데 정렬
        grid.childAlignment = TextAnchor.MiddleCenter;

        _tiles = new RectTransform[width, height];

        // 타일 생성
        for (int genY = 0; genY < height; genY++)
        {
            for (int x = 0; x < width; x++)
            {
                var tile = Instantiate(tilePrefab, grid.transform);
                tile.name = $"Tile_{x}_{genY}";

                // ✅ 클릭을 받아야 하므로 raycastTarget 활성화
                tile.raycastTarget = true;

                // 내부 y좌표 뒤집어 저장
                int y = (height - 1) - genY;
                _tiles[x, y] = tile.rectTransform;
                _tiles[x, y].localScale = Vector3.one;

                // ✅ Button 자동 부착
                var btn = tile.GetComponent<Button>();
                if (btn == null) btn = tile.gameObject.AddComponent<Button>();
                btn.targetGraphic = tile;

                int cx = x;
                int cy = y;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnTileClicked?.Invoke(cx, cy));
            }
        }

        AutoSizeAndCenterBoard();

        // 레이아웃 강제 갱신
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid.GetComponent<RectTransform>());
    }

    private void AutoSizeAndCenterBoard()
    {
        if (boardRoot == null || grid == null) return;

        var padding = grid.padding;
        var cell = grid.cellSize;
        var sp = grid.spacing;

        float totalW = padding.left + padding.right + (cell.x * width) + (sp.x * (width - 1));
        float totalH = padding.top + padding.bottom + (cell.y * height) + (sp.y * (height - 1));

        // ✅ BoardRoot는 중앙 고정
        boardRoot.anchorMin = new Vector2(0.5f, 0.5f);
        boardRoot.anchorMax = new Vector2(0.5f, 0.5f);
        boardRoot.pivot = new Vector2(0.5f, 0.5f);
        boardRoot.sizeDelta = new Vector2(totalW, totalH);

        // anchoredPosition이 씬/캔버스 상태에 따라 먹통일 때가 있어서 더 강하게 고정
        boardRoot.anchoredPosition = Vector2.zero;
        boardRoot.anchoredPosition3D = Vector3.zero;
        boardRoot.localPosition = Vector3.zero;
        boardRoot.localScale = Vector3.one;

        // ✅ Grid는 BoardRoot 꽉 채우기
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
    }

    public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;

    public RectTransform GetTile(int x, int y)
    {
        if (_tiles == null) return null;
        if (!InBounds(x, y)) return null;
        return _tiles[x, y];
    }

    public void ClearTokenAt(int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null) return;

        for (int i = tile.childCount - 1; i >= 0; i--)
            Destroy(tile.GetChild(i).gameObject);
    }

    public CombatUnitToken PlaceToken(CombatUnitToken tokenPrefab, int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null || tokenPrefab == null) return null;

        ClearTokenAt(x, y);

        var token = Instantiate(tokenPrefab, tile);
        token.name = $"{tokenPrefab.unitType}_Token_{x}_{y}";

        var rt = token.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        return token;
    }

    public void MoveExistingToken(CombatUnitToken tokenInstance, int toX, int toY)
    {
        if (tokenInstance == null) return;

        var targetTile = GetTile(toX, toY);
        if (targetTile == null) return;

        tokenInstance.transform.SetParent(targetTile, worldPositionStays: false);

        var rt = tokenInstance.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;

        tokenInstance.name = $"{tokenInstance.unitType}_Token_{toX}_{toY}";
    }
}
