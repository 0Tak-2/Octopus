using UnityEngine;
using TMPro;

/// <summary>
/// 던전 출구 포탈
/// 필드로 복귀
/// </summary>
public class DungeonExitPortal : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode interactKey = KeyCode.E;
    [Range(0.5f, 3f)] public float triggerRadius = 1.5f;
    
    [Header("UI")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    public string promptMessage = "필드로 복귀 (E)";
    
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
        
        // 프롬프트 초기화
        if (promptUI != null)
            promptUI.SetActive(false);
        
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
    
    private void OnPlayerInteract()
    {
        if (logInteraction)
            Debug.Log("[DungeonExit] 필드로 복귀");
        
        GameManager gm = GameManager.Instance;
        if (gm == null)
        {
            Debug.LogError("[DungeonExit] GameManager를 찾을 수 없습니다!");
            return;
        }
        
        // 던전 나가기 (클리어 안 함)
        gm.ExitDungeon();
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
