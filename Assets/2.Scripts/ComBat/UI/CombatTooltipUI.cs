using UnityEngine;
using TMPro;

public class CombatTooltipUI : MonoBehaviour
{
    public static CombatTooltipUI Instance { get; private set; }

    [Header("UI")]
    public GameObject root;      // TooltipRoot (Panel)
    public TMP_Text titleText;   // TitleText
    public TMP_Text bodyText;    // BodyText

    [Header("Position")]
    public Vector2 offset = new Vector2(18f, -18f);
    public bool followMouse = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (root != null)
            root.SetActive(false);
    }

    private void Update()
    {
        if (!followMouse) return;
        if (root == null || !root.activeSelf) return;

        root.transform.position = Input.mousePosition + (Vector3)offset;
    }

    public static void Show(CombatAttackDefinition skill, RectTransform anchor = null)
    {
        if (skill == null) return;
        if (Instance == null || Instance.root == null) return;

        Instance.root.SetActive(true);

        if (Instance.titleText != null)
            Instance.titleText.text = skill.displayName;

        if (Instance.bodyText != null)
        {
            string pattern = skill.pattern.ToString();
            Instance.bodyText.text =
                $"AP: {skill.apCost}\n" +
                $"Damage: {skill.damage}\n" +
                $"Range: {skill.range}\n" +
                $"Pattern: {pattern}";
        }

        if (!Instance.followMouse && anchor != null)
            Instance.root.transform.position = anchor.position + (Vector3)Instance.offset;
    }

    public static void Hide()
    {
        if (Instance == null || Instance.root == null) return;
        Instance.root.SetActive(false);
    }
}
