using UnityEngine;

/// <summary>
/// �� �ν� ����ǥ - ������ �ִϸ��̼�
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

    private void Awake()
    {
        // ���ſ� �� ������Ʈ�� �Ǽ��� ��� �� �����տ� ������ �־, �� Update ����
        // transform.localPosition �� ���� ��ǥ�� ���ͽ�Ű�� ���׸� �����ߴ�.
        // (���: ���� �պ� �� ��� ���� ���� ��ġ�� �ڷ���Ʈ)
        // �� ������Ʈ�� ���� alertIconPrefab �� �ڽ� ������ GameObject ���� �پ�� �Ѵ�.
        if (GetComponent<EnemyInstance>() != null)
        {
            Debug.LogWarning(
                $"[EnemyAlertIcon] '{name}' �� �߸� ������ (EnemyInstance �� ����). " +
                "��Ȱ��ȭ�մϴ�. �ش� �� �����տ��� EnemyAlertIcon ������Ʈ�� �������ּ���.",
                this);
            enabled = false;
        }
    }

    private void Start()
    {
        _startPos = transform.localPosition;
    }

    private void Update()
    {
        _time += Time.deltaTime;

        // ���Ʒ��� ��鸲
        float yOffset = Mathf.Sin(_time * bobSpeed) * bobHeight;
        transform.localPosition = _startPos + Vector3.up * yOffset;

        // ũ�� �޽�
        float scale = 1f + Mathf.Sin(_time * scaleSpeed) * (maxScale - 1f) * 0.5f;
        transform.localScale = Vector3.one * scale;
    }
}