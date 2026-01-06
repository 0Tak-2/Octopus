using UnityEngine;
using TMPro;
using System.Reflection;

public class FieldSkillBarUI : MonoBehaviour
{
    [Header("Refs")]
    public FieldSkillCaster caster;
    public FieldSkillLoadout loadout;

    [Header("Slots (4)")]
    public FieldSkillSlotUI slot1;
    public FieldSkillSlotUI slot2;
    public FieldSkillSlotUI slot3;
    public FieldSkillSlotUI slot4;

    [Header("Tooltip")]
    public FieldSkillTooltipUI tooltip; // 툴팁 UI

    [Header("Optional")]
    public TMP_Text selectedNameText;

    private FieldSkillSlotUI[] _slots;

    private void Awake()
    {
        if (caster == null) caster = FindObjectOfType<FieldSkillCaster>();
        if (loadout == null && caster != null) loadout = caster.loadout;

        _slots = new[] { slot1, slot2, slot3, slot4 };

        for (int i = 0; i < _slots.Length; i++)
        {
            int slotIndex = i + 1;
            if (_slots[i] != null)
                _slots[i].Init(slotIndex, OnClickSlot, OnHoverSlot);
        }
    }

    private void OnEnable()
    {
        if (caster != null)
        {
            caster.OnSelectedSlotChanged += HandleSelectedChanged;
            caster.OnCooldownChanged += HandleCooldownChanged;
        }

        RefreshAll();
    }

    private void OnDisable()
    {
        if (caster != null)
        {
            caster.OnSelectedSlotChanged -= HandleSelectedChanged;
            caster.OnCooldownChanged -= HandleCooldownChanged;
        }
    }

    private void OnClickSlot(int slotIndex1Based)
    {
        TrySelectSlotOnCaster(slotIndex1Based);
    }

    private void OnHoverSlot(int slotIndex1Based, bool isEnter)
    {
        if (tooltip == null || caster == null) return;

        if (!isEnter)
        {
            tooltip.Hide();
            return;
        }

        var def = caster.GetDefinition(slotIndex1Based);
        int cd = caster.GetCooldownRemaining(slotIndex1Based);
        tooltip.Show(def, cd);
    }

    private void HandleSelectedChanged(int _)
    {
        RefreshSelection();
        RefreshSelectedName();
    }

    private void HandleCooldownChanged()
    {
        RefreshCooldowns();

        // 툴팁 떠있는 중이면 내용 갱신(선택된 슬롯 기준으로 갱신하지 않고, 그냥 숨김 처리도 가능)
        // 여기선 단순히 숨김 처리: 깔끔함
        if (tooltip != null) tooltip.Hide();
    }

    public void RefreshAll()
    {
        RefreshIcons();
        RefreshCooldowns();
        RefreshSelection();
        RefreshSelectedName();
    }

    private void RefreshIcons()
    {
        if (caster == null) return;

        for (int i = 0; i < 4; i++)
        {
            int slotIndex = i + 1;
            var ui = _slots[i];
            if (ui == null) continue;

            ScriptableObject def = caster.GetDefinition(slotIndex);
            bool hasSkill = def != null;

            Sprite icon = TryReadSprite(def, new[] { "icon", "sprite", "uiIcon" });
            ui.SetIcon(icon, hasSkill);
        }
    }

    private void RefreshCooldowns()
    {
        if (caster == null) return;

        for (int i = 0; i < 4; i++)
        {
            int slotIndex = i + 1;
            var ui = _slots[i];
            if (ui == null) continue;

            int cd = caster.GetCooldownRemaining(slotIndex);
            ui.SetCooldown(cd);
        }
    }

    private void RefreshSelection()
    {
        if (caster == null) return;

        int sel = caster.SelectedSlot;
        for (int i = 0; i < 4; i++)
        {
            var ui = _slots[i];
            if (ui == null) continue;

            ui.SetSelected((i + 1) == sel);
        }
    }

    private void RefreshSelectedName()
    {
        if (selectedNameText == null || caster == null) return;

        var def = caster.GetDefinition(caster.SelectedSlot);
        string name = TryReadString(def, new[] { "displayName", "skillName", "name" }, def != null ? def.name : "-");
        selectedNameText.text = name;
    }

    private static Sprite TryReadSprite(ScriptableObject def, string[] names)
    {
        if (def == null) return null;
        var t = def.GetType();

        foreach (var n in names)
        {
            var f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (f != null && typeof(Sprite).IsAssignableFrom(f.FieldType))
                return f.GetValue(def) as Sprite;

            var p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && typeof(Sprite).IsAssignableFrom(p.PropertyType))
                return p.GetValue(def) as Sprite;
        }
        return null;
    }

    private static string TryReadString(ScriptableObject def, string[] names, string fallback)
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

    private void TrySelectSlotOnCaster(int slotIndex1Based)
    {
        if (caster == null) return;

        // public 우선
        var m = caster.GetType().GetMethod("SelectSlot", BindingFlags.Instance | BindingFlags.Public);
        if (m != null)
        {
            m.Invoke(caster, new object[] { slotIndex1Based });
            return;
        }

        // private fallback
        m = caster.GetType().GetMethod("SelectSlot", BindingFlags.Instance | BindingFlags.NonPublic);
        if (m != null)
        {
            m.Invoke(caster, new object[] { slotIndex1Based });
            return;
        }

        Debug.LogWarning("[FieldSkillBarUI] Could not call FieldSkillCaster.SelectSlot. Make it public or keep key-only selection.");
    }
}
