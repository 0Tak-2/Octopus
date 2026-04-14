using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldSkillCaster : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;
    public FieldSkillLoadout loadout;
    public FieldTurnCoordinator turnCoordinator;

    [Header("Targeting")]
    public Camera worldCamera;
    [Tooltip("적 판정에 사용할 레이어(Enemy 권장)")]
    public LayerMask enemyQueryLayerMask = ~0;

    [Header("Rules")]
    public bool requireLoS = true;

    [Header("Cooldown (Turn-based)")]
    public bool autoCooldownFromApCost = true;
    public int defaultCooldownTurnsIfNoCost = 1;

    [Header("AOE Query")]
    [Tooltip("셀 중심에서 적을 찾는 반경(셀 크기/콜라이더에 맞춰 조절)")]
    public float cellOverlapRadius = 0.18f;

    [Header("Cast Visual")]
    public float castDashDuration = 0.06f;
    public float castReturnDuration = 0.05f;
    public float castDashFraction = 0.55f;

    [Header("Behavior")]
    [Tooltip("시전 성공 시 자동으로 스킬 들기 상태를 해제")]
    public bool autoDisarmAfterCast = true;

    [Header("Debug")]
    public bool log = true;

    // ===== State =====
    private int _selectedSlot = 1;
    private bool _armed = false;

    // slot => cooldown remaining (turns)
    private readonly Dictionary<int, int> _cooldownRemaining = new();
    // slot => skip next time tick once (prevents cd immediately dropping to 0)
    private readonly Dictionary<int, int> _cooldownSkipNextTick = new();

    // ===== UI API =====
    public int SelectedSlot => _selectedSlot;
    public bool IsArmed => _armed;

    public event Action<int> OnSelectedSlotChanged;
    public event Action OnCooldownChanged;
    public event Action<bool> OnArmedChanged;

    public int GetCooldownRemaining(int slot)
        => _cooldownRemaining.TryGetValue(slot, out var v) ? Mathf.Max(0, v) : 0;

    public CombatAttackDefinition GetDefinition(int slot)
        => loadout != null ? loadout.GetSlot(slot) : null;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (fieldTimeManager == null) fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (loadout == null) loadout = GetComponent<FieldSkillLoadout>();
        if (turnCoordinator == null) turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();

        for (int i = 1; i <= 4; i++)
        {
            _cooldownRemaining[i] = 0;
            _cooldownSkipNextTick[i] = 0;
        }
    }

    private void OnEnable()
    {
        if (fieldTimeManager == null)
            fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();

        if (fieldTimeManager != null)
            fieldTimeManager.OnTimeAdvanced += HandleTimeAdvanced;
    }

    private void OnDisable()
    {
        if (fieldTimeManager != null)
            fieldTimeManager.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private void HandleTimeAdvanced(int delta, int newTotalTime)
    {
        bool changed = false;

        for (int s = 1; s <= 4; s++)
        {
            if (_cooldownRemaining[s] <= 0) continue;

            if (_cooldownSkipNextTick[s] > 0)
            {
                _cooldownSkipNextTick[s]--;
                continue;
            }

            int before = _cooldownRemaining[s];
            _cooldownRemaining[s] = Mathf.Max(0, _cooldownRemaining[s] - Mathf.Max(1, delta));
            if (before != _cooldownRemaining[s]) changed = true;
        }

        if (changed) OnCooldownChanged?.Invoke();
    }

    private void Update()
    {
        if (gridBoard == null || fieldTimeManager == null || loadout == null) return;
        if (turnCoordinator != null && turnCoordinator.IsBusy) return;

        // 1~4 = 슬롯 선택 + 무장
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectAndArm(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectAndArm(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectAndArm(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectAndArm(4);

        // 우클릭 = 무장 해제(스킬 취소)
        if (Input.GetMouseButtonDown(1))
        {
            Disarm();
            return;
        }

        // 좌클릭 = 무장 상태에서만 시전 시도
        if (Input.GetMouseButtonDown(0))
        {
            if (!_armed) return;

            CombatAttackDefinition def = loadout.GetSlot(_selectedSlot);
            if (def == null)
            {
                Disarm();
                return;
            }

            int cd = GetCooldownRemaining(_selectedSlot);
            if (cd > 0)
            {
                if (log) Debug.Log($"[FieldSkill] Slot{_selectedSlot} cooldown: {cd}");
                return; // 무장은 유지(다음에 다른 스킬 들거나 취소 가능)
            }

            if (!TryGetMouseCell(out Vector2Int cell))
                return;

            // ✅ 타겟팅 스킬은 "셀에 적이 없으면 아무 일도 안 함(취소)"
            if (def.requireEnemyOnTarget)
            {
                if (!TryGetEnemyOnCell(cell, out _))
                {
                    // 아무 일도 안 함: Time 소비 X / 쿨 X / 무장 유지
                    return;
                }
            }

            // 사거리 / LOS 체크 후 시전
            TryCastAtCell(def, cell);
        }
    }

    // ===== Arm/Disarm =====
    public void SelectAndArm(int slot)
    {
        _selectedSlot = Mathf.Clamp(slot, 1, 4);
        OnSelectedSlotChanged?.Invoke(_selectedSlot);

        // 슬롯에 스킬이 없으면 무장하지 않음
        CombatAttackDefinition def = loadout != null ? loadout.GetSlot(_selectedSlot) : null;
        if (def == null)
        {
            SetArmed(false);
            if (log) Debug.Log("[FieldSkill] No skill in slot. Disarmed.");
            return;
        }

        SetArmed(true);
        if (log) Debug.Log($"[FieldSkill] Selected slot {_selectedSlot} (ARMED)");
    }

    public void Disarm()
    {
        if (!_armed) return;
        SetArmed(false);
        if (log) Debug.Log("[FieldSkill] Disarmed (cancel).");
    }

    private void SetArmed(bool armed)
    {
        if (_armed == armed) return;
        _armed = armed;
        OnArmedChanged?.Invoke(_armed);
    }

    // ===== Cast =====
    private void TryCastAtCell(CombatAttackDefinition def, Vector2Int targetCell)
    {
        Vector2Int pCell = gridBoard.WorldToCell(transform.position);

        int dist = FieldCombatUtils.Chebyshev(pCell, targetCell);
        if (dist > def.range)
            return;

        if (requireLoS && !FieldCombatUtils.HasLineOfSight(gridBoard, pCell, targetCell))
            return;

        StartCoroutine(CastRoutine(def, targetCell));
    }

    private IEnumerator CastRoutine(CombatAttackDefinition def, Vector2Int targetCell)
    {
        // 대시 연출(선택)
        Vector3 start = transform.position;
        Vector3 target = gridBoard.CellToWorld(targetCell);
        Vector3 dir = target - start;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dashPos = start + dir * castDashFraction;
            yield return SmoothMove(transform, start, dashPos, castDashDuration);
            yield return SmoothMove(transform, dashPos, start, castReturnDuration);
        }

        // Field 전용 override 처리
        int damage = def.fieldDamageOverride > 0 ? def.fieldDamageOverride : def.damage;
        damage = Mathf.Max(0, damage);

        // 패턴 셀 계산
        List<Vector2Int> cells = GetAffectedCells(def.pattern, targetCell);

        int hit = ApplyDamageToEnemiesInCells(cells, damage);

        if (log) Debug.Log($"[FieldSkill] Cast '{def.displayName}' pattern={def.pattern} hit={hit}");

        // 쿨다운 설정
        int cd = ResolveCooldownTurns(def);
        _cooldownRemaining[_selectedSlot] = Mathf.Max(0, cd);
        if (cd > 0) _cooldownSkipNextTick[_selectedSlot] = 1;
        OnCooldownChanged?.Invoke();

        // Time 1 소비 + 적 턴 처리
        if (turnCoordinator == null)
            turnCoordinator = FieldTurnCoordinator.Instance ?? FindObjectOfType<FieldTurnCoordinator>();

        if (turnCoordinator != null)
        {
            yield return turnCoordinator.CommitPlayerActionAndWait(1, true);
        }
        else
        {
            fieldTimeManager.Advance(1);
            FieldMultiEnemyAttack multiAttack = FindObjectOfType<FieldMultiEnemyAttack>();
            multiAttack?.OnPlayerTurnEnd();
        }

        // 시전 성공 후 자동 무장 해제(추천 UX)
        if (autoDisarmAfterCast)
            Disarm();
    }

    private int ResolveCooldownTurns(CombatAttackDefinition def)
    {
        if (!autoCooldownFromApCost) return defaultCooldownTurnsIfNoCost;

        if (def.apCost >= 3) return 2;
        if (def.apCost == 2) return 1;
        return 0;
    }

    private List<Vector2Int> GetAffectedCells(AttackPattern pattern, Vector2Int center)
    {
        switch (pattern)
        {
            case AttackPattern.CrossPlus:
                return new List<Vector2Int>
                {
                    center,
                    center + Vector2Int.up,
                    center + Vector2Int.down,
                    center + Vector2Int.left,
                    center + Vector2Int.right
                };

            case AttackPattern.CrossX:
                return new List<Vector2Int>
                {
                    center,
                    center + new Vector2Int(1,1),
                    center + new Vector2Int(1,-1),
                    center + new Vector2Int(-1,1),
                    center + new Vector2Int(-1,-1)
                };

            default:
                return new List<Vector2Int> { center };
        }
    }

    private int ApplyDamageToEnemiesInCells(List<Vector2Int> cells, int damage)
    {
        if (cells == null || cells.Count == 0) return 0;

        int hitEnemies = 0;

        // 단일기인데 셀에 적이 여러 명이면? -> 현재는 셀 기반이라 여러 콜라이더가 있으면 여러 명 맞음
        // 원하면 "단일기는 1명만"으로 바꿀 수도 있음.

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 w = gridBoard.CellToWorld(cells[i]);
            Vector2 wp = new Vector2(w.x, w.y);

            Collider2D[] hits2D = Physics2D.OverlapCircleAll(wp, cellOverlapRadius, enemyQueryLayerMask);
            if (hits2D == null) continue;

            for (int h = 0; h < hits2D.Length; h++)
            {
                var inst = hits2D[h].GetComponentInParent<EnemyInstance>();
                if (inst == null || inst.currentHP <= 0) continue;

                // ✅ TakeDamage 사용 (피격 효과 포함)
                inst.TakeDamage(damage);

                hitEnemies++;
            }
        }

        return hitEnemies;
    }

    // ===== Mouse helpers =====
    private bool TryGetMouseCell(out Vector2Int cell)
    {
        cell = default;
        if (worldCamera == null || gridBoard == null) return false;

        Vector3 w = worldCamera.ScreenToWorldPoint(Input.mousePosition);
        w.z = 0f;
        cell = gridBoard.WorldToCell(w);
        return true;
    }

    private bool TryGetEnemyOnCell(Vector2Int cell, out EnemyInstance enemy)
    {
        enemy = null;
        Vector3 w = gridBoard.CellToWorld(cell);
        Vector2 wp = new Vector2(w.x, w.y);

        Collider2D[] hits = Physics2D.OverlapCircleAll(wp, cellOverlapRadius, enemyQueryLayerMask);
        if (hits == null || hits.Length == 0) return false;

        for (int i = 0; i < hits.Length; i++)
        {
            var inst = hits[i].GetComponentInParent<EnemyInstance>();
            if (inst == null || inst.currentHP <= 0) continue;

            enemy = inst;
            return true;
        }
        return false;
    }
    /// <summary>
    /// 기존 UI 호환 + 현재 UX: 슬롯 선택하면 바로 무장(스킬 들기)
    /// </summary>
    public void SelectSlot(int slot)
    {
        SelectAndArm(slot);
    }


    private static IEnumerator SmoothMove(Transform tf, Vector3 from, Vector3 to, float duration)
    {
        if (tf == null) yield break;
        if (duration <= 0f) { tf.position = to; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            tf.position = Vector3.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }
        tf.position = to;
    }
}