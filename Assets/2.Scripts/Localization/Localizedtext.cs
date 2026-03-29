using UnityEngine;
using TMPro;

/// <summary>
/// TMP 텍스트에 붙이면 자동으로 로컬라이즈되는 컴포넌트.
/// Inspector에서 Key만 지정하면 언어 변경 시 자동 갱신됩니다.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [Header("로컬라이제이션 키")]
    [SerializeField] private string localizationKey;

    private TextMeshProUGUI textComponent;

    private void Awake()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += UpdateText;
        UpdateText();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= UpdateText;
    }

    public void UpdateText()
    {
        if (string.IsNullOrEmpty(localizationKey) || LocalizationManager.Instance == null)
            return;

        textComponent.text = LocalizationManager.T(localizationKey);
    }

    /// <summary>
    /// 런타임에서 키를 변경하고 즉시 갱신합니다.
    /// 예: 인벤토리 슬롯에 아이템이 바뀔 때
    /// </summary>
    public void SetKey(string newKey)
    {
        localizationKey = newKey;
        UpdateText();
    }
}