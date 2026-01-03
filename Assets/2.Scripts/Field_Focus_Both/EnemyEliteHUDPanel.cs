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

    [Header("Buff UI")]
    public GameObject buffRoot;
    public Image buffIconImage;
    public TMP_Text buffTurnsText;

    public bool hookHoverEvents = true;

    private EnemyDefinition _cachedDef;

    private void Awake()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        TryHookHover();
    }

    private void OnEnable()
    {
        // 켜질 때마다 한번 초기 리프레시
        _cachedDef = null;
        Refresh();
    }

    private void TryHookHover()
    {
        if (!hookHoverEvents) return;
        Hook(slot1); Hook(slot2); Hook(slot3); Hook(slot4);

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
        // 나중에 Tooltip.Show(...) 연결
    }

    private void HandleHoverExit(CombatAttackDefinition skill, SkillIconSlot slot)
    {
        // 나중에 Tooltip.Hide()
    }

    private void Update()
    {
        if (combat == null) combat = FocusedCombatManager.Instance;
        if (combat == null) return;

        if (!combat.IsInFocusedCombat) return;

        bool isElite = combat.GetEnemyAIType() == EnemyAIType.AI3_Elite;
        if (showOnlyWhenElite && !isElite) return;

        Refresh();
    }

    public void Refresh()
    {
        if (combat == null) return;

        // ✅ 항상 “전투 매니저가 쓰는 enemyAI”를 본다
        EnemyAIController enemyAI = combat.enemyAI;

        // Def 바뀌면 스킬 슬롯 재바인딩
        var def = combat.CurrentEnemyDef;
        if (def != _cachedDef)
        {
            _cachedDef = def;
            RebindSkillSlots(def);
        }

        // HP
        if (enemyHpText != null)
        {
            int hp = combat.State.enemyHP;
            int max = Mathf.Max(1, combat.GetEnemyMaxHP());
            enemyHpText.text = $"HP: {hp}/{max}";
            var c = enemyHpText.color; c.a = 1f; enemyHpText.color = c;
        }

        // AP (엘리트면 enemyAI에서)
        int ap = 0;
        int apMax = 5;

        if (enemyAI != null && combat.GetEnemyAIType() == EnemyAIType.AI3_Elite)
        {
            ap = Mathf.Clamp(enemyAI.EliteAP, 0, 5);
            apMax = Mathf.Clamp(enemyAI.EliteAPMax, 1, 5);
        }

        ApplyDots(ap, apMax);

        // usable alpha by AP
        ApplySkillUsable(slot1, ap);
        ApplySkillUsable(slot2, ap);
        ApplySkillUsable(slot3, ap);
        ApplySkillUsable(slot4, ap);

        // Buff status
        RefreshBuffUI(def, enemyAI);
    }

    private void RebindSkillSlots(EnemyDefinition def)
    {
        if (slot1 != null) slot1.Bind(def != null ? def.skill1_MeleeNormal : null);
        if (slot2 != null) slot2.Bind(def != null ? def.skill2_MeleeCritical : null);
        if (slot3 != null) slot3.Bind(def != null ? def.skill3_Range2 : null);
        if (slot4 != null) slot4.Bind(def != null ? def.skill4_AttackBuff : null);
    }

    private void ApplySkillUsable(SkillIconSlot slot, int ap)
    {
        if (slot == null) return;
        if (slot.boundSkill == null)
        {
            slot.SetUsable(false);
            return;
        }

        bool canUse = ap >= Mathf.Max(0, slot.boundSkill.apCost);
        slot.SetUsable(canUse);
    }

    private void RefreshBuffUI(EnemyDefinition def, EnemyAIController enemyAI)
    {
        if (buffRoot == null) return;

        bool active = (enemyAI != null && enemyAI.EliteBuffTurnsLeft > 0);
        buffRoot.SetActive(active);

        if (!active) return;

        // 아이콘: 버프 스킬의 buffStatusIcon 우선
        Sprite icon = null;
        if (def != null && def.skill4_AttackBuff != null)
        {
            icon = def.skill4_AttackBuff.buffStatusIcon != null
                ? def.skill4_AttackBuff.buffStatusIcon
                : def.skill4_AttackBuff.icon;
        }

        if (buffIconImage != null)
        {
            buffIconImage.sprite = icon;
            buffIconImage.enabled = (icon != null);
            var c = buffIconImage.color; c.a = 1f; buffIconImage.color = c;
        }

        if (buffTurnsText != null)
        {
            buffTurnsText.text = $"{Mathf.Max(0, enemyAI.EliteBuffTurnsLeft)}T";
            var c = buffTurnsText.color; c.a = 1f; buffTurnsText.color = c;
        }
    }

    private void ApplyDots(int ap, int apMax)
    {
        if (apDots == null || apDots.Length == 0) return;

        for (int i = 0; i < apDots.Length; i++)
        {
            if (apDots[i] == null) continue;

            bool enabledByMax = i < apMax;
            bool on = enabledByMax && i < ap;

            // dot 자체는 항상 활성화(레이아웃 유지), 알파로 표현
            apDots[i].gameObject.SetActive(true);

            var c = apDots[i].color;
            c.a = on ? apDotOnAlpha : apDotOffAlpha;
            apDots[i].color = c;
        }
    }
}
