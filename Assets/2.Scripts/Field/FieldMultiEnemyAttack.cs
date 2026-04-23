using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 턴 종료 후 주변 모든 적이 동시에 공격하는 시스템
/// </summary>
public class FieldMultiEnemyAttack : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GridBoard gridBoard;
    public PlayerStats playerStats;
    public GridOccupancyRegistry occupancy;
    public HitEffectManager hitEffectManager;

    [Header("Detection")]
    [Tooltip("이 거리 이내의 적들이 공격 시도")]
    public int detectionRange = 8;
    [Tooltip("원거리 공격(attackRange>=2)에 LOS를 강제할지 여부. 기본값 OFF(기존 투사체 동작 유지)")]
    public bool requireLineOfSightForRanged = false;

    [Header("Visual")]
    [Tooltip("각 적의 공격 사이 딜레이")]
    public float attackDelay = 0.3f;
    public float dashDuration = 0.15f;
    [Tooltip("EnemyDefinition.projectilePrefab가 비어있을 때 사용할 임시 발사체 속도")]
    public float fallbackProjectileSpeed = 9f;
    public Color fallbackProjectileColor = new Color(0.95f, 0.75f, 0.25f, 1f);
    public float fallbackProjectileScale = 0.22f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool _isProcessing = false;
    public bool IsResolvingEnemyTurn => _isProcessing;
    private static Sprite _fallbackProjectileSprite;

    private void Awake()
    {
        if (player == null) player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (gridBoard == null) gridBoard = FindObjectOfType<GridBoard>();
        if (playerStats == null) playerStats = FindObjectOfType<PlayerStats>();
        if (occupancy == null) occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();
        if (hitEffectManager == null) hitEffectManager = HitEffectManager.Instance ?? FindObjectOfType<HitEffectManager>();
    }

    /// <summary>
    /// 플레이어 턴 종료 시 호출 - 주변 모든 적이 공격
    /// </summary>
    public void OnPlayerTurnEnd()
    {
        if (_isProcessing)
        {
            if (showDebugLogs)
                Debug.Log("[MultiEnemyAttack] Already processing, skip");
            return;
        }

        StartCoroutine(ProcessAllEnemyAttacks());
    }

    private IEnumerator ProcessAllEnemyAttacks()
    {
        EnsureRuntimeReferences();
        _isProcessing = true;

        // ✅ 적 이동 완료 대기 (StepMove 코루틴 완료)
        yield return new WaitForSeconds(0.1f);

        // ✅ 모든 적의 transform.position을 occupancy 기준으로 동기화
        SyncAllEnemyPositions();

        // 1. 주변 적들 찾기
        List<EnemyInstance> nearbyEnemies = FindNearbyEnemies();

        if (showDebugLogs)
            Debug.Log($"[MultiEnemyAttack] Found {nearbyEnemies.Count} enemies nearby (grid={(gridBoard != null ? gridBoard.name : "NULL")}, detectionRange={detectionRange})");

        // 적이 없으면 바로 종료
        if (nearbyEnemies.Count == 0)
        {
            _isProcessing = false;
            yield break;
        }

        // 2. 각 적마다 공격 판정
        foreach (var enemy in nearbyEnemies)
        {
            if (enemy == null || enemy.currentHP <= 0)
                continue;

            yield return ProcessSingleEnemyAttack(enemy);

            // 플레이어가 죽었으면 중단
            if (playerStats != null && playerStats.hp <= 0)
                break;
        }

        _isProcessing = false;
    }

    /// <summary>
    /// 주변 적 찾기
    /// </summary>
    private List<EnemyInstance> FindNearbyEnemies()
    {
        List<EnemyInstance> result = new List<EnemyInstance>();

        if (player == null || gridBoard == null)
            return result;

        Vector2Int playerCell;
        var occ = GridOccupancyRegistry.Instance;
        if (occ != null && occ.TryGetCurrentCell(player, out var pOccCell))
            playerCell = pOccCell;
        else
            playerCell = gridBoard.WorldToCell(player.position);

        // 모든 적 찾기 (필드 루트 우선 — 던전/비활성 루트 혼입 방지)
        EnemyInstance[] allEnemies;
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            allEnemies = ModeManager.Instance.fieldRoot.GetComponentsInChildren<EnemyInstance>(true);
        else
            allEnemies = FindObjectsOfType<EnemyInstance>(true);

        foreach (var enemy in allEnemies)
        {
            if (enemy.currentHP <= 0)
                continue;

            Vector2Int enemyCell;
            if (occ != null && occ.TryGetCurrentCell(enemy.transform, out var eOccCell))
                enemyCell = eOccCell;
            else
                enemyCell = gridBoard.WorldToCell(enemy.transform.position);
            int distance = Chebyshev(playerCell, enemyCell);
            int enemyAttackRange = (enemy != null && enemy.definition != null) ? Mathf.Max(1, enemy.definition.attackRange) : 1;
            int effectiveDetection = Mathf.Max(detectionRange, enemyAttackRange);

            if (distance <= effectiveDetection)
            {
                result.Add(enemy);
            }
            else if (showDebugLogs && enemy != null && enemy.definition != null)
            {
                Debug.Log($"[MultiEnemyAttack] Filter out {enemy.definition.displayName}: dist={distance}, detect={detectionRange}, enemyRange={enemyAttackRange}, effective={effectiveDetection}");
            }
        }

        return result;
    }

    private void EnsureRuntimeReferences()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (playerStats == null)
            playerStats = FindObjectOfType<PlayerStats>();

        if (occupancy == null)
            occupancy = GridOccupancyRegistry.Instance ?? FindObjectOfType<GridOccupancyRegistry>();

        GridBoard resolved = null;
        if (ModeManager.Instance != null && ModeManager.Instance.fieldRoot != null)
            resolved = FieldMapGenerator.ResolveGameplayGrid(ModeManager.Instance.fieldRoot);

        if (resolved == null)
        {
            var mover = player != null ? player.GetComponent<PlayerGridMover>() : null;
            if (mover != null && mover.grid != null)
                resolved = mover.grid;
        }

        if (resolved == null)
        {
            var boards = FindObjectsOfType<GridBoard>();
            for (int i = 0; i < boards.Length; i++)
            {
                if (boards[i] != null && boards[i].gameObject.activeInHierarchy)
                {
                    resolved = boards[i];
                    break;
                }
            }
        }

        if (resolved != null)
            gridBoard = resolved;
    }

    /// <summary>
    /// 한 적의 공격 처리
    /// </summary>
    private IEnumerator ProcessSingleEnemyAttack(EnemyInstance enemy)
    {
        if (enemy == null || enemy.definition == null)
            yield break;

        // ✅ 이미 행동했으면 스킵
        if (enemy.hasActedThisTurn)
        {
            if (showDebugLogs)
                Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} already acted this turn. Skip.");
            yield break;
        }

        // ✅ Occupancy 기준 셀 사용 (transform.position과 불일치 방지)
        Vector2Int enemyCell;
        var occ = GridOccupancyRegistry.Instance;
        if (occ != null && occ.TryGetCurrentCell(enemy.transform, out var occCell))
            enemyCell = occCell;
        else
            enemyCell = gridBoard.WorldToCell(enemy.transform.position);

        // 공격 전 위치가 크게 어긋난 경우에만 보정 (연출 끊김 방지)
        SnapRootIfDrifted(enemy.transform, enemyCell);

        Vector2Int playerCell;
        if (occ != null && occ.TryGetCurrentCell(player, out var pOccCell))
            playerCell = pOccCell;
        else
            playerCell = gridBoard.WorldToCell(player.position);

        int distance = Chebyshev(enemyCell, playerCell);
        int attackRange = enemy.definition.attackRange;

        // 공격 범위 안인지 체크
        if (distance > attackRange)
        {
            if (showDebugLogs)
                Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} too far ({distance} > {attackRange})");
            yield break;
        }

        // 원거리 LOS는 옵션. (기본 OFF: 과거처럼 사거리만 맞으면 발사)
        if (requireLineOfSightForRanged && attackRange >= 2)
        {
            bool hasLoS = FieldCombatUtils.HasLineOfSight(gridBoard, enemyCell, playerCell);
            if (!hasLoS)
            {
                if (showDebugLogs)
                    Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} no line of sight! (LOS required ON)");
                yield break;
            }
        }

        // 공격!
        int damage = enemy.definition.attackDamage;

        if (showDebugLogs)
            Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} attacks! Distance={distance}, Damage={damage}, Range={attackRange}");

        // 공격 애니메이션
        if (attackRange == 1)
        {
            // 근접: 대시 공격
            yield return DashAttack(enemy.transform, enemyCell, playerCell);

            // 데미지 적용
            ApplyDamage(damage);

            // ✅ 공격했으므로 행동 완료 표시
            enemy.MarkAsActed();
        }
        else
        {
            // 원거리: 발사체(프리팹 우선) -> 없으면 임시 발사체 -> 최후에 반짝임
            if (enemy.definition.projectilePrefab != null)
            {
                // 발사체 발사
                yield return LaunchProjectile(enemy, enemyCell, playerCell);
            }
            else
            {
                if (showDebugLogs)
                    Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} projectilePrefab is NULL. Using fallback projectile.");

                yield return LaunchFallbackProjectile(enemyCell, playerCell);
            }

            // 데미지 적용
            ApplyDamage(damage);

            // ✅ 공격했으므로 행동 완료 표시
            enemy.MarkAsActed();
        }

        // 다음 적 공격까지 딜레이
        yield return new WaitForSeconds(attackDelay);
    }

    /// <summary>
    /// 데미지 적용 + 피격 효과
    /// </summary>
    private void ApplyDamage(int damage)
    {
        if (playerStats != null)
        {
            playerStats.TakeDamage(damage);

            if (showDebugLogs)
                Debug.Log($"[MultiEnemyAttack] Player HP: {playerStats.hp}/{playerStats.maxHP}");
        }
    }

    /// <summary>
    /// 발사체 발사
    /// </summary>
    private IEnumerator LaunchProjectile(EnemyInstance enemy, Vector2Int fromCell, Vector2Int toCell)
    {
        Vector3 startPos = gridBoard.CellToWorld(fromCell);
        Vector3 targetPos = gridBoard.CellToWorld(toCell);

        // 발사체 생성
        GameObject projectile = Instantiate(enemy.definition.projectilePrefab, startPos, Quaternion.identity);

        // Projectile 스크립트 추가 (없으면)
        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<Projectile>();
        }

        // 회전 설정
        projectileScript.rotateTowardsTarget = enemy.definition.projectileRotates;

        // 발사!
        projectileScript.Launch(startPos, targetPos);

        // 발사체가 도착할 때까지 대기
        float distance = Vector3.Distance(startPos, targetPos);
        float speed = Mathf.Max(0.01f, projectileScript.speed);
        float travelTime = distance / speed;
        yield return new WaitForSeconds(travelTime);
    }

    private IEnumerator LaunchFallbackProjectile(Vector2Int fromCell, Vector2Int toCell)
    {
        if (gridBoard == null)
            yield break;

        Vector3 startPos = gridBoard.CellToWorld(fromCell);
        // 플레이어 실좌표 우선 (셀 스냅 오차/점유 지연 대비)
        Vector3 targetPos = player != null ? player.position : gridBoard.CellToWorld(toCell);

        GameObject go = new GameObject("FallbackProjectile");
        go.transform.position = startPos;
        go.transform.localScale = Vector3.one * Mathf.Max(0.05f, fallbackProjectileScale);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetFallbackProjectileSprite();
        sr.color = fallbackProjectileColor;
        sr.sortingOrder = 120;

        float speed = Mathf.Max(0.01f, fallbackProjectileSpeed);
        float distance = Vector3.Distance(startPos, targetPos);
        float travelTime = Mathf.Max(0.05f, distance / speed);

        if (showDebugLogs)
            Debug.Log($"[MultiEnemyAttack] Fallback projectile: from={startPos} to={targetPos}, distance={distance:F2}, t={travelTime:F2}");

        float t = 0f;
        while (t < 1f && go != null)
        {
            t += Time.deltaTime / travelTime;
            go.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.Clamp01(t));
            yield return null;
        }

        if (go != null)
            Destroy(go);
    }

    private static Sprite GetFallbackProjectileSprite()
    {
        if (_fallbackProjectileSprite != null)
            return _fallbackProjectileSprite;

        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float r = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c);
                float a = Mathf.Clamp01(1f - (d / r));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        _fallbackProjectileSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size
        );
        return _fallbackProjectileSprite;
    }

    /// <summary>
    /// 대시 공격 애니메이션
    /// </summary>
    /// <summary>
    /// 모든 적의 transform.position을 GridOccupancyRegistry 기준으로 동기화
    /// (StepMove 코루틴과 DashAttack 사이의 위치 불일치 방지)
    /// </summary>
    private void SyncAllEnemyPositions()
    {
        var occ = GridOccupancyRegistry.Instance;
        if (occ == null || gridBoard == null) return;

        EnemyInstance[] allEnemies = FindObjectsOfType<EnemyInstance>();
        foreach (var enemy in allEnemies)
        {
            if (enemy == null || enemy.currentHP <= 0) continue;
            if (occ.TryGetCurrentCell(enemy.transform, out var cell))
            {
                SnapRootIfDrifted(enemy.transform, cell);
            }
        }
    }

    private IEnumerator DashAttack(Transform enemyTransform, Vector2Int fromCell, Vector2Int toCell)
    {
        if (enemyTransform == null) yield break;

        Transform visual = FieldVisualMotionUtil.ResolveVisualRoot(enemyTransform);
        if (visual == null) yield break;

        Vector3 startPos = visual.position;
        Vector3 fromWorld = gridBoard.CellToWorld(fromCell);
        Vector3 toWorld = gridBoard.CellToWorld(toCell);
        Vector3 targetPos = startPos + (toWorld - fromWorld) * 0.7f;

        // 전진
        float t = 0f;
        while (t < 1f)
        {
            if (visual == null) yield break;
            t += Time.deltaTime / dashDuration;
            visual.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        // 후퇴
        t = 0f;
        while (t < 1f)
        {
            if (visual == null) yield break;
            t += Time.deltaTime / dashDuration;
            visual.position = Vector3.Lerp(targetPos, startPos, t);
            yield return null;
        }

        if (visual != null)
            visual.position = startPos;
    }

    /// <summary>
    /// 원거리 공격 효과 (반짝임)
    /// </summary>
    private IEnumerator RangedAttackEffect(Transform enemyTransform)
    {
        if (enemyTransform == null) yield break;

        // SpriteRenderer 찾기
        SpriteRenderer sprite = enemyTransform.GetComponentInChildren<SpriteRenderer>();
        if (sprite == null)
        {
            // 없으면 그냥 짧은 딜레이
            yield return new WaitForSeconds(0.1f);
            yield break;
        }

        Color originalColor = sprite.color;

        // 밝게 (공격 표시)
        float flashDuration = 0.15f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / flashDuration;
            sprite.color = Color.Lerp(originalColor, Color.white, Mathf.PingPong(t * 2f, 1f));
            yield return null;
        }

        sprite.color = originalColor;
    }

    private int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    private void SnapRootIfDrifted(Transform root, Vector2Int cell)
    {
        if (root == null || gridBoard == null) return;

        Vector3 expected = gridBoard.CellToWorld(cell);
        float threshold = Mathf.Max(0.1f, gridBoard.cellSize * 0.45f);
        if (Vector3.SqrMagnitude(root.position - expected) > threshold * threshold)
            root.position = expected;
    }
}