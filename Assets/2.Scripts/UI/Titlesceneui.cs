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

        if (engraveButton != null)
            engraveButton.onClick.AddListener(OnEngrave);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuit);

        SetContinueEnabled(RunSlotSaveService.HasAnySlot());

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        ApplyEngraveMenuLockState();
    }

    private void EnsureRunSlotSelectUI()
    {
        if (runSlotSelectUI != null)
            return;

        var canvas = FindObjectOfType<Canvas>();
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
