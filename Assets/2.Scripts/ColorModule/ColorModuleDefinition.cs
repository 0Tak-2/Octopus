using UnityEngine;

/// <summary>
/// 색 모듈 정의 (ScriptableObject)
/// 에디터에서 생성: Create > ColorModule > Module Definition
/// </summary>
[CreateAssetMenu(menuName = "ColorModule/Module Definition", fileName = "NewColorModule")]
public class ColorModuleDefinition : ScriptableObject
{
    [Header("기본 정보")]
    public string moduleName = "새 모듈";
    public string description = "모듈 설명";
    public Sprite icon;
    // ── 기존 moduleName, description 아래에 추가 ──
    [Header("Localization Keys")]
    public string nameKey;   // 예: "SKILL_NAME_CRIT"
    public string descKey;   // 예: "SKILL_DESC_CRIT"

    [Header("분류")]
    public ColorType colorType = ColorType.Red;
    public ModuleType moduleType = ModuleType.Skill;
    
    [Header("스킬 설정 (ModuleType.Skill일 때만)")]
    [Tooltip("AP 비용")]
    [Min(0)] public int apCost = 1;
    
    [Tooltip("사거리 (0 = 자신)")]
    [Min(0)] public int range = 1;
    
    [Tooltip("필드/집중전투 모두 사용 가능")]
    public bool usableInField = true;
    public bool usableInFocusedCombat = true;
    
    [Header("스킬 ID (코드에서 효과 분기용)")]
    [Tooltip("고유 스킬 ID - 코드에서 switch문으로 효과 처리")]
    public SkillID skillID = SkillID.None;
    
    [Header("패시브 ID (ModuleType.Passive일 때만)")]
    public PassiveID passiveID = PassiveID.None;
}

/// <summary>
/// 스킬 ID - 각 스킬의 고유 효과를 코드에서 처리하기 위한 식별자
/// </summary>
public enum SkillID
{
    None = 0,
    
    // === 빨강 스킬 ===
    /// <summary>날카로운촉수: 출혈 부여. 이미 출혈이면 기본 공격력의 40% 추가 피해</summary>
    Red_SharpTentacle = 101,
    
    /// <summary>파고드는촉수: 출혈 부여. 이미 출혈이면 입힌 피해의 50% 회복</summary>
    Red_PiercingTentacle = 102,
    
    // === 파랑 스킬 ===
    /// <summary>단단한촉수: 불완전 부여 (최대 4중첩). 다른 공격 시 중첩당 25% 추가 피해</summary>
    Blue_HardTentacle = 201,
    
    /// <summary>늘어나는촉수: 3칸 내 위치로 이동</summary>
    Blue_StretchTentacle = 202,
    
    // === 검정 스킬 ===
    /// <summary>치명적인촉수: 해당 적에게 첫 공격 시 확정 치명타</summary>
    Black_DeadlyTentacle = 301,
    
    /// <summary>응징하는촉수: 1턴간 공격력 1.75배</summary>
    Black_PunishingTentacle = 302,
}

/// <summary>
/// 패시브 ID
/// </summary>
public enum PassiveID
{
    None = 0,
    
    // === 빨강 패시브 ===
    /// <summary>약자감지: 적 상태이상 1개당 공격력 +6% (최대 5중첩, +30%)</summary>
    Red_WeaknessSense = 111,
    
    /// <summary>포식: 적 HP 20% 이하 시 처형 + 입힌 피해 100% 회복</summary>
    Red_Predation = 112,
    
    // === 파랑 패시브 ===
    /// <summary>기본기: 기본공격 25% 확률 2회 공격</summary>
    Blue_Fundamentals = 211,
    
    /// <summary>촉수강화: 기본공격 25% 확률 스턴 (동일 적 1턴 1회)</summary>
    Blue_TentacleEnhance = 212,
    
    // === 검정 패시브 ===
    /// <summary>자신감: 내 HP 비례 공격력 증가 (100% → +20%)</summary>
    Black_Confidence = 311,
    
    /// <summary>용맹함: 적 HP 높을수록 공격력 증가 (100% → +25%)</summary>
    Black_Bravery = 312,
    
    /// <summary>악랄함: 적 HP 낮을수록 공격력 증가 (0% → +25%)</summary>
    Black_Cruelty = 313,
}
