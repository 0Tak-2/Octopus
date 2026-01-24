using UnityEngine;

/// <summary>
/// 채집 가능한 오브젝트 (해초, 산호, 조개 등 모두 처리)
/// </summary>
public class Harvestable : MonoBehaviour
{
    [Header("아이템 설정")]
    public string itemName = "해초"; // 아이템 이름
    public int itemID = 1; // 아이템 ID (1=해초, 2=산호, 3=조개...)
    public GameObject itemDropPrefab; // 떨어질 아이템 프리팹

    [Header("드랍 설정")]
    public int minDropCount = 1; // 최소 드랍 개수
    public int maxDropCount = 1; // 최대 드랍 개수
    public float dropSpread = 0.2f; // 드랍 흩어짐 정도

    [Header("특수 효과 (선택)")]
    public int stealthBonus = 0; // 은신력 보너스 (해초만 사용)

    [Header("상태")]
    public bool isHarvested = false;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>
    /// 채집
    /// </summary>
    public void Harvest()
    {
        if (isHarvested) return;

        isHarvested = true;

        // 아이템 드랍
        if (itemDropPrefab != null)
        {
            // 랜덤 개수
            int actualDropCount = Random.Range(minDropCount, maxDropCount + 1);

            for (int i = 0; i < actualDropCount; i++)
            {
                // 약간씩 다른 위치에 드랍
                Vector3 dropPos = transform.position + new Vector3(
                    Random.Range(-dropSpread, dropSpread),
                    Random.Range(-dropSpread, dropSpread),
                    0
                );

                Instantiate(itemDropPrefab, dropPos, Quaternion.identity);
            }
        }

        // 오브젝트 제거
        Destroy(gameObject);
    }

    /// <summary>
    /// 플레이어가 이 오브젝트 안에 있는지 체크 (은신용)
    /// </summary>
    public bool IsPlayerInside(Vector3 playerPosition, GridBoard gridBoard)
    {
        if (gridBoard == null || stealthBonus <= 0) return false;

        Vector2Int objectCell = gridBoard.WorldToCell(transform.position);
        Vector2Int playerCell = gridBoard.WorldToCell(playerPosition);

        return objectCell == playerCell;
    }
}