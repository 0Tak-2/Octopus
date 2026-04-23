using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneUI : MonoBehaviour
{
    [Header("��ư")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button engraveButton;
    [SerializeField] private Button quitButton;

    [Header("�̾��ϱ� ��Ȱ�� ǥ��")]
    [SerializeField] private CanvasGroup continueCanvasGroup;

    [Header("���� ��ư (���� ���� Lv2 �� ���, ����)")]
    [SerializeField] private CanvasGroup engraveCanvasGroup;

    [Header("���� Ʈ�� UI")]
    [SerializeField] private EngraveTreeUI engraveTreeUI;

    [Header("���� �г� (���߿� ����)")]
    [SerializeField] private GameObject settingsPanel;

    [Header("�� �̸�")]
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

    private void OnNewGame()
    {
        RunSlotSaveService.StartNewRunInSlot(1, deleteExisting: true);
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinue()
    {
        if (!RunSlotSaveService.PrepareContinueLatest())
        {
            SetContinueEnabled(false);
            return;
        }

        SceneManager.LoadScene(gameSceneName);
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
