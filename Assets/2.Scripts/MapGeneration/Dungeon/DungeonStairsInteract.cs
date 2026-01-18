using UnityEngine;
using TMPro;

/// <summary>
/// 던전 계단 상호작용
/// 다음 층으로 이동
/// </summary>
public class DungeonStairsInteract : MonoBehaviour
{
    [Header("Settings")]
    public KeyCode interactKey = KeyCode.E;
    [Range(0.5f, 3f)] public float triggerRadius = 1.5f;
    
    [Header("References")]
    public DungeonController dungeonController;
    
    [Header("UI")]
    public GameObject promptUI;
    public TextMeshProUGUI promptText;
    public string promptMessage = "다음 층으로 (E)";
    
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
        
        // DungeonController 찾기
        if (dungeonController == null)
            dungeonController = FindObjectOfType<DungeonController>();
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
            Debug.Log("[DungeonStairs] 다음 층으로 이동");
        
        if (dungeonController != null)
        {
            dungeonController.GoToNextFloor();
        }
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
