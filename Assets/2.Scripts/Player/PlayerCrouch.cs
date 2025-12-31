using UnityEngine;

public class PlayerCrouch : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.C;

    public bool IsCrouching { get; private set; }

    public event System.Action<bool> OnCrouchChanged;

    private RestController rest;

    private void Awake()
    {
        rest = FindObjectOfType<RestController>();
    }

    private void Update()
    {
        // ✅ 휴식 중에는 웅크리기 토글 불가
        if (rest != null && rest.IsResting)
            return;

        if (Input.GetKeyDown(toggleKey))
        {
            IsCrouching = !IsCrouching;
            OnCrouchChanged?.Invoke(IsCrouching);
        }
    }
}
