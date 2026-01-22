using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Max Values")]
    public int maxHP = 100;
    public int maxFatigue = 100;
    public int maxHunger = 100;

    [Header("Current Values")]
    public int hp = 80;
    public int fatigue = 60;
    public int hunger = 70;

    [Header("Periodic Drain (per 5 Time)")]
    public int drainIntervalTime = 5;
    public int fatigueDrainPerInterval = 1;
    public int hungerDrainPerInterval = 2;

    private FieldTimeManager _fieldTime;
    private int _nextDrainAtTime = 5;

    private void Awake()
    {
        ClampAll();
    }

    private void Start()
    {
        _fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (_fieldTime != null)
        {
            int t = _fieldTime.time;
            _nextDrainAtTime = Mathf.Max(drainIntervalTime, ((t / drainIntervalTime) + 1) * drainIntervalTime);

            _fieldTime.OnTimeAdvanced += HandleTimeAdvanced;
        }
    }

    private void OnDestroy()
    {
        if (_fieldTime != null)
            _fieldTime.OnTimeAdvanced -= HandleTimeAdvanced;
    }

    private void HandleTimeAdvanced(int delta, int newTotalTime)
    {
        while (newTotalTime >= _nextDrainAtTime)
        {
            fatigue -= Mathf.Max(0, fatigueDrainPerInterval);
            hunger -= Mathf.Max(0, hungerDrainPerInterval);
            ClampAll();

            _nextDrainAtTime += drainIntervalTime;
        }
    }

    public void ClampAll()
    {
        hp = Mathf.Clamp(hp, 0, maxHP);
        fatigue = Mathf.Clamp(fatigue, 0, maxFatigue);
        hunger = Mathf.Clamp(hunger, 0, maxHunger);
    }

    /// <summary>
    /// 휴식 시 피로/체력 회복
    /// </summary>
    public void RestRecover(int fatigueGainPerTick)
    {
        int gain = Mathf.Max(0, fatigueGainPerTick);

        int fatigueBefore = fatigue;
        fatigue = Mathf.Min(maxFatigue, fatigue + gain);

        int actualFatigueGained = fatigue - fatigueBefore;
        if (actualFatigueGained > 0)
        {
            hp = Mathf.Min(maxHP, hp + actualFatigueGained);
        }

        ClampAll();
    }

    /// <summary>
    /// 데미지를 받음
    /// </summary>
    public void TakeDamage(int damage)
    {
        hp -= Mathf.Max(0, damage);
        ClampAll();

        // ✅ 피격 효과 추가
        HitEffectManager hitEffect = HitEffectManager.Instance ?? FindObjectOfType<HitEffectManager>();
        if (hitEffect != null)
        {
            hitEffect.ShowHitEffect(transform, damage);
        }

        Debug.Log($"[PlayerStats] Took {damage} damage. HP: {hp}/{maxHP}");
    }

    public bool IsStarving => hunger <= 0;
    public bool IsExhausted => fatigue <= 0;
}