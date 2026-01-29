using UnityEngine;
using TMPro;

/// <summary>
/// 던전 입구 상호작용
/// 플레이어가 근처에 가면 프롬프트 표시, E키로 던전 입장
/// </summary>
public class DungeonEntranceInteract : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("상호작용 키")]
    public KeyCode interactKey = KeyCode.F;

    [Tooltip("트리거 반경")]
    [Range(0.5f, 3f)] public float triggerRadius = 1.5f;

    [Header("Dungeon")]
    [Tooltip("던전 ID (자동 생성됨)")]
    public string dungeonId;

    [Header("UI")]
    [Tooltip("프롬프트 UI (TMP)")]
    public GameObject promptUI;

    [Tooltip("프롬프트 텍스트")]
    public TextMeshProUGUI promptText;

    [Tooltip("표시할 메시지")]
    public string promptMessage = "던전 입장 (F)";

    [Header("Debug")]
    public bool logInteraction = true;

    private Transform player;
    private bool playerInRange = false;
    private bool alreadyCleared = false;

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

        // 이미 클리어한 던전인지 체크
        CheckIfCleared();
    }

    private void Update()
    {
        if (player == null) return;
        if (alreadyCleared) return; // 클리어한 던전은 상호작용 불가

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
    /// 이미 클리어한 던전인지 확인
    /// </summary>
    private void CheckIfCleared()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        if (gm.clearedDungeons.Contains(dungeonId))
        {
            alreadyCleared = true;

            if (logInteraction)
                Debug.Log($"[DungeonEntrance] 이미 클리어한 던전: {dungeonId}");

            // 시각적 표시 (선택)
            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.color = Color.gray;
        }
    }

    /// <summary>
    /// 플레이어가 E키를 눌렀을 때
    /// </summary>
    private void OnPlayerInteract()
    {
        if (logInteraction)
            Debug.Log($"[DungeonEntrance] 던전 입장: {dungeonId}");

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[DungeonEntrance] GameManager를 찾을 수 없습니다!");
            return;
        }

        // ✅ 던전 입구 위치 저장 (나올 때 여기로 복귀)
        GridBoard gridBoard = FindObjectOfType<GridBoard>();
        if (gridBoard != null)
        {
            gm.playerData.dungeonEntrancePosition = gridBoard.WorldToCell(transform.position);
            if (logInteraction)
                Debug.Log($"[DungeonEntrance] 입구 위치 저장: {gm.playerData.dungeonEntrancePosition}");
        }

        // 던전 씬으로 전환
        gm.EnterDungeon(dungeonId);
    }

    /// <summary>
    /// Gizmo로 트리거 범위 표시
    /// </summary>
    private void OnDrawGizmos()
    {
        Gizmos.color = alreadyCleared ? Color.gray : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}