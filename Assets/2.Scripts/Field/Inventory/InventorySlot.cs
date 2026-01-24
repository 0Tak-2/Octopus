using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 인벤토리 슬롯 (마우스 이벤트 포함)
/// </summary>
public class InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI Elements")]
    public Image iconImage;
    public TextMeshProUGUI countText;
    public Image background;

    [Header("Colors")]
    public Color normalColor = new Color(0.2f, 0.2f, 0.3f, 1f);
    public Color hoverColor = new Color(0.3f, 0.3f, 0.4f, 1f);
    public Color emptyColor = new Color(0.1f, 0.1f, 0.15f, 0.5f);

    private InventoryItem item;
    private InventoryUI inventoryUI;
    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.3f;

    public void Setup(InventoryItem item, InventoryUI ui)
    {
        this.item = item;
        this.inventoryUI = ui;

        // 아이콘
        if (iconImage != null)
        {
            if (item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.enabled = true;
                iconImage.color = Color.white;
            }
            else
            {
                iconImage.enabled = false;
            }
        }

        // 개수
        if (countText != null)
        {
            if (item.count > 1)
            {
                countText.text = item.count.ToString();
                countText.enabled = true;
            }
            else
            {
                countText.enabled = false;
            }
        }

        // 배경
        if (background != null)
        {
            background.color = normalColor;
        }
    }

    public void SetupEmpty(InventoryUI ui)
    {
        this.item = null;
        this.inventoryUI = ui;

        if (iconImage != null)
            iconImage.enabled = false;

        if (countText != null)
            countText.enabled = false;

        if (background != null)
            background.color = emptyColor;
    }

    // 마우스 올림
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item == null) return;

        if (background != null)
            background.color = hoverColor;

        if (inventoryUI != null)
            inventoryUI.ShowTooltip(item.itemName, transform.position);
    }

    // 마우스 벗어남
    public void OnPointerExit(PointerEventData eventData)
    {
        if (item == null) return;

        if (background != null)
            background.color = normalColor;

        if (inventoryUI != null)
            inventoryUI.HideTooltip();
    }

    // 클릭 (더블클릭 감지)
    public void OnPointerClick(PointerEventData eventData)
    {
        if (item == null) return;

        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick < doubleClickThreshold)
        {
            // 더블클릭!
            OnDoubleClick();
        }

        lastClickTime = Time.time;
    }

    private void OnDoubleClick()
    {
        Debug.Log($"[InventorySlot] Double-clicked {item.itemName}");

        if (inventoryUI != null)
        {
            inventoryUI.ShowDetail(item);
        }
    }
}