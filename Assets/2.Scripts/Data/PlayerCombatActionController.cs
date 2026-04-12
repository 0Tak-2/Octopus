using System.Collections;
using UnityEngine;

public class PlayerCombatActionController : MonoBehaviour
{
    [Header("Wiring")]
    public FocusedCombatManager combat;

    [Header("Action List")]
    public CombatAttackDefinition[] attacks;

    [Header("Preview")]
    public bool enableRangePreview = true;

    [Header("Cancel Input")]
    public KeyCode cancelKey = KeyCode.Escape;
    public bool cancelWithRightClick = true;

    private CombatAttackDefinition _currentAttack;
    public bool IsInActionMode => _currentAttack != null;

    private void Awake()
    {
        if (combat == null) combat = FindObjectOfType<FocusedCombatManager>();
    }

    private void Update()
    {
        if (!IsInActionMode) return;

        if (Input.GetKeyDown(cancelKey))
        {
            CancelAction();
            return;
        }

        if (cancelWithRightClick && Input.GetMouseButtonDown(1))
        {
            CancelAction();
            return;
        }
    }

    private void OnEnable() => HookBoardEvents(true);
    private void OnDisable() => HookBoardEvents(false);

    private void HookBoardEvents(bool on)
    {
        if (combat == null || combat.boardUI == null) return;

        if (on)
        {
            combat.boardUI.OnTileHovered -= HandleTileHovered;
            combat.boardUI.OnTileUnhovered -= HandleTileUnhovered;
            combat.boardUI.OnTileHovered += HandleTileHovered;
            combat.boardUI.OnTileUnhovered += HandleTileUnhovered;
        }
        else
        {
            combat.boardUI.OnTileHovered -= HandleTileHovered;
            combat.boardUI.OnTileUnhovered -= HandleTileUnhovered;
        }
    }

    public void SelectAttack(int index)
    {
        if (attacks == null || index < 0 || index >= attacks.Length) return;
        SelectAttack(attacks[index]);
    }

    public void SelectAttack(CombatAttackDefinition attack)
    {
        _currentAttack = attack;
        RefreshTargetHighlights();

        combat?.ShowPlayerHint("([우클릭 / ESC])");
    }

    public void CancelAction()
    {
        _currentAttack = null;

        if (combat != null && combat.boardUI != null)
            combat.boardUI.ClearHighlights();

        combat?.RefreshMoveHighlightsExternal();
        combat?.HidePlayerHint();
    }

    public void OnTileClicked(int x, int y)
    {
        if (!IsInActionMode) return;
        if (combat == null || combat.boardUI == null) return;
        if (combat.IsBusyForInput) return;

        Vector2Int target = new Vector2Int(x, y);

        if (!IsTargetValid(target)) return;
        if (!combat.CanSpendAP(_currentAttack.apCost)) return;

        StartCoroutine(ExecuteAttackRoutine(target));
    }

    private IEnumerator ExecuteAttackRoutine(Vector2Int target)
    {
        if (_currentAttack.requireEnemyOnTarget)
            combat.TrySelectEnemyTargetForAction(target);

        bool isMeleeDash =
            _currentAttack.requireEnemyOnTarget &&
            combat.HasLivingEnemyAtCombatCell(target);

        if (isMeleeDash)
        {
            yield return combat.Anim_PlayerDashHitReturn(
                targetCell: target,
                onImpact: () => ApplyDamageAtTarget(target)
            );
        }
        else
        {
            ApplyDamageAtTarget(target);
        }

        // AP 소모
        combat.SpendAP(_currentAttack.apCost);

        // 공격 모드 종료
        CancelAction();

        // UI/이동 하이라이트 갱신
        combat.RefreshUIExternal();
        combat.RefreshMoveHighlightsExternal();

        // ✅ 턴 자동 종료 제거!
        // 플레이어가 AP를 다 쓰거나(옵션) 턴넘기기 버튼/키를 눌렀을 때만 적 턴.
        yield break;
    }

    private void ApplyDamageAtTarget(Vector2Int target)
    {
        Vector2Int[] offsets = CombatPatterns.GetOffsets(_currentAttack.pattern);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int hit = target + offsets[i];

            if (!combat.boardUI.InBounds(hit.x, hit.y)) continue;
            if (combat.IsBlocked(hit)) continue;

            if (combat.HasLivingEnemyAtCombatCell(hit))
            {
                combat.TrySelectEnemyTargetForAction(hit);
                combat.TryDamageEnemy(_currentAttack.damage);
            }
        }
    }

    private void HandleTileHovered(int x, int y)
    {
        if (!IsInActionMode) return;
        if (!enableRangePreview) return;

        Vector2Int cell = new Vector2Int(x, y);

        RefreshTargetHighlights();

        if (_currentAttack.requireEnemyOnTarget)
        {
            if (combat.HasLivingEnemyAtCombatCell(cell) && IsTargetValid(cell))
                combat.boardUI.SetHighlight(cell.x, cell.y, true);
            else if (combat.boardUI.InBounds(cell.x, cell.y))
                combat.boardUI.SetInvalidTarget(cell.x, cell.y);

            return;
        }

        if (!IsTargetValid(cell)) return;
        ShowPatternPreview(cell);
    }

    private void HandleTileUnhovered(int x, int y)
    {
        if (!IsInActionMode) return;
        if (!enableRangePreview) return;
        RefreshTargetHighlights();
    }

    private void RefreshTargetHighlights()
    {
        if (combat == null || combat.boardUI == null) return;

        combat.boardUI.ClearHighlights();

        if (!IsInActionMode)
        {
            combat.RefreshMoveHighlightsExternal();
            return;
        }

        if (!combat.CanSpendAP(_currentAttack.apCost))
            return;

        if (_currentAttack.requireEnemyOnTarget)
        {
            var enemyCells = combat.GetLivingEnemyCombatCellsSnapshot();
            for (int i = 0; i < enemyCells.Count; i++)
            {
                Vector2Int ec = enemyCells[i];
                if (IsTargetValid(ec))
                    combat.boardUI.SetHighlight(ec.x, ec.y, true);
            }
            return;
        }

        int r = Mathf.Max(1, GetEffectiveRange());
        Vector2Int from = combat.State.playerCell;

        for (int x = from.x - r; x <= from.x + r; x++)
            for (int y = from.y - r; y <= from.y + r; y++)
            {
                if (!combat.boardUI.InBounds(x, y)) continue;

                Vector2Int t = new Vector2Int(x, y);
                int dist = Chebyshev(from, t);
                if (dist <= 0 || dist > r) continue;

                if (combat.IsBlocked(t)) continue;

                combat.boardUI.SetHighlight(x, y, true);
            }
    }

    private void ShowPatternPreview(Vector2Int target)
    {
        Vector2Int[] offsets = CombatPatterns.GetOffsets(_currentAttack.pattern);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int hit = target + offsets[i];
            if (!combat.boardUI.InBounds(hit.x, hit.y)) continue;
            if (combat.IsBlocked(hit)) continue;

            combat.boardUI.SetPreviewHighlight(hit.x, hit.y, true);
        }
    }

    private bool IsTargetValid(Vector2Int target)
    {
        int dist = Chebyshev(combat.State.playerCell, target);
        int range = Mathf.Max(1, GetEffectiveRange());
        if (dist <= 0 || dist > range) return false;

        if (!combat.boardUI.InBounds(target.x, target.y)) return false;

        if (_currentAttack.requireEnemyOnTarget && !combat.HasLivingEnemyAtCombatCell(target))
            return false;

        if (combat.IsBlocked(target)) return false;

        return true;
    }

    private int GetEffectiveRange()
    {
        int baseRange = (_currentAttack != null) ? _currentAttack.range : 1;
        int bonus = (combat != null) ? combat.PlayerRangeBonus : 0;
        return baseRange + bonus;
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy);
    }
}
