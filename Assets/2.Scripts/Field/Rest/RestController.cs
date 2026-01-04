using UnityEngine;

public class RestController : MonoBehaviour
{
    public enum RestState
    {
        Idle,
        Confirm,   // "휴식을 취하시겠습니까? (R)"
        Resting    // 자동 휴식 진행 중
    }

    [Header("Refs")]
    public FieldTimeManager fieldTime;
    public PlayerStats stats;

    [Header("Input")]
    public KeyCode restKey = KeyCode.R;

    [Header("Rest Rule")]
    public int timePerTick = 1;           // 휴식 1틱 = Time +1
    public int fatigueGainPerTick = 1;    // 휴식 1틱 = 피로 +1 (→ 체력도 +1)

    [Header("Speed")]
    public float ticksPerSecond = 12f;    // 자동으로 빠르게 시간 보내기

    public RestState State { get; private set; } = RestState.Idle;

    private float _accum;

    private void Awake()
    {
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (stats == null) stats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(restKey))
        {
            HandleRestKeyDown();
        }

        if (State == RestState.Resting)
        {
            TickResting();
        }
    }

    private void HandleRestKeyDown()
    {
        if (stats == null || fieldTime == null) return;

        // 굶주림 0이면 휴식해도 되는지 여부는 기획에 따라 달라서 지금은 막지 않음.
        // 필요하면 여기서 stats.hunger <= 0일 때 Confirm 진입 금지로 바꿀 수 있음.

        if (State == RestState.Idle)
        {
            State = RestState.Confirm;
            return;
        }

        if (State == RestState.Confirm)
        {
            // 확정 → 휴식 시작
            if (stats.fatigue >= stats.maxFatigue)
            {
                // 이미 피로가 꽉 차 있으면 아무 일도 안 함(Confirm 해제)
                State = RestState.Idle;
                return;
            }

            State = RestState.Resting;
            _accum = 0f;
            return;
        }

        if (State == RestState.Resting)
        {
            // 휴식 중에 R을 누르면 즉시 중단(선택)
            State = RestState.Idle;
            _accum = 0f;
            return;
        }
    }

    private void TickResting()
    {
        if (stats == null || fieldTime == null)
        {
            State = RestState.Idle;
            return;
        }

        // 피로가 꽉 차면 종료
        if (stats.fatigue >= stats.maxFatigue)
        {
            State = RestState.Idle;
            _accum = 0f;
            return;
        }

        _accum += Time.deltaTime * ticksPerSecond;
        while (_accum >= 1f)
        {
            _accum -= 1f;

            // 1) 시간 경과
            fieldTime.Advance(timePerTick);

            // 2) 피로 회복(+1) → 체력도 동일량 회복
            stats.RestRecover(fatigueGainPerTick);

            // 3) 혹시 한 번에 max에 도달하면 즉시 종료
            if (stats.fatigue >= stats.maxFatigue)
            {
                State = RestState.Idle;
                _accum = 0f;
                break;
            }
        }
    }

    public bool IsConfirmPrompt => State == RestState.Confirm;
    public bool IsResting => State == RestState.Resting;
}
