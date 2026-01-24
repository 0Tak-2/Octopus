using UnityEngine;
using TMPro;

/// <summary>
/// 아이템 획득 시 플레이어 주위에 뜨는 텍스트
/// </summary>
public class ItemPickupText : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 2f;
    public float riseSpeed = 1f;
    public float fadeSpeed = 1f;

    private TextMeshPro textMesh;
    private Color startColor;
    private float timer = 0f;

    private void Awake()
    {
        textMesh = GetComponent<TextMeshPro>();
        if (textMesh != null)
        {
            startColor = textMesh.color;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 위로 올라감
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // 페이드 아웃
        if (textMesh != null && timer > lifetime * 0.5f)
        {
            float alpha = Mathf.Lerp(startColor.a, 0, (timer - lifetime * 0.5f) / (lifetime * 0.5f));
            textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
        }

        // 제거
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 텍스트 설정
    /// </summary>
    public void Setup(string itemName, int count, Vector3 worldPosition)
    {
        if (textMesh == null)
        {
            textMesh = GetComponent<TextMeshPro>();
        }

        textMesh.text = $"+{count} {itemName}";
        textMesh.fontSize = 4;
        textMesh.color = new Color(0.6f, 1f, 0.6f, 1f); // 연두색
        textMesh.alignment = TextAlignmentOptions.Center;

        // 랜덤 위치 (플레이어 주위)
        float randomX = Random.Range(-0.5f, 0.5f);
        float randomY = Random.Range(0.5f, 1.5f);
        transform.position = worldPosition + new Vector3(randomX, randomY, 0);
    }
}