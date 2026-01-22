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

    [Header("Visual")]
    [Tooltip("각 적의 공격 사이 딜레이")]
    public float attackDelay = 0.3f;
    public float dashDuration = 0.15f;

    [Header("Debug")]
    public bool showDebugLogs = true;

    private bool _isProcessing = false;

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
        _isProcessing = true;

        // 1. 주변 적들 찾기
        List<EnemyInstance> nearbyEnemies = FindNearbyEnemies();

        if (showDebugLogs)
            Debug.Log($"[MultiEnemyAttack] Found {nearbyEnemies.Count} enemies nearby");

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

        Vector2Int playerCell = gridBoard.WorldToCell(player.position);

        // 모든 적 찾기
        EnemyInstance[] allEnemies = FindObjectsOfType<EnemyInstance>();

        foreach (var enemy in allEnemies)
        {
            if (enemy.currentHP <= 0)
                continue;

            Vector2Int enemyCell = gridBoard.WorldToCell(enemy.transform.position);
            int distance = Chebyshev(playerCell, enemyCell);

            if (distance <= detectionRange)
            {
                result.Add(enemy);
            }
        }

        return result;
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
        yield break;

        Vector2Int enemyCell = gridBoard.WorldToCell(enemy.transform.position);
        Vector2Int playerCell = gridBoard.WorldToCell(player.position);

        int distance = Chebyshev(enemyCell, playerCell);
        int attackRange = enemy.definition.attackRange;

        // 공격 범위 안인지 체크
        if (distance > attackRange)
        {
            if (showDebugLogs)
                Debug.Log($"[MultiEnemyAttack] {enemy.definition.displayName} too far ({distance} > {attackRange})");
            yield break;
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
            // 원거리: 발사체 or 기본 효과
            if (enemy.definition.projectilePrefab != null)
            {
                // 발사체 발사
                yield return LaunchProjectile(enemy, enemyCell, playerCell);
            }
            else
            {
                // 기본 반짝임 효과
                yield return RangedAttackEffect(enemy.transform);
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

            // 피격 효과 (흔들림 + 데미지 텍스트)
            if (hitEffectManager != null && player != null)
            {
                hitEffectManager.ShowHitEffect(player, damage);
            }

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
        float travelTime = distance / projectileScript.speed;
        yield return new WaitForSeconds(travelTime);
    }

    /// <summary>
    /// 대시 공격 애니메이션
    /// </summary>
    private IEnumerator DashAttack(Transform enemyTransform, Vector2Int fromCell, Vector2Int toCell)
    {
        Vector3 startPos = gridBoard.CellToWorld(fromCell);
        Vector3 targetPos = gridBoard.CellToWorld(toCell);

        // 전진
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dashDuration;
            enemyTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        // 후퇴
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dashDuration;
            enemyTransform.position = Vector3.Lerp(targetPos, startPos, t);
            yield return null;
        }

        enemyTransform.position = startPos;
    }

    /// <summary>
    /// 원거리 공격 효과 (반짝임)
    /// </summary>
    private IEnumerator RangedAttackEffect(Transform enemyTransform)
    {
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
}