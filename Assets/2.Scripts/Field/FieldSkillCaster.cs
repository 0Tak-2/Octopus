using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class FieldSkillCaster : MonoBehaviour
{
    [Header("Refs")]
    public GridBoard gridBoard;
    public FieldTimeManager fieldTimeManager;
    public FieldSkillLoadout loadout;

    [Header("Targeting")]
    public Camera worldCamera;
    public LayerMask enemyClickLayerMask = ~0;
    public bool requireLoS = true;

    [Header("Cooldown (Turn-based via Time)")]
    [Tooltip("AP 2 => 1턴 쿨, AP 3 => 2턴 쿨 같은 식으로 자동 계산. (Definition에서 AP cost를 찾으면 사용)")]
    public bool autoCooldownFromAPCost = true;
    public int defaultCooldownTurnsIfNoCost = 1;

    [Header("Cast Visual (optional)")]
    public float castDashDuration = 0.06f;
    public float castReturnDuration = 0.05f;
    public float castDashFraction = 0.55f;

    [Header("Debug")]
    public bool log = true;

    private int _selectedSlot = 1;

    // slotIndex(1~4) => remaining turns
    private readonly Dictionary<int, int> _cooldownRemaining = new Dictionary<int, int>();

    // ✅ 핵심: "쿨다운을 막 설정했을 때" 바로 들어오는 TimeAdvanced 1회를 스킵하기 위한 카운터
    // (slotIndex => skip ticks)
    private readonly Dictionary<int, int> _cooldownSkipNextTick = new Dictionary<int, int>();

    private int _lastSeenTime = -1;

    // === UI/외부 접근용 ===
    public int SelectedSlot => _selectedSlot;

    public int GetCooldownRemaining(int slotIndex1Based)
    {
        if (!_cooldownRemaining.TryGetValue(slotIndex1Based, out int v)) return 0;
        return Mathf.Max(0, v);
    }

    public ScriptableObject GetDefinition(int slotIndex1Based)
    {
        if (loadout == null) return null;
        return loadout.GetSlot(slotIndex1Based);
    }

    public event System.Action<int> OnSelectedSlotChanged;
    public event System.Action OnCooldownChanged;

    private void NotifySelectedChanged() => OnSelectedSlotChanged?.Invoke(_selectedSlot);
    private void NotifyCooldownChanged() => OnCooldownChanged?.Invoke();

    private void Awake()
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (fieldTimeManager == null) fieldTimeManager = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (loadout == null) loadout = GetComponent<FieldSkillLoadout>();

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
        {
            _lastSeenTime = fieldTimeManager.time;
            fieldTimeManager.OnTimeAdvanced += HandleTimeAdvanced;
        }
    }

    private void OnDisable()
    {
        if (fieldTimeManager != null)
            fieldTimeManager.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private void HandleTimeAdvanced(int delta, int newTotalTime)
    {
        if (_lastSeenTime < 0) _lastSeenTime = newTotalTime - delta;

        int steps = Mathf.Max(0, newTotalTime - _lastSeenTime);
        _lastSeenTime = newTotalTime;

        if (steps <= 0) return;

        bool changed = false;

        // ✅ 각 슬롯별로: "스킵해야 하는 tick"을 먼저 소비한 뒤, 남은 steps만큼 쿨다운 감소
        for (int s = 1; s <= 4; s++)
        {
            if (_cooldownRemaining[s] <= 0) continue;

            int skip = _cooldownSkipNextTick.TryGetValue(s, out int v) ? v : 0;

            // delta가 1일 거라 보통 1번만 스킵하면 되지만,
            // 혹시 steps가 2 이상일 때도 안전하게 처리
            int consumeSkip = Mathf.Min(skip, steps);
            if (consumeSkip > 0)
            {
                skip -= consumeSkip;
                steps -= consumeSkip; // ✅ 그만큼은 쿨다운 감소에 사용하지 않음
                _cooldownSkipNextTick[s] = skip;
            }

            if (steps <= 0) continue;

            int before = _cooldownRemaining[s];
            _cooldownRemaining[s] = Mathf.Max(0, _cooldownRemaining[s] - steps);
            if (_cooldownRemaining[s] != before) changed = true;
        }

        if (changed) NotifyCooldownChanged();
    }

    private void Update()
    {
        if (loadout == null || gridBoard == null || fieldTimeManager == null) return;

        // 1~4로 선택
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectSlot(1);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectSlot(2);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectSlot(3);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectSlot(4);

        // 클릭 시전
        if (Input.GetMouseButtonDown(0))
        {
            var def = loadout.GetSlot(_selectedSlot);
            if (def == null) return;

            if (_cooldownRemaining[_selectedSlot] > 0)
            {
                if (log) Debug.Log($"[FieldSkill] Slot{_selectedSlot} on cooldown: {_cooldownRemaining[_selectedSlot]} turn(s) left");
                return;
            }

            if (!TryGetClickedEnemy(out Transform enemyTf))
                return;

            TryCast(def, enemyTf);
        }
    }

    // ✅ UI 클릭에서도 쓰게 public으로 열어둠(원치 않으면 다시 private로)
    public void SelectSlot(int slot)
    {
        _selectedSlot = Mathf.Clamp(slot, 1, 4);
        if (log) Debug.Log($"[FieldSkill] Selected slot {_selectedSlot}");
        NotifySelectedChanged();
    }

    private bool TryCast(ScriptableObject def, Transform enemyTf)
    {
        int range = DefReadInt(def, new[] { "range", "castRange", "attackRange" }, fallback: 1);
        int damage = DefReadInt(def, new[] { "damage", "baseDamage", "power" }, fallback: 1);
        string skillName = DefReadString(def, new[] { "displayName", "skillName", "name" }, fallback: def.name);

        Vector2Int pCell = gridBoard.WorldToCell(transform.position);
        Vector2Int eCell = gridBoard.WorldToCell(enemyTf.position);

        int dist = FieldCombatUtils.Chebyshev(pCell, eCell);
        if (dist > range)
        {
            if (log) Debug.Log($"[FieldSkill] '{skillName}' out of range. dist={dist}, range={range}");
            return false;
        }

        if (requireLoS && !FieldCombatUtils.HasLineOfSight(gridBoard, pCell, eCell))
        {
            if (log) Debug.Log($"[FieldSkill] '{skillName}' blocked by LOS.");
            return false;
        }

        var enemyInst = enemyTf.GetComponent<EnemyInstance>();
        if (enemyInst == null)
        {
            if (log) Debug.LogWarning("[FieldSkill] EnemyInstance not found on clicked enemy.");
            return false;
        }

        StartCoroutine(CastRoutine(skillName, enemyTf, enemyInst, damage, def));
        return true;
    }

    private IEnumerator CastRoutine(string skillName, Transform enemyTf, EnemyInstance enemyInst, int damage, ScriptableObject def)
    {
        Vector3 start = transform.position;
        Vector3 target = enemyTf.position;
        Vector3 dir = target - start;

        if (dir.sqrMagnitude > 0.0001f)
        {
            Vector3 dashPos = start + dir * castDashFraction;
            yield return SmoothMove(transform, start, dashPos, castDashDuration);
            yield return SmoothMove(transform, dashPos, start, castReturnDuration);
        }

        // 데미지 적용
        enemyInst.currentHP -= Mathf.Max(0, damage);
        enemyInst.Clamp();

        if (log) Debug.Log($"[FieldSkill] Cast '{skillName}' => damage {damage} (enemyHP={enemyInst.currentHP})");

        // ✅ 쿨다운 설정 + "다음 TimeAdvanced 1회는 감소 스킵" 예약
        int cd = ResolveCooldownTurns(def);
        _cooldownRemaining[_selectedSlot] = Mathf.Max(0, cd);

        if (cd > 0)
        {
            _cooldownSkipNextTick[_selectedSlot] = 1; // ✅ 바로 들어오는 +1(time)에서 깎이지 않게
        }

        NotifyCooldownChanged();

        // 턴(Time) 1 소비
        fieldTimeManager.Advance(1);

        // 사망 처리(간단 버전)
        if (enemyInst.currentHP <= 0)
            enemyTf.gameObject.SetActive(false);
    }

    private int ResolveCooldownTurns(ScriptableObject def)
    {
        if (!autoCooldownFromAPCost)
            return defaultCooldownTurnsIfNoCost;

        int apCost = DefReadInt(def, new[] { "apCost", "costAP", "cost", "ap" }, fallback: -1);
        if (apCost <= 0) return defaultCooldownTurnsIfNoCost;

        // 네 규칙대로:
        // 2AP => 다음 내 턴 1번 동안 사용 불가 => cd=1
        // 3AP => 내 턴 2번 지나야 => cd=2
        if (apCost >= 3) return 2;
        if (apCost == 2) return 1;
        return 0;
    }

    private bool TryGetClickedEnemy(out Transform hitTransform)
    {
        hitTransform = null;
        if (worldCamera == null) return false;

        Vector3 mouse = Input.mousePosition;

        // 2D
        Vector3 world = worldCamera.ScreenToWorldPoint(mouse);
        Vector2 world2 = new Vector2(world.x, world.y);
        RaycastHit2D hit2D = Physics2D.Raycast(world2, Vector2.zero, 0f, enemyClickLayerMask);
        if (hit2D.collider != null)
        {
            hitTransform = hit2D.collider.transform;
            return true;
        }

        // 3D fallback
        Ray ray = worldCamera.ScreenPointToRay(mouse);
        if (Physics.Raycast(ray, out RaycastHit hit3D, 500f, enemyClickLayerMask))
        {
            hitTransform = hit3D.collider.transform;
            return true;
        }

        return false;
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

    // ---------------------------
    // Definition reflection helpers
    // ---------------------------
    private static int DefReadInt(ScriptableObject def, string[] fieldOrPropNames, int fallback)
    {
        if (def == null) return fallback;
        Type t = def.GetType();

        foreach (var n in fieldOrPropNames)
        {
            FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(float)))
            {
                object v = f.GetValue(def);
                if (v is int i) return i;
                if (v is float fl) return Mathf.RoundToInt(fl);
            }

            PropertyInfo p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && (p.PropertyType == typeof(int) || p.PropertyType == typeof(float)))
            {
                object v = p.GetValue(def);
                if (v is int i2) return i2;
                if (v is float fl2) return Mathf.RoundToInt(fl2);
            }
        }

        return fallback;
    }

    private static string DefReadString(ScriptableObject def, string[] fieldOrPropNames, string fallback)
    {
        if (def == null) return fallback;
        Type t = def.GetType();

        foreach (var n in fieldOrPropNames)
        {
            FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
            {
                var v = f.GetValue(def) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }

            PropertyInfo p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string))
            {
                var v = p.GetValue(def) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }

        return fallback;
    }
}
