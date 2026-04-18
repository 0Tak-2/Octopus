using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TitleSceneUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button engraveButton;
    [SerializeField] private Button quitButton;

    [Header("이어하기 비활성 표시")]
    [SerializeField] private CanvasGroup continueCanvasGroup;

    [Header("각인 버튼 (계정 최초 Lv2 전 잠금, 선택)")]
    [SerializeField] private CanvasGroup engraveCanvasGroup;

    [Header("각인 트리 UI")]
    [SerializeField] private EngraveTreeUI engraveTreeUI;

    [Header("설정 패널 (나중에 구현)")]
    [SerializeField] private GameObject settingsPanel;

    [Header("씬 이름")]
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

        SetContinueEnabled(false);

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
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinue()
    {
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
