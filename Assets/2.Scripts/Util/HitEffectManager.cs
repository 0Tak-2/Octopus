using System.Collections;
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

        // 흔들림
        StartCoroutine(ShakeCoroutine(target));

        // 데미지 텍스트
        if (showDamageText)
        {
            ShowDamageText(damage, target.position);
        }
    }

    /// <summary>
    /// 좌우 흔들림
    /// </summary>
    private IEnumerator ShakeCoroutine(Transform target)
    {
        // ✅ 현재 위치를 기준으로 오프셋만 적용
        Vector3 startPos = target.position;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // 좌우로 흔들림 (시간에 따라 감쇠)
            float progress = elapsed / shakeDuration;
            float currentShake = shakeAmount * (1f - progress); // 점점 약해짐
            float offsetX = Random.Range(-currentShake, currentShake);

            target.position = startPos + new Vector3(offsetX, 0, 0);

            yield return null;
        }

        // 원래 위치로 복귀
        target.position = startPos;
    }

    /// <summary>
    /// 데미지 텍스트 생성
    /// </summary>
    private void ShowDamageText(int damage, Vector3 worldPosition)
    {
        GameObject textObj = new GameObject("DamageText");
        DamageText damageText = textObj.AddComponent<DamageText>();
        damageText.Setup(damage, worldPosition);
    }
}