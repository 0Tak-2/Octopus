using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

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

    [Header("각인 트리 UI")]
    [SerializeField] private EngraveTreeUI engraveTreeUI;

    [Header("설정 패널 (나중에 구현)")]
    [SerializeField] private GameObject settingsPanel;

    [Header("씬 이름")]
    [SerializeField] private string gameSceneName = "NewMapScene";

    private void Start()
    {
        // 버튼 바인딩
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

        // 이어하기 비활성화 (아직 세이브/로드 미구현)
        SetContinueEnabled(false);

        // 설정 패널 숨김
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // =========================================================
    // 버튼 핸들러
    // =========================================================

    private void OnNewGame()
    {
        // TODO: 기존 세이브 데이터 초기화 로직 필요 시 여기에 추가
        SceneManager.LoadScene(gameSceneName);
    }

    private void OnContinue()
    {
        // 세이브/로드 구현 후 활성화
        // SaveManager.Instance.Load();
        // SceneManager.LoadScene(gameSceneName);
    }

    private void OnSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void OnEngrave()
    {
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

    // =========================================================
    // 이어하기 활성/비활성
    // =========================================================

    /// <summary>
    /// 세이브 데이터 존재 여부에 따라 이어하기 버튼 활성화.
    /// 세이브/로드 구현 후 Start()에서 호출.
    /// </summary>
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