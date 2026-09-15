using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 타이틀의 유물 화면. 보유 유물·중첩 레벨·효과를 보고, 슬롯 한도 안에서 장착을 고른다.
///
/// 유물은 계정 단위로 쌓이는데 여태 이걸 볼 방법이 아예 없었다("죽어도 강해진다"가 체감되지 않음).
/// 프리팹 없이 런타임에 생성한다.
/// </summary>
public class RelicCollectionUI : MonoBehaviour
{
    public static RelicCollectionUI Instance { get; private set; }

    private GameObject _root;
    private Transform _listContent;
    private TMP_Text _slotStatusText;
    private Button _unlockButton;
    private TMP_Text _unlockLabel;

    public bool IsOpen => _root != null && _root.activeSelf;

    public static RelicCollectionUI Ensure()
    {
        if (Instance != null) return Instance;

        var existing = FindObjectOfType<RelicCollectionUI>(true);
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        var go = new GameObject("RelicCollectionUI");
        Instance = go.AddComponent<RelicCollectionUI>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_root == null)
            Build();
    }

    [Header("Debug")]
    [Tooltip("유물 화면이 열려 있을 때만 동작하는 테스트용 단축키. 출시 전 false 로 두거나 지울 것.")]
    public bool enableDebugKeys = true;

    private void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        if (enableDebugKeys)
            HandleDebugKeys();
    }

    /// <summary>
    /// 테스트용. 조각은 원래 챕터 보스를 잡아야 나오고 유물은 런이 끝나야 나오는데,
    /// 그걸 다 하고 UI를 확인하기엔 너무 오래 걸려서 넣어둔 단축키다.
    /// </summary>
    private void HandleDebugKeys()
    {
        var mgr = RelicManager.Instance;
        if (mgr == null) return;

        // F1/F2 는 에디터가 도움말·이름변경으로 가로채서 Game 뷰까지 오지 않는다. 숫자키를 쓴다.
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            mgr.AddShards(1);
            Debug.Log($"[RelicCollectionUI] (디버그) 조각 +1 → 보유 {mgr.Shards}");
            Refresh();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            var relic = mgr.GetRandomRelic();
            if (relic != null)
            {
                mgr.AddRelic(relic);
                Debug.Log($"[RelicCollectionUI] (디버그) 유물 획득: {relic.relicName} " +
                          $"(중첩 {mgr.GetOwnedCount(relic.relicID)})");
                Refresh();
            }
            else
            {
                Debug.LogWarning("[RelicCollectionUI] 뽑을 유물이 없다. RelicManager.allRelics 가 비었는지 확인.");
            }
        }
    }

    // 0 이면 '보기 전용'(캐릭터가 정해지지 않음). 1 이상이면 그 캐릭터의 장착을 편집한다.
    private int _editingSlot;
    private string _editingName = "";

    /// <summary>타이틀에서 보기 전용으로 연다. 슬롯 해금은 여기서도 가능하다.</summary>
    public void Open()
    {
        OpenInternal(0, "");
    }

    /// <summary>특정 캐릭터(런 슬롯)의 유물 장착을 편집한다.</summary>
    public void OpenForSlot(int slot, string characterName)
    {
        OpenInternal(slot, characterName);
    }

    private void OpenInternal(int slot, string characterName)
    {
        if (_root == null) Build();
        if (_root == null) return;

        _editingSlot = slot;
        _editingName = characterName ?? "";

        // 장착 조합은 캐릭터마다 다르므로 해당 슬롯 세이브에서 읽어온다.
        var mgr = RelicManager.Instance;
        if (mgr != null)
        {
            mgr.SetEquippedByIDs(slot > 0
                ? RunSlotSaveService.GetEquippedRelicIDs(slot)
                : new List<string>());
        }

        _root.SetActive(true);
        Refresh();
    }

    /// <summary>편집 중인 캐릭터 슬롯에 현재 조합을 기록한다.</summary>
    private void PersistEquipped()
    {
        if (_editingSlot <= 0) return;
        var mgr = RelicManager.Instance;
        if (mgr == null) return;

        RunSlotSaveService.SetEquippedRelicIDs(_editingSlot, mgr.GetEquippedRelicIDs());
    }

    public void Close()
    {
        if (_root != null)
            _root.SetActive(false);
    }

    // ============================================
    // 내용 갱신
    // ============================================

    private void Refresh()
    {
        var mgr = RelicManager.Instance;
        if (mgr == null || _listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        var equipped = mgr.GetEquippedRelics();

        string who = _editingSlot > 0
            ? $"[{(string.IsNullOrEmpty(_editingName) ? $"슬롯 {_editingSlot}" : _editingName)}]  "
            : "[보기 전용 — 캐릭터를 골라야 장착할 수 있다]  ";

        _slotStatusText.text = who +
            $"장착 {equipped.Count} / {mgr.UnlockedSlots}   " +
            $"(최대 {RelicManager.MaxSlots})   조각 {mgr.Shards}";

        bool maxed = mgr.UnlockedSlots >= RelicManager.MaxSlots;
        _unlockButton.gameObject.SetActive(!maxed);
        if (!maxed)
        {
            _unlockLabel.text = $"슬롯 해금  (조각 {mgr.NextSlotCost})";
            _unlockButton.interactable = mgr.CanUnlockNextSlot;
        }

        var owned = mgr.GetAllOwnedRelics();
        if (owned.Count == 0)
        {
            CreateText(_listContent, "아직 얻은 유물이 없다.\n런이 끝나면 유물을 하나 얻는다.",
                22, FontStyles.Italic, new Color(0.6f, 0.66f, 0.74f));
            return;
        }

        var sorted = new List<RelicDefinition>(owned.Keys);
        sorted.Sort((a, b) =>
        {
            int byRarity = b.rarity.CompareTo(a.rarity);
            if (byRarity != 0) return byRarity;
            return string.Compare(a.relicName, b.relicName, System.StringComparison.Ordinal);
        });

        foreach (var relic in sorted)
            CreateRelicRow(relic, mgr, equipped.Contains(relic));
    }

    private void CreateRelicRow(RelicDefinition relic, RelicManager mgr, bool isEquipped)
    {
        int stack = mgr.GetOwnedCount(relic.relicID);
        bool maxed = RelicDefinition.IsStackMaxed(stack);
        int level = Mathf.Min(stack, RelicDefinition.MaxStackLevel);

        var rowColor = isEquipped
            ? new Color(0.12f, 0.28f, 0.22f, 0.95f)
            : new Color(0.08f, 0.12f, 0.2f, 0.95f);

        var row = CreatePanel($"Relic_{relic.relicID}", _listContent, rowColor);
        row.AddComponent<SlotRowHover>();

        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 10);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 84;
        rowLe.preferredHeight = 84;

        var info = CreatePanel("Info", row.transform, Color.clear);
        var infoLe = info.AddComponent<LayoutElement>();
        infoLe.flexibleWidth = 1f;
        var infoLayout = info.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        string name = !string.IsNullOrEmpty(relic.nameKey)
            ? LocalizationManager.T(relic.nameKey) : relic.relicName;
        string levelTag = stack > 1 ? $"  Lv.{level}{(maxed ? " (MAX)" : "")}" : "";

        var title = CreateText(info.transform, $"{name}{levelTag}", 22, FontStyles.Bold, RarityColor(relic.rarity));
        title.alignment = TextAlignmentOptions.Left;

        var effect = CreateText(info.transform, BuildEffectText(relic, stack), 16, FontStyles.Normal,
            new Color(0.78f, 0.84f, 0.9f));
        effect.alignment = TextAlignmentOptions.TopLeft;

        var toggle = CreateButton(row.transform, isEquipped ? "해제" : "장착",
            isEquipped ? new Color(0.4f, 0.22f, 0.18f) : new Color(0.14f, 0.32f, 0.48f), 96);

        // 슬롯이 꽉 찼으면 새로 장착은 막고, 이미 낀 것의 해제는 허용한다.
        // 캐릭터가 정해지지 않은 보기 전용 모드에선 장착 자체가 불가능하다.
        bool slotsFull = mgr.GetEquippedRelics().Count >= mgr.UnlockedSlots;
        toggle.interactable = _editingSlot > 0 && (isEquipped || !slotsFull);

        var captured = relic;
        toggle.onClick.AddListener(() =>
        {
            if (isEquipped) mgr.UnequipRelic(captured);
            else mgr.EquipRelic(captured);
            PersistEquipped();
            Refresh();
        });
    }

    /// <summary>중첩까지 반영된 실제 효과를 보여준다. 0인 항목은 생략.</summary>
    private static string BuildEffectText(RelicDefinition relic, int stack)
    {
        float mul = relic.GetStackMultiplier(stack);
        var parts = new List<string>();

        int hp = Mathf.RoundToInt(relic.bonusMaxHP * mul);
        if (hp != 0) parts.Add($"최대HP +{hp}");

        float atk = relic.bonusATKPercent * mul;
        if (Mathf.Abs(atk) > 0.0001f) parts.Add($"공격력 +{atk:P0}");

        int def = Mathf.RoundToInt(relic.bonusDEF * mul);
        if (def != 0) parts.Add($"방어력 +{def}");

        float eva = relic.bonusEVA * mul;
        if (Mathf.Abs(eva) > 0.0001f) parts.Add($"회피 +{eva:P1}");

        float crit = relic.bonusCRIT * mul;
        if (Mathf.Abs(crit) > 0.0001f) parts.Add($"치명타 +{crit:P1}");

        float critDmg = relic.bonusCRIT_DMG * mul;
        if (Mathf.Abs(critDmg) > 0.0001f) parts.Add($"치명타 피해 +{critDmg:P0}");

        if (parts.Count == 0)
        {
            return !string.IsNullOrEmpty(relic.descKey)
                ? LocalizationManager.T(relic.descKey) : relic.description;
        }

        return string.Join("   ", parts);
    }

    private static Color RarityColor(RelicRarity rarity)
    {
        switch (rarity)
        {
            case RelicRarity.Rare: return new Color(0.45f, 0.7f, 1f);
            case RelicRarity.Epic: return new Color(0.75f, 0.45f, 0.95f);
            case RelicRarity.Legendary: return new Color(1f, 0.82f, 0.25f);
            default: return new Color(0.85f, 0.88f, 0.92f);
        }
    }

    // ============================================
    // 런타임 UI 생성
    // ============================================

    private void Build()
    {
        var canvas = UICanvasUtil.FindCanvasInActiveScene();
        if (canvas == null)
        {
            Debug.LogError("[RelicCollectionUI] 현재 씬에서 Canvas를 찾을 수 없습니다.");
            return;
        }

        _root = CreatePanel("RelicCollectionRoot", canvas.transform, new Color(0f, 0f, 0f, 0.78f));
        Stretch(_root.GetComponent<RectTransform>());

        var panel = CreatePanel("Panel", _root.transform, new Color(0.06f, 0.1f, 0.17f, 0.99f));
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(860f, 760f);

        var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(22, 22, 22, 22);
        panelLayout.spacing = 12;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        var header = CreateText(panel.transform, "유물", 30, FontStyles.Bold, Color.white);
        header.alignment = TextAlignmentOptions.Center;

        _slotStatusText = CreateText(panel.transform, "", 18, FontStyles.Normal, new Color(0.75f, 0.82f, 0.9f));
        _slotStatusText.alignment = TextAlignmentOptions.Center;

        _unlockButton = CreateButton(panel.transform, "슬롯 해금", new Color(0.3f, 0.26f, 0.1f), 0);
        _unlockLabel = _unlockButton.GetComponentInChildren<TMP_Text>();
        _unlockButton.onClick.AddListener(() =>
        {
            if (RelicManager.Instance != null && RelicManager.Instance.TryUnlockNextSlot())
                Refresh();
        });

        var scrollGo = CreatePanel("Scroll", panel.transform, new Color(0.04f, 0.07f, 0.12f, 1f));
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 480f;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

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
        contentLayout.spacing = 8;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;
        _listContent = content.transform;

        if (enableDebugKeys)
        {
            var hint = CreateText(panel.transform,
                "[디버그]  1 = 조각 +1     2 = 유물 획득",
                15, FontStyles.Italic, new Color(0.55f, 0.6f, 0.68f));
            hint.alignment = TextAlignmentOptions.Center;
        }

        var closeBtn = CreateButton(panel.transform, "닫기", new Color(0.12f, 0.22f, 0.35f), 140);
        closeBtn.onClick.AddListener(Close);

        _root.SetActive(false);
    }

    private const float ButtonHeight = 42f;

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
        return tmp;
    }

    private static Button CreateButton(Transform parent, string label, Color bg, float width)
    {
        var go = CreatePanel(label + "Button", parent, bg);

        // 높이를 안 주면 childForceExpandHeight=false 인 세로 레이아웃에서 높이가 0이 되어
        // 글자만 보이고 클릭은 안 되는 버튼이 된다.
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = ButtonHeight;
        le.preferredHeight = ButtonHeight;
        if (width > 0f)
        {
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bg * 1.2f;
        colors.pressedColor = bg * 0.85f;
        btn.colors = colors;

        var text = CreateText(go.transform, label, 18, FontStyles.Bold, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);

        return btn;
    }
}
