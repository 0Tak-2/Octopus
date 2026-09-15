using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneUI : MonoBehaviour
{
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button engraveButton;
    [SerializeField] private Button quitButton;

    // 런타임에 복제해서 만드는 유물 버튼
    private Button _relicButton;

    [SerializeField] private CanvasGroup continueCanvasGroup;

    [SerializeField] private CanvasGroup engraveCanvasGroup;

    [SerializeField] private EngraveTreeUI engraveTreeUI;

    [SerializeField] private GameObject settingsPanel;

    [SerializeField] private RunSlotSelectUI runSlotSelectUI;

    [SerializeField] private string gameSceneName = "NewMapScene";

    private void OnEnable()
    {
        EngraveAccountUnlock.OnMenuUnlocked += ApplyEngraveMenuLockState;
        ApplyEngraveMenuLockState();
    }

    private void OnDisable()
    {
        EngraveAccountUnlock.OnMenuUnlocked -= ApplyEngraveMenuLockState;
    }

    private void Start()
    {
        DeathManager.ClearDeathInputLock();

        EnsureRunSlotSelectUI();

        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGame);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettings);

        // 각인 시스템 제거 — 버튼 자체를 숨긴다. (메타 성장은 유물로 통합)
        if (engraveButton != null)
            engraveButton.gameObject.SetActive(false);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);

        SetContinueEnabled(RunSlotSaveService.HasAnySlot());

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        ApplyEngraveMenuLockState();
        EnsureRelicButton();
    }

    /// <summary>
    /// 유물 버튼을 씬 편집 없이 붙인다. 기존 버튼을 복제해 스타일·레이아웃을 그대로 물려받는다.
    /// (유물은 계정 단위로 쌓이는데 여태 확인할 화면이 없었다)
    /// </summary>
    private void EnsureRelicButton()
    {
        if (_relicButton != null) return;

        // 각인 버튼은 비활성화해 뒀으므로 그걸 복제하면 유물 버튼도 꺼진 채로 나온다.
        // 살아 있는 버튼을 본으로 쓰고, 위치만 각인 버튼 자리에 맞춘다.
        Button template = newGameButton != null ? newGameButton : engraveButton;
        if (template == null) return;

        Transform anchor = engraveButton != null ? engraveButton.transform : template.transform;

        var clone = Instantiate(template.gameObject, anchor.parent);
        clone.name = "RelicButton";
        clone.SetActive(true);
        clone.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);

        _relicButton = clone.GetComponent<Button>();
        // RemoveAllListeners() 는 런타임 리스너만 지운다. 인스펙터로 등록된 영구 onClick
        // (각인 버튼의 EngraveTreeUI.Open)은 Instantiate 로 복사돼 그대로 남아서,
        // 유물 버튼을 누르면 각인 창까지 같이 열렸다. 이벤트를 통째로 갈아끼운다.
        _relicButton.onClick = new Button.ButtonClickedEvent();
        _relicButton.interactable = true;
        _relicButton.onClick.AddListener(OnRelics);

        // 복제본에 붙어 있을 수 있는 잠금용 CanvasGroup 을 정상화한다.
        var cg = clone.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        foreach (var label in clone.GetComponentsInChildren<TMPro.TMP_Text>(true))
            label.text = "유물";

        foreach (var legacy in clone.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            legacy.text = "유물";
    }

    private void OnRelics()
    {
        var ui = RelicCollectionUI.Ensure();
        if (ui != null)
            ui.Open();
    }

    private void EnsureRunSlotSelectUI()
    {
        if (runSlotSelectUI != null)
            return;

        // 영구(DontDestroyOnLoad) 캔버스가 아니라 타이틀 씬의 캔버스를 써야 한다.
        var canvas = UICanvasUtil.FindCanvasInActiveScene();
        if (canvas == null)
            return;

        var go = new GameObject("RunSlotSelectUI");
        go.transform.SetParent(canvas.transform, false);
        runSlotSelectUI = go.AddComponent<RunSlotSelectUI>();
        runSlotSelectUI.Configure(this, gameSceneName);
    }

    private void ApplyEngraveMenuLockState()
    {
        bool unlocked = EngraveAccountUnlock.IsMenuUnlocked;

        if (engraveButton != null)
            engraveButton.interactable = unlocked;

        if (engraveCanvasGroup != null)
        {
            engraveCanvasGroup.alpha = unlocked ? 1f : 0.35f;
            engraveCanvasGroup.interactable = unlocked;
            engraveCanvasGroup.blocksRaycasts = unlocked;
        }
    }

    /// <summary>새 캐릭터: 빈 슬롯이 있으면 선택 UI, 없으면 목록에서 덮어쓰기/삭제.</summary>
    private void OnNewGame()
    {
        EnsureRunSlotSelectUI();
        if (runSlotSelectUI == null)
            return;

        runSlotSelectUI.Open(focusLatestIfAny: false);
    }

    /// <summary>캐릭터 선택 UI — 저장된 캐릭터 중 하나를 골라 이어하기.</summary>
    private void OnContinue()
    {
        if (!RunSlotSaveService.HasAnySlot())
        {
            SetContinueEnabled(false);
            return;
        }

        EnsureRunSlotSelectUI();
        if (runSlotSelectUI == null)
            return;

        runSlotSelectUI.Open(focusLatestIfAny: true);
    }

    private void OnSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void OnEngrave()
    {
        if (!EngraveAccountUnlock.IsMenuUnlocked)
            return;

        if (engraveTreeUI != null)
            engraveTreeUI.Open();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetContinueEnabled(bool enabled)
    {
        if (continueButton != null)
            continueButton.interactable = enabled;

        if (continueCanvasGroup != null)
        {
            continueCanvasGroup.alpha = enabled ? 1f : 0.4f;
            continueCanvasGroup.blocksRaycasts = enabled;
        }
    }
}
