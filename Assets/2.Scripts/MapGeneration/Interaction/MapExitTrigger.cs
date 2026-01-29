using UnityEngine;
using TMPro;

/// <summary>
/// 맵 출구 트리거
/// 플레이어가 밟으면 프롬프트 표시, E키로 다음 맵 이동
/// </summary>
public class MapExitTrigger : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("상호작용 키")]
    public KeyCode interactKey = KeyCode.F;

    [Tooltip("트리거 반경 (0 = 정확히 밟아야 함, 1 = 1칸 근처)")]
    [Range(0f, 2f)] public float triggerRadius = 0.5f;

    [Header("UI")]
    [Tooltip("프롬프트 UI (TMP)")]
    public GameObject promptUI;

    [Tooltip("프롬프트 텍스트")]
    public TextMeshProUGUI promptText;

    [Tooltip("표시할 메시지")]
    public string promptMessage = "다음 지역으로 이동 (F)";

    [Header("Direction")]
    [Tooltip("다음 맵인가? (false면 이전 맵)")]
    public bool isNextMap = true;

    [Header("Debug")]
    public bool logInteraction = true;

    private Transform player;
    private bool playerInRange = false;

    private void Start()
    {
        // 플레이어 찾기
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // 프롬프트 초기 비활성화
        if (promptUI != null)
            promptUI.SetActive(false);

        // 텍스트 설정
        if (promptText != null)
            promptText.text = promptMessage;
    }

    private void Update()
    {
        if (player == null) return;

        // 거리 체크
        float distance = Vector3.Distance(transform.position, player.position);
        playerInRange = distance <= triggerRadius;

        // 프롬프트 표시/숨김
        if (promptUI != null)
            promptUI.SetActive(playerInRange);

        // E키 입력
        if (playerInRange && Input.GetKeyDown(interactKey))
        {
            OnPlayerInteract();
        }
    }

    /// <summary>
    /// 플레이어가 E키를 눌렀을 때
    /// </summary>
    private void OnPlayerInteract()
    {
        if (logInteraction)
            Debug.Log($"[MapExitTrigger] 플레이어가 출구와 상호작용");

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[MapExitTrigger] GameManager를 찾을 수 없습니다!");
            return;
        }

        // 다음 맵 or 이전 맵 로드
        if (isNextMap)
            gm.LoadNextMap();
        else
            gm.LoadPreviousMap();
    }

    /// <summary>
    /// Gizmo로 트리거 범위 표시
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}