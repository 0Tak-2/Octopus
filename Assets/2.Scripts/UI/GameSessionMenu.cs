using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 필드/던전에서 Esc로 열어 저장 후 타이틀로 복귀.
/// Inspector 연결이 없으면 런타임에 기본 UI를 생성한다.
/// </summary>
public class GameSessionMenu : MonoBehaviour
{
    public static GameSessionMenu Instance { get; private set; }

    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private GameObject _root;
    private bool _isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (_root == null)
            BuildDefaultUI();
    }

    private void Update()
    {
        if (DeathManager.IsDeathInputLocked)
            return;

        if (!Input.GetKeyDown(toggleKey))
            return;

        if (_isOpen)
            Close();
        else if (IsInGameplay())
            Open();
    }

    public void Open()
    {
        if (!IsInGameplay())
            return;

        if (_root == null)
            BuildDefaultUI();

        _root.SetActive(true);
        _isOpen = true;
        Time.timeScale = 0f;
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
        _isOpen = false;
        Time.timeScale = 1f;
    }

    public void SaveAndReturnToTitle()
    {
        Time.timeScale = 1f;
        _isOpen = false;

        var gm = GameManager.Instance;
        if (gm != null)
            gm.SaveAllCurrentState();

        DeathManager.ClearDeathInputLock();
        SceneManager.LoadScene(titleSceneName);
    }

    private bool IsInGameplay()
    {
        if (GameManager.Instance == null)
            return false;

        string scene = SceneManager.GetActiveScene().name;
        var gm = GameManager.Instance;
        return scene == gm.fieldSceneName || scene.Contains("Field") || scene.Contains("Map");
    }

    private void BuildDefaultUI()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasGo = new GameObject("GameSessionMenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        _root = new GameObject("GameSessionMenuRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _root.transform.SetParent(canvas.transform, false);
        var bg = _root.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.65f);
        Stretch(_root.GetComponent<RectTransform>());

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(_root.transform, false);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.16f, 0.26f, 0.98f);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(360f, 220f);
        var layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = true;

        var font = FindExistingFont();
        CreateLabel(panel.transform, "일시 정지", 24, font);
        CreateButton(panel.transform, "저장하고 타이틀로", new Color(0.12f, 0.28f, 0.45f), font, SaveAndReturnToTitle);
        CreateButton(panel.transform, "계속하기", new Color(0.2f, 0.22f, 0.26f), font, Close);

        _root.SetActive(false);
    }

    private TMP_FontAsset FindExistingFont()
    {
        var any = FindObjectOfType<TMP_Text>();
        return any != null ? any.font : TMP_Settings.defaultFontAsset;
    }

    private void CreateLabel(Transform parent, string text, float size, TMP_FontAsset font)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;
    }

    private void CreateButton(Transform parent, string label, Color bg, TMP_FontAsset font, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<LayoutElement>().minHeight = 44f;

        var btn = go.GetComponent<Button>();
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 18;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (font != null) tmp.font = font;
        Stretch(textGo.GetComponent<RectTransform>());
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
