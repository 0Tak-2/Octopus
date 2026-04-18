using UnityEngine;

public class EnemyInstance : MonoBehaviour
{
    [Header("Definition")]
    public EnemyDefinition definition;

    [Header("Runtime")]
    public int currentHP;

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