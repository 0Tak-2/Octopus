using UnityEngine;

/// <summary>
/// 카메라를 씬 전환 시에도 유지하고 Player 추적
/// </summary>
public class CameraPersistence : MonoBehaviour
{
    public static CameraPersistence Instance { get; private set; }

    [Header("Follow Settings")]
    public Transform target; // Player
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 0, -10);
    public bool followPlayer = true;

    private void Awake()
    {
        Debug.Log($"[CameraPersistence] Awake - Camera: {gameObject.name}");

        if (Instance == null)
        {
            Instance = this;

            // 부모에서 분리
            if (transform.parent != null)
            {
                Debug.Log($"[CameraPersistence] Detaching from parent: {transform.parent.name}");
                transform.SetParent(null);
            }

            // DontDestroyOnLoad
            DontDestroyOnLoad(gameObject);

            Debug.Log($"[CameraPersistence] ✅ Camera set to persist");
        }
        else
        {
            Debug.Log($"[CameraPersistence] ❌ Duplicate camera, destroying: {gameObject.name}");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Player 찾기
        FindPlayer();
    }

    private void LateUpdate()
    {
        if (followPlayer && target != null)
        {
            FollowTarget();
        }
        else if (followPlayer && target == null)
        {
            // Player를 못 찾았으면 다시 찾기
            FindPlayer();
        }
    }

    /// <summary>
    /// Player 찾기
    /// </summary>
    private void FindPlayer()
    {
        // 1. Tag로 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");

        // 2. PlayerGridMover로 찾기
        if (playerObj == null)
        {
            PlayerGridMover playerMover = FindObjectOfType<PlayerGridMover>(true);
            if (playerMover != null)
            {
                playerObj = playerMover.gameObject;
            }
        }

        if (playerObj != null)
        {
            target = playerObj.transform;
            Debug.Log($"[CameraPersistence] Found Player: {target.name}");
        }
        else
        {
            Debug.LogWarning("[CameraPersistence] Player not found!");
        }
    }

    /// <summary>
    /// Target 추적
    /// </summary>
    private void FollowTarget()
    {
        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
    }

    /// <summary>
    /// Player 설정 (외부에서 호출 가능)
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        Debug.Log($"[CameraPersistence] Target set to: {(newTarget != null ? newTarget.name : "null")}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}