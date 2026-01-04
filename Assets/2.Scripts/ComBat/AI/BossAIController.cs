using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossAIController : MonoBehaviour
{
    private FocusedCombatManager _combat;
    private BossState _state;

    private static readonly Vector2Int[] DIRS8 = new Vector2Int[]
    {
        new Vector2Int( 1, 0), new Vector2Int(-1, 0),
        new Vector2Int( 0, 1), new Vector2Int( 0,-1),
        new Vector2Int( 1, 1), new Vector2Int( 1,-1),
        new Vector2Int(-1, 1), new Vector2Int(-1,-1),
    };

    private const int INF = 1_000_000_000;

    public void Bind(FocusedCombatManager combat)
    {
        _combat = combat;

        if (_state == null) _state = GetComponent<BossState>();
        if (_state == null) _state = gameObject.AddComponent<BossState>();

        ApplyDefinitionConfig();
    }

    public BossState GetState() => _state;

    public void ResetBossState()
    {
        if (_state == null) _state = GetComponent<BossState>();
        if (_state == null) _state = gameObject.AddComponent<BossState>();

        ApplyDefinitionConfig();
        _state.ResetForCombat(startAp: 1);
    }

    private EnemyDefinition Def => (_combat != null) ? _combat.CurrentEnemyDef : null;

    private void ApplyDefinitionConfig()
    {
        var def = Def;
        int apMax = (def != null) ? Mathf.Clamp(def.bossMaxAP, 1, 5) : 5;
        int freeMoves = (def != null) ? Mathf.Max(0, def.bossFreeMovesPerTurn) : 1;
        _state.Configure(apMax, freeMoves);
    }

    // =========================
    // Skill helpers (read from Attack Definition)
    // =========================
    private CombatAttackDefinition SkillBasic => Def != null ? Def.bossSkill1_Basic : null;
    private CombatAttackDefinition SkillStun => Def != null ? Def.bossSkill2_Stun : null;
    private CombatAttackDefinition SkillCharge => Def != null ? Def.bossSkill3_Charge : null;
    private CombatAttackDefinition SkillWithdraw => Def != null ? Def.bossSkill4_Withdraw : null;
    private CombatAttackDefinition SkillMeditation => Def != null ? Def.bossSkill5_Meditation : null;

    private int GetSkillCostOrFallback(CombatAttackDefinition skill, int fallback)
    {
        if (skill != null) return Mathf.Max(0, skill.apCost);
        return Mathf.Max(0, fallback);
    }

    private int GetSkillDamageOrFallback(CombatAttackDefinition skill, int fallback)
    {
        if (skill != null) return Mathf.Max(0, skill.damage);
        return Mathf.Max(0, fallback);
    }

    public IEnumerator TakeTurn()
    {
        if (_combat == null) yield break;
        if (!_combat.IsInFocusedCombat) yield break;
        if (_state == null) yield break;

        ApplyDefinitionConfig();

        var def = Def;

        // Parameters from EnemyDefinition (tuning)
        int stunTurns = (def != null) ? Mathf.Max(1, def.bossStunTurns) : 1;

        int dashRange = (def != null) ? Mathf.Max(1, def.bossChargeDashRange) : 3;
        float chargeChance = (def != null) ? Mathf.Clamp01(def.bossChargeUseChance) : 0.35f;

        int withdrawDist = (def != null) ? Mathf.Max(0, def.bossWithdrawMoveDistance) : 3;

        int meditationApThreshold = (def != null) ? Mathf.Max(0, def.bossMeditationApThreshold) : 2;
        float meditationChance = (def != null) ? Mathf.Clamp01(def.bossMeditationUseChance) : 0.5f;
        int meditationApGain = (def != null) ? Mathf.Max(0, def.bossMeditationApGain) : 1;
        int meditationTurns = (def != null) ? Mathf.Max(1, def.bossMeditationTurns) : 1;

        // Skill costs (prefer skill.apCost; fallback to def fields for safety)
        int COST_BASIC = GetSkillCostOrFallback(SkillBasic, def != null ? def.bossBasicApCost : 1);
        int COST_STUN = GetSkillCostOrFallback(SkillStun, def != null ? def.bossStunApCost : 1);
        int COST_CHARGE = GetSkillCostOrFallback(SkillCharge, def != null ? def.bossChargeApCost : 2);
        int COST_WITHDRAW = GetSkillCostOrFallback(SkillWithdraw, def != null ? def.bossWithdrawApCost : 1);
        // meditation cost는 0 권장(효과는 로직에서 처리). 필요하면 SkillMeditation.apCost로 바꿔도 됨.

        // Skill damages (ALL from Attack Definition)
        int DMG_BASIC = GetSkillDamageOrFallback(SkillBasic, _combat.GetEnemyAttackDamage());
        int DMG_STUN = GetSkillDamageOrFallback(SkillStun, Mathf.Max(0, _combat.GetEnemyAttackDamage() - 1));
        int DMG_WITHDRAW = GetSkillDamageOrFallback(SkillWithdraw, Mathf.Max(0, _combat.GetEnemyAttackDamage() - 1));
        // ✅ 돌진 데미지도 Attack Definition에서 관리
        int DMG_CHARGE_DASH = GetSkillDamageOrFallback(SkillCharge, Mathf.Max(0, _combat.GetEnemyAttackDamage() * 2));

        _state.OnBossTurnStart();

        int moveN = Mathf.Max(1, _combat.GetEnemyMoveRange());

        Vector2Int e = _combat.State.enemyCell;
        Vector2Int p = _combat.State.playerCell;

        // 0) charging resolve first
        if (_state.IsCharging)
        {
            ShowBossPopup("돌진!");

            int dmg = ApplyBossPassiveMultiplier(DMG_CHARGE_DASH, def);

            if (IsInLineRange(e, p, dashRange))
            {
                yield return StartCoroutine(ChargeDashPierce_NoOverlap(e, p, dmg, dashRange));
            }
            else
            {
                ShowBossPopup("차징 실패!");
                // 차징 실패 시 "뒤로 3칸 빠져" = withdrawDist 우선, 없으면 dashRange
                int back = (withdrawDist > 0) ? withdrawDist : dashRange;
                yield return StartCoroutine(RetreatSteps(_combat.State.enemyCell, _combat.State.playerCell, back));
            }

            _state.SetCharging(false);
            yield break;
        }

        // 1) follow-up after stun: basic vs charge 50%
        if (_state.ForceFollowUpAfterStun)
        {
            _state.SetFollowUpAfterStun(false);

            if (Random.value < 0.5f)
            {
                if (Chebyshev(_combat.State.enemyCell, _combat.State.playerCell) <= 1 && _state.CanSpendAP(COST_BASIC))
                {
                    _state.SpendAP(COST_BASIC);

                    int dmg = ApplyBossPassiveMultiplier(DMG_BASIC, def);
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
                }
                yield break;
            }
            else
            {
                if (_state.CanSpendAP(COST_CHARGE))
                {
                    _state.SpendAP(COST_CHARGE);
                    _state.SetCharging(true);
                    ShowBossPopup("차징!");
                }
                yield break;
            }
        }

        e = _combat.State.enemyCell;
        p = _combat.State.playerCell;
        int dist = Chebyshev(e, p);

        // 2) meditation candidate (AP <= threshold) - 거리 1에서도 후보에 포함
        if (_state.AP <= meditationApThreshold && Random.value < meditationChance)
        {
            // (선택) SkillMeditation.apCost를 쓰고 싶다면 여기서 SpendAP 처리 가능
            // 현재는 "명상은 AP+1, 보호막"이라 cost 0 권장
            _state.GainAP(meditationApGain);
            _state.ActivateMeditation(meditationTurns);

            ShowBossPopup("명상");
            yield break;
        }

        // 3) adjacent: 50% stun vs withdraw attack
        if (dist == 1)
        {
            if (Random.value < 0.5f)
            {
                if (_state.CanSpendAP(COST_STUN))
                {
                    _state.SpendAP(COST_STUN);

                    int dmg = ApplyBossPassiveMultiplier(DMG_STUN, def);
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));

                    // ✅ 기절 적용(네 FocusedCombatManager에 있어야 함)
                    _combat.ApplyPlayerStun(stunTurns);

                    // "기절 성공 시 다음턴 플레이어 스킵" 이후, 보스는 다음턴에 기본/차징 50%
                    _state.SetFollowUpAfterStun(true);
                }
                yield break;
            }
            else
            {
                if (_state.CanSpendAP(COST_WITHDRAW))
                {
                    _state.SpendAP(COST_WITHDRAW);

                    int dmg = ApplyBossPassiveMultiplier(DMG_WITHDRAW, def);
                    yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));

                    if (withdrawDist > 0)
                        yield return StartCoroutine(RetreatSteps(_combat.State.enemyCell, _combat.State.playerCell, withdrawDist));
                }
                yield break;
            }
        }

        // 4) start charging if within dashRange and aligned
        if (_state.CanSpendAP(COST_CHARGE) && IsInLineRange(e, p, dashRange) && Random.value < chargeChance)
        {
            _state.SpendAP(COST_CHARGE);
            _state.SetCharging(true);
            ShowBossPopup("차징!");
            yield break;
        }

        // 5) move toward (one move action) then maybe basic
        if (_state.CanPayMoveActionCost())
        {
            if (_state.PayMoveActionCost())
            {
                var distMap = BuildDistanceMapFrom(p);
                Vector2Int[] steps = BuildApproachStepsStepwise(
                    startEnemy: _combat.State.enemyCell,
                    playerPos: p,
                    maxSteps: moveN,
                    stopRange: 1,
                    distMap: distMap);

                if (steps.Length > 0)
                    yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(steps));
            }
        }

        e = _combat.State.enemyCell;
        p = _combat.State.playerCell;
        dist = Chebyshev(e, p);

        if (dist <= 1 && _state.CanSpendAP(COST_BASIC))
        {
            _state.SpendAP(COST_BASIC);

            int dmg = ApplyBossPassiveMultiplier(DMG_BASIC, def);
            yield return _combat.StartCoroutine(_combat.EnemyAttackRoutineOverride(dmg, _combat.GetEnemyUseDash()));
        }
    }

    // =========================
    // Passive damage by HP (x1~xMax, xMax at <=fullRatio)
    // =========================
    private int ApplyBossPassiveMultiplier(int rawSkillDamage, EnemyDefinition def)
    {
        int dmg = Mathf.Max(0, rawSkillDamage);

        int maxHp = Mathf.Max(1, _combat.GetEnemyMaxHP());
        float hpRatio = Mathf.Clamp01((float)_combat.State.enemyHP / maxHp);

        float fullRatio = (def != null) ? Mathf.Clamp(def.bossRageFullRatio, 0.05f, 1f) : 0.4f;
        float maxMult = (def != null) ? Mathf.Max(1f, def.bossRageMaxMultiplier) : 2f;

        if (hpRatio <= fullRatio) return Mathf.Max(0, Mathf.RoundToInt(dmg * maxMult));

        float t = Mathf.InverseLerp(1f, fullRatio, hpRatio); // 1 -> 0
        float mult = Mathf.Lerp(1f, maxMult, t);
        return Mathf.Max(0, Mathf.RoundToInt(dmg * mult));
    }

    // =========================
    // Popups
    // =========================
    private void ShowBossPopup(string text)
    {
        if (_combat == null || _combat.vfx == null) return;

        if (_combat.vfx.popupPrefab == null && _combat.playerHintPrefab != null)
            _combat.vfx.popupPrefab = _combat.playerHintPrefab;

        _combat.vfx.ShowPopup(_combat.EnemyToken, text);
    }

    // =========================
    // Charge dash pierce (NO overlap with player cell)
    // =========================
    private IEnumerator ChargeDashPierce_NoOverlap(Vector2Int from, Vector2Int playerPos, int damage, int dashRange)
    {
        bool hit = IsInLineRange(from, playerPos, dashRange);

        Vector2Int dir = GetLineDir(from, playerPos);

        List<Vector2Int> movePath = new List<Vector2Int>(dashRange);
        Vector2Int cur = from;

        for (int i = 0; i < dashRange; i++)
        {
            Vector2Int next = cur + dir;
            if (!_combat.boardUI.InBounds(next.x, next.y)) break;
            if (_combat.IsBlocked(next)) break;

            if (next == playerPos) break;

            movePath.Add(next);
            cur = next;
        }

        if (movePath.Count > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(movePath.ToArray()));

        if (hit)
        {
            _combat.TryDamagePlayer(damage);

            if (_combat.vfx != null && _combat.PlayerToken != null)
                yield return _combat.StartCoroutine(_combat.vfx.HitPulse(_combat.PlayerToken));
        }
    }

    private bool IsInLineRange(Vector2Int from, Vector2Int to, int maxRange)
    {
        Vector2Int d = to - from;
        int dx = d.x;
        int dy = d.y;

        bool isLine = (dx == 0) || (dy == 0) || (Mathf.Abs(dx) == Mathf.Abs(dy));
        if (!isLine) return false;

        int dist = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        return dist >= 1 && dist <= maxRange;
    }

    private Vector2Int GetLineDir(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;

        dx = (dx == 0) ? 0 : (dx > 0 ? 1 : -1);
        dy = (dy == 0) ? 0 : (dy > 0 ? 1 : -1);
        return new Vector2Int(dx, dy);
    }

    // =========================
    // Retreat N (away from player)
    // =========================
    private IEnumerator RetreatSteps(Vector2Int enemyPos, Vector2Int playerPos, int steps)
    {
        if (steps <= 0) yield break;
        if (!_state.CanPayMoveActionCost()) yield break;
        if (!_state.PayMoveActionCost()) yield break;

        var distMap = BuildDistanceMapFrom(playerPos);

        List<Vector2Int> path = new List<Vector2Int>(steps);
        Vector2Int cur = _combat.State.enemyCell;

        for (int i = 0; i < steps; i++)
        {
            Vector2Int next = PickNextStepAwayFromPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            path.Add(next);
            cur = next;
        }

        if (path.Count > 0)
            yield return _combat.StartCoroutine(_combat.EnemyMoveRoutine(path.ToArray()));
    }

    // =========================
    // Movement helpers (BFS map)
    // =========================
    private Vector2Int[] BuildApproachStepsStepwise(
        Vector2Int startEnemy,
        Vector2Int playerPos,
        int maxSteps,
        int stopRange,
        Dictionary<Vector2Int, int> distMap)
    {
        var steps = new List<Vector2Int>(maxSteps);
        Vector2Int cur = startEnemy;

        for (int i = 0; i < maxSteps; i++)
        {
            if (Chebyshev(cur, playerPos) <= stopRange) break;

            Vector2Int next = PickNextStepTowardPlayer(cur, playerPos, distMap);
            if (next == cur) break;
            if (next == playerPos) break;

            steps.Add(next);
            cur = next;
        }

        return steps.ToArray();
    }

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
