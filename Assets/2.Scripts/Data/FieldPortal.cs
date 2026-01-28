using UnityEngine;

/// <summary>
/// 다른 필드로 이동하는 포털
/// </summary>
public class FieldPortal : MonoBehaviour
{
    [Header("Target Field")]
    public string targetFieldName = "Field_Forest"; // 이동할 필드 이름

    [Header("Spawn Position")]
    public Vector2 spawnPosition = Vector2.zero; // 도착 위치 (0,0이면 기본 위치)

    /// <summary>
    /// 플레이어가 F 키로 포털 사용
    /// </summary>
    public void UsePortal()
    {
        if (SceneTransitionManager.Instance != null)
        {
            // 도착 위치 설정
            if (GameDataManager.Instance != null && spawnPosition != Vector2.zero)
            {
                GameDataManager.Instance.playerData.position = spawnPosition;
            }

            // 필드 이동
            SceneTransitionManager.Instance.MoveToField(targetFieldName);
        }
        else
        {
            Debug.LogError("[FieldPortal] SceneTransitionManager not found!");
        }
    }
}