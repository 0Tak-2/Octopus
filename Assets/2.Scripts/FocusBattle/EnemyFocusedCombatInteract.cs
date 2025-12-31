using UnityEngine;
using TMPro;

public class EnemyFocusedCombatInteract : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;
    public PlayerGridMover playerMover;
    public GridBoard grid;

    [Header("Prompt (world text)")]
    public TextMeshProUGUI promptText;   // 플레이어 머리 위에 띄울 TMP
    [TextArea] public string promptMessage = "집중해서 싸운다\n(E)";
    public KeyCode interactKey = KeyCode.E;

    [Header("Enemy Cell (optional)")]
    public bool useTransformToCell = true;
    public Vector2Int enemyCellOverride; // 필요하면 강제로 지정(테스트)

    private void Awake()
    {
        if (grid == null) grid = FindObjectOfType<GridBoard>();
        if (playerMover == null) playerMover = FindObjectOfType<PlayerGridMover>();
        if (player == null && playerMover != null) player = playerMover.transform;

        HidePrompt();
    }

    private void Update()
    {
        var mgr = FocusedCombatManager.Instance;
        if (mgr == null) return;

        // 이미 집중전투면 아무것도 안 함
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

        if (adjacent)
        {
            ShowPrompt();

            if (Input.GetKeyDown(interactKey))
            {
                HidePrompt();
                mgr.EnterFocusedCombat();
            }
        }
        else
        {
            HidePrompt();
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

        if (!promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(true);

        if (promptText.text != promptMessage)
            promptText.text = promptMessage;
    }

    private void HidePrompt()
    {
        if (promptText == null) return;
        if (promptText.gameObject.activeSelf)
            promptText.gameObject.SetActive(false);
    }
}
