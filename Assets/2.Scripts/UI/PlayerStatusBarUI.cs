using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum StatusBarScreenCorner
{
    TopCenter,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomCenter,
    BottomRight
}

/// <summary>
/// 체력 / 허기 / 피로도 바 (각각 다른 색). PlayerStats를 표시합니다.
/// 인스펙터에 Slider·Image를 연결하거나, Auto Build로 런타임 생성할 수 있습니다.
/// </summary>
public class PlayerStatusBarUI : MonoBehaviour
{
    [Header("References")]
    public PlayerStats playerStats;

    [Header("Auto Build (에디터 연결 없이 실행 시 UI 생성)")]
    [Tooltip("체크 시 Slider/Image 참조가 비어 있으면 자식으로 바를 만듭니다.")]
    public bool autoBuildUiAtRuntime = true;
    public Vector2 barSize = new Vector2(200f, 16f);
    [Tooltip("체력 → 허기 → 피로 순으로 쌓습니다.")]
    public bool useKoreanLabels = true;

    [Header("화면 위치")]
    [Tooltip("캔버스 전체(큰 박스) 기준으로 바를 둘 모서리. 게임 화면이 왼쪽 아래에만 있으면 BottomLeft 등으로 바꿔 보세요.")]
    public StatusBarScreenCorner screenCorner = StatusBarScreenCorner.TopCenter;
    [Tooltip("모서리에서 안쪽으로 얼마나 띄울지 (픽셀, Canvas Scaler 기준)")]
    public Vector2 cornerPadding = new Vector2(16f, 16f);

    [Header("HP Bar")]
    public Slider hpSlider;
    public Image hpFillImage;
    public TMP_Text hpText;
    public Color hpColorFull = new Color(0.25f, 0.85f, 0.35f);
    public Color hpColorLow = new Color(0.85f, 0.15f, 0.15f);
    [Range(0f, 0.5f)] public float hpLowThreshold = 0.3f;

    [Header("Hunger Bar")]
    public Slider hungerSlider;
    public Image hungerFillImage;
    public TMP_Text hungerText;
    public Color hungerColorFull = new Color(1f, 0.72f, 0.2f);
    public Color hungerColorLow = new Color(0.55f, 0.25f, 0.1f);
    [Range(0f, 0.5f)] public float hungerLowThreshold = 0.25f;

    [Header("Fatigue Bar")]
    public Slider fatigueSlider;
    public Image fatigueFillImage;
    public TMP_Text fatigueText;
    public Color fatigueColorFull = new Color(0.45f, 0.65f, 1f);
    public Color fatigueColorLow = new Color(0.25f, 0.28f, 0.45f);
    [Range(0f, 0.5f)] public float fatigueLowThreshold = 0.2f;

    [Header("Status Icons (Optional)")]
    public GameObject lowHPWarning;
    public GameObject lowHungerWarning;
    public GameObject lowFatigueWarning;

    [Header("Animation")]
    public bool animateLowBars = true;
    public float pulseSpeed = 2f;

    private float _pulseTimer;
    private bool _didBuild;

    private static Sprite _whiteSprite;

    private static Sprite WhiteSprite
    {
        get
        {
            if (_whiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return _whiteSprite;
        }
    }

    /// <summary>
    /// UI는 new GameObject(name, typeof(RectTransform))로 만든다. 일반 GameObject는 SetParent 직후 as RectTransform이 null일 수 있다.
    /// </summary>
    private static RectTransform GetRect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt != null) return rt;
        return go.transform as RectTransform;
    }

    private static GameObject CreateUiObject(string name)
    {
        return new GameObject(name, typeof(RectTransform));
    }

    private void ApplyScreenCorner(RectTransform rootRt)
    {
        Vector2 aMin, aMax, pivot;
        Vector2 pos;

        switch (screenCorner)
        {
            case StatusBarScreenCorner.TopLeft:
                aMin = aMax = pivot = new Vector2(0f, 1f);
                pos = new Vector2(cornerPadding.x, -cornerPadding.y);
                break;
            case StatusBarScreenCorner.TopCenter:
                aMin = aMax = pivot = new Vector2(0.5f, 1f);
                pos = new Vector2(0f, -cornerPadding.y);
                break;
            case StatusBarScreenCorner.TopRight:
                aMin = aMax = pivot = new Vector2(1f, 1f);
                pos = new Vector2(-cornerPadding.x, -cornerPadding.y);
                break;
            case StatusBarScreenCorner.BottomLeft:
                aMin = aMax = pivot = new Vector2(0f, 0f);
                pos = new Vector2(cornerPadding.x, cornerPadding.y);
                break;
            case StatusBarScreenCorner.BottomCenter:
                aMin = aMax = pivot = new Vector2(0.5f, 0f);
                pos = new Vector2(0f, cornerPadding.y);
                break;
            case StatusBarScreenCorner.BottomRight:
                aMin = aMax = pivot = new Vector2(1f, 0f);
                pos = new Vector2(-cornerPadding.x, cornerPadding.y);
                break;
            default:
                aMin = aMax = pivot = new Vector2(0.5f, 1f);
                pos = new Vector2(0f, -cornerPadding.y);
                break;
        }

        rootRt.anchorMin = aMin;
        rootRt.anchorMax = aMax;
        rootRt.pivot = pivot;
        rootRt.anchoredPosition = pos;
    }

    private void Awake()
    {
        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();

        TryBuildDefaultUi();
    }

    private void OnEnable()
    {
        // 비활성 상태로 씬에 있다가 나중에 켜지면 Awake만으로는 빌드가 안 될 수 있음
        TryBuildDefaultUi();
    }

    private void TryBuildDefaultUi()
    {
        if (_didBuild) return;
        if (!autoBuildUiAtRuntime) return;
        if (hpFillImage != null || hungerFillImage != null || fatigueFillImage != null) return;

        try
        {
            BuildDefaultUi();
            Debug.Log("[PlayerStatusBarUI] Auto UI build 완료 (자식 Row·Bar 생성). 플레이 중에만 하이어라키에 보입니다.", this);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PlayerStatusBarUI] Auto UI build 실패: {e.Message}\n{e.StackTrace}", this);
        }
    }

    private void Start()
    {
        if (lowHPWarning != null) lowHPWarning.SetActive(false);
        if (lowHungerWarning != null) lowHungerWarning.SetActive(false);
        if (lowFatigueWarning != null) lowFatigueWarning.SetActive(false);
    }

    private void Update()
    {
        if (playerStats == null) return;

        UpdateHPBar();
        UpdateHungerBar();
        UpdateFatigueBar();

        if (animateLowBars)
            _pulseTimer += Time.deltaTime * pulseSpeed;
    }

    private void BuildDefaultUi()
    {
        var root = gameObject;
        var rootRt = root.GetComponent<RectTransform>();
        if (rootRt == null)
        {
            Debug.LogError("[PlayerStatusBarUI] Canvas 등 RectTransform이 있는 UI 오브젝트에 붙여 주세요.", this);
            return;
        }

        // 이전 빌드 실패로 남은 자식(중복 체력_Row 등) 제거
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Destroy(root.transform.GetChild(i).gameObject);

        var vlg = root.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = root.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 8f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
        }

        string hpL = useKoreanLabels ? "체력" : "HP";
        string hunL = useKoreanLabels ? "허기" : "Hunger";
        string fatL = useKoreanLabels ? "피로" : "Fatigue";

        hpFillImage = CreateBarRow(hpL, root.transform, out hpText);
        hungerFillImage = CreateBarRow(hunL, root.transform, out hungerText);
        fatigueFillImage = CreateBarRow(fatL, root.transform, out fatigueText);

        // 최소 가로폭 (Bar가 0으로 접히는 것 방지) + 화면 모서리
        rootRt.sizeDelta = new Vector2(Mathf.Max(280f, barSize.x + 120f), 110f);
        ApplyScreenCorner(rootRt);

        _didBuild = true;
    }

    private Image CreateBarRow(string label, Transform parent, out TMP_Text valueText)
    {
        var row = CreateUiObject(label + "_Row");
        row.transform.SetParent(parent, false);

        var rowRt = GetRect(row);
        if (rowRt == null)
            throw new System.InvalidOperationException("Row에 RectTransform이 없습니다. 부모가 UI(RectTransform)인지 확인하세요.");
        rowRt.sizeDelta = new Vector2(barSize.x + 88f, 28f);

        var leRow = row.AddComponent<LayoutElement>();
        leRow.minHeight = 28f;
        leRow.preferredHeight = 28f;
        leRow.minWidth = Mathf.Max(260f, barSize.x + 130f);

        var hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.padding = new RectOffset(0, 0, 2, 2);
        // false면 Bar(LayoutElement flexibleWidth)가 너비 0으로 남는 경우가 많음
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var labelGo = CreateUiObject("Label");
        labelGo.transform.SetParent(row.transform, false);
        var labelRt = GetRect(labelGo);
        if (labelRt == null)
            throw new System.InvalidOperationException("Label에 RectTransform이 없습니다.");
        labelRt.sizeDelta = new Vector2(52f, 24f);
        var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
        labelTmp.text = label;
        labelTmp.fontSize = 18;
        labelTmp.alignment = TextAlignmentOptions.MidlineLeft;
        labelTmp.color = Color.white;
        if (TMP_Settings.defaultFontAsset != null)
            labelTmp.font = TMP_Settings.defaultFontAsset;

        var leLabel = labelGo.AddComponent<LayoutElement>();
        leLabel.preferredWidth = 52f;
        leLabel.minWidth = 52f;

        var barContainer = CreateUiObject("Bar");
        barContainer.transform.SetParent(row.transform, false);
        var barLe = barContainer.AddComponent<LayoutElement>();
        barLe.minWidth = barSize.x;
        barLe.preferredWidth = barSize.x;
        barLe.flexibleWidth = 1f;
        barLe.minHeight = barSize.y;
        barLe.preferredHeight = barSize.y;

        var barRt = GetRect(barContainer);
        if (barRt == null)
            throw new System.InvalidOperationException("Bar에 RectTransform이 없습니다.");
        barRt.sizeDelta = barSize;

        var bg = CreateUiObject("Bg");
        bg.transform.SetParent(barContainer.transform, false);
        var bgRt = GetRect(bg);
        if (bgRt == null)
            throw new System.InvalidOperationException("Bg에 RectTransform이 없습니다.");
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = WhiteSprite;
        bgImg.color = new Color(0.12f, 0.12f, 0.14f, 0.92f);
        bgImg.raycastTarget = false;

        var fillGo = CreateUiObject("Fill");
        fillGo.transform.SetParent(bg.transform, false);
        var fillRt = GetRect(fillGo);
        if (fillRt == null)
            throw new System.InvalidOperationException("Fill에 RectTransform이 없습니다.");
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;
        var fill = fillGo.AddComponent<Image>();
        fill.sprite = WhiteSprite;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;

        var valueGo = CreateUiObject("Value");
        valueGo.transform.SetParent(row.transform, false);
        var valueRt = GetRect(valueGo);
        if (valueRt == null)
            throw new System.InvalidOperationException("Value에 RectTransform이 없습니다.");
        valueRt.sizeDelta = new Vector2(72f, 24f);
        valueText = valueGo.AddComponent<TextMeshProUGUI>();
        valueText.fontSize = 16;
        valueText.alignment = TextAlignmentOptions.MidlineRight;
        valueText.color = new Color(0.95f, 0.95f, 0.95f);
        if (TMP_Settings.defaultFontAsset != null)
            valueText.font = TMP_Settings.defaultFontAsset;

        var leVal = valueGo.AddComponent<LayoutElement>();
        leVal.preferredWidth = 72f;
        leVal.minWidth = 72f;

        return fill;
    }

    private void UpdateHPBar()
    {
        int current = playerStats.hp;
        int max = Mathf.Max(1, playerStats.maxHP);
        float ratio = Mathf.Clamp01((float)current / max);

        if (hpSlider != null)
        {
            hpSlider.maxValue = max;
            hpSlider.value = current;
        }

        if (hpText != null)
            hpText.text = $"{current}/{max}";

        if (hpFillImage != null)
        {
            ApplyFill(hpFillImage, ratio);
            Color targetColor = Color.Lerp(hpColorLow, hpColorFull, ratio);

            if (ratio <= hpLowThreshold && animateLowBars)
            {
                float pulse = (Mathf.Sin(_pulseTimer) + 1f) * 0.5f;
                targetColor = Color.Lerp(hpColorLow, Color.white, pulse * 0.3f);
            }

            hpFillImage.color = targetColor;
        }

        if (lowHPWarning != null)
            lowHPWarning.SetActive(ratio <= hpLowThreshold);
    }

    private void UpdateHungerBar()
    {
        int current = playerStats.hunger;
        int max = Mathf.Max(1, playerStats.maxHunger);
        float ratio = Mathf.Clamp01((float)current / max);

        if (hungerSlider != null)
        {
            hungerSlider.maxValue = max;
            hungerSlider.value = current;
        }

        if (hungerText != null)
            hungerText.text = $"{current}/{max}";

        if (hungerFillImage != null)
        {
            ApplyFill(hungerFillImage, ratio);
            Color targetColor = Color.Lerp(hungerColorLow, hungerColorFull, ratio);

            if (ratio <= hungerLowThreshold && animateLowBars)
            {
                float pulse = (Mathf.Sin(_pulseTimer * 1.2f) + 1f) * 0.5f;
                targetColor = Color.Lerp(hungerColorLow, Color.red, pulse * 0.35f);
            }

            hungerFillImage.color = targetColor;
        }

        if (lowHungerWarning != null)
            lowHungerWarning.SetActive(ratio <= hungerLowThreshold);
    }

    private void UpdateFatigueBar()
    {
        int current = playerStats.fatigue;
        int max = Mathf.Max(1, playerStats.maxFatigue);
        float ratio = Mathf.Clamp01((float)current / max);

        if (fatigueSlider != null)
        {
            fatigueSlider.maxValue = max;
            fatigueSlider.value = current;
        }

        if (fatigueText != null)
            fatigueText.text = $"{current}/{max}";

        if (fatigueFillImage != null)
        {
            ApplyFill(fatigueFillImage, ratio);
            Color targetColor = Color.Lerp(fatigueColorLow, fatigueColorFull, ratio);

            if (ratio <= fatigueLowThreshold && animateLowBars)
            {
                float pulse = (Mathf.Sin(_pulseTimer * 0.85f) + 1f) * 0.5f;
                targetColor = Color.Lerp(fatigueColorLow, Color.gray, pulse * 0.28f);
            }

            fatigueFillImage.color = targetColor;
        }

        if (lowFatigueWarning != null)
            lowFatigueWarning.SetActive(ratio <= fatigueLowThreshold);
    }

    private static void ApplyFill(Image img, float ratio)
    {
        if (img == null) return;
        if (img.type == Image.Type.Filled)
            img.fillAmount = ratio;
    }

    public void FlashHP()
    {
        StartCoroutine(FlashBarCoroutine(hpFillImage, Color.white));
    }

    public void FlashHunger()
    {
        StartCoroutine(FlashBarCoroutine(hungerFillImage, new Color(1f, 0.95f, 0.5f)));
    }

    private System.Collections.IEnumerator FlashBarCoroutine(Image image, Color flashColor)
    {
        if (image == null) yield break;

        Color originalColor = image.color;
        float originalFill = image.type == Image.Type.Filled ? image.fillAmount : 1f;
        image.color = flashColor;

        yield return new WaitForSeconds(0.1f);

        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            image.color = Color.Lerp(flashColor, originalColor, t / 0.2f);
            yield return null;
        }

        image.color = originalColor;
        if (image.type == Image.Type.Filled)
            image.fillAmount = originalFill;
    }
}
