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
    public KeyCode interactKey = KeyCode.E;

    [Tooltip("트리거 반경 (0 = 정확히 밟아야 함, 1 = 1칸 근처)")]
    [Range(0f, 2f)] public float triggerRadius = 0.5f;

    [Header("UI")]
    [Tooltip("프롬프트 UI (TMP)")]
    public GameObject promptUI;

    [Tooltip("프롬프트 텍스트")]
    public TextMeshProUGUI promptText;

    [Tooltip("표시할 메시지")]
    public string promptMessage = "다음 지역으로 이동 (E)";

    [Header("Direction")]
    [Tooltip("다음 맵인가? (false면 이전 맵)")]
    public bool isNextMap = true;

    [Tooltip("출구가 놓인 가장자리 (필드 생성기에서 설정 — 입구 반대편 스폰용)")]
    public EdgeSide exitEdge = EdgeSide.Right;

    [Tooltip("맵 연결 위치 계산용 (비워두면 상호작용 시 추론)")]
    public Vector2Int exitCell;
    public int sourceMapWidth;
    public int sourceMapHeight;

    [Header("Debug")]
    public bool logInteraction = true;

    private Transform player;
    private bool playerInRange = false;

    /// <summary>FieldMapGenerator가 스폰 직후 호출</summary>
    public void ConfigurePortal(EdgeSide edge, Vector2Int cell, int mapW, int mapH)
    {
        exitEdge = edge;
        exitCell = cell;
        sourceMapWidth = mapW;
        sourceMapHeight = mapH;
    }

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

        EdgeSide edge = exitEdge;
        Vector2Int cell = exitCell;
        int mw = sourceMapWidth;
        int mh = sourceMapHeight;

        if (cell == Vector2Int.zero || mw <= 0 || mh <= 0)
            gridBoardTryInfer(ref edge, ref cell, ref mw, ref mh);

        if (mw > 0 && mh > 0 && cell != Vector2Int.zero)
            gm.RegisterFieldExitForNextScene(edge, cell, mw, mh);

        if (isNextMap)
            gm.LoadNextMap();
        else
            gm.LoadPreviousMap();
    }

    private bool gridBoardTryInfer(ref EdgeSide edge, ref Vector2Int cell, ref int mw, ref int mh)
    {
        var grid = FindObjectOfType<GridBoard>();
        if (grid == null) return false;
        mw = grid.width;
        mh = grid.height;
        cell = grid.WorldToCell(transform.position);
        edge = FieldTransitionUtil.InferClosestEdge(cell, mw, mh);
        return true;
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