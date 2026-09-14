using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 런 종료 화면 - 통계와 유물 보상을 보여준다. 사망/탈출 둘 다 이 화면을 쓴다.
///
/// 인스펙터로 직접 꾸며도 되고, 비워두면 런타임에 스스로 만든다(BuildRuntimeUIIfNeeded).
/// 씬에 배치돼 있지 않으면 DeathManager 가 필요할 때 생성한다.
/// </summary>
public class DeathScreenUI : MonoBehaviour
{
    [Header("References")]
    public DeathManager deathManager;

    [Header("UI Elements - Stats")]
    public TMP_Text titleText;
    public TMP_Text playTimeText;
    public TMP_Text killsText;
    public TMP_Text progressText;
    public TMP_Text damageText;
    public TMP_Text survivalText;
    public TMP_Text causeOfDeathText;

    [Header("UI Elements - Relic Reward")]
    public GameObject relicRewardPanel;
    public Image relicIcon;
    public TMP_Text relicNameText;
    public TMP_Text relicDescText;
    public TMP_Text relicRarityText;

    [Header("Buttons")]
    public Button continueButton;
    public Button mainMenuButton;

    [Header("Rarity Colors")]
    public Color commonColor = Color.gray;
    public Color rareColor = new Color(0.35f, 0.6f, 1f);
    public Color epicColor = new Color(0.6f, 0.2f, 0.8f); // Purple
    public Color legendaryColor = new Color(1f, 0.8f, 0f); // Gold

    private CanvasGroup _group;
    private bool _subscribed;

    /// <summary>씬에 없을 때 DeathManager 가 부른다.</summary>
    public static DeathScreenUI CreateRuntime()
    {
        var go = new GameObject("DeathScreenUI");
        var ui = go.AddComponent<DeathScreenUI>();
        DontDestroyOnLoad(go);
        return ui;
    }

    private void Awake()
    {
        BuildRuntimeUIIfNeeded();
    }

    private void Start()
    {
        Subscribe();
        SetVisible(false);
    }

    private void OnEnable() => Subscribe();

    /// <summary>
    /// 이 화면은 씬을 넘어 살아남지만 DeathManager 는 씬마다 새로 생긴다.
    /// 챕터를 넘어간 뒤에도 통계가 뜨려면 새 인스턴스에 다시 붙어야 한다.
    /// </summary>
    public void BindTo(DeathManager manager)
    {
        if (manager == null || deathManager == manager) return;

        if (deathManager != null)
            deathManager.OnPlayerDeath -= OnRunEnded;

        deathManager = manager;
        _subscribed = false;
        Subscribe();
        SetVisible(false);
    }

    private void Subscribe()
    {
        // 이전 DeathManager 가 파괴됐으면 다시 붙어야 한다.
        if (deathManager == null)
            _subscribed = false;

        if (_subscribed) return;

        if (deathManager == null)
            deathManager = DeathManager.Instance ?? FindObjectOfType<DeathManager>();

        if (deathManager == null) return;

        deathManager.OnPlayerDeath -= OnRunEnded;
        deathManager.OnPlayerDeath += OnRunEnded;
        _subscribed = true;

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    private void OnDestroy()
    {
        if (deathManager != null)
            deathManager.OnPlayerDeath -= OnRunEnded;
    }

    [Header("Timing")]
    [Tooltip("사망/생환 오버레이 연출을 보여준 뒤 통계 화면이 뜨기까지의 시간(초)")]
    [Range(0f, 4f)] public float showDelaySeconds = 1.3f;

    private void OnRunEnded(RunStatistics stats, RelicDefinition rewardedRelic)
    {
        StartCoroutine(ShowAfterDelay(stats, rewardedRelic));
    }

    private System.Collections.IEnumerator ShowAfterDelay(RunStatistics stats, RelicDefinition rewardedRelic)
    {
        // 간이 오버레이("YOU DIED" / "SURVIVED") 페이드를 먼저 보여준 뒤 통계를 띄운다.
        yield return new WaitForSecondsRealtime(showDelaySeconds);

        DisplayStats(stats);
        DisplayRelicReward(rewardedRelic);
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        if (_group != null)
        {
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }

    private void DisplayStats(RunStatistics stats)
    {
        bool win = stats.victory;

        if (titleText != null)
        {
            titleText.text = win ? "생환" : "사망";
            titleText.color = win ? new Color(0.95f, 0.82f, 0.25f) : new Color(0.85f, 0.2f, 0.2f);
        }

        if (playTimeText != null)
        {
            int minutes = Mathf.FloorToInt(stats.totalPlayTime / 60f);
            int seconds = Mathf.FloorToInt(stats.totalPlayTime % 60f);
            playTimeText.text = $"생존 시간   {minutes}:{seconds:D2}";
        }

        if (killsText != null)
        {
            killsText.text = $"처치   {stats.totalKills}\n" +
                             $"  엘리트   {stats.eliteKills}\n" +
                             $"  보스   {stats.bossKills}";
        }

        if (progressText != null)
        {
            progressText.text = $"도달 챕터   {stats.highestFieldReached}\n" +
                                $"클리어한 던전   {stats.dungeonsCleared}";
        }

        if (damageText != null)
        {
            damageText.text = $"준 피해   {stats.totalDamageDealt}\n" +
                              $"받은 피해   {stats.totalDamageTaken}\n" +
                              $"위기 상황   {stats.closeCalls}";
        }

        if (survivalText != null)
        {
            survivalText.text = $"식사   {stats.mealsEaten}\n" +
                                $"휴식   {stats.timesRested}\n" +
                                $"제작   {stats.itemsCrafted}";
        }

        if (causeOfDeathText != null)
        {
            causeOfDeathText.text = win
                ? "보스를 잡고 빠져나왔다."
                : $"사인   {stats.causeOfDeath}";
        }
    }

    private void DisplayRelicReward(RelicDefinition relic)
    {
        if (relicRewardPanel == null) return;

        if (relic == null)
        {
            relicRewardPanel.SetActive(false);
            return;
        }

        relicRewardPanel.SetActive(true);

        if (relicIcon != null)
        {
            if (relic.icon != null)
            {
                relicIcon.sprite = relic.icon;
                relicIcon.enabled = true;
            }
            else
            {
                relicIcon.enabled = false;
            }
        }

        if (relicNameText != null)
        {
            string name = !string.IsNullOrEmpty(relic.nameKey)
                ? LocalizationManager.T(relic.nameKey) : relic.relicName;

            int stack = RelicManager.Instance != null ? RelicManager.Instance.GetOwnedCount(relic.relicID) : 1;
            bool maxed = RelicDefinition.IsStackMaxed(stack);

            relicNameText.text = stack > 1
                ? $"{name}  Lv.{Mathf.Min(stack, RelicDefinition.MaxStackLevel)}{(maxed ? " (MAX)" : "")}"
                : name;
        }

        if (relicDescText != null)
            relicDescText.text = !string.IsNullOrEmpty(relic.descKey)
                ? LocalizationManager.T(relic.descKey) : relic.description;

        if (relicRarityText != null)
        {
            relicRarityText.text = relic.rarity.ToString();
            relicRarityText.color = GetRarityColor(relic.rarity);
        }
    }

    private Color GetRarityColor(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Common: return commonColor;
            case RelicRarity.Rare: return rareColor;
            case RelicRarity.Epic: return epicColor;
            case RelicRarity.Legendary: return legendaryColor;
            default: return Color.white;
        }
    }

    private void OnContinueClicked()
    {
        SetVisible(false);
        // 기존 코드는 존재하지 않는 "CharacterSelect" 씬을 로드해 터졌다.
        // 런 종료 후 갈 곳은 타이틀뿐이다.
        deathManager?.ReturnToTitleNow();
    }

    private void OnMainMenuClicked()
    {
        SetVisible(false);
        deathManager?.ReturnToTitleNow();
    }

    // ============================================
    // 런타임 UI 생성 (인스펙터로 안 꾸몄을 때만)
    // ============================================

    private void BuildRuntimeUIIfNeeded()
    {
        // 하나라도 수동 연결돼 있으면 건드리지 않는다.
        if (titleText != null || continueButton != null) return;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 간이 사망 오버레이(30000)보다 위에 떠야 통계가 보인다.
        canvas.sortingOrder = 31000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var bg = NewRect("BG", transform);
        var bgImg = bg.gameObject.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.04f, 0.07f, 0.96f);
        Stretch(bg);

        titleText = NewText("Title", transform, 84, FontStyles.Bold, Color.white);
        Anchor(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 110f), new Vector2(0f, -90f));
        titleText.alignment = TextAlignmentOptions.Center;

        causeOfDeathText = NewText("Cause", transform, 28, FontStyles.Italic, new Color(0.7f, 0.74f, 0.8f));
        Anchor(causeOfDeathText.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 40f), new Vector2(0f, -190f));
        causeOfDeathText.alignment = TextAlignmentOptions.Center;

        // 통계 4칸을 가로로
        float colW = 340f;
        float y = -300f;
        playTimeText = NewStatColumn("PlayTime", new Vector2(-colW * 1.5f, y), colW);
        killsText = NewStatColumn("Kills", new Vector2(-colW * 0.5f, y), colW);
        progressText = NewStatColumn("Progress", new Vector2(colW * 0.5f, y), colW);
        damageText = NewStatColumn("Damage", new Vector2(colW * 1.5f, y), colW);

        survivalText = NewText("Survival", transform, 26, FontStyles.Normal, new Color(0.78f, 0.82f, 0.88f));
        Anchor(survivalText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1400f, 110f), new Vector2(0f, -470f));
        survivalText.alignment = TextAlignmentOptions.Center;

        BuildRelicPanel();

        var btnRect = NewRect("ContinueButton", transform);
        Anchor(btnRect, new Vector2(0.5f, 0f), new Vector2(460f, 96f), new Vector2(0f, 110f));
        var btnImg = btnRect.gameObject.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.34f, 0.5f);
        continueButton = btnRect.gameObject.AddComponent<Button>();
        continueButton.targetGraphic = btnImg;

        var btnLabel = NewText("Label", btnRect, 32, FontStyles.Bold, Color.white);
        btnLabel.alignment = TextAlignmentOptions.Center;
        Stretch(btnLabel.rectTransform);
        btnLabel.text = "타이틀로";

        EnsureEventSystem();
    }

    private void BuildRelicPanel()
    {
        var panel = NewRect("RelicRewardPanel", transform);
        Anchor(panel, new Vector2(0.5f, 0f), new Vector2(900f, 230f), new Vector2(0f, 250f));
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.11f, 0.16f, 0.9f);
        relicRewardPanel = panel.gameObject;

        var header = NewText("Header", panel, 24, FontStyles.Bold, new Color(0.6f, 0.65f, 0.72f));
        Anchor(header.rectTransform, new Vector2(0.5f, 1f), new Vector2(860f, 34f), new Vector2(0f, -14f));
        header.alignment = TextAlignmentOptions.Center;
        header.text = "획득한 유물";

        var iconRect = NewRect("Icon", panel);
        Anchor(iconRect, new Vector2(0f, 0.5f), new Vector2(96f, 96f), new Vector2(90f, -20f));
        relicIcon = iconRect.gameObject.AddComponent<Image>();
        relicIcon.preserveAspect = true;

        relicNameText = NewText("Name", panel, 32, FontStyles.Bold, Color.white);
        Anchor(relicNameText.rectTransform, new Vector2(0f, 1f), new Vector2(620f, 42f), new Vector2(480f, -62f));
        relicNameText.alignment = TextAlignmentOptions.Left;

        relicRarityText = NewText("Rarity", panel, 24, FontStyles.Bold, Color.gray);
        Anchor(relicRarityText.rectTransform, new Vector2(0f, 1f), new Vector2(620f, 32f), new Vector2(480f, -104f));
        relicRarityText.alignment = TextAlignmentOptions.Left;

        relicDescText = NewText("Desc", panel, 24, FontStyles.Normal, new Color(0.76f, 0.8f, 0.86f));
        Anchor(relicDescText.rectTransform, new Vector2(0f, 1f), new Vector2(620f, 64f), new Vector2(480f, -146f));
        relicDescText.alignment = TextAlignmentOptions.TopLeft;
    }

    private TMP_Text NewStatColumn(string name, Vector2 offset, float width)
    {
        var t = NewText(name, transform, 26, FontStyles.Normal, new Color(0.84f, 0.88f, 0.93f));
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(width, 150f), offset);
        t.alignment = TextAlignmentOptions.Top;
        return t;
    }

    private static RectTransform NewRect(string name, Transform parent)
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

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, anchor.y);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    /// <summary>폰트를 지정하지 않으면 TMP 기본 폰트(Pretendard)를 써서 한글이 정상 출력된다.</summary>
    private static TMP_Text NewText(string name, Transform parent, float size, FontStyles style, Color color)
    {
        var rt = NewRect(name, parent);
        var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        return text;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        DontDestroyOnLoad(go);
    }
}
