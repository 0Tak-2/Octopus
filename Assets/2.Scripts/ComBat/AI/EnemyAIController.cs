using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy AI Controller
/// - AI 판단 로직은 BoardUI로 들어가지 않음
/// - 이동은 벽 고려(BFS 거리맵) + 1칸씩 계획
/// - AI3(엘리트): AP(최대5, 턴+1) + 무료이동 1회 + 4스킬 + 버프(6턴 공격력2배)
/// </summary>
public class EnemyAIController : MonoBehaviour
{
    private FocusedCombatManager _combat;

    // 8방향 이동
    private static readonly Vector2Int[] DIRS8 = new Vector2Int[]
    {
        new Vector2Int( 1, 0), new Vector2Int(-1, 0),
        new Vector2Int( 0, 1), new Vector2Int( 0,-1),
        new Vector2Int( 1, 1), new Vector2Int( 1,-1),
        new Vector2Int(-1, 1), new Vector2Int(-1,-1),
    };

    // ==========================
    // AI3 Elite State (runtime)
    // ==========================
    [Header("AI3 Elite Runtime (ReadOnly)")]
    [SerializeField] private int eliteAP = 1;
    [SerializeField] private int eliteFreeMovesUsedThisTurn = 0;
    [SerializeField] private int eliteAtkBuffTurnsLeft = 0;

    public int EliteAP => eliteAP;
    public int EliteAPMax => 5;
    public int EliteBuffTurnsLeft => eliteAtkBuffTurnsLeft;
    public bool EliteBuffActive => eliteAtkBuffTurnsLeft > 0;

    // ==========================
    // AI3 Constants (as described)
    // ==========================
    private const int ELITE_AP_MAX = 5;
    private const int ELITE_AP_REGEN = 1;
    private const int ELITE_FREE_MOVES_PER_TURN = 1;

    // Skills
    // 1) melee normal: range1 AP1 dmg = base
    private const int SK1_COST = 1;
    private const int SK1_RANGE = 1;

    // 2) melee critical: range1 AP2 dmg = base * 2 (치명적)
    private const int SK2_COST = 2;
    private const int SK2_RANGE = 1;
    private const int SK2_MULT = 2;

    // 3) mid range: range2 AP1 dmg = base (적당)
    private const int SK3_COST = 1;
    private const int SK3_RANGE = 2;

    // 4) attack buff: AP2, 6 turns, x2 damage
    private const int SK4_COST = 2;
    private const int BUFF_TURNS = 6;
    private const int BUFF_MULT = 2;

    // Law: if AP < 3 -> never move toward player
    private const int MOVE_TOWARD_AP_THRESHOLD = 3;

    public void Bind(FocusedCombatManager combat) => _combat = combat;

    public void ResetEliteState()
    {
        eliteAP = 1;
        eliteFreeMovesUsedThisTurn = 0;
        eliteAtkBuffTurnsLeft = 0;
    }

    public IEnumerator TakeTurn()
    {
        if (_combat == null) yield break;
        if (!_combat.IsInFocusedCombat) yield break;

        int moveN = Mathf.Max(1, _combat.GetEnemyMoveRange());
        int rangeV = Mathf.Max(1, _combat.GetEnemyAttackRange());

        EnemyAIType type = _combat.GetEnemyAIType();

        switch (type)
        {
            case EnemyAIType.AI3_Elite:
                yield return StartCoroutine(AI3_Elite(moveN));
                break;

            case EnemyAIType.AI2_HitAndRun:
                yield return StartCoroutine(AI2_HitAndRun(moveN, rangeV));
                break;

            case EnemyAIType.AI1_MeleeChase:
            default:
                yield return StartCoroutine(AI1_MeleeChase(moveN, rangeV));
                break;
        }
    }

    // ============================================================
    // AI3 Elite (your spec)
    // ============================================================
    private IEnumerator AI3_Elite(int moveN)
    {
        // Turn start: AP +1
        eliteAP = Mathf.Clamp(eliteAP + ELITE_AP_REGEN, 0, ELITE_AP_MAX);
        eliteFreeMovesUsedThisTurn = 0;

        // Buff tick down at turn start (6턴 유지: "이후 6번의 적 턴"으로 해석)
        if (eliteAtkBuffTurnsLeft > 0)
            eliteAtkBuffTurnsLeft = Mathf.Max(0, eliteAtkBuffTurnsLeft - 1);

        Vector2Int e = _combat.State.enemyCell;
        Vector2Int p = _combat.State.playerCell;

        int d = Chebyshev(e, p);
        var distMap = BuildDistanceMapFrom(p);

        // ✅ 변경 1) 도망 조건: AP가 1 이하일 때만
        if (eliteAP <= 1)
        {
            yield return StartCoroutine(EliteMoveAwayOneStep(e, p, distMap));
            yield break;
        }

        // ✅ 변경 2) 버프는 "없을 때만" 사용
        // 거리 5 이상이면 버프를 먼저 고려하되,
        // 이미 버프가 있으면(남은 턴 > 0) => 버프 안 쓰고 접근/공격 로직으로 넘어감.
        if (d >= 5 && !EliteBuffActive)
        {
            if (eliteAP >= SK4_COST)
            {
                eliteAP -= SK4_COST;
                eliteAtkBuffTurnsLeft = BUFF_TURNS;
                yield break; // 버프만 쓰고 턴 종료
            }
            // AP 부족하면 그냥 접근 로직으로 넘어감
        }

        // =========================
        // 여기부터는 "접근/공격" 메인 로직
        // =========================

        // 절대 법칙: AP가 3 미만이면 플레이어 쪽으로 이동하지 않는다.
        // (단, 공격은 사거리 안이면 할 수 있음)
        bool canMoveToward = eliteAP >= MOVE_TOWARD_AP_THRESHOLD;

        // 1) 사거리2 공격(3번)을 쓸 수 있는 상황이면 우선 사용 조건 체크
        // - 거리 3~4이면: 접근해서 사거리2 만들고 3번
        if ((d == 4 || d == 3) && canMoveToward)
        {
            yield return StartCoroutine(EliteMoveTowardUntilInRange2(moveN, p, distMap));

            e = _combat.State.enemyCell;
            d = Chebyshev(e, p);

            if (d <= SK3_RANGE && eliteAP >= SK3_COST)
            {
                eliteAP -= SK3_COST;
                int dmg = GetBuffedBaseDamage();
                yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
            }
            yield break;
        }

        // 2) 거리 1~2면: 2번 우선(AP2, range1), 부족하면 1번(AP1, range1)
        if (d == 1 || d == 2)
        {
            // 거리 2면 range1 공격을 위해 1칸 접근이 필요.
            // 단, AP>=3일 때만 "toward move" 가능
            if (d == 2 && canMoveToward)
            {
                yield return StartCoroutine(EliteMoveTowardSteps(1, p, distMap));
            }

            e = _combat.State.enemyCell;
            d = Chebyshev(e, p);

            if (d <= 1)
            {
                if (eliteAP >= SK2_COST)
                {
                    eliteAP -= SK2_COST;
                    int dmg = GetBuffedBaseDamage() * SK2_MULT;
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
                }
                else if (eliteAP >= SK1_COST)
                {
                    eliteAP -= SK1_COST;
                    int dmg = GetBuffedBaseDamage();
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
                }
            }

            yield break;
        }

        // 3) 거리 5 이상인데 버프가 이미 있거나(AP 부족) -> "접근" 시도
        //    (단, AP>=3 아니면 접근 금지)
        if (d >= 5)
        {
            if (canMoveToward)
            {
                // 목표: 우선 사거리2(3번)까지 접근 시도
                yield return StartCoroutine(EliteMoveTowardUntilInRange2(moveN, p, distMap));

                e = _combat.State.enemyCell;
                d = Chebyshev(e, p);

                // 사거리2 들어오면 3번 쏘기
                if (d <= SK3_RANGE && eliteAP >= SK3_COST)
                {
                    eliteAP -= SK3_COST;
                    int dmg = GetBuffedBaseDamage();
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
                }
            }
            // AP 부족(2)인데 도망 조건(<=1)은 아니면 => 그냥 대기
            yield break;
        }

        // 4) 나머지(거리=3~4인데 AP<3 등): 접근 금지라서 대기
        yield break;
    }


    private int GetBuffedBaseDamage()
    {
        int baseDmg = Mathf.Max(0, _combat.GetEnemyAttackDamage());
        if (EliteBuffActive) baseDmg *= BUFF_MULT;
        return baseDmg;
    }

    // ==========================
    // Elite move helpers (AP + free move)
    // ==========================
    private IEnumerator EliteMoveAwayOneStep(Vector2Int enemyPos, Vector2Int playerPos, Dictionary<Vector2Int, int> distMap)
    {
        // 1칸 후퇴 = "move action 1회"로 취급
        if (!CanPayMoveActionCost()) yield break;
        PayMoveActionCost();

        var step = PickNextStepAwayFromPlayer(enemyPos, playerPos, distMap);
        if (step == enemyPos) yield break;

        yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(new Vector2Int[] { step }));
    }

    private IEnumerator EliteMoveTowardUntilInRange2(int moveN, Vector2Int playerPos, Dictionary<Vector2Int, int> distMap)
    {
        // move action 1회(최대 moveN칸)로 접근
        if (!CanPayMoveActionCost()) yield break;
        PayMoveActionCost();

        Vector2Int start = _combat.State.enemyCell;

        // 이동 중 사거리2 들어오면 정지
        var steps = new List<Vector2Int>(moveN);
        Vector2Int cur = start;

        for (int i = 0; i < moveN; i++)
        {
            if (Chebyshev(cur, playerPos) <= SK3_RANGE) break;

            Vector2Int next = PickNextStepTowardPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            steps.Add(next);
            cur = next;
        }

        if (steps.Count > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(steps.ToArray()));
    }

    private IEnumerator EliteMoveTowardSteps(int stepsCount, Vector2Int playerPos, Dictionary<Vector2Int, int> distMap)
    {
        if (stepsCount <= 0) yield break;
        if (!CanPayMoveActionCost()) yield break;
        PayMoveActionCost();

        Vector2Int start = _combat.State.enemyCell;

        var steps = new List<Vector2Int>(stepsCount);
        Vector2Int cur = start;

        for (int i = 0; i < stepsCount; i++)
        {
            Vector2Int next = PickNextStepTowardPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            steps.Add(next);
            cur = next;
        }

        if (steps.Count > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(steps.ToArray()));
    }

    private bool CanPayMoveActionCost()
    {
        // 무료이동 1회가 남으면 0코스트
        if (eliteFreeMovesUsedThisTurn < ELITE_FREE_MOVES_PER_TURN) return true;

        // 이후 이동 액션은 AP 1 소모
        return eliteAP >= 1;
    }

    private void PayMoveActionCost()
    {
        if (eliteFreeMovesUsedThisTurn < ELITE_FREE_MOVES_PER_TURN)
        {
            eliteFreeMovesUsedThisTurn++;
            return;
        }

        eliteAP = Mathf.Max(0, eliteAP - 1);
    }

    // ============================================================
    // AI1 / AI2 (기존 유지)
    // ============================================================
    private IEnumerator AI1_MeleeChase(int moveN, int rangeV)
    {
        Vector2Int enemyPos = _combat.State.enemyCell;
        Vector2Int playerPos = _combat.State.playerCell;

        if (InChebyshevRange(enemyPos, playerPos, rangeV))
        {
            yield return _combat.StartCoroutine(_combat.EnemyAttackRoutine());
            yield break;
        }

        var distMap = BuildDistanceMapFrom(playerPos);
        var steps = BuildApproachStepsStepwise(enemyPos, playerPos, moveN, rangeV, distMap);

        if (steps.Length > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(steps));

        enemyPos = _combat.State.enemyCell;
        playerPos = _combat.State.playerCell;

        if (InChebyshevRange(enemyPos, playerPos, rangeV))
            yield return _combat.StartCoroutine(_combat.EnemyAttackRoutine());
    }

    private IEnumerator AI2_HitAndRun(int moveN, int rangeV)
    {
        Vector2Int enemyPos = _combat.State.enemyCell;
        Vector2Int playerPos = _combat.State.playerCell;

        if (InChebyshevRange(enemyPos, playerPos, rangeV))
        {
            yield return _combat.StartCoroutine(_combat.EnemyAttackRoutine());
            var distMap0 = BuildDistanceMapFrom(playerPos);
            var retreat0 = BuildRetreatStepsStepwise(enemyPos, playerPos, moveN, distMap0);
            if (retreat0.Length > 0) yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(retreat0));
            yield break;
        }

        var distMap = BuildDistanceMapFrom(playerPos);
        var steps = BuildApproachStepsStepwise(enemyPos, playerPos, moveN, rangeV, distMap);

        if (steps.Length > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(steps));

        enemyPos = _combat.State.enemyCell;
        playerPos = _combat.State.playerCell;

        if (!InChebyshevRange(enemyPos, playerPos, rangeV))
            yield break;

        yield return _combat.StartCoroutine(_combat.EnemyAttackRoutine());

        var distMapAfter = BuildDistanceMapFrom(playerPos);
        var retreatSteps = BuildRetreatStepsStepwise(enemyPos, playerPos, moveN, distMapAfter);

        if (retreatSteps.Length > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(retreatSteps));
    }

    // ==========================
    // Stepwise builders (AI1/AI2)
    // ==========================
    private Vector2Int[] BuildApproachStepsStepwise(
        Vector2Int startEnemy,
        Vector2Int playerPos,
        int moveN,
        int rangeV,
        Dictionary<Vector2Int, int> distMap)
    {
        var steps = new List<Vector2Int>(moveN);
        Vector2Int cur = startEnemy;

        for (int i = 0; i < moveN; i++)
        {
            if (InChebyshevRange(cur, playerPos, rangeV))
                break;

            Vector2Int next = PickNextStepTowardPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            steps.Add(next);
            cur = next;
        }

        return steps.ToArray();
    }

    private Vector2Int[] BuildRetreatStepsStepwise(
        Vector2Int startEnemy,
        Vector2Int playerPos,
        int moveN,
        Dictionary<Vector2Int, int> distMap)
    {
        var steps = new List<Vector2Int>(moveN);
        Vector2Int cur = startEnemy;

        for (int i = 0; i < moveN; i++)
        {
            Vector2Int next = PickNextStepAwayFromPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            steps.Add(next);
            cur = next;
        }

        return steps.ToArray();
    }

    private Vector2Int PickNextStepTowardPlayer(Vector2Int cur, Vector2Int playerPos, Dictionary<Vector2Int, int> distMap)
    {
        int curD = GetDist(distMap, cur);

        Vector2Int best = cur;
        int bestD = curD;

        bool useFallback = curD >= INF;

        for (int i = 0; i < DIRS8.Length; i++)
        {
            Vector2Int n = cur + DIRS8[i];
            if (!InBounds(n)) continue;
            if (IsBlocked(n)) continue;
            if (n == playerPos) continue;

            if (useFallback)
            {
                int cd = Chebyshev(cur, playerPos);
                int nd = Chebyshev(n, playerPos);
                if (nd < cd) best = n;
            }
            else
            {
                int nd = GetDist(distMap, n);
                if (nd < bestD)
                {
                    bestD = nd;
                    best = n;
                }
            }
        }

        return best;
    }

    private Vector2Int PickNextStepAwayFromPlayer(Vector2Int cur, Vector2Int playerPos, Dictionary<Vector2Int, int> distMap)
    {
        int curD = GetDist(distMap, cur);

        Vector2Int best = cur;
        int bestD = curD;

        bool useFallback = curD >= INF;

        for (int i = 0; i < DIRS8.Length; i++)
        {
            Vector2Int n = cur + DIRS8[i];
            if (!InBounds(n)) continue;
            if (IsBlocked(n)) continue;
            if (n == playerPos) continue;

            if (useFallback)
            {
                int cd = Chebyshev(cur, playerPos);
                int nd = Chebyshev(n, playerPos);
                if (nd > cd) best = n;
            }
            else
            {
                int nd = GetDist(distMap, n);
                if (nd > bestD)
                {
                    bestD = nd;
                    best = n;
                }
            }
        }

        return best;
    }

    // ==========================
    // Distance map (BFS)
    // ==========================
    private const int INF = 1_000_000_000;

    private Dictionary<Vector2Int, int> BuildDistanceMapFrom(Vector2Int source)
    {
        var dist = new Dictionary<Vector2Int, int>();
        if (!InBounds(source) || IsBlocked(source)) return dist;

        var q = new Queue<Vector2Int>();
        dist[source] = 0;
        q.Enqueue(source);

        while (q.Count > 0)
        {
            var c = q.Dequeue();
            int cd = dist[c];

            for (int i = 0; i < DIRS8.Length; i++)
            {
                var n = c + DIRS8[i];
                if (!InBounds(n)) continue;
                if (IsBlocked(n)) continue;
                if (dist.ContainsKey(n)) continue;

                dist[n] = cd + 1;
                q.Enqueue(n);
            }
        }

        return dist;
    }

    private int GetDist(Dictionary<Vector2Int, int> dist, Vector2Int cell)
        => (dist != null && dist.TryGetValue(cell, out int v)) ? v : INF;

    // ==========================
    // helpers
    // ==========================
    private bool InChebyshevRange(Vector2Int a, Vector2Int b, int range)
        => Chebyshev(a, b) <= range;

    private int Chebyshev(Vector2Int a, Vector2Int b)
        => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private bool InBounds(Vector2Int cell)
    {
        if (_combat == null || _combat.boardUI == null) return false;
        return _combat.boardUI.InBounds(cell.x, cell.y);
    }

    private bool IsBlocked(Vector2Int cell)
    {
        if (_combat == null) return true;
        return _combat.IsBlocked(cell);
    }
}
