using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터 보스를 잡았을 때 뜨는 선택 화면.
/// 로그라이크 RPG라 여기서 런이 끝나지 않는다. 더 깊이 갈지, 여기서 챙겨 나갈지 고른다.
/// 프리팹 없이 런타임에 생성한다 (DeathManager 의 오버레이와 같은 방식).
/// </summary>
public class ChapterClearUI : MonoBehaviour
{
    public static ChapterClearUI Instance { get; private set; }

    private CanvasGroup _group;
    private TextMeshProUGUI _headline;
    private TextMeshProUGUI _subline;
    private Button _advanceButton;
    private Button _extractButton;
    private TextMeshProUGUI _advanceLabel;
    private TextMeshProUGUI _extractLabel;

    private Action _onAdvance;
    private Action _onExtract;

    /// <summary>
    /// 선택 화면을 띄운다. canAdvance 가 false면 전진 버튼이 사라진다(마지막 챕터).
    /// </summary>
    public static void Show(
        string headline,
        string subline,
        bool canAdvance,
        string advanceLabel,
        string extractLabel,
        Action onAdvance,
        Action onExtract)
    {
        if (Instance == null)
        {
            // DontDestroyOnLoad 로 두지 않는다. 선택 후에는 어느 쪽이든 씬이 바뀌고,
            // 살아남으면 다음 씬의 UI 빌더가 이 캔버스를 집어가 클릭을 삼키는 사고가 난다.
            var go = new GameObject("ChapterClearUI");
            Instance = go.AddComponent<ChapterClearUI>();
            Instance.Build();
        }

        Instance.Display(headline, subline, canAdvance, advanceLabel, extractLabel, onAdvance, onExtract);
    }

    public static void HideIfOpen()
    {
        if (Instance != null)
            Instance.Hide();
    }

    private void Display(
        string headline,
        string subline,
        bool canAdvance,
        string advanceLabel,
        string extractLabel,
        Action onAdvance,
        Action onExtract)
    {
        _onAdvance = onAdvance;
        _onExtract = onExtract;

        _headline.text = headline;
        _subline.text = subline;
        _advanceLabel.text = advanceLabel;
        _extractLabel.text = extractLabel;

        _advanceButton.gameObject.SetActive(canAdvance);

        _group.alpha = 1f;
        _group.blocksRaycasts = true;
        _group.interactable = true;
        gameObject.SetActive(true);
    }

    private void Hide()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        _onAdvance = null;
        _onExtract = null;
    }

    private void OnAdvanceClicked()
    {
        var cb = _onAdvance;
        Hide();
        cb?.Invoke();
    }

    private void OnExtractClicked()
    {
        var cb = _onExtract;
        Hide();
        cb?.Invoke();
    }

    // ============================================
    // 런타임 UI 생성
    // ============================================

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // DeathManager 의 사망 오버레이(30000)보다 아래. 동시에 뜨면 사망이 이겨야 한다.
        canvas.sortingOrder = 29000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var bg = CreateChild("BG", transform);
        var bgImage = bg.gameObject.AddComponent<Image>();
        bgImage.color = new Color(0f, 0f, 0f, 0.86f);
        Stretch(bg);

        var panel = CreateChild("Panel", transform);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(900f, 520f);
        panel.anchoredPosition = Vector2.zero;

        _headline = CreateText("Headline", panel, 72, FontStyles.Bold, new Color(0.95f, 0.82f, 0.25f, 1f));
        _headline.rectTransform.anchorMin = new Vector2(0f, 1f);
        _headline.rectTransform.anchorMax = new Vector2(1f, 1f);
        _headline.rectTransform.pivot = new Vector2(0.5f, 1f);
        _headline.rectTransform.sizeDelta = new Vector2(0f, 110f);
        _headline.rectTransform.anchoredPosition = Vector2.zero;
        _headline.alignment = TextAlignmentOptions.Center;

        _subline = CreateText("Subline", panel, 30, FontStyles.Normal, new Color(0.82f, 0.86f, 0.9f, 1f));
        _subline.rectTransform.anchorMin = new Vector2(0f, 1f);
        _subline.rectTransform.anchorMax = new Vector2(1f, 1f);
        _subline.rectTransform.pivot = new Vector2(0.5f, 1f);
        _subline.rectTransform.sizeDelta = new Vector2(0f, 90f);
        _subline.rectTransform.anchoredPosition = new Vector2(0f, -120f);
        _subline.alignment = TextAlignmentOptions.Top;

        _advanceButton = CreateButton("AdvanceButton", panel, new Vector2(0f, -70f),
            new Color(0.16f, 0.36f, 0.52f, 1f), out _advanceLabel);
        _advanceButton.onClick.AddListener(OnAdvanceClicked);

        _extractButton = CreateButton("ExtractButton", panel, new Vector2(0f, -200f),
            new Color(0.34f, 0.28f, 0.14f, 1f), out _extractLabel);
        _extractButton.onClick.AddListener(OnExtractClicked);

        EnsureEventSystem();
    }

    /// <summary>
    /// EventSystem 이 없으면 버튼 클릭이 먹지 않아 런이 잠긴다. 여기서만큼은 막아둔다.
    /// </summary>
    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null)
            return;

        // 씬 스코프로 둔다. 영구 EventSystem 을 만들면 다음 씬의 EventSystem 과 중복되어
        // 둘 다 제대로 동작하지 않는다.
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private static RectTransform CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// 폰트를 지정하지 않으면 TMP 가 TMP Settings 의 기본 폰트(Pretendard)를 쓴다 → 한글이 정상 출력된다.
    /// </summary>
    private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        var rt = CreateChild(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Vector2 anchoredPos, Color color, out TextMeshProUGUI label)
    {
        var rt = CreateChild(name, parent);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(620f, 104f);
        rt.anchoredPosition = anchoredPos;

        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        label = CreateText("Label", rt, 32, FontStyles.Bold, Color.white);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);

        return button;
    }
}
