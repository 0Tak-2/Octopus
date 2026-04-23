using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 입장 직전의 필드 적 위치를 "얼음땡" 처럼 잠가 두었다가,
/// 필드 복귀 시 동일한 셀/월드 좌표로 되돌려 놓는다.
///
/// 설계 방침:
/// - "진짜" 위치는 <see cref="GridOccupancyRegistry"/> 의 셀이다. transform.position 은
///   Lerp/Animator/Late-snap 등 여러 곳에서 덮어써질 수 있으므로, 스냅샷을 뜰 때는
///   점유 셀을 우선적으로 사용한다.
/// - 페이드 아웃(0.25s) 중에 적이 움직이거나 위치가 리셋되는 경쟁 상태를 피하기 위해,
///   플레이어가 던전 입장을 확정한 시점(<see cref="EarlyCapture"/>) 에 스냅샷을 뜬다.
///   이후 실제 <see cref="ModeManager.EnterDungeonCore"/> 에서 <see cref="Capture"/> 가
///   호출되면 이미 준비된 스냅샷을 그대로 사용한다(중복 촬영 방지).
/// - 계층 분리(부모에서 떼어내기) + <c>SetActive(false)</c> 는 <see cref="EarlyCapture"/>
///   에서 곧바로 수행한다. 페이드 아웃이 진행되는 ~250ms 동안 다른 시스템이
///   <c>transform.position</c> 을 스폰 좌표로 덮어써도 GameObject 가 비활성이라
///   화면에는 순간이동이 절대 보이지 않는다.
/// </summary>
public static class FieldFreezeController
{
    private class FrozenEnemy
    {
        public EnemyInstance enemy;
        public int instanceId;
        public string name;
        public Transform originalParent;
        public Vector2Int cell;          // 점유 셀(=논리 위치). 복원 시 이 값이 우선.
        public Vector3 worldPosition;    // 참고용 월드 좌표(회전 복원/애니메이션용).
        public Quaternion worldRotation;
        public bool wasActive;
        public int currentHP;
        public bool detached;            // Capture 단계에서 anchor 아래로 옮겼는지.
    }

    private static readonly List<FrozenEnemy> _snapshot = new List<FrozenEnemy>();
    private static bool _hasSnapshot;
    private static Transform _freezeAnchor;

    public static bool verboseLogs = true;

    /// <summary>
    /// 필드 AI 가 스냅샷 이후에 추가로 이동하지 못하도록 잠그는 글로벌 플래그.
    /// 적 AI 스크립트 ( <see cref="FieldEnemyWanderChase"/> 등 ) 가 각자 체크한다.
    /// </summary>
    public static bool IsLocked { get; private set; }

    private static Transform EnsureFreezeAnchor()
    {
        if (_freezeAnchor != null) return _freezeAnchor;
        var go = new GameObject("[FieldFreezeAnchor]");
        Object.DontDestroyOnLoad(go);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _freezeAnchor = go.transform;
        return _freezeAnchor;
    }

    /// <summary>
    /// 플레이어가 던전 입장을 확정한 바로 그 프레임에 호출한다.
    ///
    /// 여기서 "완전 얼음땡"을 끝낸다:
    ///   1) 점유 셀을 진짜 값으로 확정 (스냅샷).
    ///   2) 바로 anchor 로 detach + <c>SetActive(false)</c> 까지 수행 →
    ///      이후 페이드 아웃 도중에 다른 스크립트가 transform.position 을 스폰으로
    ///      되돌려도 GameObject 가 이미 비활성이라 화면에 "순간이동" 이 보이지 않는다.
    ///   3) 글로벌 AI 락( <see cref="IsLocked"/> ) 을 건다.
    /// </summary>
    public static void EarlyCapture(GameObject fieldRoot, GameObject dungeonRoot = null)
    {
        if (_hasSnapshot)
        {
            IsLocked = true;
            return;
        }

        _snapshot.Clear();

        GridBoard grid = null;
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            grid = FieldMapGenerator.ResolveGameplayGrid(ModeManager.Instance.fieldRoot);
        if (grid == null && fieldRoot != null)
            grid = FieldMapGenerator.ResolveGameplayGrid(fieldRoot);

        var anchor = EnsureFreezeAnchor();
        var occ = GridOccupancyRegistry.Instance;
        var enemies = Object.FindObjectsOfType<EnemyInstance>(true);

        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (dungeonRoot != null && e.transform.IsChildOf(dungeonRoot.transform))
                continue;

            Vector2Int cell;
            if (occ == null || !occ.TryGetCurrentCell(e.transform, out cell))
            {
                cell = grid != null
                    ? grid.WorldToCell(e.transform.position)
                    : Vector2Int.zero;
            }

            Vector3 worldPos = grid != null ? grid.CellToWorld(cell) : e.transform.position;

            var snap = new FrozenEnemy
            {
                enemy = e,
                instanceId = e.gameObject.GetInstanceID(),
                name = e.gameObject.name,
                originalParent = e.transform.parent,
                cell = cell,
                worldPosition = worldPos,
                worldRotation = e.transform.rotation,
                wasActive = e.gameObject.activeSelf && e.currentHP > 0,
                currentHP = e.currentHP,
                detached = false,
            };

            // 살아있는 개체만 즉시 하드-프리즈. (이미 죽어있으면 나중에 Apply 가 비활성 처리)
            if (snap.currentHP > 0 && snap.wasActive)
            {
                // occupancy 에서도 해제: 던전 동안 점유 셀이 남아 있으면
                // 다른 시스템이 "이 셀 점유됨" 이라고 오인할 수 있다.
                if (occ != null)
                    occ.Release(e.transform);

                e.transform.SetParent(anchor, true);
                snap.detached = true;
                e.gameObject.SetActive(false);
            }

            _snapshot.Add(snap);
        }

        _hasSnapshot = true;
        IsLocked = true;

        if (verboseLogs)
        {
            Debug.Log($"[FieldFreeze] EarlyCapture: 필드 적 {_snapshot.Count}마리 하드-프리즈 (씬 전체={enemies.Length}). 이후 AI/렌더 전부 정지.");
            LogFieldGridInfo("EarlyCapture");
            foreach (var f in _snapshot)
            {
                if (f.enemy == null) continue;
                Debug.Log($"[FieldFreeze]   EarlyCapture '{f.name}' cell={f.cell} world={f.worldPosition} hp={f.currentHP} detached={f.detached}");
            }
        }
    }

    /// <summary>
    /// <see cref="ModeManager.EnterDungeonCore"/> 시점에 호출.
    /// 실제 얼음땡 처리는 <see cref="EarlyCapture"/> 가 이미 끝냈으므로 이 메서드는
    /// 폴백(EarlyCapture 가 호출되지 못한 예외 경로) 용도만 남겨둔다.
    /// </summary>
    public static void Capture(GameObject fieldRoot, GameObject dungeonRoot = null)
    {
        if (!_hasSnapshot)
        {
            EarlyCapture(fieldRoot, dungeonRoot);
            return;
        }

        // 스냅샷은 이미 있지만 아직 detach 안 된 항목이 있을 수 있으므로 보정.
        var anchor = EnsureFreezeAnchor();
        var occ = GridOccupancyRegistry.Instance;
        int newlyDetached = 0;
        foreach (var f in _snapshot)
        {
            if (f.enemy == null) continue;
            if (f.currentHP <= 0)
            {
                if (f.enemy.gameObject.activeSelf)
                    f.enemy.gameObject.SetActive(false);
                continue;
            }
            if (!f.detached)
            {
                if (occ != null) occ.Release(f.enemy.transform);
                f.enemy.transform.SetParent(anchor, true);
                f.detached = true;
                newlyDetached++;
            }
            if (f.enemy.gameObject.activeSelf)
                f.enemy.gameObject.SetActive(false);
        }

        IsLocked = true;

        if (verboseLogs)
            Debug.Log($"[FieldFreeze] Capture: 스냅샷 재사용 (추가 detach {newlyDetached}마리).");
    }

    public static void Apply(GameObject fieldRoot, GridBoard fieldGrid)
    {
        if (!_hasSnapshot)
        {
            Debug.LogWarning("[FieldFreeze] Apply: 스냅샷 없음 — 던전 입장 경로에서 Capture 가 호출되지 않았습니다.");
            IsLocked = false;
            return;
        }

        var occ = GridOccupancyRegistry.Instance;
        int matched = 0;
        var applied = new List<(EnemyInstance e, Vector3 target)>();

        if (verboseLogs)
            LogFieldGridInfo("Apply");

        foreach (var f in _snapshot)
        {
            var enemy = f.enemy;
            if (enemy == null) continue;

            matched++;

            if (f.currentHP <= 0)
            {
                enemy.gameObject.SetActive(false);
                continue;
            }

            Vector3 before = enemy.transform.position;

            // 1) 원래 부모로 복귀.
            if (f.originalParent != null && enemy.transform.parent != f.originalParent)
                enemy.transform.SetParent(f.originalParent, true);

            // 2) 하드 프리즈 해제.
            if (!enemy.gameObject.activeSelf && f.wasActive)
                enemy.gameObject.SetActive(true);

            // 3) 셀 → 월드 변환을 현재 필드 그리드 기준으로 다시 계산해서 적용.
            //    (이렇게 하면 그리드 원점이 달라져 있어도 논리 셀은 유지된다.)
            Vector3 targetWorld = fieldGrid != null ? fieldGrid.CellToWorld(f.cell) : f.worldPosition;
            enemy.transform.SetPositionAndRotation(targetWorld, f.worldRotation);
            enemy.currentHP = f.currentHP;
            enemy.Clamp();

            if (occ != null && enemy.gameObject.activeInHierarchy)
            {
                occ.Release(enemy.transform);
                occ.TryOccupy(enemy.transform, f.cell);
            }

            applied.Add((enemy, targetWorld));

            if (verboseLogs)
                Debug.Log($"[FieldFreeze]   Apply '{f.name}': before={before} -> cell={f.cell} world={targetWorld} parent={(f.originalParent != null ? f.originalParent.name : "<null>")}");
        }

        if (verboseLogs)
            Debug.Log($"[FieldFreeze] Apply: 매칭 {matched}/{_snapshot.Count}.");

        var runner = FieldFreezeWatchdog.Ensure();
        runner.StartWatch(applied, 4);

        _snapshot.Clear();
        _hasSnapshot = false;
        IsLocked = false;
    }

    public static void Clear()
    {
        _snapshot.Clear();
        _hasSnapshot = false;
        IsLocked = false;
    }

    public static bool HasSnapshot => _hasSnapshot;

    private static void LogFieldGridInfo(string tag)
    {
        GridBoard gb = null;
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            gb = FieldMapGenerator.ResolveGameplayGrid(ModeManager.Instance.fieldRoot);
        if (gb == null) return;
        var g = gb.GetComponent<Grid>();
        Vector3 c00 = g != null ? g.GetCellCenterWorld(Vector3Int.zero) : gb.CellToWorld(Vector2Int.zero);
        Debug.Log($"[FieldFreeze][{tag}] grid '{gb.name}' tPos={gb.transform.position} origin={gb.origin} cellSize={gb.cellSize:F3} cellCenter(0,0)={c00}");
    }
}
