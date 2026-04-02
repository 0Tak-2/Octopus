using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 필드에서 색 모듈 스킬 사용
/// 집중전투와 동일한 스킬 효과, 단 AP 대신 1턴 1행동
/// 숫자키로 스킬 선택 → 마우스 클릭으로 대상 지정
/// </summary>
public class FieldColorSkillCaster : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;
    public PlayerStats playerStats;
    public ColorModuleSlots moduleSlots;

    [Header("Targeting")]
    public Camera worldCamera;
    [Tooltip("적 판정에 사용할 레이어")]
    public LayerMask enemyQueryLayerMask = ~0;
    public float cellOverlapRadius = 0.18f;

    [Header("Rules")]
    public bool requireLoS = true;

    [Header("Cooldown")]
    [Tooltip("AP 2 스킬의 필드 쿨다운 (턴)")]
    public int ap2SkillCooldown = 1;

    [Header("Cast Visual")]
    public float castDashDuration = 0.06f;
    public float castReturnDuration = 0.05f;
    public float castDashFraction = 0.55f;

    [Header("Debug")]
    public bool log = true;

    // ===== State =====
    private ColorModuleInstance _selectedSkill;
    private int _selectedSlotIndex = -1;
    private bool _armed = false;
    private bool _isCasting = false;

    // 슬롯 인덱스 => 쿨다운 남은 턴
    private readonly Dictionary<int, int> _cooldownRemaining = new();

    // ===== Events (UI용) =====
    public event Action<int> OnSelectedChanged;
    public event Action OnCooldownChanged;
    public event Action<bool> OnArmedChanged;

    public bool IsArmed => _armed;
    public int SelectedSlotIndex => _selectedSlotIndex;
    public ColorModuleInstance SelectedSkill => _selectedSkill;

    public int GetCooldownRemaining(int slotIndex)
        => _cooldownRemaining.TryGetValue(slotIndex, out var v) ? Mathf.Max(0, v) : 0;

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (fieldTimeManager == null) fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (moduleSlots == null) moduleSlots = ColorModuleSlots.Instance;
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
        var keys = new List<int>(_cooldownRemaining.Keys);
        foreach (int key in keys)
        {
            if (_cooldownRemaining[key] > 0)
            {
                _cooldownRemaining[key] = Mathf.Max(0, _cooldownRemaining[key] - Mathf.Max(1, delta));
                changed = true;
            }
        }
        if (changed) OnCooldownChanged?.Invoke();
    }

    private void Update()
    {
        if (_isCasting) return;
        if (gridBoard == null || moduleSlots == null) return;

        // 집중전투 중이면 필드 스킬 비활성화
        var focusedCombat = FocusedCombatManager.Instance;
        if (focusedCombat != null && focusedCombat.IsInFocusedCombat) return;

        // 숫자키 1~5 = 장착된 스킬 선택
        var skills = moduleSlots.GetEquippedSkills();
        for (int i = 0; i < skills.Count && i < 5; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                SelectSkill(i, skills[i]);
                break;
            }
        }

        // ESC / 우클릭 = 해제
        if (_armed)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            {
                Disarm();
                return;
            }
        }

        // 좌클릭 = 시전
        if (_armed && Input.GetMouseButtonDown(0))
        {
            if (!TryGetMouseCell(out Vector2Int cell))
                return;

            TryCastSkill(cell);
        }
    }

    // ===== Select / Disarm =====

    public void SelectSkill(int index, ColorModuleInstance skill)
    {
        if (skill == null) return;

        // 쿨다운 체크
        if (GetCooldownRemaining(index) > 0)
        {
            if (log) Debug.Log($"[FieldSkill] 쿨다운 중! 남은 턴: {GetCooldownRemaining(index)}");
            return;
        }

        // 같은 스킬 다시 누르면 취소
        if (_armed && _selectedSlotIndex == index)
        {
            Disarm();
            return;
        }

        _selectedSlotIndex = index;
        _selectedSkill = skill;
        SetArmed(true);
        OnSelectedChanged?.Invoke(index);

        if (log) Debug.Log($"[FieldSkill] {skill.Name} 선택! 대상을 클릭하세요.");
    }

    public void Disarm()
    {
        if (!_armed) return;
        _selectedSkill = null;
        _selectedSlotIndex = -1;
        SetArmed(false);
        if (log) Debug.Log("[FieldSkill] 스킬 해제");
    }

    private void SetArmed(bool armed)
    {
        if (_armed == armed) return;
        _armed = armed;
        OnArmedChanged?.Invoke(_armed);
    }

    // ===== Cast =====

    private void TryCastSkill(Vector2Int targetCell)
    {
        if (_selectedSkill == null) return;

        Vector2Int pCell = gridBoard.WorldToCell(transform.position);
        int range = _selectedSkill.definition != null ? _selectedSkill.definition.range : 1;

        // === 늘어나는촉수 (이동기) ===
        if (_selectedSkill.Skill == SkillID.Blue_StretchTentacle)
        {
            if (targetCell == pCell) return;

            int dist = Chebyshev(pCell, targetCell);
            if (dist > 3)
            {
                if (log) Debug.Log("[FieldSkill] 3칸 초과!");
                return;
            }
            if (gridBoard.IsMoveBlocked(targetCell))
            {
                if (log) Debug.Log("[FieldSkill] 이동 불가 지형!");
                return;
            }

            StartCoroutine(FieldMoveSkillRoutine(targetCell));
            return;
        }

        // === 응징하는촉수 (자기 버프) ===
        if (_selectedSkill.Skill == SkillID.Black_PunishingTentacle)
        {
            StartCoroutine(FieldSelfBuffRoutine());
            return;
        }

        // === 공격 스킬 ===
        // 셀에 적이 있는지 확인
        if (!TryGetEnemyOnCell(targetCell, out EnemyInstance targetEnemy))
        {
            if (log) Debug.Log("[FieldSkill] 적이 없는 타일!");
            return;
        }

        int attackDist = Chebyshev(pCell, targetCell);
        if (attackDist > range)
        {
            if (log) Debug.Log($"[FieldSkill] 사거리 밖! 거리: {attackDist}, 사거리: {range}");
            return;
        }

        if (requireLoS && !FieldCombatUtils.HasLineOfSight(gridBoard, pCell, targetCell))
        {
            if (log) Debug.Log("[FieldSkill] 시야 차단!");
            return;
        }

        StartCoroutine(FieldAttackSkillRoutine(targetCell, targetEnemy));
    }

    // ============================================
    // 공격 스킬 실행
    // ============================================
    private IEnumerator FieldAttackSkillRoutine(Vector2Int targetCell, EnemyInstance enemy)
    {
        _isCasting = true;
        ColorModuleInstance skill = _selectedSkill;
        int slotIdx = _selectedSlotIndex;

        // 대시 연출
        Vector3 start = transform.position;
        Vector3 target = gridBoard.CellToWorld(targetCell);
        Vector3 dir = target - start;
        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dashPos = start + dir * castDashFraction;
            yield return SmoothMove(transform, start, dashPos, castDashDuration);
            yield return SmoothMove(transform, dashPos, start, castReturnDuration);
        }

        // 패시브 ATK 배율 계산
        var skillExec = ColorSkillExecutor.Instance;
        float passiveMultiplier = 1f;
        if (skillExec != null)
        {
            int enemyStatusCount = 0; // 필드 적은 StatusEffectManager가 없을 수 있음
            var enemySE = enemy.GetComponent<StatusEffectManager>();
            if (enemySE != null) enemyStatusCount = enemySE.TotalEffectCount;

            int enemyMaxHP = enemy.definition != null ? enemy.definition.maxHP : enemy.currentHP;
            passiveMultiplier = skillExec.CalculatePassiveATKMultiplier(
                playerStats.hp, playerStats.maxHP,
                enemy.currentHP, enemyMaxHP,
                enemyStatusCount
            );
        }

        // 기본 데미지 계산
        int baseDamage = Mathf.RoundToInt(playerStats.ATK * passiveMultiplier);
        int bonusDamage = 0;

        // 적 StatusEffectManager (없으면 추가)
        var enemyStatus = enemy.GetComponent<StatusEffectManager>();
        if (enemyStatus == null)
            enemyStatus = enemy.gameObject.AddComponent<StatusEffectManager>();

        // === 스킬별 효과 ===
        switch (skill.Skill)
        {
            case SkillID.Red_SharpTentacle:
                // 날카로운촉수: 출혈 부여, 이미 출혈이면 ATK 40% 추가
                if (enemyStatus.HasEffect(StatusEffectType.Bleeding))
                    bonusDamage = Mathf.RoundToInt(playerStats.ATK * 0.4f);
                enemy.TakeDamage(baseDamage + bonusDamage);
                enemyStatus.ApplyBleeding();
                if (log) Debug.Log($"[FieldSkill] 날카로운촉수: {baseDamage + bonusDamage} 피해, 출혈 부여");
                break;

            case SkillID.Red_PiercingTentacle:
                // 파고드는촉수: 출혈 부여, 이미 출혈이면 50% 회복
                bool wasBleeding = enemyStatus.HasEffect(StatusEffectType.Bleeding);
                int dmg = baseDamage;
                enemy.TakeDamage(dmg);
                if (wasBleeding && dmg > 0)
                {
                    int heal = Mathf.RoundToInt(dmg * 0.5f);
                    playerStats.Heal(heal);
                    if (log) Debug.Log($"[FieldSkill] 파고드는촉수: {heal} 회복!");
                }
                enemyStatus.ApplyBleeding();
                if (log) Debug.Log($"[FieldSkill] 파고드는촉수: {dmg} 피해, 출혈 부여");
                break;

            case SkillID.Blue_HardTentacle:
                // 단단한촉수: 불완전 부여
                enemy.TakeDamage(baseDamage);
                enemyStatus.ApplyImperfect(1);
                int stacks = enemyStatus.GetImperfectStacks();
                if (log) Debug.Log($"[FieldSkill] 단단한촉수: {baseDamage} 피해, 불완전 {stacks}중첩");
                break;

            case SkillID.Black_DeadlyTentacle:
                // 치명적인촉수: 첫 공격 확정 치명타
                bool isFirst = !skill.firstStrikeUsedOn.Contains(enemy.transform);
                int critDmg = baseDamage;
                if (isFirst)
                {
                    critDmg = Mathf.RoundToInt(critDmg * playerStats.CRIT_DMG);
                    skill.firstStrikeUsedOn.Add(enemy.transform);
                    if (log) Debug.Log($"[FieldSkill] 치명적인촉수: 확정 치명타! {critDmg} 피해");
                }
                else
                {
                    if (log) Debug.Log($"[FieldSkill] 치명적인촉수: {critDmg} 피해");
                }
                enemy.TakeDamage(critDmg);
                break;

            default:
                enemy.TakeDamage(baseDamage);
                if (log) Debug.Log($"[FieldSkill] {skill.Name}: {baseDamage} 피해");
                break;
        }

        // === 패시브 후처리 ===
        // 포식: 적 HP 20% 이하 처형
        if (skillExec != null && enemy.currentHP > 0)
        {
            int enemyMaxHP = enemy.definition != null ? enemy.definition.maxHP : enemy.currentHP;
            if (skillExec.CheckPredation(enemy.currentHP, enemyMaxHP, out int healAmt))
            {
                int remaining = enemy.currentHP;
                enemy.TakeDamage(remaining);
                playerStats.Heal(remaining);
                if (log) Debug.Log($"[FieldSkill] 포식 처형! {remaining} 회복!");
            }
        }

        // 촉수강화: 25% 확률 스턴
        if (skillExec != null && skillExec.CheckStunOnHit() && enemy.currentHP > 0)
        {
            enemyStatus.TryApplyStun(transform);
            if (log) Debug.Log("[FieldSkill] 촉수강화: 스턴!");
        }

        // 기본기: 25% 확률 2회 공격
        if (skillExec != null && skillExec.CheckDoubleAttack() && enemy.currentHP > 0)
        {
            int extraDmg = Mathf.RoundToInt(playerStats.ATK * passiveMultiplier);
            enemy.TakeDamage(extraDmg);
            if (log) Debug.Log($"[FieldSkill] 기본기 2회 공격! 추가 {extraDmg} 피해");

            // 추가 공격에도 촉수강화 체크
            if (skillExec.CheckStunOnHit() && enemy.currentHP > 0)
            {
                enemyStatus.TryApplyStun(transform);
                if (log) Debug.Log("[FieldSkill] 기본기+촉수강화: 추가 스턴!");
            }
        }

        // 쿨다운 (AP 비용만큼 턴 쿨다운)
        if (skill.APCost >= 2)
        {
            _cooldownRemaining[slotIdx] = skill.APCost;
            OnCooldownChanged?.Invoke();
        }

        // 턴 소비 + 적 반격
        FinishFieldAction();

        Disarm();
        _isCasting = false;
    }

    // ============================================
    // 이동 스킬 (늘어나는촉수)
    // ============================================
    private IEnumerator FieldMoveSkillRoutine(Vector2Int targetCell)
    {
        _isCasting = true;
        int slotIdx = _selectedSlotIndex;

        // 이동
        Vector3 targetPos = gridBoard.CellToWorld(targetCell);
        yield return SmoothMove(transform, transform.position, targetPos, 0.15f);
        transform.position = targetPos;

        // GridOccupancy 갱신
        var occupancy = GridOccupancyRegistry.Instance;
        if (occupancy != null)
        {
            occupancy.Release(transform);
            occupancy.TryOccupy(transform, targetCell);
        }

        // PlayerGridMover 동기화
        var mover = GetComponent<PlayerGridMover>();
        if (mover != null)
            mover.SetCurrentCell(targetCell);

        // 쿨다운 (AP 비용만큼)
        if (_selectedSkill != null && _selectedSkill.APCost >= 2)
        {
            _cooldownRemaining[slotIdx] = _selectedSkill.APCost;
            OnCooldownChanged?.Invoke();
        }

        if (log) Debug.Log($"[FieldSkill] 늘어나는촉수: {targetCell}로 이동!");

        // 턴 소비 + 적 반격
        FinishFieldAction();

        Disarm();
        _isCasting = false;
    }

    // ============================================
    // 자기 버프 (응징하는촉수)
    // ============================================
    private IEnumerator FieldSelfBuffRoutine()
    {
        _isCasting = true;
        int slotIdx = _selectedSlotIndex;

        _selectedSkill.punishingBuffNextAttack = 1;
        if (log) Debug.Log("[FieldSkill] 응징하는촉수: 다음 공격 시 1.75배!");

        // 쿨다운 (AP 비용만큼)
        if (_selectedSkill != null && _selectedSkill.APCost >= 2)
        {
            _cooldownRemaining[slotIdx] = _selectedSkill.APCost;
            OnCooldownChanged?.Invoke();
        }

        yield return new WaitForSeconds(0.2f);

        // 턴 소비 + 적 반격
        FinishFieldAction();

        Disarm();
        _isCasting = false;
    }

    // ============================================
    // 공통: 턴 소비 + 적 반격
    // ============================================
    private void FinishFieldAction()
    {
        // Time 1 소비
        if (fieldTimeManager != null)
            fieldTimeManager.Advance(1);

        // 적들 반격
        FieldMultiEnemyAttack multiAttack = FindObjectOfType<FieldMultiEnemyAttack>();
        if (multiAttack != null)
            multiAttack.OnPlayerTurnEnd();
    }

    // ============================================
    // 유틸
    // ============================================
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

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
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