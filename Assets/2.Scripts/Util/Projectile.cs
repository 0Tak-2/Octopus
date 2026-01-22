using System.Collections;
using UnityEngine;

/// <summary>
/// 발사체 - 적에서 플레이어로 날아감
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public bool rotateTowardsTarget = true; // 화살 같은 것은 true

    [Header("Lifetime")]
    public float maxLifetime = 3f; // 3초 후 자동 삭제

    private Vector3 targetPosition;
    private bool isFlying = false;
    private float lifetime = 0f;

    /// <summary>
    /// 발사체 발사
    /// </summary>
    public void Launch(Vector3 from, Vector3 to)
    {
        transform.position = from;
        targetPosition = to;
        isFlying = true;

        // 방향 회전 (화살 등)
        if (rotateTowardsTarget)
        {
            Vector3 direction = (to - from).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void Update()
    {
        if (!isFlying) return;

        lifetime += Time.deltaTime;

        // 수명 체크
        if (lifetime >= maxLifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 목표로 이동
        transform.position = Vector3.MoveTowards(
            transform.position,
            targetPosition,
            speed * Time.deltaTime
        );

        // 도착 확인
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            // 도착! 삭제
            Destroy(gameObject);
        }
    }
}