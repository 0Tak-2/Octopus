using UnityEngine;

public class PlayerCrouch : MonoBehaviour
{
    [Header("Toggle")]
    public KeyCode toggleKey = KeyCode.C;

    public bool IsCrouching { get; private set; }

    public event System.Action<bool> OnCrouchChanged;

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            IsCrouching = !IsCrouching;
            Debug.Log($"Crouch: {(IsCrouching ? "ON" : "OFF")}");
            OnCrouchChanged?.Invoke(IsCrouching);
        }
    }
}
