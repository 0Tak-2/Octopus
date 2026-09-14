using UnityEngine;

public class EnemyInstance : MonoBehaviour
{
    [Header("Definition")]
    public EnemyDefinition definition;

    [Header("Runtime")]
    public int currentHP;

    [Header("Color Module Drop")]
    [Tooltip("일반 적 처치 시 컬러 모듈 드랍 확률")]
    [Range(0f, 1f)] public float colorModuleDropChance = 0.08f;

    [Tooltip("엘리트 처치 시 확률")]
    [Range(0f, 1f)] public float eliteColorModuleDropChance = 0.35f;

    [Tooltip("보스 처치 시 확률")]
    [Range(0f, 1f)] public float bossColorModuleDropChance = 1f;

    // 턴 행동 추적
    [HideInInspector] public bool hasActedThisTurn = false;

    /// <summary>이 인스턴스에 대해 처치 보상(메타 XP 등)을 이미 지급했으면 true</summary>
    private bool _defeatRewardsApplied;

    private void Awake()
    {
        if (definition != null && currentHP <= 0)
            currentHP = Mathf.Max(1, definition.maxHP);
    }

    private void OnEnable()
    {
        if (definition != null && currentHP > 0)
            _defeatRewardsApplied = false;
    }

    public void Clamp()
    {
        if (definition == null) return;
        currentHP = Mathf.Clamp(currentHP, 0, Mathf.Max(1, definition.maxHP));
    }

    /// <summary>
    /// 데미지 받기 + 피격 효과
    /// </summary>
    public void TakeDamage(int damage)
    {
        currentHP -= Mathf.Max(0, damage);
        Clamp();

        HitEffectManager hitEffect = HitEffectManager.Instance ?? FindObjectOfType<HitEffectManager>();
        if (hitEffect != null)
        {
            hitEffect.ShowHitEffect(transform, damage);
        }

        if (currentHP <= 0)
        {
            TryRegisterDefeatRewards();
            gameObject.SetActive(false);
        }
    }

    /// <summary>필드 처치 또는 집중전투 종료 시 1회만 호출. 중복 지급 방지.</summary>
    public void TryRegisterDefeatRewards()
    {
        if (_defeatRewardsApplied || definition == null)
            return;

        _defeatRewardsApplied = true;
        if (MetaProgression.Instance != null)
            MetaProgression.Instance.RegisterKill(definition);

        TryDropColorModule();
    }

    /// <summary>
    /// 컬러 모듈 드랍. 이 호출이 없으면 게임 내에서 모듈을 얻을 방법이 아예 없다.
    /// 보스/엘리트일수록 확률이 높다.
    /// </summary>
    private void TryDropColorModule()
    {
        var dropper = ColorModuleDropper.Instance;
        if (dropper == null) return;

        float chance = colorModuleDropChance;
        if (definition.aiType == EnemyAIType.AI4_Boss) chance = bossColorModuleDropChance;
        else if (definition.aiType == EnemyAIType.AI3_Elite) chance = eliteColorModuleDropChance;

        if (chance <= 0f) return;

        // 세 색 중 하나가 같은 확률로 나오게 나눠 넘긴다.
        dropper.TryDropModule(chance / 3f, chance / 3f, chance / 3f);
    }

    /// <summary>
    /// 행동 완료 표시
    /// </summary>
    public void MarkAsActed()
    {
        hasActedThisTurn = true;
    }

    /// <summary>
    /// 턴 초기화
    /// </summary>
    public void ResetTurn()
    {
        hasActedThisTurn = false;
    }
}