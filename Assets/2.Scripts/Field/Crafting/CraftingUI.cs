using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 제작창. 화면 왼쪽 아래에 뜬다.
///
/// 예전 구현의 문제:
///   - ScrollRect 가 없어서 패널 밖으로 넘어간 레시피는 보이지도 눌리지도 않았다.
///     레시피가 24개라 대부분 잘렸다.
///   - 카테고리 버튼이 4개(전체/도구/건축/저장)뿐인데 RecipeCategory 는 10종이라
///     무기·갑옷·장신구·음식·회복은 '전체'로만 접근 가능했고, 그마저 스크롤이 없어 못 눌렀다.
///
/// 그래서 프리팹 의존을 버리고 런타임 생성으로 다시 만들었다.
/// 클래스 이름과 공개 메서드는 그대로 두어 기존 호출부(InventoryUI, InventoryManager,
/// GameHUDManager)를 건드리지 않는다.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    [Header("Legacy (더 이상 쓰지 않음 — 인스펙터 연결이 남아 있어도 무시된다)")]
    public GameObject craftingPanel;
    public Transform recipeGridParent;
    public GameObject recipeSlotPrefab;
    public GameObject recipeTooltip;
    public TextMeshProUGUI tooltipText;
    public Button btnAll;
    public Button btnTool;
    public Button btnBuilding;
    public Button btnStorage;

    [Header("Colors")]
    public Color selectedColor = new Color(0.22f, 0.42f, 0.62f);
    public Color normalColor = new Color(0.16f, 0.18f, 0.24f);

    [Header("Layout")]
    [Tooltip("패널 크기 (왼쪽 아래 기준)")]
    public Vector2 panelSize = new Vector2(620f, 560f);
    [Tooltip("화면 가장자리에서 띄울 여백")]
    public Vector2 panelMargin = new Vector2(24f, 24f);

    // 카테고리 탭 — RecipeCategory 전체를 노출한다.
    private static readonly RecipeCategory[] Categories =
    {
        RecipeCategory.All,
        RecipeCategory.Tool,
        RecipeCategory.Weapon,
        RecipeCategory.Armor,
        RecipeCategory.Accessory,
        RecipeCategory.Food,
        RecipeCategory.Medicine,
        RecipeCategory.Material,
        RecipeCategory.Building,
        RecipeCategory.Storage,
    };

    private static string CategoryLabel(RecipeCategory c)
    {
        switch (c)
        {
            case RecipeCategory.All: return "전체";
            case RecipeCategory.Tool: return "도구";
            case RecipeCategory.Weapon: return "무기";
            case RecipeCategory.Armor: return "갑옷";
            case RecipeCategory.Accessory: return "장신구";
            case RecipeCategory.Food: return "음식";
            case RecipeCategory.Medicine: return "회복";
            case RecipeCategory.Material: return "재료";
            case RecipeCategory.Building: return "건축";
            case RecipeCategory.Storage: return "저장";
            default: return c.ToString();
        }
    }

    private GameObject _root;
    private Transform _listContent;
    private TMP_Text _headerText;
    private readonly Dictionary<RecipeCategory, Button> _tabButtons = new Dictionary<RecipeCategory, Button>();
    private RecipeCategory _currentCategory = RecipeCategory.All;

    private void Awake()
    {
        HideLegacyPanel();
        if (_root == null) Build();
    }

    /// <summary>
    /// 씬에 남아 있는 예전 제작창 오브젝트를 끈다.
    /// 참조만 안 쓰는 게 아니라 오브젝트 자체가 켜져 있으면 화면에 그대로 뜬다.
    /// </summary>
    private void HideLegacyPanel()
    {
        if (craftingPanel != null && craftingPanel != _root)
            craftingPanel.SetActive(false);

        if (recipeTooltip != null)
            recipeTooltip.SetActive(false);

        // 옛 패널이 인스펙터에 연결돼 있지 않아도, 레시피 그리드 부모가 살아 있으면 같이 끈다.
        if (recipeGridParent != null)
        {
            var go = recipeGridParent.gameObject;
            if (go != _root) go.SetActive(false);
        }
    }

    // ============================================
    // 공개 API (기존과 동일)
    // ============================================

    public void OpenCrafting()
    {
        if (_root == null) Build();
        if (_root == null) return;

        _root.SetActive(true);
        RefreshRecipes();
    }

    public void CloseCrafting()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    public bool IsOpen() => _root != null && _root.activeSelf;

    public void RefreshRecipes()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        var mgr = CraftingManager.Instance;
        if (mgr == null)
        {
            CreateText(_listContent, "CraftingManager 를 찾을 수 없습니다.", 18,
                FontStyles.Italic, new Color(0.9f, 0.5f, 0.5f));
            return;
        }

        var inventory = InventoryManager.Instance ?? FindObjectOfType<InventoryManager>();
        var recipes = mgr.GetRecipesByCategory(_currentCategory);

        // 제작 가능한 것을 위로 올린다.
        recipes.Sort((a, b) =>
        {
            bool ca = inventory != null && a.CanCraft(inventory);
            bool cb = inventory != null && b.CanCraft(inventory);
            if (ca != cb) return ca ? -1 : 1;
            return 0;
        });

        int craftable = 0;
        foreach (var r in recipes)
        {
            bool can = inventory != null && r.CanCraft(inventory);
            if (can) craftable++;
            CreateRecipeRow(r, can, inventory);
        }

        if (recipes.Count == 0)
        {
            CreateText(_listContent, "이 분류에는 레시피가 없습니다.", 18,
                FontStyles.Italic, new Color(0.6f, 0.65f, 0.72f));
        }

        if (_headerText != null)
            _headerText.text = $"제작   {craftable} / {recipes.Count}";

        RefreshTabColors();
    }

    /// <summary>레거시 호환 — 툴팁은 각 행에 직접 표시하므로 더 이상 쓰지 않는다.</summary>
    public void ShowRecipeTooltip(string text) { }
    public void HideRecipeTooltip() { }

    public void TryCraft(CraftingRecipe recipe)
    {
        var mgr = CraftingManager.Instance;
        if (mgr == null || recipe == null) return;

        if (mgr.CraftItem(recipe))
            RefreshRecipes();
    }

    // ============================================
    // 목록 항목
    // ============================================

    private void CreateRecipeRow(CraftingRecipe recipe, bool canCraft, InventoryManager inventory)
    {
        var rowColor = canCraft
            ? new Color(0.11f, 0.2f, 0.16f, 0.95f)
            : new Color(0.13f, 0.13f, 0.16f, 0.95f);

        var row = CreatePanel($"Recipe_{recipe.resultItemID}", _listContent, rowColor);
        row.AddComponent<SlotRowHover>();

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 64;
        rowLe.preferredHeight = 64;

        // 결과 아이템 아이콘
        var resultData = ItemDatabase.Instance != null
            ? ItemDatabase.Instance.GetItemData(recipe.resultItemID)
            : null;

        var iconRect = NewRect("Icon", row.transform);
        var iconLe = iconRect.gameObject.AddComponent<LayoutElement>();
        iconLe.minWidth = 44; iconLe.preferredWidth = 44; iconLe.flexibleWidth = 0;
        var iconImg = iconRect.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        if (resultData != null && resultData.icon != null)
            iconImg.sprite = resultData.icon;
        else
            iconImg.color = new Color(1f, 1f, 1f, 0.12f);

        // 이름 + 재료
        var info = CreatePanel("Info", row.transform, Color.clear);
        var infoLe = info.AddComponent<LayoutElement>();
        infoLe.flexibleWidth = 1f;
        var infoLayout = info.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        string resultName = resultData != null ? resultData.itemName : $"#{recipe.resultItemID}";
        if (recipe.resultCount > 1) resultName += $" x{recipe.resultCount}";

        var title = CreateText(info.transform, resultName, 19, FontStyles.Bold,
            canCraft ? Color.white : new Color(0.62f, 0.64f, 0.68f));
        title.alignment = TextAlignmentOptions.Left;

        var mats = CreateText(info.transform, BuildIngredientText(recipe, inventory), 15,
            FontStyles.Normal, new Color(0.74f, 0.79f, 0.85f));
        mats.alignment = TextAlignmentOptions.TopLeft;

        // 제작 버튼
        var btn = CreateButton(row.transform, "제작",
            canCraft ? new Color(0.16f, 0.4f, 0.26f) : new Color(0.25f, 0.25f, 0.28f), 78);
        btn.interactable = canCraft;

        var captured = recipe;
        btn.onClick.AddListener(() => TryCraft(captured));
    }

    /// <summary>재료를 '보유/필요' 로 보여준다. 모자란 건 빨갛게.</summary>
    private static string BuildIngredientText(CraftingRecipe recipe, InventoryManager inventory)
    {
        if (recipe.ingredients == null || recipe.ingredients.Count == 0)
            return "재료 없음";

        var parts = new List<string>();
        foreach (var ing in recipe.ingredients)
        {
            var data = ItemDatabase.Instance != null
                ? ItemDatabase.Instance.GetItemData(ing.itemID)
                : null;

            string name = data != null ? data.itemName : $"#{ing.itemID}";
            int have = inventory != null ? inventory.GetItemCount(ing.itemID) : 0;

            string chunk = $"{name} {have}/{ing.count}";
            if (have < ing.count)
                chunk = $"<color=#E06C6C>{chunk}</color>";

            parts.Add(chunk);
        }

        return string.Join("   ", parts);
    }

    // ============================================
    // 런타임 UI 생성
    // ============================================

    private void Build()
    {
        var canvas = UICanvasUtil.FindCanvasInActiveScene();
        if (canvas == null)
        {
            Debug.LogError("[CraftingUI] 현재 씬에서 Canvas를 찾을 수 없습니다.");
            return;
        }

        // 왼쪽 아래 고정 패널. 전체 화면을 덮지 않으므로 뒤쪽 조작을 가리지 않는다.
        _root = CreatePanel("CraftingPanel", canvas.transform, new Color(0.07f, 0.09f, 0.14f, 0.97f));
        var rt = _root.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.sizeDelta = panelSize;
        rt.anchoredPosition = panelMargin;

        var panelLayout = _root.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(12, 12, 12, 12);
        panelLayout.spacing = 8;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        _headerText = CreateText(_root.transform, "제작", 22, FontStyles.Bold, Color.white);
        _headerText.alignment = TextAlignmentOptions.Left;
        var headerLe = _headerText.gameObject.AddComponent<LayoutElement>();
        headerLe.minHeight = 28; headerLe.preferredHeight = 28;

        BuildCategoryTabs();

        // 스크롤 목록 — 예전엔 이게 없어서 넘친 레시피를 누를 수 없었다.
        var scrollGo = CreatePanel("Scroll", _root.transform, new Color(0.04f, 0.06f, 0.1f, 1f));
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 300f;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        var viewport = CreatePanel("Viewport", scrollGo.transform, Color.clear);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        // CreatePanel 이 이미 Image 를 붙이므로 AddComponent 하지 말고 색만 바꾼다.
        viewport.GetComponent<Image>().color = Color.white;

        var content = CreatePanel("Content", viewport.transform, Color.clear);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;

        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6;
        contentLayout.padding = new RectOffset(6, 6, 6, 6);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;
        _listContent = content.transform;

        var closeBtn = CreateButton(_root.transform, "닫기", new Color(0.12f, 0.22f, 0.35f), 0);
        closeBtn.onClick.AddListener(CloseCrafting);

        EnsureEventSystem();
        _root.SetActive(false);
    }

    private void BuildCategoryTabs()
    {
        // 10종이라 한 줄에 다 넣으면 좁다. 두 줄로 나눈다.
        int perRow = 5;
        for (int start = 0; start < Categories.Length; start += perRow)
        {
            var rowGo = CreatePanel($"Tabs_{start}", _root.transform, Color.clear);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 4;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;

            var rowLe = rowGo.AddComponent<LayoutElement>();
            rowLe.minHeight = 34; rowLe.preferredHeight = 34;

            for (int i = start; i < Mathf.Min(start + perRow, Categories.Length); i++)
            {
                var cat = Categories[i];
                var b = CreateButton(rowGo.transform, CategoryLabel(cat), normalColor, 0, 30f, 15);
                _tabButtons[cat] = b;
                b.onClick.AddListener(() =>
                {
                    _currentCategory = cat;
                    RefreshRecipes();
                });
            }
        }
    }

    private void RefreshTabColors()
    {
        foreach (var kv in _tabButtons)
        {
            if (kv.Value == null) continue;
            var img = kv.Value.GetComponent<Image>();
            if (img != null)
                img.color = kv.Key == _currentCategory ? selectedColor : normalColor;
        }
    }

    // ============================================
    // 생성 헬퍼
    // ============================================

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
        tmp.enableWordWrapping = true;
        tmp.richText = true;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string label, Color bg, float width,
        float height = 38f, float fontSize = 17f)
    {
        var go = CreatePanel(label + "Button", parent, bg);

        // 높이를 안 주면 childForceExpandHeight=false 인 세로 레이아웃에서 높이가 0이 되어
        // 글자만 보이고 클릭은 안 되는 버튼이 된다.
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        if (width > 0f)
        {
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bg * 1.25f;
        colors.pressedColor = bg * 0.85f;
        btn.colors = colors;

        var text = CreateText(go.transform, label, fontSize, FontStyles.Bold, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);

        return btn;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null) return;

        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }
}
