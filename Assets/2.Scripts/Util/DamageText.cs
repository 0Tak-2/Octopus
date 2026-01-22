using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 데미지 텍스트 - 머리 위에 빨간 글씨로 표시
/// </summary>
public class DamageText : MonoBehaviour
{
    [Header("Settings")]
    public float duration = 1.0f;
    public float floatSpeed = 2f;
    public float fadeSpeed = 1f;

    private TextMeshPro textMesh;
    private float timer = 0f;

    public void Setup(int damage, Vector3 worldPosition)
    {
        // TextMeshPro 생성
        textMesh = gameObject.AddComponent<TextMeshPro>();
        textMesh.text = $"-{damage}";
        textMesh.fontSize = 4;
        textMesh.color = Color.red;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.sortingOrder = 1000; // 맨 앞에 표시

        // 위치 설정 (머리 위)
        transform.position = worldPosition + new Vector3(0, 1f, 0);

        // 자동 삭제
        Destroy(gameObject, duration);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 위로 떠오름
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 페이드 아웃
        if (textMesh != null)
        {
            float alpha = 1f - (timer / duration);
            textMesh.color = new Color(1f, 0f, 0f, alpha);
        }
    }
}