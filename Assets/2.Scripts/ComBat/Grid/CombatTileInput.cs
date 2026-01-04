using UnityEngine;
using UnityEngine.EventSystems;

public class CombatTileInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public CombatBoardUI board;
    public int x;
    public int y;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (board != null) board.NotifyTileHovered(x, y);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (board != null) board.NotifyTileUnhovered(x, y);
    }
}
