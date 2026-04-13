using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 맵 그리드가 화면을 다 채우지 않을 때, 남는 투명 영역을 검정으로 덮어
/// "맵 밖은 존재하지 않는다"처럼 보이게 합니다.
/// 빈 오브젝트에 붙이면 Canvas·패널을 자동 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public class FieldMapScreenBlackBars : MonoBehaviour
{
    [Header("Refs (비워두면 자동 탐색/생성)")]
    public Camera worldCamera;
    public GridBoard gridBoard;
    public Canvas targetCanvas;

    [Header("Style")]
    [SerializeField] private Color barColor = Color.black;
    [Tooltip("Canvas sorting order (높을수록 위)")]
    [SerializeField] private int canvasSortOrder = 500;

    [Header("Camera clear (선택)")]
    [SerializeField] private bool setCameraClearToSolidBlack = true;

    private RectTransform _left, _right, _top, _bottom;
    private Image _imgL, _imgR, _imgT, _imgB;

    private void Awake()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;

        EnsureCanvasAndBars();
    }

    private void Start()
    {
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();

        if (setCameraClearToSolidBlack && worldCamera != null)
        {
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = Color.black;
        }
    }

    private void LateUpdate()
    {
        if (gridBoard == null)
            gridBoard = FindObjectOfType<GridBoard>();
        if (gridBoard == null || worldCamera == null)
            return;

        int w = gridBoard.width;
        int h = gridBoard.height;
        if (w <= 0 || h <= 0)
            return;

        float half = GetHalfCellSize();
        Vector3 bl = gridBoard.CellToWorld(new Vector2Int(0, 0)) + new Vector3(-half, -half, 0f);
        Vector3 br = gridBoard.CellToWorld(new Vector2Int(w - 1, 0)) + new Vector3(half, -half, 0f);
        Vector3 tl = gridBoard.CellToWorld(new Vector2Int(0, h - 1)) + new Vector3(-half, half, 0f);
        Vector3 tr = gridBoard.CellToWorld(new Vector2Int(w - 1, h - 1)) + new Vector3(half, half, 0f);

        float minSx = float.PositiveInfinity, maxSx = float.NegativeInfinity;
        float minSy = float.PositiveInfinity, maxSy = float.NegativeInfinity;
        foreach (var p in new[] { bl, br, tl, tr })
        {
            Vector3 s = worldCamera.WorldToScreenPoint(p);
            if (s.z <= 0f) return;
            minSx = Mathf.Min(minSx, s.x);
            maxSx = Mathf.Max(maxSx, s.x);
            minSy = Mathf.Min(minSy, s.y);
            maxSy = Mathf.Max(maxSy, s.y);
        }

        float sw = Screen.width;
        float sh = Screen.height;

        float left = Mathf.Clamp(minSx, 0f, sw);
        float right = Mathf.Clamp(maxSx, 0f, sw);
        float bottom = Mathf.Clamp(minSy, 0f, sh);
        float top = Mathf.Clamp(maxSy, 0f, sh);

        float barLeft = left;
        float barRight = sw - right;
        float barBottom = bottom;
        float barTop = sh - top;

        RectTransform canvasRt = targetCanvas.transform as RectTransform;
        ApplyBarScreen(canvasRt, _left, _imgL, 0f, 0f, barLeft, sh);
        ApplyBarScreen(canvasRt, _right, _imgR, right, 0f, barRight, sh);
        ApplyBarScreen(canvasRt, _bottom, _imgB, left, 0f, right - left, barBottom);
        ApplyBarScreen(canvasRt, _top, _imgT, left, top, right - left, sh - top);
    }

    private static void ApplyBarScreen(RectTransform canvasRt, RectTransform rt, Image img, float x, float y, float width, float height)
    {
        if (rt == null || img == null || canvasRt == null) return;
        bool on = width > 0.5f && height > 0.5f;
        img.enabled = on;
        rt.gameObject.SetActive(on);
        if (!on) return;

        Vector2 a, b;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, new Vector2(x, y), null, out a);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, new Vector2(x + width, y + height), null, out b);

        Vector2 min = new Vector2(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y));
        Vector2 max = new Vector2(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = max - min;
        rt.anchoredPosition = (min + max) * 0.5f;
    }

    private float GetHalfCellSize()
    {
        var ug = gridBoard != null ? gridBoard.GetComponent<Grid>() : null;
        if (ug != null)
            return ug.cellSize.x * 0.5f;
        return gridBoard.cellSize * 0.5f;
    }

    private void EnsureCanvasAndBars()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
            if (targetCanvas == null)
                targetCanvas = gameObject.AddComponent<Canvas>();
        }

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.sortingOrder = canvasSortOrder;
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        if (GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        RectTransform canvasRt = targetCanvas.transform as RectTransform;
        _left = CreateBar(canvasRt, "BlackLeft", out _imgL);
        _right = CreateBar(canvasRt, "BlackRight", out _imgR);
        _bottom = CreateBar(canvasRt, "BlackBottom", out _imgB);
        _top = CreateBar(canvasRt, "BlackTop", out _imgT);
    }

    private RectTransform CreateBar(RectTransform parent, string name, out Image img)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        img = go.AddComponent<Image>();
        img.color = barColor;
        img.raycastTarget = false;
        return rt;
    }
}
