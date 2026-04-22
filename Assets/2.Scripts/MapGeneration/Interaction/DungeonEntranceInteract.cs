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
    public KeyCode interactKey = KeyCode.E;

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

        if (gm.IsDungeonClearedOnCurrentField(dungeonId))
        {
            alreadyCleared = true;

            if (logInteraction)
                Debug.Log($"[DungeonEntrance] 이미 클리어한 던전(현재 필드): {dungeonId}");

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
        if (string.IsNullOrEmpty(dungeonId))
        {
            Debug.LogError("[DungeonEntrance] dungeonId 가 비어 있습니다. FieldDefinition.dungeons·던전 입구 스폰을 확인하세요.");
            return;
        }

        if (logInteraction)
            Debug.Log($"[DungeonEntrance] 던전 입장: {dungeonId}");

        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[DungeonEntrance] GameManager를 찾을 수 없습니다!");
            return;
        }

        // 얼음땡 스냅샷(+ SetActive(false)) 은 ModeManager.EnterDungeonCore 의
        // Capture 가 페이드 아웃 완료 시점에 수행한다. 그 전까지는 적이 필드 위에
        // 그대로 보여야 하므로 여기서는 EarlyCapture 를 호출하지 않는다.
        // (과거에는 EnemyAlertIcon 버그로 인해 페이드 아웃 중에 적이 스폰으로
        //  순간이동하는 걸 가리려고 EarlyCapture 를 여기서 불렀지만, 이제는 그 범인이
        //  Enemyalerticon.cs 의 안전장치로 막혀 있어서 더 이상 필요 없다.)

        GridBoard gridBoard = FindActiveFieldGridBoard();
        if (gridBoard != null)
        {
            gm.playerData.dungeonEntrancePosition = gridBoard.WorldToCell(transform.position);
            gm.playerData.hasDungeonReturnPosition = true;
            if (logInteraction)
                Debug.Log($"[DungeonEntrance] 입구 위치 저장: {gm.playerData.dungeonEntrancePosition}");
        }
        else if (logInteraction)
            Debug.LogWarning("[DungeonEntrance] 활성 필드 GridBoard 를 찾지 못해 입구 셀을 저장하지 못했습니다.");

        gm.EnterDungeon(dungeonId);
    }

    private static GridBoard FindActiveFieldGridBoard()
    {
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null && ModeManager.Instance.fieldRoot.activeInHierarchy)
        {
            var gb = FieldMapGenerator.ResolveGameplayGrid(ModeManager.Instance.fieldRoot);
            if (gb != null && gb.isActiveAndEnabled) return gb;
        }

        foreach (var gb in FindObjectsOfType<GridBoard>())
        {
            if (gb != null && gb.gameObject.activeInHierarchy) return gb;
        }

        return null;
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