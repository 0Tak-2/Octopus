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

    private void Update()
    {
        if (!IsInActionMode) return;

        // ESC로 취소
        if (Input.GetKeyDown(cancelKey))
        {
            CancelAction();
            return;
        }

        // 우클릭으로 취소
        if (cancelWithRightClick && Input.GetMouseButtonDown(1))
        {
            CancelAction();
            return;
        }
    }


    private void Awake()
    {
        if (combat == null) combat = FindObjectOfType<FocusedCombatManager>();
    }

    private void OnEnable()
    {
        HookBoardEvents(true);
    }

    private void OnDisable()
    {
        HookBoardEvents(false);
    }

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
    SelectAttack(attacks[index]); // 여기서는 힌트 호출 X
}

public void SelectAttack(CombatAttackDefinition attack)
{
    _currentAttack = attack;
    
    RefreshTargetHighlights();

    if (combat != null)
        combat.ShowPlayerHint("([우클릭 / ESC])");
}

    public void CancelAction()
    {
        _currentAttack = null;
        

        if (combat != null && combat.boardUI != null)
            combat.boardUI.ClearHighlights();

        if (combat != null)
            combat.RefreshMoveHighlightsExternal();
        if (combat != null)
            combat.HidePlayerHint();
    }

    public void OnTileClicked(int x, int y)
    {
        if (!IsInActionMode) return;
        if (combat == null || combat.boardUI == null) return;
        if (combat.IsBusyForInput) return;

        Vector2Int target = new Vector2Int(x, y);

        if (!IsTargetValid(target))
            return;

        if (!combat.CanSpendAP(_currentAttack.apCost))
            return;

        StartCoroutine(ExecuteAttackRoutine(target));
    }

    private IEnumerator ExecuteAttackRoutine(Vector2Int target)
    {
        // requireEnemyOnTarget면 "적 위를 찍는 공격"이라고 가정
        bool isMeleeDash =
            _currentAttack.requireEnemyOnTarget &&
            target == combat.EnemyCell;

        if (isMeleeDash)
        {
            // 돌진-타격-복귀 연출 + 임팩트 시점에 데미지 적용
            yield return combat.Anim_PlayerDashHitReturn(
                targetCell: combat.EnemyCell,
                onImpact: () => ApplyDamageAtTarget(target)
            );
        }
        else
        {
            // 즉시 적용(범위/원거리)
            ApplyDamageAtTarget(target);
        }

        // AP 소모
        combat.SpendAP(_currentAttack.apCost);

        // 공격 모드 종료
        CancelAction();

        // UI/이동 하이라이트 갱신
        combat.RefreshUIExternal();
        combat.RefreshMoveHighlightsExternal();
    }

    private void ApplyDamageAtTarget(Vector2Int target)
    {
        Vector2Int[] offsets = CombatPatterns.GetOffsets(_currentAttack.pattern);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int hit = target + offsets[i];

            if (!combat.boardUI.InBounds(hit.x, hit.y)) continue;

            // 벽/지형이 막으면 해당 칸은 타격 제외 (원하면 규칙 바꿀 수 있음)
            if (combat.IsBlocked(hit)) continue;

            if (hit == combat.EnemyCell)
                combat.DamageEnemy(_currentAttack.damage);
        }
    }

    private void HandleTileHovered(int x, int y)
    {
        if (!IsInActionMode) return;
        if (!enableRangePreview) return;

        Vector2Int cell = new Vector2Int(x, y);

        // 기본 타겟 하이라이트(범위 가능 타일 or 적 타일) 먼저 그린 다음,
        RefreshTargetHighlights();

        // ✅ 적 지정(단일 타격) 공격: 호버 타일 색 피드백
        if (_currentAttack.requireEnemyOnTarget)
        {
            // 적 위 & 타겟 유효 -> 초록
            if (cell == combat.EnemyCell && IsTargetValid(cell))
            {
                combat.boardUI.SetHighlight(cell.x, cell.y, true); // 초록
            }
            else
            {
                // 일반 타일 위 -> 빨강 (단, 보드 안일 때)
                if (combat.boardUI.InBounds(cell.x, cell.y))
                    combat.boardUI.SetInvalidTarget(cell.x, cell.y);
            }
            return;
        }

        // ✅ 범위 스킬: 타겟 유효한 타일 위에서만 "패턴 프리뷰" 표시
        if (!IsTargetValid(cell)) return;

        ShowPatternPreview(cell); // 내부에서 SetPreviewHighlight(초록 진하게)로 칠해짐
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

        // AP 부족이면 아무것도 표시 X
        if (!combat.CanSpendAP(_currentAttack.apCost))
            return;

        // ✅ 1) 적 지정 공격: 적 타일만 "가능하면" 초록 표시 (항상은 아님)
        if (_currentAttack.requireEnemyOnTarget)
        {
            // 적이 사거리 안 + 타겟 유효하면 초록 표시
            if (IsTargetValid(combat.EnemyCell))
                combat.boardUI.SetHighlight(combat.EnemyCell.x, combat.EnemyCell.y, true);

            return;
        }

        // ✅ 2) 범위 스킬/타일 지정 스킬: 타격 가능한 "타겟 타일"들을 초록 표시
        int r = Mathf.Max(1, _currentAttack.range);
        Vector2Int from = combat.PlayerCell;

        for (int x = from.x - r; x <= from.x + r; x++)
            for (int y = from.y - r; y <= from.y + r; y++)
            {
                if (!combat.boardUI.InBounds(x, y)) continue;

                Vector2Int t = new Vector2Int(x, y);
                int dist = Chebyshev(from, t);
                if (dist <= 0 || dist > r) continue;

                if (combat.IsBlocked(t)) continue;

                combat.boardUI.SetHighlight(x, y, true); // 초록 반투명
            }
    }


    private void ShowPatternPreview(Vector2Int target)
    {
        // requireEnemyOnTarget가 꺼진 범위공격이면 더 중요하고,
        // 켜진 단일공격이라도 프리뷰가 있으면 UX가 좋긴 함.
        Vector2Int[] offsets = CombatPatterns.GetOffsets(_currentAttack.pattern);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2Int hit = target + offsets[i];
            if (!combat.boardUI.InBounds(hit.x, hit.y)) continue;

            // 막힌 칸은 프리뷰에서도 제외(규칙 일관성)
            if (combat.IsBlocked(hit)) continue;

            combat.boardUI.SetPreviewHighlight(hit.x, hit.y, true);
        }
    }

    private bool IsTargetValid(Vector2Int target)
    {
        // 사거리(체비셰프) 체크
        int dist = Chebyshev(combat.PlayerCell, target);
        if (dist <= 0 || dist > Mathf.Max(1, _currentAttack.range)) return false;

        // 인바운드
        if (!combat.boardUI.InBounds(target.x, target.y)) return false;

        // 적만 찍는 공격이면 적 칸만
        if (_currentAttack.requireEnemyOnTarget && target != combat.EnemyCell)
            return false;

        // 타겟 타일 막힘이면 불가
        if (combat.IsBlocked(target)) return false;

        return true;
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy);
    }
}
