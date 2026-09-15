using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 슬롯 행에 마우스를 올렸을 때 색이 밝아지게 한다.
/// 선택 하이라이트(HighlightSlot)와 섞이지 않도록 '기본색'을 따로 들고 있다가
/// 포인터가 빠지면 그 색으로 되돌린다.
/// </summary>
[RequireComponent(typeof(Image))]
public class SlotRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("마우스를 올렸을 때 곱해질 밝기")]
    public float hoverBrightness = 1.45f;

    private Image _image;
    private Color _baseColor;
    private bool _hovering;

    private void Awake()
    {
        _image = GetComponent<Image>();
        _baseColor = _image.color;
    }

    /// <summary>선택 상태 등으로 기본색이 바뀔 때 호출. 호버 중이면 즉시 반영한다.</summary>
    public void SetBaseColor(Color color)
    {
        _baseColor = color;
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
        Apply();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
        Apply();
    }

    private void Apply()
    {
        if (_image == null) return;

        if (!_hovering)
        {
            _image.color = _baseColor;
            return;
        }

        // 알파는 유지하고 밝기만 올린다.
        Color c = _baseColor * hoverBrightness;
        c.a = _baseColor.a;
        _image.color = c;
    }
}
