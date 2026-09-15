using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 타이틀에서 캐릭터(슬롯) 목록 · 이름 입력 · 이어하기.
/// Inspector 연결이 없으면 런타임에 기본 UI를 생성한다.
/// </summary>
public class RunSlotSelectUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "NewMapScene";
    [SerializeField] private TitleSceneUI titleSceneUI;

    private GameObject _root;
    private GameObject _listPanel;
    private GameObject _nameDialog;
    private Transform _listContent;
    private TMP_InputField _nameInput;
    private TMP_Text _nameDialogHint;
    private int _pendingNewSlot = -1;
    private TMP_FontAsset _font;

    public bool IsOpen => _root != null && _root.activeSelf;

    private void Awake()
    {
        if (_root == null)
            BuildDefaultUI();
    }

    public void Configure(TitleSceneUI title, string sceneName)
    {
        titleSceneUI = title;
        if (!string.IsNullOrEmpty(sceneName))
            gameSceneName = sceneName;
    }

    public void Open(bool focusLatestIfAny = false)
    {
        if (_root == null)
            BuildDefaultUI();

        _root.SetActive(true);
        RefreshList();

        if (focusLatestIfAny)
        {
            int latest = RunSlotSaveService.GetLatestSlot();
            if (latest > 0)
                HighlightSlot(latest);
        }
    }

    private void Update()
    {
        if (!IsOpen) return;
        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        // 이름 입력 중이면 그것만 닫고, 아니면 목록을 닫는다.
        if (_nameDialog != null && _nameDialog.activeSelf)
            HideNameDialog();
        else
            Close();
    }

    public void Close()
    {
        if (_nameDialog != null)
            _nameDialog.SetActive(false);
        if (_root != null)
            _root.SetActive(false);
    }

    private void RefreshList()
    {
        if (_listContent == null) return;

        for (int i = _listContent.childCount - 1; i >= 0; i--)
            Destroy(_listContent.GetChild(i).gameObject);

        var summaries = RunSlotSaveService.GetAllSummaries();
        foreach (var summary in summaries)
            CreateSlotRow(summary);
    }

    private void CreateSlotRow(RunSlotSummary summary)
    {
        var row = CreatePanel($"SlotRow_{summary.slot}", _listContent, new Color(0.08f, 0.16f, 0.28f, 0.95f));
        row.AddComponent<SlotRowHover>();
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.MiddleLeft;
        // true 로 두면 남는 폭을 '자식 수'로 균등 분배한다. 빈 슬롯(자식 2개)과
        // 저장된 슬롯(삭제 버튼까지 3개)의 분배가 달라져 버튼 위치가 행마다 어긋난다.
        // 슬랙은 정보 영역이 전부 흡수하고 버튼은 고정 폭을 유지하게 한다.
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var le = row.AddComponent<LayoutElement>();
        le.minHeight = 56;
        le.preferredHeight = 56;

        var info = CreatePanel("Info", row.transform, Color.clear);
        var infoLe = info.AddComponent<LayoutElement>();
        infoLe.flexibleWidth = 1f;
        var infoLayout = info.AddComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 2;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        string title = summary.isEmpty
            ? $"슬롯 {summary.slot} — 빈 슬롯"
            : $"{summary.DisplayName}  (슬롯 {summary.slot})";
        CreateText(info.transform, title, 18, FontStyles.Bold, Color.white);

        if (!summary.isEmpty)
        {
            string sub = $"{summary.ProgressText}   |   {summary.SavedAtText}";
            CreateText(info.transform, sub, 14, FontStyles.Normal, new Color(0.75f, 0.85f, 0.95f));
        }
        else
        {
            CreateText(info.transform, "클릭하여 새 캐릭터 만들기", 14, FontStyles.Italic, new Color(0.65f, 0.75f, 0.85f));
        }

        if (!summary.isEmpty)
        {
            int relicSlot = summary.slot;
            string relicName = summary.DisplayName;
            var relicBtn = CreateButton(row.transform, "유물", new Color(0.3f, 0.26f, 0.1f, 1f), 72);
            relicBtn.onClick.AddListener(() =>
            {
                // 장착 조합은 캐릭터마다 다르므로 이 슬롯을 지정해서 연다.
                var ui = RelicCollectionUI.Ensure();
                if (ui != null) ui.OpenForSlot(relicSlot, relicName);
            });

            var deleteBtn = CreateButton(row.transform, "삭제", new Color(0.45f, 0.12f, 0.12f, 1f), 72);
            int slot = summary.slot;
            deleteBtn.onClick.AddListener(() => ConfirmDelete(slot, summary.DisplayName));
        }

        var playBtn = CreateButton(row.transform, summary.isEmpty ? "새로" : "시작", new Color(0.12f, 0.28f, 0.45f, 1f), 72);
        playBtn.onClick.AddListener(() =>
        {
            if (summary.isEmpty)
                ShowNameDialog(summary.slot);
            else
                StartContinue(summary.slot);
        });
    }

    private void HighlightSlot(int slot)
    {
        if (_listContent == null) return;
        for (int i = 0; i < _listContent.childCount; i++)
        {
            var child = _listContent.GetChild(i);
            if (child.name != $"SlotRow_{slot}") continue;

            // 호버 컴포넌트가 기본색을 들고 있으므로 그쪽에 알려야 마우스가 빠질 때 되돌아간다.
            var selected = new Color(0.14f, 0.32f, 0.52f, 1f);
            if (child.TryGetComponent<SlotRowHover>(out var hover))
                hover.SetBaseColor(selected);
            else if (child.TryGetComponent<Image>(out var img))
                img.color = selected;
        }
    }

    private void ConfirmDelete(int slot, string displayName)
    {
        // 간단 확인: 바로 삭제 (추후 확인 팝업으로 교체 가능)
        RunSlotSaveService.DeleteSlot(slot);
        RefreshList();
        titleSceneUI?.SetContinueEnabled(RunSlotSaveService.HasAnySlot());
    }

    private void ShowNameDialog(int slot)
    {
        _pendingNewSlot = slot;
        if (_nameDialog != null)
        {
            _nameDialog.SetActive(true);
            if (_nameDialogHint != null)
                _nameDialogHint.text = $"슬롯 {slot}에 새 캐릭터를 만듭니다.";
            if (_nameInput != null)
            {
                _nameInput.text = "";
                _nameInput.Select();
                _nameInput.ActivateInputField();
            }
        }
    }

    private void HideNameDialog()
    {
        _pendingNewSlot = -1;
        if (_nameDialog != null)
            _nameDialog.SetActive(false);
    }

    private void ConfirmNewCharacter()
    {
        if (_pendingNewSlot <= 0 || _nameInput == null)
            return;

        string name = RunSlotSaveService.SanitizeCharacterName(_nameInput.text);
        if (string.IsNullOrEmpty(name))
            return;

        // 캐릭터를 만들면 슬롯 목록으로 돌아간다. 바로 게임에 들어가지 않는다.
        // 플레이는 목록에서 '시작'을 눌러야 시작된다.
        int createdSlot = _pendingNewSlot;
        if (!RunSlotSaveService.CreateNewCharacterSlot(createdSlot, name))
            return;

        HideNameDialog();
        RefreshList();
        HighlightSlot(createdSlot);
        titleSceneUI?.SetContinueEnabled(RunSlotSaveService.HasAnySlot());
    }

    private void StartContinue(int slot)
    {
        if (!RunSlotSaveService.PrepareContinueSlot(slot))
            return;

        Close();
        SceneManager.LoadScene(gameSceneName);
    }

    private void BuildDefaultUI()
    {
        // 영구(DontDestroyOnLoad) 캔버스를 잡으면 그쪽 CanvasGroup 설정(alpha 0 등)을 물려받아
        // 목록이 안 보이거나 클릭이 안 먹는다. 현재 씬의 캔버스만 쓴다.
        var canvas = UICanvasUtil.FindCanvasInActiveScene();
        if (canvas == null)
        {
            Debug.LogError("[RunSlotSelectUI] Canvas를 찾을 수 없습니다.");
            return;
        }

        _font = FindExistingFont();

        _root = CreatePanel("RunSlotSelectRoot", canvas.transform, new Color(0f, 0f, 0f, 0.72f));
        Stretch(_root.GetComponent<RectTransform>());

        _listPanel = CreatePanel("RunSlotSelectPanel", _root.transform, new Color(0.06f, 0.12f, 0.2f, 0.98f));
        var panelRt = _listPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(640f, 720f);

        var panelLayout = _listPanel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(20, 20, 20, 20);
        panelLayout.spacing = 12;
        panelLayout.childAlignment = TextAnchor.UpperCenter;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        CreateText(_listPanel.transform, "캐릭터 선택", 28, FontStyles.Bold, Color.white);

        var scrollGo = CreatePanel("Scroll", _listPanel.transform, new Color(0.04f, 0.08f, 0.14f, 1f));
        var scrollLe = scrollGo.AddComponent<LayoutElement>();
        scrollLe.flexibleHeight = 1f;
        scrollLe.minHeight = 420f;

        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        var viewport = CreatePanel("Viewport", scrollGo.transform, Color.clear);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        // CreatePanel 이 이미 Image 를 붙여서 만든다. 여기서 또 AddComponent<Image> 하면
        // Unity 가 거부하고 null 을 반환해, .color 대입에서 NullReferenceException 이 난다.
        // 그러면 아래의 슬롯 목록·닫기 버튼·이름 입력창이 통째로 생성되지 않는다.
        // Mask 가 영역을 잡으려면 알파가 있어야 하므로 기존 Image 의 색만 바꾼다.
        viewport.GetComponent<Image>().color = Color.white;

        var content = CreatePanel("Content", viewport.transform, Color.clear);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);

        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRt;
        _listContent = content.transform;

        var backBtn = CreateButton(_listPanel.transform, "닫기", new Color(0.12f, 0.22f, 0.35f, 1f), 120);
        backBtn.onClick.AddListener(Close);

        BuildNameDialog(canvas.transform);
        _root.SetActive(false);
    }

    private void BuildNameDialog(Transform canvasRoot)
    {
        _nameDialog = CreatePanel("NewCharacterDialog", canvasRoot, new Color(0f, 0f, 0f, 0.82f));
        Stretch(_nameDialog.GetComponent<RectTransform>());

        var box = CreatePanel("DialogBox", _nameDialog.transform, new Color(0.08f, 0.16f, 0.26f, 1f));
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(420f, 220f);

        var layout = box.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 20, 20);
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;

        CreateText(box.transform, "캐릭터 이름", 22, FontStyles.Bold, Color.white);
        _nameDialogHint = CreateText(box.transform, "", 14, FontStyles.Normal, new Color(0.75f, 0.85f, 0.95f));

        _nameInput = CreateInputField(box.transform);

        var btnRow = CreatePanel("Buttons", box.transform, Color.clear);
        var btnLayout = btnRow.AddComponent<HorizontalLayoutGroup>();
        btnLayout.spacing = 12;
        btnLayout.childAlignment = TextAnchor.MiddleCenter;
        btnLayout.childForceExpandWidth = true;

        var cancel = CreateButton(btnRow.transform, "취소", new Color(0.25f, 0.25f, 0.28f, 1f), 0);
        cancel.onClick.AddListener(HideNameDialog);
        var ok = CreateButton(btnRow.transform, "만들기", new Color(0.12f, 0.35f, 0.22f, 1f), 0);
        ok.onClick.AddListener(ConfirmNewCharacter);

        _nameDialog.SetActive(false);
    }

    private TMP_FontAsset FindExistingFont()
    {
        var any = FindObjectOfType<TMP_Text>();
        return any != null ? any.font : TMP_Settings.defaultFontAsset;
    }

    private GameObject CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private TMP_Text CreateText(Transform parent, string text, float size, FontStyles style, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.color = color;
        if (_font != null) tmp.font = _font;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        return tmp;
    }

    private const float ButtonHeight = 40f;

    private Button CreateButton(Transform parent, string label, Color bg, float width)
    {
        var go = CreatePanel(label + "Button", parent, bg);

        // 높이를 지정하지 않으면 childForceExpandHeight=false 인 VerticalLayoutGroup 안에서
        // 높이가 0이 된다. TMP 는 rect 밖으로도 글자를 그리기 때문에
        // '글자는 보이는데 클릭은 안 되는' 버튼이 만들어진다.
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = ButtonHeight;
        le.preferredHeight = ButtonHeight;
        if (width > 0f)
        {
            le.preferredWidth = width;
            le.minWidth = width;
            // 남는 가로 공간은 정보 영역이 가져가고 버튼은 고정 폭을 유지한다.
            le.flexibleWidth = 0f;
        }

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = bg * 1.15f;
        colors.pressedColor = bg * 0.85f;
        btn.colors = colors;

        var text = CreateText(go.transform, label, 16, FontStyles.Bold, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return btn;
    }

    private TMP_InputField CreateInputField(Transform parent)
    {
        var root = CreatePanel("NameInput", parent, new Color(0.04f, 0.08f, 0.12f, 1f));
        var le = root.AddComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;

        var textArea = CreatePanel("TextArea", root.transform, Color.clear);
        Stretch(textArea.GetComponent<RectTransform>());
        textArea.GetComponent<RectTransform>().offsetMin = new Vector2(10, 6);
        textArea.GetComponent<RectTransform>().offsetMax = new Vector2(-10, -6);

        var placeholderGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
        placeholderGo.transform.SetParent(textArea.transform, false);
        var placeholder = placeholderGo.GetComponent<TextMeshProUGUI>();
        placeholder.text = "이름 입력 (최대 20자)";
        placeholder.fontSize = 16;
        placeholder.color = new Color(0.55f, 0.6f, 0.65f);
        placeholder.fontStyle = FontStyles.Italic;
        if (_font != null) placeholder.font = _font;
        Stretch(placeholder.rectTransform);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(textArea.transform, false);
        var text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = 18;
        text.color = Color.white;
        if (_font != null) text.font = _font;
        Stretch(text.rectTransform);

        var input = root.AddComponent<TMP_InputField>();
        input.textViewport = textArea.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = RunSlotSaveService.MaxCharacterNameLength;
        return input;
    }
}
