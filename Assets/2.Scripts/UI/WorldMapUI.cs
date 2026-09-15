using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전체 지도. M 키로 열고 닫는다.
///
/// 미니맵(항상 떠 있는 실시간 표시) 대신 '펼쳐 보는 축소도'를 택했다.
/// 턴제 정적 게임에서 실시간 정보가 상시 노출되면 긴장감이 깎이고 화면도 가린다.
///
/// 표시 내용은 FogOfWarRenderer 가 이미 들고 있는 '탐험한 칸' 기록을 그대로 쓴다.
/// 안 가본 곳은 검게 남으므로, 길찾기보다 "여기 아직 안 가봤네"를 보는 용도다.
/// 적은 일부러 표시하지 않는다 — 위치가 보이면 긴장감이 사라진다.
/// </summary>
public class WorldMapUI : MonoBehaviour
{
    public static WorldMapUI Instance { get; private set; }

    [Header("Input")]
    public KeyCode toggleKey = KeyCode.M;

    [Header("Layout")]
    [Tooltip("지도 패널 크기(픽셀)")]
    public Vector2 panelSize = new Vector2(720f, 720f);

    [Header("Colors")]
    public Color wallColor = new Color(0.18f, 0.2f, 0.26f);
    public Color floorColor = new Color(0.45f, 0.5f, 0.58f);
    public Color unexploredColor = new Color(0.04f, 0.05f, 0.07f);
    public Color playerColor = new Color(0.35f, 0.95f, 0.5f);
    public Color dungeonColor = new Color(0.95f, 0.65f, 0.2f);
    public Color exitColor = new Color(0.45f, 0.75f, 1f);

    private GameObject _root;
    private RawImage _mapImage;
    private Texture2D _mapTex;
    private TMP_Text _infoText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log($"[WorldMapUI] 준비 완료 — '{toggleKey}' 키로 지도를 엽니다.");
    }

    private void Update()
    {
        if (DeathManager.IsDeathInputLocked) return;

        if (Input.GetKeyDown(toggleKey))
            Toggle();
        else if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (_root == null) Build();
        if (_root == null)
        {
            Debug.LogWarning("[WorldMapUI] 지도 UI를 만들지 못했습니다. 현재 씬에 Canvas 가 있는지 확인하세요.");
            return;
        }

        _root.SetActive(true);
        Redraw();
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    // ============================================
    // 그리기
    // ============================================

    private void Redraw()
    {
        var generator = FindObjectOfType<FieldMapGenerator>();
        var map = generator != null ? generator.currentMap : null;
        var fog = FogOfWarRenderer.Instance;

        if (map == null || map.width <= 0 || map.height <= 0)
        {
            if (_infoText != null) _infoText.text = "지도 정보를 불러올 수 없습니다.";
            return;
        }

        EnsureTexture(map.width, map.height);

        var pixels = new Color32[map.width * map.height];

        // 탐험 여부를 빠르게 보기 위해 집합을 그대로 쓴다.
        var explored = fog != null ? fog.ExploredCells : null;
        var exploredSet = explored as System.Collections.Generic.ICollection<Vector2Int>;

        int exploredCount = 0;

        for (int y = 0; y < map.height; y++)
        {
            for (int x = 0; x < map.width; x++)
            {
                var cell = new Vector2Int(x, y);
                bool seen = exploredSet != null && exploredSet.Contains(cell);

                Color c;
                if (!seen)
                {
                    c = unexploredColor;
                }
                else
                {
                    exploredCount++;
                    c = map.IsWalkable(x, y) ? floorColor : wallColor;
                }

                pixels[y * map.width + x] = c;
            }
        }

        // 마커 — 탐험한 곳만 표시한다. 안 가본 던전 입구를 미리 알려주면 탐험 의미가 없다.
        if (map.dungeonEntrances != null)
        {
            foreach (var e in map.dungeonEntrances)
                MarkIfExplored(pixels, map, exploredSet, e, dungeonColor, 1);
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            var entry = gm.playerData;
            if (entry != null && entry.lastFieldPosition != Vector2Int.zero)
            {
                // 출구는 저장된 통로 좌표를 쓴다(있을 때만).
                var mapData = gm.GetOrCreateFieldMapData(gm.GetCurrentMapKey());
                if (mapData != null && mapData.hasSavedPortalCells)
                    MarkIfExplored(pixels, map, exploredSet, mapData.savedExitToNext, exitColor, 1);
            }
        }

        // 현재 위치는 탐험 여부와 무관하게 항상 표시.
        var player = GameObject.FindGameObjectWithTag("Player");
        var grid = fog != null ? fog.grid : null;
        if (player != null && grid != null)
        {
            Vector2Int pc = grid.WorldToCell(player.transform.position);
            Mark(pixels, map, pc, playerColor, 1);
        }

        _mapTex.SetPixels32(pixels);
        _mapTex.Apply(false);

        int total = map.width * map.height;
        if (_infoText != null)
        {
            float pct = total > 0 ? exploredCount * 100f / total : 0f;
            _infoText.text = $"{map.width} x {map.height}    탐험 {pct:F0}%    " +
                             $"<color=#F2A633>■</color> 던전   <color=#73BFFF>■</color> 출구   " +
                             $"<color=#59F280>■</color> 현재 위치      [M] 닫기";
        }
    }

    private void MarkIfExplored(Color32[] pixels, MapData map,
        System.Collections.Generic.ICollection<Vector2Int> explored,
        Vector2Int cell, Color color, int radius)
    {
        if (explored == null || !explored.Contains(cell)) return;
        Mark(pixels, map, cell, color, radius);
    }

    private static void Mark(Color32[] pixels, MapData map, Vector2Int cell, Color color, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                int x = cell.x + dx;
                int y = cell.y + dy;
                if (x < 0 || y < 0 || x >= map.width || y >= map.height) continue;
                pixels[y * map.width + x] = color;
            }
        }
    }

    private void EnsureTexture(int w, int h)
    {
        if (_mapTex != null && _mapTex.width == w && _mapTex.height == h) return;

        if (_mapTex != null) Destroy(_mapTex);

        _mapTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        // 격자가 또렷하게 보이도록 보간을 끈다.
        _mapTex.filterMode = FilterMode.Point;
        _mapTex.wrapMode = TextureWrapMode.Clamp;

        if (_mapImage != null)
            _mapImage.texture = _mapTex;
    }

    // ============================================
    // 런타임 UI 생성
    // ============================================

    private void Build()
    {
        // 씬에 캔버스가 여러 개라 어느 것이 잡힐지 불확실하고 정렬 순서도 제각각이다.
        // 다른 런타임 UI(ChapterClearUI 등)처럼 자체 캔버스를 만들어 확실하게 위에 그린다.
        var canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 28000;

        var scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        _root = CreatePanel("WorldMapRoot", transform, new Color(0f, 0f, 0f, 0.82f));
        Stretch(_root.GetComponent<RectTransform>());

        var panel = CreatePanel("Panel", _root.transform, new Color(0.06f, 0.08f, 0.13f, 0.99f));
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(panelSize.x + 40f, panelSize.y + 110f);

        var title = CreateText(panel.transform, "지도", 26, FontStyles.Bold, Color.white);
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 1f);
        trt.anchorMax = new Vector2(0.5f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(panelSize.x, 34f);
        trt.anchoredPosition = new Vector2(0f, -14f);
        title.alignment = TextAlignmentOptions.Center;

        var mapGo = NewRect("Map", panel.transform);
        mapGo.anchorMin = new Vector2(0.5f, 0.5f);
        mapGo.anchorMax = new Vector2(0.5f, 0.5f);
        mapGo.pivot = new Vector2(0.5f, 0.5f);
        mapGo.sizeDelta = panelSize;
        mapGo.anchoredPosition = new Vector2(0f, -10f);

        _mapImage = mapGo.gameObject.AddComponent<RawImage>();
        _mapImage.color = Color.white;

        _infoText = CreateText(panel.transform, "", 15, FontStyles.Normal,
            new Color(0.75f, 0.8f, 0.87f));
        var irt = _infoText.rectTransform;
        irt.anchorMin = new Vector2(0.5f, 0f);
        irt.anchorMax = new Vector2(0.5f, 0f);
        irt.pivot = new Vector2(0.5f, 0f);
        irt.sizeDelta = new Vector2(panelSize.x, 30f);
        irt.anchoredPosition = new Vector2(0f, 14f);
        _infoText.alignment = TextAlignmentOptions.Center;
        _infoText.richText = true;

        _root.SetActive(false);
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateText(Transform parent, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }
}
