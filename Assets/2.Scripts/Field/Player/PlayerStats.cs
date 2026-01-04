using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Max Values")]
    public int maxHP = 100;
    public int maxFatigue = 100;   // 높을수록 '덜 피곤함'(회복 게이지)
    public int maxHunger = 100;    // 높을수록 '배부름'

    [Header("Current Values")]
    public int hp = 80;
    public int fatigue = 60;
    public int hunger = 70;

    [Header("Periodic Drain (per 5 Time)")]
    public int drainIntervalTime = 5;
    public int fatigueDrainPerInterval = 1; // 5 Time마다 피로 -1
    public int hungerDrainPerInterval = 2;  // 5 Time마다 허기 -2

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
            // 현재 time 기준으로 다음 드레인 타이밍 맞추기
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
        // 시간이 한 번에 많이 증가할 수 있으니 while로 처리
        while (newTotalTime >= _nextDrainAtTime)
        {
            // 5 Time마다: 피로 -1, 허기 -2
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
    /// 휴식 틱(1 time)에서 피로/체력을 같이 회복.
    /// 규칙: fatigue +1당 hp +1
    /// </summary>
    public void RestRecover(int fatigueGainPerTick)
    {
        int gain = Mathf.Max(0, fatigueGainPerTick);

        // 피로가 이미 풀이라면 회복 0
        int fatigueBefore = fatigue;
        fatigue = Mathf.Min(maxFatigue, fatigue + gain);

        int actualFatigueGained = fatigue - fatigueBefore;
        if (actualFatigueGained > 0)
        {
            hp = Mathf.Min(maxHP, hp + actualFatigueGained); // 피로 +1당 체력 +1
        }

        ClampAll();
    }

    public bool IsStarving => hunger <= 0;
    public bool IsExhausted => fatigue <= 0;
}
