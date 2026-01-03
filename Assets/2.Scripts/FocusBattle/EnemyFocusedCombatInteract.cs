using UnityEngine;
using TMPro;

public class EnemyFocusedCombatInteract : MonoBehaviour
{
    [Header("Refs")]
    public PlayerGridMover playerMover;
    public GridBoard grid;

    [Header("Enemy Definition")]
    public EnemyDefinition definition;

    [Header("Runtime Instance")]
    public EnemyInstance enemyInstance;

    [Header("Player Stats")]
    public PlayerStats playerStats;

    [Header("Prompt (world text)")]
    public TextMeshProUGUI promptText;
    [TextArea] public string promptMessage = "집중해서 싸운다\n(E)";
    public KeyCode interactKey = KeyCode.E;

    [Header("Enemy Cell (optional)")]
    public bool useTransformToCell = true;
    public Vector2Int enemyCellOverride;

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (playerMover == null) playerMover = FindObjectOfType<PlayerGridMover>();

        if (enemyInstance == null) enemyInstance = GetComponent<EnemyInstance>();
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();

        // EnemyInstance가 없으면 자동으로 붙여서 "HP 유지" 보장
        if (enemyInstance == null)
            enemyInstance = gameObject.AddComponent<EnemyInstance>();

        // definition 연결 보정
        if (enemyInstance.definition == null)
            enemyInstance.definition = definition;

        HidePrompt();
    }

    private void Update()
    {
        var mgr = FocusedCombatManager.Instance;
        if (mgr == null) return;

        if (mgr.IsInFocusedCombat)
        {
            HidePrompt();
            return;
        }

        if (grid == null || playerMover == null)
        {
            HidePrompt();
            return;
        }

        Vector2Int p = playerMover.CurrentCell;
        Vector2Int e = GetEnemyCell();

        bool adjacent = (Mathf.Abs(p.x - e.x) + Mathf.Abs(p.y - e.y)) == 1;
        if (!adjacent)
        {
            HidePrompt();
            return;
        }

        ShowPrompt();

        if (Input.GetKeyDown(interactKey))
        {
            HidePrompt();

            // ✅ 컨텍스트 기반 진입 (HP 이어짐)
            if (playerStats != null && enemyInstance != null && enemyInstance.definition != null)
            {
                var ctx = new EncounterContext(playerStats, enemyInstance, p, e);

                Debug.Log($"[FocusedCombatInteract] Enter with ctx. PlayerHP={playerStats.hp}/{playerStats.maxHP}, EnemyHP={enemyInstance.currentHP}/{enemyInstance.definition.maxHP}");
                mgr.EnterFocusedCombat(ctx);
            }
            else
            {
                Debug.LogWarning("[FocusedCombatInteract] Missing ctx refs. Fallback to definition only (HP may reset).");
                mgr.EnterFocusedCombat(definition);
            }
        }
    }

    private Vector2Int GetEnemyCell()
    {
        if (!useTransformToCell)
            return enemyCellOverride;

        return grid.WorldToCell(transform.position);
    }

    private void ShowPrompt()
    {
        if (promptText == null) return;
        if (!promptText.gameObject.activeSelf) promptText.gameObject.SetActive(true);
        if (promptText.text != promptMessage) promptText.text = promptMessage;
    }

    private void HidePrompt()
    {
        if (promptText == null) return;
        if (promptText.gameObject.activeSelf) promptText.gameObject.SetActive(false);
    }
}
