using UnityEngine;

/// <summary>
/// 플레이어를 즉각 따라가는 카메라
/// </summary>
public class SnapCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // 플레이어

    [Header("Offset")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Header("Settings")]
    public bool snapInstantly = true; // 즉각 이동
    public float smoothSpeed = 0.1f; // 부드러움 (snapInstantly = false일 때)

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPosition = target.position + offset;

        if (snapInstantly)
        {
            // 즉각 이동 (딱딱 붙음)
            transform.position = targetPosition;
        }
        else
        {
            // 약간 부드럽게
            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                smoothSpeed
            );
        }
    }
}