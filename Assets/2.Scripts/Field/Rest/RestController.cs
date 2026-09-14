using UnityEngine;

public class RestController : MonoBehaviour
{
    public enum RestState
    {
        Idle,
        Confirm,   // "�޽��� ���Ͻðڽ��ϱ�? (R)"
        Resting    // �ڵ� �޽� ���� ��
    }

    [Header("Refs")]
    public FieldTimeManager fieldTime;
    public PlayerStats stats;
    public FieldTurnCoordinator turnCoordinator;

    [Header("Rest Risk")]
    [Tooltip("휴식 틱을 턴 코디네이터로 넘겨 적도 행동하게 한다. 끄면 자는 동안 무적이 된다.")]
    public bool enemiesActWhileResting = true;

    [Tooltip("휴식 중 피해를 입으면 즉시 깨어난다.")]
    public bool wakeOnDamage = true;

    [Header("Input")]
    public KeyCode restKey = KeyCode.R;

    [Header("Rest Rule")]
    public int timePerTick = 1;           // �޽� 1ƽ = Time +1
    public int fatigueGainPerTick = 1;    // �޽� 1ƽ = �Ƿ� +1 (�� ü�µ� +1)

    [Header("Speed")]
    public float ticksPerSecond = 12f;    // �ڵ����� ������ �ð� ������

    public RestState State { get; private set; } = RestState.Idle;

    private float _accum;

    private void Awake()
    {
        if (fieldTime == null) fieldTime = FieldTimeManager.Instance ?? FindObjectOfType<FieldTimeManager>();
        if (stats == null) stats = GetComponent<PlayerStats>() ?? FindObjectOfType<PlayerStats>();
        if (turnCoordinator == null) turnCoordinator = FindObjectOfType<FieldTurnCoordinator>();
    }

    private void OnEnable()
    {
        if (stats != null && wakeOnDamage)
            stats.OnDamageTaken += HandleDamagedWhileResting;
    }

    private void OnDisable()
    {
        if (stats != null)
            stats.OnDamageTaken -= HandleDamagedWhileResting;
    }

    /// <summary>자는 동안 맞으면 깬다. 안전한 곳을 찾는 것이 플레이어의 판단이 되도록.</summary>
    private void HandleDamagedWhileResting(int damage)
    {
        if (State != RestState.Resting) return;

        State = RestState.Idle;
        _accum = 0f;
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

        // ���ָ� 0�̸� �޽��ص� �Ǵ��� ���δ� ��ȹ�� ���� �޶� ������ ���� ����.
        // �ʿ��ϸ� ���⼭ stats.hunger <= 0�� �� Confirm ���� ������ �ٲ� �� ����.

        if (State == RestState.Idle)
        {
            State = RestState.Confirm;
            return;
        }

        if (State == RestState.Confirm)
        {
            // Ȯ�� �� �޽� ����
            if (stats.fatigue >= stats.maxFatigue)
            {
                // �̹� �Ƿΰ� �� �� ������ �ƹ� �ϵ� �� ��(Confirm ����)
                State = RestState.Idle;
                return;
            }

            State = RestState.Resting;
            _accum = 0f;
            return;
        }

        if (State == RestState.Resting)
        {
            // �޽� �߿� R�� ������ ��� �ߴ�(����)
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

        // �Ƿΰ� �� ���� ����
        if (stats.fatigue >= stats.maxFatigue)
        {
            State = RestState.Idle;
            _accum = 0f;
            return;
        }

        // 턴 코디네이터가 적 턴을 처리하는 동안에는 새 틱을 넣지 않는다.
        if (enemiesActWhileResting && turnCoordinator != null && turnCoordinator.IsBusy)
            return;

        _accum += Time.deltaTime * ticksPerSecond;
        while (_accum >= 1f)
        {
            _accum -= 1f;

            // 1) �ð� ���
            if (enemiesActWhileResting && turnCoordinator != null)
            {
                // 턴 코디네이터를 거쳐야 적도 한 턴 움직인다.
                // 예전처럼 fieldTime.Advance 만 호출하면 FieldMultiEnemyAttack.OnPlayerTurnEnd 가
                // 돌지 않아서, 자는 동안 플레이어가 사실상 무적이 된다.
                turnCoordinator.TryCommitPlayerAction(timePerTick, true);
                stats.RestRecover(fatigueGainPerTick);

                if (stats.fatigue >= stats.maxFatigue)
                {
                    State = RestState.Idle;
                    _accum = 0f;
                }

                // 적 턴이 끝날 때까지 다음 틱은 미룬다.
                return;
            }

            fieldTime.Advance(timePerTick);

            // 2) �Ƿ� ȸ��(+1) �� ü�µ� ���Ϸ� ȸ��
            stats.RestRecover(fatigueGainPerTick);

            // 3) Ȥ�� �� ���� max�� �����ϸ� ��� ����
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
