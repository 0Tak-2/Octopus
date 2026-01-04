using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyEliteHUDPanel : MonoBehaviour
{
    [Header("Refs")]
    public FocusedCombatManager combat;

    [Header("Visibility")]
    public bool showOnlyWhenElite = true;

    [Header("Enemy HP")]
    public TMP_Text enemyHpText;

    [Header("Enemy AP Dots (5)")]
    public Image[] apDots;
    public float apDotOnAlpha = 1f;
    public float apDotOffAlpha = 0.25f;

    [Header("Skill Icon Slots (4)")]
    public SkillIconSlot slot1;
    public SkillIconSlot slot2;
    public SkillIconSlot slot3;
    public SkillIconSlot slot4;

    [Header("Skill Icon Slot (5) - Boss Only (Optional)")]
    public SkillIconSlot slot5; // AI4용(없으면 null OK)

    [Header("Buff UI")]
    public GameObject buffRoot;
    public Image buffIconImage;
    public TMP_Text buffTurnsText;

    [Header("Extra Status (Optional)")]
    public TMP_Text statusText;

    public bool hookHoverEvents = true;

    private EnemyDefinition _cachedDef;

    private void Awake()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        TryHookHover();
    }

    private void OnEnable()
    {
        _cachedDef = null;
        TryHookHover(); // ✅ enable 타이밍에도 재연결
        Refresh();
    }

    private void TryHookHover()
    {
        if (!hookHoverEvents) return;

        Hook(slot1);
        Hook(slot2);
        Hook(slot3);
        Hook(slot4);
        Hook(slot5);

        void Hook(SkillIconSlot s)
        {
            if (s == null) return;
            s.OnHoverEnter -= HandleHoverEnter;
            s.OnHoverExit -= HandleHoverExit;
            s.OnHoverEnter += HandleHoverEnter;
            s.OnHoverExit += HandleHoverExit;
        }
    }

    private void HandleHoverEnter(CombatAttackDefinition skill, SkillIconSlot slot)
    {
        if (skill == null) return;
        CombatTooltipUI.Show(skill, slot != null ? slot.transform as RectTransform : null);
    }

    private void HandleHoverExit(CombatAttackDefinition skill, SkillIconSlot slot)
    {
        CombatTooltipUI.Hide();
    }

    private void Update()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        if (combat == null) return;
        if (!combat.IsInFocusedCombat) return;

        bool isSpecial = IsEliteOrBoss(combat.GetEnemyAIType());
        if (showOnlyWhenElite && !isSpecial) return;

        Refresh();
    }

    private bool IsEliteOrBoss(EnemyAIType t) =>
        (t == EnemyAIType.AI3_Elite) || (t == EnemyAIType.AI4_Boss);

    public void Refresh()
    {
        if (combat == null) return;

        EnemyAIType type = combat.GetEnemyAIType();
        bool isSpecial = IsEliteOrBoss(type);

        if (showOnlyWhenElite)
        {
            gameObject.SetActive(isSpecial);
            if (!isSpecial) return;
        }

        var def = combat.CurrentEnemyDef;
        if (def != _cachedDef)
        {
            _cachedDef = def;
            RebindSkillSlots(def, type);
        }

        if (enemyHpText != null)
        {
            int hp = combat.State.enemyHP;
            int max = Mathf.Max(1, combat.GetEnemyMaxHP());
            enemyHpText.text = $"HP: {hp}/{max}";
        }

        int ap = 0;
        int apMax = 5;

        if (type == EnemyAIType.AI3_Elite)
        {
            EnemyAIController enemyAI = combat.enemyAI;
            if (enemyAI != null)
            {
                ap = Mathf.Clamp(enemyAI.EliteAP, 0, 5);
                apMax = Mathf.Clamp(enemyAI.EliteAPMax, 1, 5);
            }

            ApplyDots(ap, apMax);
            ApplySkillUsable(slot1, ap);
            ApplySkillUsable(slot2, ap);
            ApplySkillUsable(slot3, ap);
            ApplySkillUsable(slot4, ap);
            ApplySkillUsable(slot5, ap);

            RefreshBuffUI_AI3(def, enemyAI);
            RefreshStatusText_AI3(enemyAI);
        }
        else if (type == EnemyAIType.AI4_Boss)
        {
            BossAIController bossAI = combat.bossAI;
            BossState bs = (bossAI != null) ? bossAI.GetState() : null;

            if (bs != null)
            {
                ap = Mathf.Clamp(bs.AP, 0, 5);
                apMax = Mathf.Clamp(bs.APMax, 1, 5);
            }

            ApplyDots(ap, apMax);
            ApplySkillUsable(slot1, ap);
            ApplySkillUsable(slot2, ap);
            ApplySkillUsable(slot3, ap);
            ApplySkillUsable(slot4, ap);
            ApplySkillUsable(slot5, ap);

            RefreshBuffUI_AI4(def, bs);
            RefreshStatusText_AI4(bs);
        }
        else
        {
            ApplyDots(0, 5);
            RefreshStatusText_Clear();
            if (buffRoot != null) buffRoot.SetActive(false);
        }
    }

    private void RebindSkillSlots(EnemyDefinition def, EnemyAIType type)
    {
        if (type == EnemyAIType.AI3_Elite)
        {
            if (slot1 != null) slot1.Bind(def != null ? def.skill1_MeleeNormal : null);
            if (slot2 != null) slot2.Bind(def != null ? def.skill2_MeleeCritical : null);
            if (slot3 != null) slot3.Bind(def != null ? def.skill3_Range2 : null);
            if (slot4 != null) slot4.Bind(def != null ? def.skill4_AttackBuff : null);
            if (slot5 != null) slot5.Bind(null);
            return;
        }

        if (type == EnemyAIType.AI4_Boss)
        {
            CombatAttackDefinition s1 = null, s2 = null, s3 = null, s4 = null, s5 = null;

            if (def != null)
            {
                s1 = def.bossSkill1_Basic;
                s2 = def.bossSkill2_Stun;
                s3 = def.bossSkill3_Charge;
                s4 = def.bossSkill4_Withdraw;
                s5 = def.bossSkill5_Meditation;
            }

            if (slot1 != null) slot1.Bind(s1);
            if (slot2 != null) slot2.Bind(s2);
            if (slot3 != null) slot3.Bind(s3);
            if (slot4 != null) slot4.Bind(s4);
            if (slot5 != null) slot5.Bind(s5);
            return;
        }

        if (slot1 != null) slot1.Bind(null);
        if (slot2 != null) slot2.Bind(null);
        if (slot3 != null) slot3.Bind(null);
        if (slot4 != null) slot4.Bind(null);
        if (slot5 != null) slot5.Bind(null);
    }

    private void ApplySkillUsable(SkillIconSlot slot, int ap)
    {
        if (slot == null) return;
        if (slot.boundSkill == null) { slot.SetUsable(false); return; }
        bool canUse = ap >= Mathf.Max(0, slot.boundSkill.apCost);
        slot.SetUsable(canUse);
    }

    private void RefreshBuffUI_AI3(EnemyDefinition def, EnemyAIController enemyAI)
    {
        if (buffRoot == null) return;
        bool active = (enemyAI != null && enemyAI.EliteBuffTurnsLeft > 0);
        buffRoot.SetActive(active);
        if (!active) return;

        Sprite icon = null;
        if (def != null && def.skill4_AttackBuff != null)
            icon = def.skill4_AttackBuff.buffStatusIcon != null ? def.skill4_AttackBuff.buffStatusIcon : def.skill4_AttackBuff.icon;

        if (buffIconImage != null)
        {
            buffIconImage.sprite = icon;
            buffIconImage.enabled = (icon != null);
        }

        if (buffTurnsText != null)
            buffTurnsText.text = $"{Mathf.Max(0, enemyAI.EliteBuffTurnsLeft)}T";
    }

    private void RefreshStatusText_AI3(EnemyAIController enemyAI)
    {
        if (statusText == null) return;
        statusText.text = "";
    }

    private void RefreshBuffUI_AI4(EnemyDefinition def, BossState bs)
    {
        if (buffRoot == null) return;
        bool meditation = (bs != null && bs.MeditationActive);
        buffRoot.SetActive(meditation);
        if (!meditation) return;

        Sprite icon = null;
        if (def != null && def.bossSkill5_Meditation != null)
            icon = def.bossSkill5_Meditation.buffStatusIcon != null ? def.bossSkill5_Meditation.buffStatusIcon : def.bossSkill5_Meditation.icon;

        if (buffIconImage != null)
        {
            buffIconImage.sprite = icon;
            buffIconImage.enabled = (icon != null);
        }

        if (buffTurnsText != null)
        {
            int t = (bs != null) ? Mathf.Max(0, bs.MeditationTurnsLeft) : 1;
            buffTurnsText.text = $"{Mathf.Max(1, t)}T";
        }
    }

    private void RefreshStatusText_AI4(BossState bs)
    {
        if (statusText == null) return;
        if (bs == null) { statusText.text = ""; return; }

        if (bs.IsCharging) statusText.text = "차징 중";
        else if (bs.MeditationActive) statusText.text = "명상";
        else statusText.text = "";
    }

    private void RefreshStatusText_Clear()
    {
        if (statusText == null) return;
        statusText.text = "";
    }

    private void ApplyDots(int ap, int apMax)
    {
        if (apDots == null || apDots.Length == 0) return;

        for (int i = 0; i < apDots.Length; i++)
        {
            if (apDots[i] == null) continue;

            bool enabledByMax = i < apMax;
            bool on = enabledByMax && i < ap;

            apDots[i].gameObject.SetActive(true);

            var c = apDots[i].color;
            c.a = on ? apDotOnAlpha : apDotOffAlpha;
            apDots[i].color = c;
        }
    }
}
