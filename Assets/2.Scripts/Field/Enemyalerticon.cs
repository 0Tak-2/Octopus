using UnityEngine;

/// <summary>
/// 적 인식 느낌표 - 간단한 애니메이션
/// </summary>
public class EnemyAlertIcon : MonoBehaviour
{
    [Header("Animation")]
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;
    public float scaleSpeed = 3f;
    public float maxScale = 1.2f;

    private Vector3 _startPos;
    private float _time;

    private void Start()
    {
        _startPos = transform.localPosition;
    }

    private void Update()
    {
        _time += Time.deltaTime;

        // 위아래로 흔들림
        float yOffset = Mathf.Sin(_time * bobSpeed) * bobHeight;
        transform.localPosition = _startPos + Vector3.up * yOffset;

        // 크기 펄스
        float scale = 1f + Mathf.Sin(_time * scaleSpeed) * (maxScale - 1f) * 0.5f;
        transform.localScale = Vector3.one * scale;
    }
}