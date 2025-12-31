using UnityEngine;
using TMPro;

public class WorldPromptUI : MonoBehaviour
{
    [Header("Refs")]
    public RestController rest;
    public TextMeshProUGUI text;

    [Header("Messages")]
    [TextArea] public string confirmMessage = "휴식을 취하시겠습니까?\n(R)";
    public string restingMessage = "휴식 중…";

    private void Awake()
    {
        if (rest == null) rest = FindObjectOfType<RestController>();
        if (text == null) text = GetComponentInChildren<TextMeshProUGUI>();

        Hide();
    }

    private void Update()
    {
        if (rest == null || text == null) return;

        switch (rest.State)
        {
            case RestController.RestState.Confirm:
                Show(confirmMessage);
                break;

            case RestController.RestState.Resting:
                Show(restingMessage);
                break;

            default:
                Hide();
                break;
        }
    }

    private void Show(string message)
    {
        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);

        if (text.text != message)
            text.text = message;
    }

    private void Hide()
    {
        if (text.gameObject.activeSelf)
            text.gameObject.SetActive(false);
    }
}
