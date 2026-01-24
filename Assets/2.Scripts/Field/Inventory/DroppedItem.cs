using UnityEngine;

/// <summary>
/// 바닥에 떨어진 아이템
/// </summary>
public class DroppedItem : MonoBehaviour
{
    [Header("Item Data")]
    public string itemName = "Seaweed";
    public int itemID = 1; // 1 = 해초
    public int count = 1;

    [Header("Visual")]
    public float floatSpeed = 0.5f;
    public float floatHeight = 0.1f;

    private Vector3 startPosition;
    private float floatTimer = 0f;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // 떠다니는 효과
        floatTimer += Time.deltaTime * floatSpeed;
        float offset = Mathf.Sin(floatTimer) * floatHeight;
        transform.position = startPosition + new Vector3(0, offset, 0);
    }

    /// <summary>
    /// 아이템 줍기
    /// </summary>
    public void Pickup(Vector3 playerPosition)
    {
        // 인벤토리에 추가
        InventoryManager inventory = FindObjectOfType<InventoryManager>();
        if (inventory != null)
        {
            inventory.AddItem(itemID, itemName, count, playerPosition);
        }

        // 아이템 제거
        Destroy(gameObject);
    }
}