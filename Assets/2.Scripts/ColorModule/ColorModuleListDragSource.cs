using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>색 모듈 보유 목록 행에서 드래그 → 장착 슬롯으로 드롭</summary>
public class ColorModuleListDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public ColorModuleInstance module;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (module == null) return;
        InventoryDragSession.BeginColorModuleList(module);

        Image icon = GetComponent<Image>();
        if (icon == null) icon = GetComponentInChildren<Image>();
        if (icon != null && icon.sprite != null)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                GameObject g = new GameObject("ModListDragGhost");
                g.transform.SetParent(c.transform, false);
                g.transform.SetAsLastSibling();
                var img = g.AddComponent<Image>();
                img.sprite = icon.sprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                var rt = g.GetComponent<RectTransform>();
                rt.sizeDelta = icon.rectTransform.rect.size;
                InventoryDragSession.DragGhost = g;
            }
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (InventoryDragSession.DragGhost != null)
            InventoryDragSession.DragGhost.transform.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventoryDragSession.Clear();
    }
}
