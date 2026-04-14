using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 피격 효과 - 흔들림 + 데미지 텍스트
/// </summary>
public class HitEffectManager : MonoBehaviour
{
    public static HitEffectManager Instance { get; private set; }

    [Header("Hit Shake")]
    public float shakeDuration = 0.2f;
    public float shakeAmount = 0.15f;

    [Header("Damage Text")]
    public bool showDamageText = true;

    private readonly Dictionary<Transform, Coroutine> _shakeByTarget = new Dictionary<Transform, Coroutine>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 피격 효과 표시 (흔들림 + 데미지 텍스트)
    /// </summary>
    public void ShowHitEffect(Transform target, int damage)
    {
        if (target == null) return;

        Transform shakeTarget = FieldVisualMotionUtil.ResolveVisualRoot(target);
        if (shakeTarget == null) return;

        StopShakeForTarget(shakeTarget);

        Vector3 baseForShake = GetShakeBasePosition(target, shakeTarget);
        var co = StartCoroutine(ShakeCoroutine(shakeTarget, baseForShake));
        _shakeByTarget[shakeTarget] = co;

        if (showDamageText)
            ShowDamageText(damage, target.position);
    }

    /// <summary>
    /// 플레이어는 격자 셀 중심을 기준으로 흔들여야 이동/스냅과 어긋나지 않는다.
    /// </summary>
    private static Vector3 GetShakeBasePosition(Transform logicalRoot, Transform shakeTarget)
    {
        if (logicalRoot == null)
            return shakeTarget != null ? shakeTarget.position : Vector3.zero;

        var mover = logicalRoot.GetComponent<PlayerGridMover>();
        if (mover != null && mover.grid != null && shakeTarget == logicalRoot)
            return mover.grid.CellToWorld(mover.CurrentCell);

        return shakeTarget != null ? shakeTarget.position : logicalRoot.position;
    }

    private void StopShakeForTarget(Transform target)
    {
        if (target == null) return;
        if (!_shakeByTarget.TryGetValue(target, out Coroutine c) || c == null)
            return;

        StopCoroutine(c);
        _shakeByTarget.Remove(target);

    }

    private IEnumerator ShakeCoroutine(Transform target, Vector3 baseWorldPos)
    {
        float elapsed = 0f;

        try
        {
            while (elapsed < shakeDuration)
            {
                if (target == null) yield break;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / shakeDuration);
                float currentShake = shakeAmount * (1f - progress);
                float offsetX = Random.Range(-currentShake, currentShake);
                target.position = baseWorldPos + new Vector3(offsetX, 0f, 0f);

                yield return null;
            }
        }
        finally
        {
            if (target != null)
                target.position = baseWorldPos;

            if (target != null)
                _shakeByTarget.Remove(target);
        }
    }

    private void ShowDamageText(int damage, Vector3 worldPosition)
    {
        GameObject textObj = new GameObject("DamageText");
        DamageText damageText = textObj.AddComponent<DamageText>();
        damageText.Setup(damage, worldPosition);
    }
}
