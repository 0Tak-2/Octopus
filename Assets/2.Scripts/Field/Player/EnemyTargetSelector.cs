using UnityEngine;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Q키로 주변 적 선택, F키로 집중전투 진입
/// 8방향 적 탐색 (왼쪽 위부터 시계방향)
/// </summary>
public class EnemyTargetSelector : MonoBehaviour
{
    [Header("References")]
    public PlayerGridMover playerMover;
    public GridBoard grid;
    public PlayerStats playerStats;

    [Header("UI")]
    [Tooltip("선택된 적 위에 표시할 프롬프트 프리팹")]
    public GameObject targetPromptPrefab;

    [Header("Keys")]
    public KeyCode selectKey = KeyCode.Q;
    public KeyCode confirmKey = KeyCode.F;

    [Header("Debug")]
    public bool logSelection = true;

    // 8방향 (왼쪽 위부터 시계방향)
    private static readonly Vector2Int[] directions = new Vector2Int[]
    {
        new Vector2Int(-1, 1),   // 0: 왼쪽 위
        new Vector2Int(0, 1),    // 1: 위
        new Vector2Int(1, 1),    // 2: 오른쪽 위
        new Vector2Int(1, 0),    // 3: 오른쪽
        new Vector2Int(1, -1),   // 4: 오른쪽 아래
        new Vector2Int(0, -1),   // 5: 아래
        new Vector2Int(-1, -1),  // 6: 왼쪽 아래
        new Vector2Int(-1, 0),   // 7: 왼쪽
    };

    // 현재 상태
    private List<EnemyInstance> nearbyEnemies = new List<EnemyInstance>();
    private int currentSelectionIndex = -1;
    private EnemyInstance selectedEnemy = null;

    // UI
    private GameObject promptInstance;
    private TextMeshProUGUI promptText;

    private void Awake()
    {
        if (playerMover == null) playerMover = GetComponent<PlayerGridMover>() ?? FindObjectOfType<PlayerGridMover>();
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
    }

    private void Start()
    {
        // 프롬프트 UI 생성
        CreatePromptUI();
    }

    private void CreatePromptUI()
    {
        if (targetPromptPrefab != null)
        {
            promptInstance = Instantiate(targetPromptPrefab);
            promptText = promptInstance.GetComponentInChildren<TextMeshProUGUI>();
        }
        else
        {
            // 프리팹 없으면 동적 생성
            promptInstance = new GameObject("TargetPrompt");

            // Canvas 추가 (World Space)
            Canvas canvas = promptInstance.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            // 텍스트 오브젝트
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(promptInstance.transform);

            promptText = textObj.AddComponent<TextMeshProUGUI>();
            promptText.text = "[집중전투 진입 (F)]";
            promptText.fontSize = 3;
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.color = Color.yellow;

            // RectTransform 설정
            RectTransform rt = promptInstance.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(5, 1);

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.sizeDelta = new Vector2(5, 1);
            textRt.localPosition = Vector3.zero;
            textRt.localScale = Vector3.one * 0.1f;
        }

        promptInstance.SetActive(false);
    }

    private void Update()
    {
        // 집중전투 중이면 무시
        var mgr = FocusedCombatManager.Instance;
        if (mgr != null && mgr.IsInFocusedCombat)
        {
            ClearSelection();
            return;
        }

        if (grid == null || playerMover == null) return;

        // Q키: 다음 적 선택
        if (Input.GetKeyDown(selectKey))
        {
            SelectNextEnemy();
        }

        // F키: 선택된 적과 집중전투
        if (Input.GetKeyDown(confirmKey) && selectedEnemy != null)
        {
            EnterCombatWithSelected();
        }

        // 선택된 적이 멀어지면 선택 해제
        if (selectedEnemy != null)
        {
            if (!IsEnemyAdjacent(selectedEnemy))
            {
                ClearSelection();
            }
            else
            {
                UpdatePromptPosition();
            }
        }
    }

    /// <summary>
    /// 다음 적 선택 (Q키)
    /// </summary>
    private void SelectNextEnemy()
    {
        // 주변 적 갱신
        RefreshNearbyEnemies();

        if (nearbyEnemies.Count == 0)
        {
            if (logSelection)
                Debug.Log("[EnemyTargetSelector] 주변에 적 없음");
            ClearSelection();
            return;
        }

        // 다음 인덱스
        currentSelectionIndex++;
        if (currentSelectionIndex >= nearbyEnemies.Count)
            currentSelectionIndex = 0;

        selectedEnemy = nearbyEnemies[currentSelectionIndex];

        if (logSelection)
            Debug.Log($"[EnemyTargetSelector] 적 선택: {selectedEnemy.name} ({currentSelectionIndex + 1}/{nearbyEnemies.Count})");

        // 프롬프트 표시
        ShowPrompt();
    }

    /// <summary>
    /// 8방향 주변 적 찾기 (왼쪽 위부터 시계방향)
    /// </summary>
    private void RefreshNearbyEnemies()
    {
        nearbyEnemies.Clear();

        Vector2Int playerCell = playerMover.CurrentCell;

        // 8방향 순서대로 체크
        foreach (var dir in directions)
        {
            Vector2Int checkCell = playerCell + dir;

            // 해당 위치의 적 찾기
            EnemyInstance enemy = FindEnemyAtCell(checkCell);
            if (enemy != null && enemy.currentHP > 0 && enemy.gameObject.activeSelf)
            {
                nearbyEnemies.Add(enemy);
            }
        }
    }

    /// <summary>
    /// 특정 셀에 있는 적 찾기
    /// </summary>
    private EnemyInstance FindEnemyAtCell(Vector2Int cell)
    {
        // 모든 적 검색
        EnemyInstance[] allEnemies = FindObjectsOfType<EnemyInstance>();

        foreach (var enemy in allEnemies)
        {
            if (enemy == null || !enemy.gameObject.activeSelf) continue;

            Vector2Int enemyCell = grid.WorldToCell(enemy.transform.position);
            if (enemyCell == cell)
                return enemy;
        }

        return null;
    }

    /// <summary>
    /// 적이 인접해있는지 확인 (8방향)
    /// </summary>
    private bool IsEnemyAdjacent(EnemyInstance enemy)
    {
        if (enemy == null) return false;

        Vector2Int playerCell = playerMover.CurrentCell;
        Vector2Int enemyCell = grid.WorldToCell(enemy.transform.position);

        int dx = Mathf.Abs(playerCell.x - enemyCell.x);
        int dy = Mathf.Abs(playerCell.y - enemyCell.y);

        // 8방향 인접 (대각선 포함)
        return dx <= 1 && dy <= 1 && !(dx == 0 && dy == 0);
    }

    /// <summary>
    /// 선택된 적과 집중전투 진입 (F키)
    /// </summary>
    private void EnterCombatWithSelected()
    {
        if (selectedEnemy == null) return;

        var mgr = FocusedCombatManager.Instance;
        if (mgr == null)
        {
            Debug.LogError("[EnemyTargetSelector] FocusedCombatManager not found!");
            return;
        }

        if (logSelection)
            Debug.Log($"[EnemyTargetSelector] 집중전투 진입: {selectedEnemy.name}");

        // 컨텍스트 생성
        Vector2Int playerCell = playerMover.CurrentCell;
        Vector2Int enemyCell = grid.WorldToCell(selectedEnemy.transform.position);

        if (playerStats != null && selectedEnemy.definition != null)
        {
            var ctx = new EncounterContext(playerStats, selectedEnemy, playerCell, enemyCell);
            mgr.EnterFocusedCombat(ctx);
        }
        else if (selectedEnemy.definition != null)
        {
            mgr.EnterFocusedCombat(selectedEnemy.definition);
        }

        ClearSelection();
    }

    /// <summary>
    /// 프롬프트 표시
    /// </summary>
    private void ShowPrompt()
    {
        if (promptInstance == null || selectedEnemy == null) return;

        promptInstance.SetActive(true);
        UpdatePromptPosition();

        if (promptText != null)
            promptText.text = "[집중전투 진입 (F)]";
    }

    /// <summary>
    /// 프롬프트 위치 업데이트
    /// </summary>
    private void UpdatePromptPosition()
    {
        if (promptInstance == null || selectedEnemy == null) return;

        // 적 위에 표시
        Vector3 pos = selectedEnemy.transform.position;
        pos.y += 1.5f; // 적 위로
        promptInstance.transform.position = pos;
    }

    /// <summary>
    /// 선택 해제
    /// </summary>
    private void ClearSelection()
    {
        selectedEnemy = null;
        currentSelectionIndex = -1;

        if (promptInstance != null)
            promptInstance.SetActive(false);
    }

    private void OnDisable()
    {
        ClearSelection();
    }

    private void OnDestroy()
    {
        if (promptInstance != null)
            Destroy(promptInstance);
    }
}