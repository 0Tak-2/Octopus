using UnityEngine;
using TMPro;
using System.Reflection;

public class FieldSkillTooltipUI : MonoBehaviour
{
    [Header("UI")]
    public RectTransform root;
    public TMP_Text text;

    [Header("Follow Mouse")]
    public Vector2 screenOffset = new Vector2(16f, -16f);
    public bool clampToScreen = true;
    public Vector2 padding = new Vector2(12f, 12f);

    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    private void Update()
    {
        if (root == null || !root.gameObject.activeSelf) return;

        Vector2 pos = (Vector2)Input.mousePosition + screenOffset;

        if (clampToScreen)
        {
            Vector2 size = root.sizeDelta;
            float minX = padding.x;
            float minY = padding.y;
            float maxX = Screen.width - padding.x - size.x;
            float maxY = Screen.height - padding.y - size.y;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
        }

        root.position = pos;
    }

    public void Show(ScriptableObject def, int cooldownRemainingTurns)
    {
        if (root == null || text == null) return;

        if (def == null)
        {
            text.text = "Empty Slot";
            root.gameObject.SetActive(true);
            return;
        }

        string name = ReadString(def, new[] { "displayName", "skillName", "name" }, def.name);
        int range = ReadInt(def, new[] { "range", "castRange", "attackRange" }, 1);
        int damage = ReadInt(def, new[] { "damage", "baseDamage", "power" }, 1);
        int apCost = ReadInt(def, new[] { "apCost", "costAP", "cost", "ap" }, -1);

        string cdLine = cooldownRemainingTurns > 0 ? $"Cooldown: {cooldownRemainingTurns} turn(s)" : "Cooldown: Ready";

        // AP는 필드에서 안 쓰지만, “원래 정의가 뭔지” 참고로 보여주기(원하면 빼도 됨)
        string costLine = (apCost > 0) ? $"(Def Cost: AP {apCost})" : "";

        text.text =
            $"<b>{name}</b>\n" +
            $"{costLine}\n" +
            $"Range: {range}\n" +
            $"Damage: {damage}\n" +
            $"{cdLine}";

        root.gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.gameObject.SetActive(false);
    }

    private static int ReadInt(ScriptableObject def, string[] names, int fallback)
    {
        if (def == null) return fallback;
        var t = def.GetType();

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && (f.FieldType == typeof(int) || f.FieldType == typeof(float)))
            {
                object v = f.GetValue(def);
                if (v is int i) return i;
                if (v is float fl) return Mathf.RoundToInt(fl);
            }

            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && (p.PropertyType == typeof(int) || p.PropertyType == typeof(float)))
            {
                object v = p.GetValue(def);
                if (v is int i2) return i2;
                if (v is float fl2) return Mathf.RoundToInt(fl2);
            }
        }

        return fallback;
    }

    private static string ReadString(ScriptableObject def, string[] names, string fallback)
    {
        if (def == null) return fallback;
        var t = def.GetType();

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && f.FieldType == typeof(string))
            {
                var v = f.GetValue(def) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }

            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.PropertyType == typeof(string))
            {
                var v = p.GetValue(def) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
        }

        return fallback;
    }
}
