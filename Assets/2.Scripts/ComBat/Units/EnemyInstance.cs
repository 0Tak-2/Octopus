using UnityEngine;

public class EnemyInstance : MonoBehaviour
{
    [Header("Definition")]
    public EnemyDefinition definition;

    [Header("Runtime")]
    public int currentHP;

    // 턴 행동 추적
    [HideInInspector] public bool hasActedThisTurn = false;

    private void Awake()
    {
        if (definition != null && currentHP <= 0)
            currentHP = Mathf.Max(1, definition.maxHP);
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
            gameObject.SetActive(false);
        }
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