using UnityEngine;
using UnityEngine.UI;

public class CombatUnitToken : MonoBehaviour
{
    public enum UnitType { Player, Enemy }

    public UnitType unitType;
    public Image image;

    private void Reset()
    {
        image = GetComponent<Image>();
    }

    private void Awake()
    {
        if (image == null) image = GetComponent<Image>();
    }
}
